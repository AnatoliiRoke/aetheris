using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Players;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Market.Api;
using Aetheris.Market.Configuration;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Services;

/// <summary>
/// The reads of the market for a player: they change nothing, so they take no lock (task 00024,
/// section 4.1; the fields of the screen of the market, task 00026).
/// </summary>
/// <remarks>
/// A list reads the other players (the nickname and the race) and the bodies (the distance) in one
/// read for the whole list, never one by one.
/// </remarks>
public sealed class MarketReadService
{
    private readonly MarketDbContext _db;
    private readonly MarketCatalog _catalog;
    private readonly MarketTradeService _trade;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly IPlanetResources _resources;
    private readonly IEconomyModule _economy;
    private readonly IPlayerDirectory _players;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketReadService"/> class.
    /// </summary>
    /// <param name="db">The context of the module.</param>
    /// <param name="catalog">The reads of the reference data.</param>
    /// <param name="trade">The commands of the market, for the limit and the quote.</param>
    /// <param name="world">The world module.</param>
    /// <param name="planets">The bodies module.</param>
    /// <param name="resources">The balance of a body.</param>
    /// <param name="economy">The balances of the currencies.</param>
    /// <param name="players">The nicknames of the players.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The game clock.</param>
    public MarketReadService(
        MarketDbContext db,
        MarketCatalog catalog,
        MarketTradeService trade,
        IWorldModule world,
        IPlanetsModule planets,
        IPlanetResources resources,
        IEconomyModule economy,
        IPlayerDirectory players,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _db = db;
        _catalog = catalog;
        _trade = trade;
        _world = world;
        _planets = planets;
        _resources = resources;
        _economy = economy;
        _players = players;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Gets what the screen of the market needs to start: the levels, the currencies and the limit
    /// of the lots.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The overview.</returns>
    public async Task<MarketOverviewDto> OverviewAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MarketSettings settings = MarketSettings.From(_config);
        MarketCurrencies currencies = await _catalog.CurrenciesAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, MarketLevelEntity> levels = await _catalog.LevelsAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<OwnedPlanet> owned = await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false);

        // Both classes of buildings are read together, in one pass over the bodies of the player.
        IReadOnlyDictionary<string, IReadOnlyList<PlanetBuildingLevel>> levelsByClass = await _planets
            .GetBuildingLevelsAsync([BuildingClasses.TradeCenter, BuildingClasses.Portal], playerId, cancellationToken)
            .ConfigureAwait(false);
        Dictionary<long, int> tradeCentres = levelsByClass[BuildingClasses.TradeCenter].ToDictionary(row => row.PlanetId, row => row.Level);
        Dictionary<long, int> portals = levelsByClass[BuildingClasses.Portal].ToDictionary(row => row.PlanetId, row => row.Level);

        return new MarketOverviewDto(
            [
                .. levels.Values
                    .Where(level => !level.IsArchived)
                    .OrderBy(level => level.SortOrder)
                    .ThenBy(level => level.Id)
                    .Select(level => new MarketLevelDto(level.Id, level.Visibility, level.CommissionPercent, level.IsActive, level.SortOrder)),
            ],
            currencies.CreditsResourceTypeId,
            currencies.TradeResourceTypeId,
            await _catalog.EnergyResourceAsync(settings.EnergyResourceCode, cancellationToken).ConfigureAwait(false),
            await _trade.LimitAsync(playerId, owned, cancellationToken).ConfigureAwait(false),
            [
                .. owned.OrderBy(planet => planet.PlanetId).Select(planet => new MarketPlanetDto(
                    planet.PlanetId,
                    tradeCentres.GetValueOrDefault(planet.PlanetId) > 0,
                    portals.GetValueOrDefault(planet.PlanetId) > 0)),
            ],
            _clock.GameNow,
            _clock.Multiplier);
    }

    /// <summary>
    /// Gets the lots a player sees: the open lots of the levels the player may read, narrowed by the
    /// filter. The same query serves the list and the groups, so they never disagree.
    /// </summary>
    private async Task<IQueryable<MarketLotEntity>> VisibleLotsAsync(string playerId, LotFilter filter, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, MarketLevelEntity> levels = await _catalog.LevelsAsync(cancellationToken).ConfigureAwait(false);
        List<int> sectors = [.. (await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false)).Select(planet => planet.SectorId).Distinct()];
        List<int> galacticLevels = [.. levels.Values.Where(level => level.IsActive && level.Visibility == MarketVisibilities.Galactic).Select(level => level.Id)];
        List<int> sectorLevels = [.. levels.Values.Where(level => level.IsActive && level.Visibility == MarketVisibilities.Sector).Select(level => level.Id)];

        IQueryable<MarketLotEntity> query = _db.MarketLots.AsNoTracking()
            .Where(lot => lot.State == MarketLotStates.Open
                && (galacticLevels.Contains(lot.MarketLevelId) || (sectorLevels.Contains(lot.MarketLevelId) && sectors.Contains(lot.SellerSectorId))));

        if (filter.MarketLevelId is { } levelId)
        {
            query = query.Where(lot => lot.MarketLevelId == levelId);
        }

        if (filter.ResourceTypeIds is not null || filter.PartTypeIds is not null)
        {
            List<int> resourceIds = [.. filter.ResourceTypeIds ?? []];
            List<int> partIds = [.. filter.PartTypeIds ?? []];

            query = query.Where(lot =>
                (lot.ResourceTypeId != null && resourceIds.Contains(lot.ResourceTypeId.Value))
                || (lot.PartTypeId != null && partIds.Contains(lot.PartTypeId.Value)));
        }

        if (filter.SellerPlayerId is { Length: > 0 } sellerId)
        {
            query = query.Where(lot => lot.SellerPlayerId == sellerId);
        }

        if (filter.MinPrice is { } minPrice)
        {
            query = query.Where(lot => lot.PricePerUnit >= minPrice);
        }

        if (filter.MaxPrice is { } maxPrice)
        {
            query = query.Where(lot => lot.PricePerUnit <= maxPrice);
        }

        if (filter.MaxSum is { } maxSum)
        {
            query = query.Where(lot => lot.Quantity * lot.PricePerUnit <= maxSum);
        }

        return query;
    }

    /// <summary>
    /// Gets the lots a player sees by item: how many lots each item has, the lowest price of a
    /// unit and the quantity of all of them, so a player with hundreds of lots finds an item first.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="filter">What the player looks for; the sort and the page do not apply.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The groups.</returns>
    public async Task<IReadOnlyList<LotGroupDto>> LotGroupsAsync(string playerId, LotFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<MarketLotEntity> query = await VisibleLotsAsync(playerId, filter, cancellationToken).ConfigureAwait(false);

        return await query
            .GroupBy(lot => new { lot.ResourceTypeId, lot.PartTypeId })
            .Select(group => new LotGroupDto(group.Key.ResourceTypeId, group.Key.PartTypeId, group.Count(), group.Min(lot => lot.PricePerUnit), group.Sum(lot => lot.Quantity)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a page of the lots a player sees. With a body of the player named, every lot also carries
    /// what the delivery to that body costs and why the player cannot buy it.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="filter">What the player looks for, in what order and which page.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page of the lots with the number of all the lots that match; the lots of the player are among them and cannot be bought.</returns>
    public async Task<LotsDto> LotsAsync(string playerId, LotFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(filter);

        MarketSettings settings = MarketSettings.From(_config);
        IQueryable<MarketLotEntity> query = await VisibleLotsAsync(playerId, filter, cancellationToken).ConfigureAwait(false);
        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        int limit = filter.Limit is > 0 ? Math.Min(filter.Limit, settings.PageSize) : settings.PageSize;
        int offset = Math.Max(0, filter.Offset);
        bool descending = filter.Descending;
        IReadOnlyDictionary<long, double> distances = new Dictionary<long, double>();
        List<MarketLotEntity> lots;

        if (filter.PlanetId is { } sortPlanetId && filter.Sort is LotSorts.Time or LotSorts.Rods)
        {
            distances = await _world
                .GetPlanetDistancesAsync(sortPlanetId, await query.Select(lot => lot.SellerPlanetId).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false);
        }

        if (filter.Sort is LotSorts.Item or LotSorts.Time or LotSorts.Rods)
        {
            // The order of an item, of a distance or of a fee is not a column, so the lightweight rows of
            // the lots that match are ordered here and only the page is read in full.
            MarketWeights sortWeights = filter.Sort == LotSorts.Rods ? await _catalog.WeightsAsync(cancellationToken).ConfigureAwait(false) : new MarketWeights(new Dictionary<int, decimal>(), new Dictionary<int, decimal>());
            Dictionary<string, int> rank = (filter.ItemOrder ?? []).Select((key, index) => (key, index)).ToDictionary(pair => pair.key, pair => pair.index, StringComparer.Ordinal);
            var rows = await query
                .Select(lot => new { lot.Id, lot.SellerPlanetId, lot.Quantity, lot.ResourceTypeId, lot.PartTypeId })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            IEnumerable<long> ordered = (descending
                ? rows.OrderByDescending(row => SortKey(row.Id, row.SellerPlanetId, row.Quantity, row.ResourceTypeId, row.PartTypeId)).ThenByDescending(row => row.Id)
                : rows.OrderBy(row => SortKey(row.Id, row.SellerPlanetId, row.Quantity, row.ResourceTypeId, row.PartTypeId)).ThenBy(row => row.Id))
                .Select(row => row.Id);
            List<long> pageIds = [.. ordered.Skip(offset).Take(limit)];
            Dictionary<long, MarketLotEntity> page = await _db.MarketLots.AsNoTracking()
                .Where(lot => pageIds.Contains(lot.Id))
                .ToDictionaryAsync(lot => lot.Id, cancellationToken)
                .ConfigureAwait(false);

            lots = [.. pageIds.Select(id => page[id])];

            double SortKey(long id, long planetId, long quantity, int? resourceTypeId, int? partTypeId)
            {
                _ = id;

                return filter.Sort switch
                {
                    LotSorts.Item => rank.GetValueOrDefault(resourceTypeId is { } r ? $"r{r}" : $"p{partTypeId}", int.MaxValue),
                    LotSorts.Time => distances.GetValueOrDefault(planetId),
                    _ => distances.GetValueOrDefault(planetId) * (double)(sortWeights.UnitWeight(resourceTypeId, partTypeId) * quantity),
                };
            }
        }
        else
        {
            IOrderedQueryable<MarketLotEntity> ordered = filter.Sort switch
            {
                LotSorts.Price => descending ? query.OrderByDescending(lot => lot.PricePerUnit) : query.OrderBy(lot => lot.PricePerUnit),
                LotSorts.Total => descending ? query.OrderByDescending(lot => lot.Quantity * lot.PricePerUnit) : query.OrderBy(lot => lot.Quantity * lot.PricePerUnit),
                LotSorts.Quantity => descending ? query.OrderByDescending(lot => lot.Quantity) : query.OrderBy(lot => lot.Quantity),
                _ => descending ? query.OrderByDescending(lot => lot.Id) : query.OrderBy(lot => lot.Id),
            };

            lots = await ordered.ThenBy(lot => lot.Id).Skip(offset).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyDictionary<string, PlayerCardDto> cards = await CardsAsync(lots.Select(lot => lot.SellerPlayerId), cancellationToken).ConfigureAwait(false);

        if (filter.PlanetId is not { } planetId)
        {
            return new LotsDto([.. lots.Select(lot => ToItem(lot, cards, null, null, null))], null, null, total, offset, limit);
        }

        if (await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false) is not { } planet
            || !string.Equals(planet.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(MarketErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }

        bool hasPortal = await _planets.GetBuildingLevelAsync(BuildingClasses.Portal, planetId, playerId, cancellationToken).ConfigureAwait(false) > 0;
        MarketCurrencies currencies = await _catalog.CurrenciesAsync(cancellationToken).ConfigureAwait(false);
        int? energyId = await _catalog.EnergyResourceAsync(settings.EnergyResourceCode, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, long> stock = await _resources.GetBalanceAsync(planetId, playerId, cancellationToken).ConfigureAwait(false);
        long energyHave = energyId is { } energyResourceId ? stock.GetValueOrDefault(energyResourceId) : 0;
        long tradeHave = (await _economy.GetBalanceAsync(playerId, currencies.TradeResourceTypeId, cancellationToken).ConfigureAwait(false)).Amount;
        MarketWeights weights = await _catalog.WeightsAsync(cancellationToken).ConfigureAwait(false);
        distances = await _world
            .GetPlanetDistancesAsync(planetId, [.. lots.Select(lot => lot.SellerPlanetId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        List<LotListItemDto> items = [];

        foreach (MarketLotEntity lot in lots)
        {
            double distance = distances.GetValueOrDefault(lot.SellerPlanetId);
            long seconds = MarketRules.DeliverySeconds(distance, settings);
            long energy = MarketRules.DeliveryEnergy(weights.UnitWeight(lot.ResourceTypeId, lot.PartTypeId) * lot.Quantity, distance, settings);
            string? reason = null;

            if (string.Equals(lot.SellerPlayerId, playerId, StringComparison.Ordinal))
            {
                reason = MarketErrorCodes.OwnLot;
            }
            else if (!hasPortal)
            {
                reason = MarketErrorCodes.PortalMissing;
            }
            else if (lot.Quantity * lot.PricePerUnit > tradeHave)
            {
                reason = MarketErrorCodes.TradeCurrencyShort;
            }
            else if (energy > energyHave)
            {
                reason = MarketErrorCodes.EnergyShort;
            }

            items.Add(ToItem(lot, cards, seconds, energy, reason));
        }

        return new LotsDto(items, energyHave, hasPortal, total, offset, limit);
    }

    /// <summary>
    /// Gets the lots of a player that are on the market now.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The open lots of the player.</returns>
    public async Task<IReadOnlyList<LotDto>> MyLotsAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<MarketLotEntity> lots = await _db.MarketLots.AsNoTracking()
            .Where(lot => lot.SellerPlayerId == playerId && lot.State == MarketLotStates.Open)
            .OrderByDescending(lot => lot.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. lots.Select(MarketMapper.ToDto)];
    }

    /// <summary>
    /// Gets the deliveries of a player: the goods that travel to them and the goods of their sold
    /// lots that travel to the buyers, with the ones that arrived a short time ago.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The incoming and the outgoing deliveries.</returns>
    public async Task<DeliveriesDto> DeliveriesAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        MarketSettings settings = MarketSettings.From(_config);
        DateTimeOffset since = _clock.GameNow.AddHours(-settings.DeliveryRecentHours);

        var rows = await (
                from delivery in _db.MarketDeliveries.AsNoTracking()
                join deal in _db.MarketDeals.AsNoTracking() on delivery.DealId equals deal.Id
                join lot in _db.MarketLots.AsNoTracking() on deal.LotId equals lot.Id
                where (deal.BuyerPlayerId == playerId || deal.SellerPlayerId == playerId)
                    && (delivery.State == MarketDeliveryStates.Travelling || delivery.ArrivesAt >= since)
                orderby delivery.ArrivesAt
                select new
                {
                    Delivery = delivery,
                    deal.SellerPlayerId,
                    deal.BuyerPlayerId,
                    StartedAt = deal.CreatedAt,
                    SourcePlanetId = lot.SellerPlanetId,
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<string, PlayerCardDto> cards = await CardsAsync(
            rows.SelectMany(row => new[] { row.SellerPlayerId, row.BuyerPlayerId }),
            cancellationToken).ConfigureAwait(false);

        List<DeliveryItemDto> incoming = [];
        List<DeliveryItemDto> outgoing = [];

        foreach (var row in rows)
        {
            bool isIncoming = string.Equals(row.BuyerPlayerId, playerId, StringComparison.Ordinal);
            string counterpartyId = isIncoming ? row.SellerPlayerId : row.BuyerPlayerId;
            MarketDeliveryEntity delivery = row.Delivery;
            DeliveryItemDto item = new(
                delivery.Id,
                cards.GetValueOrDefault(counterpartyId) ?? new PlayerCardDto(counterpartyId, counterpartyId, null, null),
                row.SourcePlanetId,
                delivery.DestinationPlanetId,
                delivery.ResourceTypeId,
                delivery.PartTypeId,
                delivery.Quantity,
                delivery.EnergyRodAmount,
                row.StartedAt,
                delivery.ArrivesAt,
                delivery.State);

            (isIncoming ? incoming : outgoing).Add(item);
        }

        return new DeliveriesDto(incoming, outgoing);
    }

    /// <summary>
    /// Gets the rate of the exchange now.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The rate, the commission and the two currencies.</returns>
    public async Task<RateDto> RateAsync(CancellationToken cancellationToken)
    {
        MarketSettings settings = MarketSettings.From(_config);
        MarketCurrencies currencies = await _catalog.CurrenciesAsync(cancellationToken).ConfigureAwait(false);
        decimal rate = await _catalog.RateAsync(currencies, settings, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        return new RateDto(rate, settings.ExchangeCommissionPercent, currencies.CreditsResourceTypeId, currencies.TradeResourceTypeId);
    }

    /// <summary>
    /// Gets the history of the rate of the exchange for the chart: the last rate of every day in
    /// the window, and how the rate changed in a day.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The points of the chart and the change in a day.</returns>
    public async Task<RateHistoryDto> RateHistoryAsync(CancellationToken cancellationToken)
    {
        MarketSettings settings = MarketSettings.From(_config);
        DateTimeOffset now = _clock.GameNow;
        DateTimeOffset since = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero).AddDays(-(settings.RateHistoryDays - 1));

        List<MarketRateEntity> rows = await _db.MarketRates.AsNoTracking()
            .Where(rate => rate.RecordedAt >= since)
            .OrderBy(rate => rate.RecordedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<RatePointDto> points =
        [
            .. rows
                .GroupBy(rate => new DateTimeOffset(rate.RecordedAt.UtcDateTime.Date, TimeSpan.Zero))
                .Select(group => new RatePointDto(group.Key, group.Last().Rate))
                .OrderBy(point => point.Day),
        ];

        decimal? previous = await _db.MarketRates.AsNoTracking()
            .Where(rate => rate.RecordedAt < now.AddDays(-1))
            .OrderByDescending(rate => rate.RecordedAt)
            .Select(rate => (decimal?)rate.Rate)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        MarketCurrencies currencies = await _catalog.CurrenciesAsync(cancellationToken).ConfigureAwait(false);
        decimal current = await _catalog.RateAsync(currencies, settings, now, cancellationToken).ConfigureAwait(false);

        return new RateHistoryDto(points, previous is { } before && before > 0 ? MarketRules.ChangePercent(before, current) : null);
    }

    /// <summary>
    /// Gets the lowest and the highest price of one unit of an item, for the form of a sale.
    /// </summary>
    /// <param name="resourceTypeId">The resource, or <see langword="null"/>.</param>
    /// <param name="partTypeId">The part, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The limit.</returns>
    /// <exception cref="GameException">The item has no reference price.</exception>
    public async Task<PriceLimitDto> PriceLimitAsync(int? resourceTypeId, int? partTypeId, CancellationToken cancellationToken) =>
        await _trade.PriceLimitAsync(resourceTypeId, partTypeId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(MarketErrorCodes.NoReferencePrice, "The item has no reference price.");

    /// <summary>
    /// Counts what a purchase of a lot would cost the player, without doing it.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="lotId">The lot.</param>
    /// <param name="destinationPlanetId">The body the goods would arrive at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum, the commission, the fee and the time.</returns>
    public async Task<QuoteDto> QuoteAsync(string playerId, long lotId, long destinationPlanetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        PlanetFacts destination = await _world.GetPlanetFactsAsync(destinationPlanetId, cancellationToken).ConfigureAwait(false) is { } facts
            && string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal)
                ? facts
                : throw new GameException(MarketErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        MarketLotEntity lot = await _db.MarketLots.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == lotId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(MarketErrorCodes.LotUnknown, "The lot does not exist.");
        MarketLevelEntity level = await _catalog.FindLevelAsync(lot.MarketLevelId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(MarketErrorCodes.LevelUnknown, "The level of the market does not exist.");

        return MarketCatalog.Sees(level, destination.SectorId, lot.SellerSectorId) || string.Equals(lot.SellerPlayerId, playerId, StringComparison.Ordinal)
            ? await _trade.QuoteAsync(lot, destination, cancellationToken).ConfigureAwait(false)
            : throw new GameException(MarketErrorCodes.LotNotVisible, "The player does not see the lot.");
    }

    /// <summary>
    /// Gets the deals of a player, as a buyer or as a seller, newest first.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="offset">How many deals to skip.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page of the deals with the number of all the deals of the player.</returns>
    public async Task<DealsDto> DealsAsync(string playerId, int offset, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        int limit = MarketSettings.From(_config).PageSize;
        IQueryable<MarketDealEntity> mine = _db.MarketDeals.AsNoTracking().Where(deal => deal.BuyerPlayerId == playerId || deal.SellerPlayerId == playerId);
        int total = await mine.CountAsync(cancellationToken).ConfigureAwait(false);
        List<MarketDealEntity> deals = await mine
            .OrderByDescending(deal => deal.Id)
            .Skip(Math.Max(0, offset))
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<string, PlayerCardDto> cards = await CardsAsync(
            deals.SelectMany(deal => new[] { deal.SellerPlayerId, deal.BuyerPlayerId }),
            cancellationToken).ConfigureAwait(false);

        return new DealsDto(
        [
            .. deals.Select(deal =>
            {
                bool bought = string.Equals(deal.BuyerPlayerId, playerId, StringComparison.Ordinal);
                string counterpartyId = bought ? deal.SellerPlayerId : deal.BuyerPlayerId;

                return new DealItemDto(
                    deal.Id,
                    bought ? MarketDealDirections.Bought : MarketDealDirections.Sold,
                    cards.GetValueOrDefault(counterpartyId) ?? new PlayerCardDto(counterpartyId, counterpartyId, null, null),
                    deal.ResourceTypeId,
                    deal.PartTypeId,
                    deal.Quantity,
                    deal.PricePerUnit,
                    deal.Sum,
                    deal.Commission,
                    deal.CreatedAt);
            }),
        ],
            total,
            Math.Max(0, offset),
            limit);
    }

    private static LotListItemDto ToItem(
        MarketLotEntity lot,
        IReadOnlyDictionary<string, PlayerCardDto> cards,
        long? seconds,
        long? energy,
        string? reason) =>
        new(
            lot.Id,
            cards.GetValueOrDefault(lot.SellerPlayerId) ?? new PlayerCardDto(lot.SellerPlayerId, lot.SellerPlayerId, null, null),
            lot.SellerPlanetId,
            lot.MarketLevelId,
            lot.ResourceTypeId,
            lot.PartTypeId,
            lot.Quantity,
            lot.PricePerUnit,
            lot.Quantity * lot.PricePerUnit,
            lot.CreatedAt,
            lot.PartConditionPercent,
            seconds,
            energy,
            reason);

    private async Task<IReadOnlyDictionary<string, PlayerCardDto>> CardsAsync(IEnumerable<string> playerIds, CancellationToken cancellationToken)
    {
        List<string> ids = [.. playerIds.Distinct(StringComparer.Ordinal)];

        if (ids.Count == 0)
        {
            return new Dictionary<string, PlayerCardDto>();
        }

        IReadOnlyDictionary<string, string> nicknames = await _players.GetNicknamesAsync(ids, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, PlayerRaceBrief> races = await _world.GetRaceBriefsAsync(ids, cancellationToken).ConfigureAwait(false);

        return ids.ToDictionary(
            id => id,
            id => new PlayerCardDto(id, nicknames.GetValueOrDefault(id) ?? id, races.GetValueOrDefault(id)?.NameKey, races.GetValueOrDefault(id)?.ColorKey),
            StringComparer.Ordinal);
    }
}

/// <summary>The orders the lots of the market may be listed in.</summary>
public static class LotSorts
{
    /// <summary>Newest first by default: the order the lots were put up in.</summary>
    public const string Created = "created";

    /// <summary>By the price of a unit.</summary>
    public const string Price = "price";

    /// <summary>By the price of the whole lot.</summary>
    public const string Total = "total";

    /// <summary>By the quantity of the lot.</summary>
    public const string Quantity = "quantity";

    /// <summary>By the item, in the order the client names.</summary>
    public const string Item = "item";

    /// <summary>By the time of the delivery to the chosen body.</summary>
    public const string Time = "time";

    /// <summary>By the fee of the delivery to the chosen body.</summary>
    public const string Rods = "rods";
}

/// <summary>What a player looks for on the market, in what order and which page.</summary>
/// <param name="MarketLevelId">The level of the market.</param>
/// <param name="ResourceTypeIds">The resources to show, or <see langword="null"/> for every item.</param>
/// <param name="PartTypeIds">The parts to show, or <see langword="null"/> for every item.</param>
/// <param name="SellerPlayerId">The seller.</param>
/// <param name="MinPrice">The lowest price of a unit.</param>
/// <param name="MaxPrice">The highest price of a unit.</param>
/// <param name="MaxSum">The largest price of a whole lot.</param>
/// <param name="Offset">How many lots to skip.</param>
/// <param name="Limit">How many lots to return; never more than the page size of the configuration.</param>
/// <param name="PlanetId">The body of the player the goods would arrive at.</param>
/// <param name="Sort">The order, one of <see cref="LotSorts"/>.</param>
/// <param name="Descending">Whether the order is from the largest to the smallest.</param>
/// <param name="ItemOrder">The items in the order of their names, as <c>r{id}</c> and <c>p{id}</c>, for the sort by item.</param>
public sealed record LotFilter(
    int? MarketLevelId = null,
    IReadOnlyCollection<int>? ResourceTypeIds = null,
    IReadOnlyCollection<int>? PartTypeIds = null,
    string? SellerPlayerId = null,
    long? MinPrice = null,
    long? MaxPrice = null,
    long? MaxSum = null,
    int Offset = 0,
    int Limit = 0,
    long? PlanetId = null,
    string Sort = LotSorts.Created,
    bool Descending = true,
    IReadOnlyList<string>? ItemOrder = null);

/// <summary>The directions of a deal in the history of a player.</summary>
public static class MarketDealDirections
{
    /// <summary>The player bought.</summary>
    public const string Bought = "bought";

    /// <summary>The player sold.</summary>
    public const string Sold = "sold";
}

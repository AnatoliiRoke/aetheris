using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Market.Api;
using Aetheris.Market.Configuration;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Services;

/// <summary>
/// The reads of the market for a player: they change nothing, so they take no lock (task 00024,
/// section 4.1).
/// </summary>
public sealed class MarketReadService
{
    private readonly MarketDbContext _db;
    private readonly MarketCatalog _catalog;
    private readonly MarketTradeService _trade;
    private readonly IWorldModule _world;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketReadService"/> class.
    /// </summary>
    /// <param name="db">The context of the module.</param>
    /// <param name="catalog">The reads of the reference data.</param>
    /// <param name="trade">The commands of the market, for the limit and the quote.</param>
    /// <param name="world">The world module.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The game clock.</param>
    public MarketReadService(
        MarketDbContext db,
        MarketCatalog catalog,
        MarketTradeService trade,
        IWorldModule world,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(trade);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _db = db;
        _catalog = catalog;
        _trade = trade;
        _world = world;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Gets the open lots a player sees, newest first.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="filter">What the player looks for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The lots; the player's own lots are not among them.</returns>
    public async Task<IReadOnlyList<LotDto>> LotsAsync(string playerId, LotFilter filter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(filter);

        IReadOnlyDictionary<int, MarketLevelEntity> levels = await _catalog.LevelsAsync(cancellationToken).ConfigureAwait(false);
        List<int> sectors = [.. (await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false)).Select(planet => planet.SectorId).Distinct()];
        List<int> galacticLevels = [.. levels.Values.Where(level => level.IsActive && level.Visibility == MarketVisibilities.Galactic).Select(level => level.Id)];
        List<int> sectorLevels = [.. levels.Values.Where(level => level.IsActive && level.Visibility == MarketVisibilities.Sector).Select(level => level.Id)];

        IQueryable<MarketLotEntity> query = _db.MarketLots.AsNoTracking()
            .Where(lot => lot.State == MarketLotStates.Open
                && lot.SellerPlayerId != playerId
                && (galacticLevels.Contains(lot.MarketLevelId) || (sectorLevels.Contains(lot.MarketLevelId) && sectors.Contains(lot.SellerSectorId))));

        if (filter.MarketLevelId is { } levelId)
        {
            query = query.Where(lot => lot.MarketLevelId == levelId);
        }

        if (filter.ResourceTypeId is { } resourceTypeId)
        {
            query = query.Where(lot => lot.ResourceTypeId == resourceTypeId);
        }

        if (filter.PartTypeId is { } partTypeId)
        {
            query = query.Where(lot => lot.PartTypeId == partTypeId);
        }

        if (filter.SellerPlayerId is { Length: > 0 } sellerId)
        {
            query = query.Where(lot => lot.SellerPlayerId == sellerId);
        }

        if (filter.MaxSum is { } maxSum)
        {
            query = query.Where(lot => lot.Quantity * lot.PricePerUnit <= maxSum);
        }

        List<MarketLotEntity> lots = await query
            .OrderByDescending(lot => lot.Id)
            .Skip(Math.Max(0, filter.Offset))
            .Take(PageSize())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. lots.Select(MarketMapper.ToDto)];
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
            .Take(PageSize())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. lots.Select(MarketMapper.ToDto)];
    }

    /// <summary>
    /// Gets the deliveries of a player: the goods that travel to them and the goods of their
    /// sold lots that travel to the buyers.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The incoming and the outgoing deliveries that still travel.</returns>
    public async Task<DeliveriesDto> DeliveriesAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<MarketDeliveryEntity> incoming = await _db.MarketDeliveries.AsNoTracking()
            .Where(delivery => delivery.BuyerPlayerId == playerId && delivery.State == MarketDeliveryStates.Travelling)
            .OrderBy(delivery => delivery.ArrivesAt)
            .Take(PageSize())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        List<MarketDeliveryEntity> outgoing = await (
                from delivery in _db.MarketDeliveries.AsNoTracking()
                join deal in _db.MarketDeals.AsNoTracking() on delivery.DealId equals deal.Id
                where deal.SellerPlayerId == playerId && delivery.State == MarketDeliveryStates.Travelling
                orderby delivery.ArrivesAt
                select delivery)
            .Take(PageSize())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DeliveriesDto([.. incoming.Select(MarketMapper.ToDto)], [.. outgoing.Select(MarketMapper.ToDto)]);
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
    /// <returns>The deals.</returns>
    public async Task<IReadOnlyList<DealDto>> DealsAsync(string playerId, int offset, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<MarketDealEntity> deals = await _db.MarketDeals.AsNoTracking()
            .Where(deal => deal.BuyerPlayerId == playerId || deal.SellerPlayerId == playerId)
            .OrderByDescending(deal => deal.Id)
            .Skip(Math.Max(0, offset))
            .Take(PageSize())
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. deals.Select(MarketMapper.ToDto)];
    }

    private int PageSize() => MarketSettings.From(_config).PageSize;
}

/// <summary>What a player looks for on the market.</summary>
/// <param name="MarketLevelId">The level of the market.</param>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="PartTypeId">The part.</param>
/// <param name="SellerPlayerId">The seller.</param>
/// <param name="MaxSum">The largest price of a whole lot.</param>
/// <param name="Offset">How many lots to skip.</param>
public sealed record LotFilter(
    int? MarketLevelId = null,
    int? ResourceTypeId = null,
    int? PartTypeId = null,
    string? SellerPlayerId = null,
    long? MaxSum = null,
    int Offset = 0);

/// <summary>The deliveries of a player that still travel.</summary>
/// <param name="Incoming">The goods that travel to the player.</param>
/// <param name="Outgoing">The goods of the sold lots of the player that travel to the buyers.</param>
public sealed record DeliveriesDto(IReadOnlyList<DeliveryDto> Incoming, IReadOnlyList<DeliveryDto> Outgoing);

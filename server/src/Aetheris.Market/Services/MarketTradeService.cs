using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Market.Api;
using Aetheris.Market.Configuration;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Services;

/// <summary>
/// The commands of the market: putting a lot up, cancelling it, buying it, the arrival of the
/// goods and the exchange of the currencies (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// Every command runs in the transaction of the request. A lot is locked as a row before it
/// changes, so two buyers, or a buyer and the seller, never act on the same lot together.
/// </remarks>
public sealed class MarketTradeService
{
    /// <summary>The type of the event of the arrival of a delivery.</summary>
    public const string DeliveryEventType = "market.delivery_ready";

    private readonly MarketDbContext _db;
    private readonly MarketUnitOfWork _unitOfWork;
    private readonly MarketCatalog _catalog;
    private readonly IPlanetsModule _planets;
    private readonly IPlanetResources _resources;
    private readonly PartStockService _parts;
    private readonly IEconomyModule _economy;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IMarketEvents _events;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketTradeService"/> class.
    /// </summary>
    /// <param name="db">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalog">The reads of the reference data of the market.</param>
    /// <param name="planets">The bodies module.</param>
    /// <param name="resources">The balance of a body.</param>
    /// <param name="parts">The storage of the parts.</param>
    /// <param name="economy">The balances of the currencies.</param>
    /// <param name="world">The world module.</param>
    /// <param name="scheduler">The scheduler of the events.</param>
    /// <param name="events">The events for the players.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The game clock.</param>
    public MarketTradeService(
        MarketDbContext db,
        MarketUnitOfWork unitOfWork,
        MarketCatalog catalog,
        IPlanetsModule planets,
        IPlanetResources resources,
        PartStockService parts,
        IEconomyModule economy,
        IWorldModule world,
        IEventScheduler scheduler,
        IMarketEvents events,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _db = db;
        _unitOfWork = unitOfWork;
        _catalog = catalog;
        _planets = planets;
        _resources = resources;
        _parts = parts;
        _economy = economy;
        _world = world;
        _scheduler = scheduler;
        _events = events;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Puts goods of a body up for sale. The goods leave the stock of the body and wait in the lot.
    /// </summary>
    /// <param name="playerId">The seller.</param>
    /// <param name="request">What the seller offers.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The lots made: one, or one per stack when parts of several marks were taken.</returns>
    public Task<IReadOnlyList<LotDto>> ListLotAsync(string playerId, ListLotRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                if (request.Quantity <= 0 || request.PricePerUnit < 0 || (request.ResourceTypeId is null) == (request.PartTypeId is null))
                {
                    throw new GameException(MarketErrorCodes.AmountInvalid, "The quantity, the price or the item of the lot is not valid.");
                }

                Checked(request.Quantity, request.PricePerUnit);
                MarketSettings settings = MarketSettings.From(_config);
                PlanetFacts planet = await OwnedPlanetAsync(request.PlanetId, playerId, cancellationToken).ConfigureAwait(false);

                MarketLevelEntity level = await _catalog.FindLevelAsync(request.MarketLevelId, cancellationToken).ConfigureAwait(false)
                    ?? throw new GameException(MarketErrorCodes.LevelUnknown, "The level of the market does not exist.");

                if (!level.IsActive)
                {
                    throw new GameException(MarketErrorCodes.LevelInactive, "The level of the market takes no lots.");
                }

                await RequireBuildingAsync(BuildingClasses.TradeCenter, request.PlanetId, playerId, MarketErrorCodes.TradeCentreMissing, cancellationToken).ConfigureAwait(false);
                await RequireBuildingAsync(BuildingClasses.Portal, request.PlanetId, playerId, MarketErrorCodes.PortalMissing, cancellationToken).ConfigureAwait(false);

                // The lots of one seller are counted under a lock of the seller, so two lots put
                // up together never pass the limit together.
                await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({playerId}))", cancellationToken).ConfigureAwait(false);
                LotLimitDto limit = await LimitAsync(playerId, cancellationToken).ConfigureAwait(false);

                if (limit.Used >= limit.Limit)
                {
                    throw new GameException(MarketErrorCodes.LotLimitReached, "The civilization has as many lots as its trade centres allow.");
                }

                await RequireItemAsync(request.ResourceTypeId, request.PartTypeId, cancellationToken).ConfigureAwait(false);
                await CheckPriceAsync(request.ResourceTypeId, request.PartTypeId, request.PricePerUnit, settings, cancellationToken).ConfigureAwait(false);

                List<MarketLotEntity> lots = [];
                DateTimeOffset now = _clock.GameNow;

                if (request.ResourceTypeId is { } resourceTypeId)
                {
                    await _resources
                        .TakeAsync(request.PlanetId, playerId, new Dictionary<int, long> { [resourceTypeId] = request.Quantity }, cancellationToken)
                        .ConfigureAwait(false);
                    lots.Add(NewLot(playerId, planet, level, request, request.Quantity, now));
                }
                else
                {
                    IReadOnlyList<PartStackTaken> taken = await _parts
                        .TakeStackAsync(request.PlanetId, request.PartTypeId!.Value, request.PartConditionPercent ?? PartCondition.Full, request.Quantity, cancellationToken)
                        .ConfigureAwait(false);

                    foreach (PartStackTaken stack in taken)
                    {
                        MarketLotEntity lot = NewLot(playerId, planet, level, request, stack.Quantity, now);
                        lot.PartMarkLevel = stack.MarkLevel;
                        lot.PartHasRaceBonus = stack.HasRaceBonus;
                        lot.PartConditionPercent = stack.ConditionPercent;
                        lots.Add(lot);
                    }

                    if (lots.Count + limit.Used > limit.Limit)
                    {
                        throw new GameException(MarketErrorCodes.LotLimitReached, "The parts are of several marks, so they need more lots than the trade centres allow.");
                    }
                }

                _db.MarketLots.AddRange(lots);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return (IReadOnlyList<LotDto>)[.. lots.Select(MarketMapper.ToDto)];
            },
            cancellationToken);
    }

    /// <summary>
    /// Takes the lot of the seller off the market and returns the goods to the stock of the body.
    /// </summary>
    /// <param name="playerId">The seller.</param>
    /// <param name="lotId">The lot.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The lot after the change.</returns>
    public Task<LotDto> CancelLotAsync(string playerId, long lotId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                MarketLotEntity lot = await LockLotAsync(lotId, cancellationToken).ConfigureAwait(false);

                if (!string.Equals(lot.SellerPlayerId, playerId, StringComparison.Ordinal))
                {
                    throw new GameException(MarketErrorCodes.LotUnknown, "The lot is not a lot of the player.");
                }

                if (!string.Equals(lot.State, MarketLotStates.Open, StringComparison.Ordinal))
                {
                    throw new GameException(MarketErrorCodes.LotUnavailable, "The lot is sold or cancelled already.");
                }

                await GiveGoodsAsync(lot, lot.SellerPlanetId, lot.SellerPlayerId, cancellationToken).ConfigureAwait(false);
                lot.State = MarketLotStates.Cancelled;
                lot.Version++;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return MarketMapper.ToDto(lot);
            },
            cancellationToken);
    }

    /// <summary>
    /// Buys a whole lot: pays the seller, takes the delivery fee and sends the goods to a body of the buyer.
    /// </summary>
    /// <param name="playerId">The buyer.</param>
    /// <param name="lotId">The lot.</param>
    /// <param name="destinationPlanetId">The body of the buyer the goods arrive at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The deal.</returns>
    public Task<DealDto> BuyAsync(string playerId, long lotId, long destinationPlanetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                MarketSettings settings = MarketSettings.From(_config);
                MarketLotEntity lot = await LockLotAsync(lotId, cancellationToken).ConfigureAwait(false);

                if (!string.Equals(lot.State, MarketLotStates.Open, StringComparison.Ordinal))
                {
                    throw new GameException(MarketErrorCodes.LotUnavailable, "The lot is sold or cancelled already.");
                }

                if (string.Equals(lot.SellerPlayerId, playerId, StringComparison.Ordinal))
                {
                    throw new GameException(MarketErrorCodes.OwnLot, "A player cannot buy their own lot.");
                }

                PlanetFacts destination = await OwnedPlanetAsync(destinationPlanetId, playerId, cancellationToken).ConfigureAwait(false);
                MarketLevelEntity level = await _catalog.FindLevelAsync(lot.MarketLevelId, cancellationToken).ConfigureAwait(false)
                    ?? throw new GameException(MarketErrorCodes.LevelUnknown, "The level of the market does not exist.");

                if (!MarketCatalog.Sees(level, destination.SectorId, lot.SellerSectorId))
                {
                    throw new GameException(MarketErrorCodes.LotNotVisible, "The player does not see the lot.");
                }

                await RequireBuildingAsync(BuildingClasses.Portal, destinationPlanetId, playerId, MarketErrorCodes.PortalMissing, cancellationToken).ConfigureAwait(false);
                await RequireBuildingAsync(BuildingClasses.Portal, lot.SellerPlanetId, lot.SellerPlayerId, MarketErrorCodes.SellerPortalMissing, cancellationToken).ConfigureAwait(false);

                (long sum, long commission) = Money(lot, level);
                double distance = await DistanceAsync(lot, destination, cancellationToken).ConfigureAwait(false);
                (int energyResourceTypeId, long energy) = await DeliveryFeeAsync(lot, distance, settings, cancellationToken).ConfigureAwait(false);

                if (energy > 0)
                {
                    await _resources
                        .TakeAsync(destinationPlanetId, playerId, new Dictionary<int, long> { [energyResourceTypeId] = energy }, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (sum > 0)
                {
                    int tradeId = (await _catalog.CurrenciesAsync(cancellationToken).ConfigureAwait(false)).TradeResourceTypeId;
                    await _economy.TakeAsync(playerId, new Dictionary<int, long> { [tradeId] = sum }, cancellationToken).ConfigureAwait(false);
                    await _economy.GiveAsync(lot.SellerPlayerId, new Dictionary<int, long> { [tradeId] = sum - commission }, cancellationToken).ConfigureAwait(false);
                }

                long seconds = MarketRules.DeliverySeconds(distance, settings);
                DateTimeOffset now = _clock.GameNow;

                lot.State = MarketLotStates.Sold;
                lot.Version++;

                MarketDealEntity deal = new()
                {
                    LotId = lot.Id,
                    SellerPlayerId = lot.SellerPlayerId,
                    BuyerPlayerId = playerId,
                    ResourceTypeId = lot.ResourceTypeId,
                    PartTypeId = lot.PartTypeId,
                    Quantity = lot.Quantity,
                    PricePerUnit = lot.PricePerUnit,
                    Sum = sum,
                    Commission = commission,
                    CreatedAt = now,
                };
                MarketDeliveryEntity delivery = new()
                {
                    BuyerPlayerId = playerId,
                    DestinationPlanetId = destinationPlanetId,
                    DestinationSectorId = destination.SectorId,
                    ResourceTypeId = lot.ResourceTypeId,
                    PartTypeId = lot.PartTypeId,
                    Quantity = lot.Quantity,
                    EnergyRodAmount = energy,
                    ArrivesAt = now.AddSeconds(seconds),
                };

                _db.MarketDeals.Add(deal);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                delivery.DealId = deal.Id;
                _db.MarketDeliveries.Add(delivery);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _scheduler.ScheduleAsync(
                    new ScheduledEventRequest(
                        destination.SectorId,
                        DeliveryEventType,
                        delivery.ArrivesAt,
                        JsonSerializer.Serialize(new DeliveryPayload(delivery.Id)),
                        DeliveryEventType + ":" + delivery.Id.ToString(CultureInfo.InvariantCulture)),
                    cancellationToken).ConfigureAwait(false);

                DealDto dealDto = MarketMapper.ToDto(deal);
                await _events.SoldAsync(lot.SellerPlayerId, dealDto, cancellationToken).ConfigureAwait(false);

                return dealDto;
            },
            cancellationToken);
    }

    /// <summary>
    /// Lands a delivery: the goods go to the stock of the body of the buyer. A delivery that
    /// has landed lands no more.
    /// </summary>
    /// <param name="deliveryId">The delivery.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the goods are in the stock.</returns>
    public Task DeliverAsync(long deliveryId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                List<MarketDeliveryEntity> rows = await _db.MarketDeliveries
                    .FromSql($"SELECT * FROM market_delivery WHERE id = {deliveryId} FOR UPDATE")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (rows.Count == 0 || !string.Equals(rows[0].State, MarketDeliveryStates.Travelling, StringComparison.Ordinal))
                {
                    return true;
                }

                MarketDeliveryEntity delivery = rows[0];
                MarketLotEntity lot = await _db.MarketLots.AsNoTracking()
                    .Where(entry => _db.MarketDeals.Any(deal => deal.Id == delivery.DealId && deal.LotId == entry.Id))
                    .SingleAsync(cancellationToken)
                    .ConfigureAwait(false);

                await GiveGoodsAsync(lot, delivery.DestinationPlanetId, delivery.BuyerPlayerId, cancellationToken).ConfigureAwait(false);
                delivery.State = MarketDeliveryStates.Arrived;
                delivery.Version++;
                await _events.DeliveredAsync(delivery.BuyerPlayerId, MarketMapper.ToDto(delivery), cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Swaps credits for the trade currency or the trade currency for credits at the rate of the moment.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="giveResourceTypeId">The currency the player gives.</param>
    /// <param name="amount">How much of it.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The exchange.</returns>
    public Task<ExchangeDto> ExchangeAsync(string playerId, int giveResourceTypeId, long amount, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                if (amount <= 0)
                {
                    throw new GameException(MarketErrorCodes.AmountInvalid, "The amount of the exchange is not positive.");
                }

                MarketSettings settings = MarketSettings.From(_config);
                MarketCurrencies currencies = await _catalog.CurrenciesAsync(cancellationToken).ConfigureAwait(false);
                bool givesTrade = giveResourceTypeId == currencies.TradeResourceTypeId;

                if (!givesTrade && giveResourceTypeId != currencies.CreditsResourceTypeId)
                {
                    throw new GameException(MarketErrorCodes.CurrencyUnknown, "The exchange swaps only the credits and the trade currency.");
                }

                DateTimeOffset now = _clock.GameNow;
                decimal rate = await _catalog.RateAsync(currencies, settings, now, cancellationToken).ConfigureAwait(false);
                decimal keep = MarketRules.KeptShare(settings.ExchangeCommissionPercent);
                int getResourceTypeId = givesTrade ? currencies.CreditsResourceTypeId : currencies.TradeResourceTypeId;
                long got = (long)decimal.Floor(givesTrade ? amount * rate * keep : amount / rate * keep);

                await _economy.TakeAsync(playerId, new Dictionary<int, long> { [giveResourceTypeId] = amount }, cancellationToken).ConfigureAwait(false);

                if (got > 0)
                {
                    await _economy.GiveAsync(playerId, new Dictionary<int, long> { [getResourceTypeId] = got }, cancellationToken).ConfigureAwait(false);
                }

                MarketExchangeEntity exchange = new()
                {
                    PlayerId = playerId,
                    FromResourceTypeId = giveResourceTypeId,
                    ToResourceTypeId = getResourceTypeId,
                    FromAmount = amount,
                    ToAmount = got,
                    Rate = rate,
                    CommissionPercent = settings.ExchangeCommissionPercent,
                    CreatedAt = now,
                };

                _db.MarketExchanges.Add(exchange);
                _db.MarketRates.Add(new MarketRateEntity { Rate = rate, RecordedAt = now });
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return MarketMapper.ToDto(exchange);
            },
            cancellationToken);
    }

    /// <summary>
    /// Counts the lots of a civilization and the lots its trade centres allow.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The lots used and the limit; the limit is the best of the bodies of the player.</returns>
    public async Task<LotLimitDto> LimitAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        int used = await _db.MarketLots.AsNoTracking()
            .CountAsync(lot => lot.SellerPlayerId == playerId && lot.State == MarketLotStates.Open, cancellationToken)
            .ConfigureAwait(false);
        long limit = 0;

        foreach (OwnedPlanet owned in await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false))
        {
            limit = Math.Max(limit, await _planets.GetBuildingEffectTotalAsync(owned.PlanetId, BuildingEffectTypes.MarketLotLimit, cancellationToken).ConfigureAwait(false));
        }

        return new LotLimitDto(used, (int)Math.Min(limit, int.MaxValue));
    }

    /// <summary>
    /// Counts what a purchase of a lot would cost, without doing it.
    /// </summary>
    /// <param name="lot">The lot.</param>
    /// <param name="destination">The body the goods would arrive at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum, the commission, the fee and the time.</returns>
    public async Task<QuoteDto> QuoteAsync(MarketLotEntity lot, PlanetFacts destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentNullException.ThrowIfNull(destination);

        MarketSettings settings = MarketSettings.From(_config);
        MarketLevelEntity level = await _catalog.FindLevelAsync(lot.MarketLevelId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(MarketErrorCodes.LevelUnknown, "The level of the market does not exist.");
        (long sum, long commission) = Money(lot, level);
        double distance = await DistanceAsync(lot, destination, cancellationToken).ConfigureAwait(false);
        (int energyResourceTypeId, long energy) = await DeliveryFeeAsync(lot, distance, settings, cancellationToken).ConfigureAwait(false);

        return new QuoteDto(sum, commission, energyResourceTypeId, energy, MarketRules.DeliverySeconds(distance, settings));
    }

    private static long Checked(long quantity, long price)
    {
        try
        {
            return checked(quantity * price);
        }
        catch (OverflowException)
        {
            throw new GameException(MarketErrorCodes.AmountInvalid, "The sum of the lot is too large.");
        }
    }

    private static (long Sum, long Commission) Money(MarketLotEntity lot, MarketLevelEntity level)
    {
        long sum = Checked(lot.Quantity, lot.PricePerUnit);

        return (sum, MarketRules.Commission(sum, level.CommissionPercent));
    }

    private static MarketLotEntity NewLot(string playerId, PlanetFacts planet, MarketLevelEntity level, ListLotRequest request, long quantity, DateTimeOffset now) =>
        new()
        {
            SellerPlayerId = playerId,
            SellerPlanetId = planet.PlanetId,
            SellerSectorId = planet.SectorId,
            MarketLevelId = level.Id,
            ResourceTypeId = request.ResourceTypeId,
            PartTypeId = request.PartTypeId,
            Quantity = quantity,
            PricePerUnit = request.PricePerUnit,
            CreatedAt = now,
        };

    private async Task<PlanetFacts> OwnedPlanetAsync(long planetId, string playerId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        return facts is not null && string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal)
            ? facts
            : throw new GameException(MarketErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
    }

    private async Task RequireBuildingAsync(string buildingClass, long planetId, string playerId, string errorCode, CancellationToken cancellationToken)
    {
        if (await _planets.GetBuildingLevelAsync(buildingClass, planetId, playerId, cancellationToken).ConfigureAwait(false) <= 0)
        {
            throw new GameException(errorCode, "The body lacks a building the market needs.");
        }
    }

    private async Task<MarketLotEntity> LockLotAsync(long lotId, CancellationToken cancellationToken)
    {
        List<MarketLotEntity> rows = await _db.MarketLots
            .FromSql($"SELECT * FROM market_lot WHERE id = {lotId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Count == 0 ? throw new GameException(MarketErrorCodes.LotUnknown, "The lot does not exist.") : rows[0];
    }

    private async Task RequireItemAsync(int? resourceTypeId, int? partTypeId, CancellationToken cancellationToken)
    {
        bool known;

        if (resourceTypeId is { } resourceId)
        {
            ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
            known = catalog.Resources.Any(resource => resource.Id == resourceId && !resource.IsArchived && !resource.IsCivilizationScope);
        }
        else
        {
            IReadOnlyDictionary<int, PartFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);
            known = facts.TryGetValue(partTypeId!.Value, out PartFacts? part) && !part.IsArchived;
        }

        if (!known)
        {
            throw new GameException(MarketErrorCodes.ItemUnknown, "The item cannot be sold on the market.");
        }
    }

    private async Task CheckPriceAsync(int? resourceTypeId, int? partTypeId, long price, MarketSettings settings, CancellationToken cancellationToken)
    {
        // A price of zero is a gift between players and always allowed (decision 5).
        if (price == 0)
        {
            return;
        }

        long reference = await _catalog.ReferencePriceAsync(resourceTypeId, partTypeId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(MarketErrorCodes.NoReferencePrice, "The item has no reference price.");
        (long deals, decimal? average) = await _catalog.DealStatisticsAsync(resourceTypeId, partTypeId, _clock.GameNow.AddDays(-settings.AverageWindowDays), cancellationToken).ConfigureAwait(false);
        (long min, long max) = MarketRules.PriceLimit(reference, deals, average, settings);

        if (price < min || price > max)
        {
            throw new GameException(
                MarketErrorCodes.PriceOutOfRange,
                "The price is outside the limit of the item.",
                new Dictionary<string, object?> { ["min"] = min, ["max"] = max });
        }
    }

    private async Task<(int ResourceTypeId, long Amount)> DeliveryFeeAsync(MarketLotEntity lot, double distance, MarketSettings settings, CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        ResourceTypeFact energy = catalog.Resources.FirstOrDefault(resource => string.Equals(resource.Code, settings.EnergyResourceCode, StringComparison.Ordinal))
            ?? throw new GameException(MarketErrorCodes.EnergyResourceMissing, "The resource of the delivery fee is not in the data.");

        // The mass of the lot is the weight of one unit times the quantity, for a resource and
        // for a part alike (task 00027).
        decimal unitWeight;

        if (lot.ResourceTypeId is { } resourceTypeId)
        {
            unitWeight = catalog.Resources.First(resource => resource.Id == resourceTypeId).Weight;
        }
        else
        {
            IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);
            unitWeight = parts[lot.PartTypeId!.Value].Weight;
        }

        return (energy.Id, MarketRules.DeliveryEnergy(unitWeight * lot.Quantity, distance, settings));
    }

    private async Task<double> DistanceAsync(MarketLotEntity lot, PlanetFacts destination, CancellationToken cancellationToken)
    {
        PlanetFacts source = await _world.GetPlanetFactsAsync(lot.SellerPlanetId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(MarketErrorCodes.PlanetUnknown, "The body of the seller is gone.");

        return source.SystemId == destination.SystemId
            ? 0
            : await _world.GetSystemDistanceAsync(source.SystemId, destination.SystemId, cancellationToken).ConfigureAwait(false) ?? 0;
    }

    private async Task GiveGoodsAsync(MarketLotEntity lot, long planetId, string playerId, CancellationToken cancellationToken)
    {
        if (lot.ResourceTypeId is { } resourceTypeId)
        {
            await _resources
                .GiveAsync(planetId, playerId, new Dictionary<int, long> { [resourceTypeId] = lot.Quantity }, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _parts
                .AddAsync(
                    planetId,
                    [new PartStackAdded(lot.PartTypeId!.Value, lot.PartMarkLevel ?? 0, lot.PartHasRaceBonus ?? false, lot.PartConditionPercent ?? PartCondition.Full, lot.Quantity)],
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>The payload of the event of the arrival of a delivery.</summary>
    /// <param name="DeliveryId">The delivery.</param>
    public sealed record DeliveryPayload(long DeliveryId);
}

/// <summary>What a seller offers.</summary>
/// <param name="PlanetId">The body whose stock the goods leave.</param>
/// <param name="MarketLevelId">The level of the market.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="PricePerUnit">The price of a unit in the trade currency.</param>
/// <param name="PartConditionPercent">The condition of the parts to sell; new parts when empty.</param>
public sealed record ListLotRequest(
    long PlanetId,
    int MarketLevelId,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long PricePerUnit,
    int? PartConditionPercent = null);

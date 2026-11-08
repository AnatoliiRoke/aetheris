using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Requirements;
using Aetheris.Research;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The shipyard queue of a body: assembly, cancellation, completion and dismantling
/// (task 00039).
/// </summary>
/// <remarks>
/// <para>
/// The queue belongs to the body. The records follow one another. One record is one batch:
/// the whole batch is paid when it enters, the ships are finished one by one, and a
/// cancellation returns only the ships that are not finished yet. The crew of a finished ship
/// stays on that ship. A dismantling reads the project and returns every component of it
/// and the crew. The resources stay where they are.
/// </para>
/// <para>
/// Every change locks the row of the queue of the body first, then the storage of the parts,
/// then the balance of the body, and the economy last.
/// </para>
/// </remarks>
public sealed class ShipyardQueueService
{
    /// <summary>The event type of the end of an assembly.</summary>
    public const string CompleteEventType = "shipyard.complete";

    /// <summary>The code a missing exact stack raises in the storage of the parts.</summary>
    private const string ForeignPartMissing = "production_part_missing";

    /// <summary>
    /// The neutral code of the resource group that is tanked into a ship and is not spent
    /// when the ship is assembled.
    /// </summary>
    private const string FuelGroupCode = "resource_group_3";

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly IProductionModule _production;
    private readonly IPartStock _parts;
    private readonly IPlanetsModule _planets;
    private readonly IPlanetResources _resources;
    private readonly IEconomyModule _economy;
    private readonly IResearchModule _research;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipyardQueueService"/> class.
    /// </summary>
    public ShipyardQueueService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        IProductionModule production,
        IPartStock parts,
        IPlanetsModule planets,
        IPlanetResources resources,
        IEconomyModule economy,
        IResearchModule research,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(research);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _production = production;
        _parts = parts;
        _planets = planets;
        _resources = resources;
        _economy = economy;
        _research = research;
        _world = world;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Puts a project into the shipyard queue of a body and pays for it.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The shipyard, or <see langword="null"/> to take the shipyard of the body.</param>
    /// <param name="designId">The project.</param>
    /// <param name="quantity">How many ships to assemble. <see langword="null"/> means one.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The shipyard after the change.</returns>
    public Task<ShipyardViewDto> EnqueueAsync(
        string playerId,
        long planetId,
        long? planetBuildingId,
        long designId,
        long? quantity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetFacts facts = await OwnedPlanetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                await LockStateAsync(planetId, cancellationToken).ConfigureAwait(false);
                List<ShipyardOrderEntity> queue = await LoadQueueAsync(planetId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, facts, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                Yard yard = await YardAsync(playerId, planetId, planetBuildingId, required: true, cancellationToken).ConfigureAwait(false);
                FleetSettings settings = FleetSettings.From(_config);
                int places = Places(settings, yard.Effects);

                if (queue.Count >= places)
                {
                    throw new GameException(FleetErrorCodes.ShipyardQueueFull, "The queue of the shipyard has no free place.");
                }

                long batch = quantity ?? 1;

                if (batch <= 0)
                {
                    throw new GameException(FleetErrorCodes.QuantityInvalid, "The batch must hold at least one ship.");
                }

                AssemblyQuote unitQuote = await QuoteAsync(playerId, designId, cancellationToken).ConfigureAwait(false);
                RefuseStructure(unitQuote);
                await RefuseHullAsync(playerId, planetId, unitQuote, cancellationToken).ConfigureAwait(false);

                if (unitQuote.BaseSeconds >= 1)
                {
                    decimal multiplier = await _research.ShipyardTimeMultiplierAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                    CheckBatch(_clock.GameNow, ShipyardAssembly.Scale(unitQuote.BaseSeconds, multiplier), batch, unitQuote);
                }

                AssemblyQuote quote = ForBatch(unitQuote, batch);
                ResourceKinds kinds = await KindsAsync(cancellationToken).ConfigureAwait(false);
                await _parts.LockAsync(planetId, facts.SectorId, cancellationToken).ConfigureAwait(false);
                await RefuseStockAsync(playerId, planetId, quote, kinds, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<ShipPartStack> stock = await _parts.ReadAsync(
                    planetId,
                    [.. quote.PartLines.Select(line => line.PartTypeId)],
                    cancellationToken).ConfigureAwait(false);
                List<TakenPart> takenParts = PartsToTake(stock, quote.PartLines);

                try
                {
                    await _parts.TakeMarkedAsync(
                        planetId,
                        [.. takenParts.Select(part => new ShipPartRequest(part.PartTypeId, part.MarkLevel, part.HasRaceBonus, part.Quantity))],
                        cancellationToken).ConfigureAwait(false);
                }
                catch (GameException exception) when (exception.Code == ForeignPartMissing)
                {
                    throw new GameException(FleetErrorCodes.PartMissing, "The body lacks the parts of the project.");
                }

                if (quote.Materials.Count > 0)
                {
                    await _resources.TakeAsync(planetId, playerId, quote.Materials, cancellationToken).ConfigureAwait(false);
                }

                if (quote.Crew > 0
                    && !await _economy.TryTakePopulationAsync(playerId, planetId, quote.Crew, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(FleetErrorCodes.CrewShort, "The body has fewer inhabitants than the crew of the ship.");
                }

                if (quote.Credits > 0 && kinds.CreditId is int creditId)
                {
                    await _economy.TakeAsync(playerId, new Dictionary<int, long> { [creditId] = quote.Credits }, cancellationToken).ConfigureAwait(false);
                }

                DateTimeOffset now = _clock.GameNow;
                PlanetBuildingLevelById building = yard.Building
                    ?? throw new GameException(FleetErrorCodes.ShipyardMissing, "The body has no shipyard.");
                ShipDesignEntity design = quote.Design
                    ?? throw new GameException(FleetErrorCodes.DesignUnknown, "The project cannot be assembled.");
                ShipyardOrderEntity order = new()
                {
                    PlayerId = playerId,
                    PlanetId = planetId,
                    PlanetBuildingId = building.PlanetBuildingId,
                    ShipDesignId = design.Id,
                    Position = queue.Count + 1,
                    Quantity = batch,
                    ProducedQuantity = 0,
                    BaseDurationSeconds = unitQuote.BaseSeconds,
                    Crew = quote.Crew,
                    UpkeepPerHour = unitQuote.UpkeepPerHour,
                    CreatedAt = now,
                    CostIds = CostsOf(quote, kinds),
                    PartIds =
                    [
                        .. takenParts.Select(part => new ShipyardOrderPartEntity
                        {
                            PartTypeId = part.PartTypeId,
                            MarkLevel = part.MarkLevel,
                            HasRaceBonus = part.HasRaceBonus,
                            ConditionPercent = PartConditions.Full,
                            Quantity = part.Quantity,
                            IsHull = part.IsHull,
                        }),
                    ],
                };

                _dbContext.ShipyardOrders.Add(order);
                queue.Add(order);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await StartNextAsync(queue, now, cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, planetId, yard, queue, settings, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a record from the queue and returns the cost of the ships it has not finished.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="orderId">The record.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The shipyard after the change.</returns>
    public Task<ShipyardViewDto> CancelAsync(string playerId, long orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                ShipyardOrderEntity? located = await _dbContext.ShipyardOrders.AsNoTracking()
                    .Where(order => order.Id == orderId && order.PlayerId == playerId)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new GameException(FleetErrorCodes.ShipyardOrderUnknown, "The record of the queue does not exist.");

                PlanetFacts facts = await OwnedPlanetAsync(playerId, located.PlanetId, cancellationToken).ConfigureAwait(false);
                await LockStateAsync(located.PlanetId, cancellationToken).ConfigureAwait(false);
                List<ShipyardOrderEntity> queue = await LoadQueueAsync(located.PlanetId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, facts, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                ShipyardOrderEntity? order = queue.SingleOrDefault(entry => entry.Id == orderId)
                    ?? throw new GameException(FleetErrorCodes.ShipyardOrderUnknown, "The record of the queue does not exist.");

                await _parts.LockAsync(located.PlanetId, facts.SectorId, cancellationToken).ConfigureAwait(false);
                long batch = order.Quantity < 1 ? 1 : order.Quantity;
                List<ShipPartStack> returnedParts = UnusedParts(order, batch);

                if (returnedParts.Count > 0)
                {
                    await _parts.AddAsync(located.PlanetId, returnedParts, cancellationToken).ConfigureAwait(false);
                }

                await ReturnCostsAsync(
                    playerId,
                    located.PlanetId,
                    [.. order.CostIds.Select(cost => new CostRow(cost.ResourceTypeId, Unused(cost.Amount, batch, order.ProducedQuantity)))],
                    extra: null,
                    cancellationToken).ConfigureAwait(false);

                queue.Remove(order);
                _dbContext.ShipyardOrders.Remove(order);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await StartNextAsync(queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                Yard yard = await YardAsync(playerId, located.PlanetId, order.PlanetBuildingId, required: false, cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, located.PlanetId, yard, queue, FleetSettings.From(_config), cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Takes a finished ship apart on the shipyard of its body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="shipId">The ship.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The shipyard after the change.</returns>
    public Task<ShipyardViewDto> DisassembleAsync(string playerId, long shipId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                ShipEntity? ship = await _dbContext.Ships
                    .SingleOrDefaultAsync(entry => entry.Id == shipId && entry.PlayerId == playerId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new GameException(FleetErrorCodes.ShipUnknown, "The ship does not exist.");

                PlanetFacts facts = await OwnedPlanetAsync(playerId, ship.PlanetId, cancellationToken).ConfigureAwait(false);
                await LockStateAsync(ship.PlanetId, cancellationToken).ConfigureAwait(false);

                // A ship in a fleet is not taken apart. The lock of the fleets of the player is the same
                // one a fleet command takes, so the ship does not join a fleet while it is taken apart.
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);

                if (await _dbContext.FleetShips.AsNoTracking().AnyAsync(entry => entry.ShipId == shipId, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(FleetErrorCodes.ShipInFleet, "The ship is in a fleet.");
                }

                List<ShipyardOrderEntity> queue = await LoadQueueAsync(ship.PlanetId, cancellationToken).ConfigureAwait(false);
                await CompleteDueAsync(playerId, facts, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                Yard yard = await YardAsync(playerId, ship.PlanetId, null, required: true, cancellationToken).ConfigureAwait(false);
                await _parts.LockAsync(ship.PlanetId, facts.SectorId, cancellationToken).ConfigureAwait(false);

                // The past interval is charged at the rate that still includes this ship.
                await _economy.FixPlayerAsync(playerId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                // The project is the list. Every component on it goes back to the storage of
                // the parts, the hull with them. The resources of the assembly do not.
                List<ShipPartStack> components = await ProjectComponentsAsync(ship, cancellationToken).ConfigureAwait(false);

                if (components.Count > 0)
                {
                    await _parts.AddAsync(ship.PlanetId, components, cancellationToken).ConfigureAwait(false);
                }

                if (ship.Crew > 0)
                {
                    await _economy.GivePopulationAsync(playerId, ship.PlanetId, ship.Crew, cancellationToken).ConfigureAwait(false);
                }

                _dbContext.Ships.Remove(ship);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await PublishUpkeepAsync(playerId, cancellationToken).ConfigureAwait(false);

                return await ViewAsync(playerId, ship.PlanetId, yard, queue, FleetSettings.From(_config), cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Finishes the assemblies of a body that are due, for the scheduler.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="now">The moment. A late event uses its own end, so the ship appears then.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>How many assemblies finished.</returns>
    public Task<int> CompleteDueAsync(string playerId, long planetId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

                if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
                {
                    return 0;
                }

                await LockStateAsync(planetId, cancellationToken).ConfigureAwait(false);
                List<ShipyardOrderEntity> queue = await LoadQueueAsync(planetId, cancellationToken).ConfigureAwait(false);

                return await CompleteDueAsync(playerId, facts, queue, now, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Reads the shipyard of a body. A read finishes a record that is already due.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The shipyard, or <see langword="null"/> to take the shipyard of the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The shipyard.</returns>
    public async Task<ShipyardViewDto> ReadAsync(
        string playerId,
        long planetId,
        long? planetBuildingId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await OwnedPlanetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        List<ShipyardOrderEntity> queue = await LoadQueueAsync(planetId, cancellationToken).ConfigureAwait(false);
        ShipyardOrderEntity? head = queue.FirstOrDefault();
        DateTimeOffset now = _clock.GameNow;

        if (head is not null && (head.FinishesAt is null || head.FinishesAt <= now))
        {
            await CompleteDueAsync(playerId, planetId, now, cancellationToken).ConfigureAwait(false);
            queue = await LoadQueueAsync(planetId, cancellationToken).ConfigureAwait(false);
        }

        Yard yard = await YardAsync(playerId, planetId, planetBuildingId, required: false, cancellationToken).ConfigureAwait(false);

        return await ViewAsync(playerId, planetId, yard, queue, FleetSettings.From(_config), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the finished ships of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body, or <see langword="null"/> for every body.</param>
    /// <param name="freeOnly">Whether to leave out the ships that are in a fleet (task 00040).</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The ships.</returns>
    public async Task<IReadOnlyList<ShipDto>> ReadShipsAsync(
        string playerId,
        long? planetId,
        bool freeOnly,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<ShipEntity> ships = await ShipsOfAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        Dictionary<long, long> fleetOf = await FleetOfShipsAsync(ships, cancellationToken).ConfigureAwait(false);

        if (freeOnly)
        {
            ships = [.. ships.Where(ship => !fleetOf.ContainsKey(ship.Id))];
        }

        Dictionary<long, string> names = await DesignNamesAsync(playerId, [.. ships.Select(ship => ship.ShipDesignId)], cancellationToken).ConfigureAwait(false);
        Dictionary<int, string> partNames = await PartNamesAsync(cancellationToken).ConfigureAwait(false);
        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<long, ShipHullDto> hullsOfShips = HullsOfShips(ships, catalog);

        return [.. ships.Select(ship => ShipOf(ship, names, partNames, fleetOf, hullsOfShips.GetValueOrDefault(ship.Id)))];
    }

    /// <summary>
    /// The payload of the end of an assembly.
    /// </summary>
    /// <param name="PlayerId">The player.</param>
    /// <param name="PlanetId">The body.</param>
    /// <param name="OrderId">The record of the queue.</param>
    public sealed record CompletePayload(string PlayerId, long PlanetId, long OrderId);

    private async Task<int> CompleteDueAsync(
        string playerId,
        PlanetFacts facts,
        List<ShipyardOrderEntity> queue,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int completed = 0;
        bool prepared = false;

        while (queue.Count > 0)
        {
            ShipyardOrderEntity head = queue[0];

            if (head.FinishesAt is null)
            {
                if (!await StartNextAsync(queue, now, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }

            if (head.FinishesAt > now)
            {
                break;
            }

            if (!prepared)
            {
                await _parts.LockAsync(facts.PlanetId, facts.SectorId, cancellationToken).ConfigureAwait(false);
                await _resources.FixPlayerAsync(playerId, now, cancellationToken).ConfigureAwait(false);
                prepared = true;
            }

            DateTimeOffset finishedAt = head.FinishesAt.Value;
            await _economy.FixPlayerAsync(playerId, finishedAt, cancellationToken).ConfigureAwait(false);
            await FinishAsync(head, finishedAt, cancellationToken).ConfigureAwait(false);
            head.ProducedQuantity++;
            completed++;

            if (head.ProducedQuantity >= head.Quantity)
            {
                queue.RemoveAt(0);
                _dbContext.ShipyardOrders.Remove(head);
                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await StartNextAsync(queue, finishedAt, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ContinueAsync(head, finishedAt, cancellationToken).ConfigureAwait(false);
            }
        }

        if (completed > 0)
        {
            await PublishUpkeepAsync(playerId, cancellationToken).ConfigureAwait(false);
        }

        return completed;
    }

    private async Task FinishAsync(ShipyardOrderEntity order, DateTimeOffset finishedAt, CancellationToken cancellationToken)
    {
        long batch = order.Quantity < 1 ? 1 : order.Quantity;
        ShipEntity ship = new()
        {
            PlayerId = order.PlayerId,
            PlanetId = order.PlanetId,
            ShipDesignId = order.ShipDesignId,
            Crew = order.Crew / batch,
            UpkeepPerHour = order.UpkeepPerHour,
            StateCode = ShipStates.Ready,
            CreatedAt = finishedAt,
            ModuleIds = ModulesOf(order, batch),
            CostIds =
            [
                .. order.CostIds
                    .Select(cost => new ShipCostEntity
                    {
                        ResourceTypeId = cost.ResourceTypeId,
                        Amount = cost.Amount / batch,
                    })
                    .Where(cost => cost.Amount > 0),
            ],
        };

        _dbContext.Ships.Add(ship);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> StartNextAsync(List<ShipyardOrderEntity> queue, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0].FinishesAt is not null)
        {
            return false;
        }

        ShipyardOrderEntity head = queue[0];
        decimal multiplier = await _research.ShipyardTimeMultiplierAsync(head.PlayerId, head.PlanetId, cancellationToken).ConfigureAwait(false);
        long seconds = ShipyardAssembly.Scale(head.BaseDurationSeconds, multiplier);
        head.StartedAt = now;
        head.FinishesAt = now.AddSeconds(seconds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(head, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Starts the next ship of a batch that already has one finished. The key names the batch
    /// and how many ships are done, so the event of the ship just finished is not reused.
    /// </summary>
    private async Task ContinueAsync(ShipyardOrderEntity head, DateTimeOffset finishedAt, CancellationToken cancellationToken)
    {
        decimal multiplier = await _research.ShipyardTimeMultiplierAsync(head.PlayerId, head.PlanetId, cancellationToken).ConfigureAwait(false);
        long seconds = ShipyardAssembly.Scale(head.BaseDurationSeconds, multiplier);
        head.StartedAt = finishedAt;
        head.FinishesAt = finishedAt.AddSeconds(seconds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ScheduleAsync(head, cancellationToken).ConfigureAwait(false);
    }

    private async Task ScheduleAsync(ShipyardOrderEntity order, CancellationToken cancellationToken)
    {
        if (order.FinishesAt is not DateTimeOffset finishesAt)
        {
            return;
        }

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(order.PlanetId, cancellationToken).ConfigureAwait(false);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                facts?.SectorId ?? 0,
                CompleteEventType,
                finishesAt,
                JsonSerializer.Serialize(new CompletePayload(order.PlayerId, order.PlanetId, order.Id)),
                CompleteEventType
                    + ":" + order.Id.ToString(CultureInfo.InvariantCulture)
                    + ":" + order.ProducedQuantity.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ShipyardQueueStateEntity> LockStateAsync(long planetId, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync(
                $"INSERT INTO shipyard_queue_state (planet_id) VALUES ({planetId}) ON CONFLICT (planet_id) DO NOTHING",
                cancellationToken)
            .ConfigureAwait(false);

        return (await _dbContext.ShipyardQueueStates
            .FromSql($"SELECT * FROM shipyard_queue_state WHERE planet_id = {planetId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).Single();
    }

    private async Task<List<ShipyardOrderEntity>> LoadQueueAsync(long planetId, CancellationToken cancellationToken) =>
        [.. await _dbContext.ShipyardOrders
            .Include(order => order.CostIds)
            .Include(order => order.PartIds)
            .Where(order => order.PlanetId == planetId)
            .OrderBy(order => order.Position)
            .ThenBy(order => order.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

    private async Task PublishUpkeepAsync(string playerId, CancellationToken cancellationToken)
    {
        decimal perHour = await _dbContext.Ships.AsNoTracking()
            .Where(ship => ship.PlayerId == playerId)
            .SumAsync(ship => (decimal?)ship.UpkeepPerHour, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        await _economy.SetShipUpkeepPerHourAsync(playerId, perHour, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReturnPartsAsync(
        long planetId,
        IReadOnlyList<ShipyardOrderPartEntity> parts,
        bool hullToo,
        CancellationToken cancellationToken)
    {
        List<ShipPartStack> stacks =
        [
            .. parts
                .Where(part => part.Quantity > 0 && (hullToo || !part.IsHull))
                .Select(part => new ShipPartStack(part.PartTypeId, part.MarkLevel, part.HasRaceBonus, part.ConditionPercent, part.Quantity)),
        ];

        if (stacks.Count > 0)
        {
            await _parts.AddAsync(planetId, stacks, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReturnCostsAsync(
        string playerId,
        long planetId,
        IReadOnlyList<CostRow> costs,
        IReadOnlyDictionary<int, long>? extra,
        CancellationToken cancellationToken)
    {
        ResourceKinds kinds = await KindsAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, long> planet = [];
        Dictionary<int, long> currencies = [];
        long crew = 0;

        foreach (CostRow cost in costs)
        {
            if (kinds.PopulationId == cost.ResourceTypeId)
            {
                crew += cost.Amount;
            }
            else if (kinds.CurrencyIds.Contains(cost.ResourceTypeId))
            {
                planetAdd(currencies, cost.ResourceTypeId, cost.Amount);
            }
            else
            {
                planetAdd(planet, cost.ResourceTypeId, cost.Amount);
            }
        }

        if (extra is not null)
        {
            foreach ((int resourceTypeId, long amount) in extra)
            {
                planetAdd(planet, resourceTypeId, amount);
            }
        }

        if (planet.Count > 0)
        {
            await _resources.GiveAsync(planetId, playerId, planet, cancellationToken).ConfigureAwait(false);
        }

        if (crew > 0)
        {
            await _economy.GivePopulationAsync(playerId, planetId, crew, cancellationToken).ConfigureAwait(false);
        }

        if (currencies.Count > 0)
        {
            await _economy.GiveAsync(playerId, currencies, cancellationToken).ConfigureAwait(false);
        }

        static void planetAdd(Dictionary<int, long> amounts, int resourceTypeId, long amount)
        {
            if (amount > 0)
            {
                amounts[resourceTypeId] = amounts.GetValueOrDefault(resourceTypeId) + amount;
            }
        }
    }

    /// <summary>
    /// The components of the project a ship was built from: the hull and every module in its slots.
    /// </summary>
    /// <param name="ship">The ship.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks to put back into the storage of the parts.</returns>
    private async Task<List<ShipPartStack>> ProjectComponentsAsync(ShipEntity ship, CancellationToken cancellationToken)
    {
        ShipDesignEntity design = await _dbContext.ShipDesigns.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == ship.ShipDesignId && entry.OwnerPlayerId == ship.PlayerId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new GameException(FleetErrorCodes.DesignUnknown, "The project of the ship does not exist.");

        List<ShipDesignSlotEntity> slots = await _dbContext.ShipDesignSlots.AsNoTracking()
            .Where(slot => slot.DesignId == design.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<int, PartRecipeFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

        if (!facts.TryGetValue(design.HullPartTypeId, out PartRecipeFacts? hullFacts))
        {
            throw new GameException(FleetErrorCodes.HullUnknown, "The hull of the project does not exist.");
        }

        return
        [
            new ShipPartStack(design.HullPartTypeId, hullFacts.MarkLevel, false, PartConditions.Full, 1),
            .. slots
                .GroupBy(slot => (slot.PartTypeId, slot.MarkLevel, slot.HasRaceBonus))
                .Select(group => new ShipPartStack(
                    group.Key.PartTypeId,
                    group.Key.MarkLevel,
                    group.Key.HasRaceBonus,
                    PartConditions.Full,
                    group.LongCount())),
        ];
    }

    private async Task<ShipyardViewDto> ViewAsync(
        string playerId,
        long planetId,
        Yard yard,
        List<ShipyardOrderEntity> queue,
        FleetSettings settings,
        CancellationToken cancellationToken)
    {
        decimal multiplier = await _research.ShipyardTimeMultiplierAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        List<ShipDesignEntity> designs = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => design.OwnerPlayerId == playerId && !design.IsArchived)
            .OrderBy(design => design.Name)
            .ThenBy(design => design.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        AssemblyBoard board = await LoadBoardAsync(playerId, planetId, designs, cancellationToken).ConfigureAwait(false);
        Dictionary<int, AvailabilityDto> opened = await OpenedHullsAsync(playerId, planetId, board.Quotes.Values, cancellationToken).ConfigureAwait(false);
        Dictionary<long, string> names = designs.ToDictionary(design => design.Id, design => design.Name);
        Dictionary<long, string> shipNames = await DesignNamesAsync(
            playerId,
            [.. board.Ships.Select(ship => ship.ShipDesignId).Where(designId => !names.ContainsKey(designId))],
            cancellationToken).ConfigureAwait(false);

        foreach ((long designId, string name) in shipNames)
        {
            names[designId] = name;
        }

        int places = yard.Building is null ? 0 : Places(settings, yard.Effects);
        Dictionary<long, long> fleetOf = await FleetOfShipsAsync(board.Ships, cancellationToken).ConfigureAwait(false);
        Dictionary<int, string> partNames = board.Facts.ToDictionary(entry => entry.Key, entry => entry.Value.NameKey);
        Dictionary<long, ShipHullDto> hullsOfShips = HullsOfShips(board.Ships, board.Catalog);

        return new ShipyardViewDto(
            planetId,
            yard.Building?.PlanetBuildingId,
            yard.Building?.Level ?? 0,
            queue.Count,
            places,
            _clock.GameNow,
            _clock.Multiplier,
            [.. queue.Select(order => OrderOf(order, names, board.Kinds, multiplier))],
            [.. designs.Select(design => DesignCard(design, board.Quotes[design.Id], yard, places, queue.Count, multiplier, board, opened, partNames))],
            [.. board.Ships.Select(ship => ShipOf(ship, names, partNames, fleetOf, hullsOfShips.GetValueOrDefault(ship.Id)))]);
    }

    private static ShipyardDesignDto DesignCard(
        ShipDesignEntity design,
        AssemblyQuote quote,
        Yard yard,
        int places,
        int used,
        decimal multiplier,
        AssemblyBoard board,
        IReadOnlyDictionary<int, AvailabilityDto> opened,
        IReadOnlyDictionary<int, string> partNames)
    {
        string? reason = quote.RefusalCode;

        if (reason is null && yard.Building is null)
        {
            reason = FleetErrorCodes.ShipyardMissing;
        }

        RequirementDto? unmet = null;

        if (reason is null && opened.TryGetValue(quote.Hull!.Id, out AvailabilityDto? availability) && !availability.IsAvailable)
        {
            unmet = availability.UnmetRequirements[0];
            reason = unmet.ReasonCode;
        }

        if (reason is null && quote.PartLines.Any(line => Available(board.Stock, line) < line.Quantity))
        {
            reason = FleetErrorCodes.PartMissing;
        }

        if (reason is null)
        {
            reason = StockReason(quote, board.Kinds, board.Inhabitants, board.PlanetBalance, board.CurrencyBalance);
        }

        if (reason is null && quote.BaseSeconds < 1)
        {
            reason = FleetErrorCodes.AssemblyTimeMissing;
        }

        if (reason is null && used >= places)
        {
            reason = FleetErrorCodes.ShipyardQueueFull;
        }

        return new ShipyardDesignDto(
            design.Id,
            design.Name,
            design.HullPartTypeId,
            quote.Hull?.NameKey ?? string.Empty,
            reason is null,
            reason,
            unmet?.TargetNameKey,
            unmet?.TargetLevel,
            quote.Crew,
            quote.Credits,
            quote.BaseSeconds < 1 ? 0 : ShipyardAssembly.Scale(quote.BaseSeconds, multiplier),
            CostDtos(quote, board),
            [
                .. quote.PartLines.Select(line => new ShipyardPartDto(
                    line.PartTypeId,
                    partNames.GetValueOrDefault(line.PartTypeId) ?? string.Empty,
                    line.MarkLevel,
                    line.HasRaceBonus,
                    line.Quantity,
                    Available(board.Stock, line),
                    line.IsHull)),
            ]);
    }

    private static ShipyardOrderDto OrderOf(
        ShipyardOrderEntity order,
        IReadOnlyDictionary<long, string> names,
        ResourceKinds kinds,
        decimal multiplier)
    {
        long unitSeconds = ShipyardAssembly.Scale(order.BaseDurationSeconds, multiplier);
        long remaining = order.Quantity - order.ProducedQuantity;
        long duration = order.FinishesAt is DateTimeOffset finishes && order.StartedAt is DateTimeOffset started
            ? Math.Max(1, (long)(finishes - started).TotalSeconds)
            : WaitingSeconds(unitSeconds, remaining);

        return new ShipyardOrderDto(
            order.Id,
            order.ShipDesignId,
            names.GetValueOrDefault(order.ShipDesignId) ?? string.Empty,
            order.Position,
            order.FinishesAt is not null,
            order.FinishesAt,
            duration,
            [
                .. order.CostIds
                    .OrderBy(cost => cost.ResourceTypeId)
                    .Select(cost => new ShipyardCostDto(cost.ResourceTypeId, cost.Amount, kinds.NameKeys.GetValueOrDefault(cost.ResourceTypeId))),
            ],
            order.Quantity < 1 ? 1 : order.Quantity,
            order.ProducedQuantity);
    }

    private static ShipDto ShipOf(
        ShipEntity ship,
        IReadOnlyDictionary<long, string> names,
        IReadOnlyDictionary<int, string> partNames,
        IReadOnlyDictionary<long, long> fleetOf,
        ShipHullDto? hull) =>
        new(
            ship.Id,
            ship.PlanetId,
            ship.ShipDesignId,
            names.GetValueOrDefault(ship.ShipDesignId) ?? string.Empty,
            hull?.NameKey ?? string.Empty,
            hull?.PartGroupNameKey ?? string.Empty,
            ship.StateCode,
            ship.Crew,
            ship.UpkeepPerHour,
            [
                .. ship.ModuleIds
                    .OrderBy(module => module.IsHull ? 0 : 1)
                    .ThenBy(module => module.PartTypeId)
                    .Select(module => new ShipyardPartDto(
                        module.PartTypeId,
                        partNames.GetValueOrDefault(module.PartTypeId) ?? string.Empty,
                        module.MarkLevel,
                        module.HasRaceBonus,
                        module.Quantity,
                        0,
                        module.IsHull)),
            ],
            fleetOf.TryGetValue(ship.Id, out long fleetId) ? fleetId : null);

    /// <summary>
    /// The hull of every ship, by the number of the ship. The hull is the part of the ship with the
    /// flag of the hull, so it is read from the parts of the ship and not from the project (the
    /// project of a ship may be gone).
    /// </summary>
    /// <param name="ships">The ships.</param>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <returns>The hull of every ship that has one.</returns>
    private static Dictionary<long, ShipHullDto> HullsOfShips(IEnumerable<ShipEntity> ships, ShipCatalogDto catalog)
    {
        Dictionary<int, ShipHullDto> hulls = catalog.Hulls.ToDictionary(hull => hull.Id);
        Dictionary<long, ShipHullDto> result = [];

        foreach (ShipEntity ship in ships)
        {
            int? hullId = ship.ModuleIds.FirstOrDefault(module => module.IsHull)?.PartTypeId;

            if (hullId is int id && hulls.TryGetValue(id, out ShipHullDto? hull))
            {
                result[ship.Id] = hull;
            }
        }

        return result;
    }

    private async Task<AssemblyQuote> QuoteAsync(string playerId, long designId, CancellationToken cancellationToken)
    {
        ShipDesignEntity? design = await _dbContext.ShipDesigns.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == designId && entry.OwnerPlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        if (design is null)
        {
            return AssemblyQuote.Refused(designId, FleetErrorCodes.DesignUnknown);
        }

        if (design.IsArchived)
        {
            return AssemblyQuote.Refused(design, FleetErrorCodes.DesignArchived);
        }

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        List<ShipDesignSlotEntity> slots = await _dbContext.ShipDesignSlots.AsNoTracking()
            .Where(slot => slot.DesignId == design.Id)
            .OrderBy(slot => slot.SlotIndex)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyDictionary<int, PartRecipeFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);
        ResourceKinds kinds = await KindsAsync(cancellationToken).ConfigureAwait(false);

        return QuoteOf(design, slots, catalog, facts, kinds, FleetSettings.From(_config));
    }

    private static AssemblyQuote QuoteOf(
        ShipDesignEntity design,
        IReadOnlyList<ShipDesignSlotEntity> slots,
        ShipCatalogDto catalog,
        IReadOnlyDictionary<int, PartRecipeFacts> facts,
        ResourceKinds kinds,
        FleetSettings settings)
    {
        ShipHullDto? hull = catalog.Hulls.FirstOrDefault(entry => entry.Id == design.HullPartTypeId);

        if (hull is null)
        {
            return AssemblyQuote.Refused(design, FleetErrorCodes.HullUnknown);
        }

        if (string.Equals(hull.Role, HullRoles.Drone, StringComparison.Ordinal))
        {
            return AssemblyQuote.Refused(design, hull, FleetErrorCodes.HullIsDrone);
        }

        if (hull.IsArchived)
        {
            return AssemblyQuote.Refused(design, hull, FleetErrorCodes.HullArchived);
        }

        Dictionary<int, ShipModuleDto> modules = catalog.Modules.ToDictionary(module => module.Id);

        foreach (ShipDesignSlotEntity slot in slots)
        {
            if (!modules.ContainsKey(slot.PartTypeId))
            {
                return AssemblyQuote.Refused(design, hull, FleetErrorCodes.ModuleUnknown);
            }
        }

        if (!facts.TryGetValue(hull.Id, out PartRecipeFacts? hullFacts))
        {
            return AssemblyQuote.Refused(design, hull, FleetErrorCodes.HullUnknown);
        }

        List<QuotedPart> partLines =
        [
            new(hull.Id, hullFacts.MarkLevel, false, 1, true),
            .. slots
                .GroupBy(slot => (slot.PartTypeId, slot.MarkLevel, slot.HasRaceBonus))
                .OrderBy(group => group.Key.PartTypeId)
                .ThenBy(group => group.Key.MarkLevel)
                .ThenBy(group => group.Key.HasRaceBonus)
                .Select(group => new QuotedPart(group.Key.PartTypeId, group.Key.MarkLevel, group.Key.HasRaceBonus, group.LongCount(), false)),
        ];

        long crew = hull.Crew > 0m ? (long)decimal.Ceiling(hull.Crew) : 0;
        decimal moduleCredits = 0m;
        Dictionary<int, decimal> moduleResources = [];

        foreach (QuotedPart line in partLines)
        {
            if (line.IsHull || !facts.TryGetValue(line.PartTypeId, out PartRecipeFacts? moduleFacts))
            {
                continue;
            }

            foreach ((int resourceTypeId, decimal amount) in moduleFacts.ResourceCost)
            {
                decimal spent = amount * line.Quantity;

                if (spent <= 0m)
                {
                    continue;
                }

                if (kinds.CreditId == resourceTypeId)
                {
                    moduleCredits += spent;
                    continue;
                }

                if (kinds.PopulationId == resourceTypeId
                    || kinds.CurrencyIds.Contains(resourceTypeId)
                    || kinds.FuelIds.Contains(resourceTypeId))
                {
                    continue;
                }

                moduleResources[resourceTypeId] = moduleResources.GetValueOrDefault(resourceTypeId) + spent;
            }
        }

        Dictionary<int, long> materials = [];

        foreach ((int resourceTypeId, decimal spent) in moduleResources)
        {
            long taken = ShipyardAssembly.Share(spent, settings.MaterialShare);

            if (taken > 0)
            {
                materials[resourceTypeId] = taken;
            }
        }

        long assemblySeconds = hull.AssemblySeconds < 1 ? 0 : hull.AssemblySeconds;

        return new AssemblyQuote(
            design,
            hull,
            null,
            partLines,
            crew,
            ShipyardAssembly.Share(moduleCredits, settings.CreditShare),
            hull.Upkeep > 0m ? hull.Upkeep : 0m,
            assemblySeconds,
            materials);
    }

    /// <summary>
    /// Refuses a batch whose end does not fit the calendar, or whose paid amounts do not fit a number.
    /// </summary>
    private static void CheckBatch(DateTimeOffset now, long unitSeconds, long quantity, AssemblyQuote quote)
    {
        try
        {
            _ = now.AddSeconds(checked(unitSeconds * quantity));
            _ = checked(quote.Crew * quantity);
            _ = checked(quote.Credits * quantity);

            foreach (long amount in quote.Materials.Values)
            {
                _ = checked(amount * quantity);
            }

            foreach (QuotedPart line in quote.PartLines)
            {
                _ = checked(line.Quantity * quantity);
            }
        }
        catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new GameException(FleetErrorCodes.QuantityInvalid, "The batch is too large.");
        }
    }

    /// <summary>
    /// The quote of a whole batch. The time and the upkeep stay the amounts of one ship.
    /// The share of one ship is already rounded, and the batch multiplies that whole amount.
    /// </summary>
    private static AssemblyQuote ForBatch(AssemblyQuote quote, long quantity)
    {
        if (quantity == 1 || quote.RefusalCode is not null)
        {
            return quote;
        }

        try
        {
            return quote with
            {
                PartLines =
                [
                    .. quote.PartLines.Select(line => line with { Quantity = checked(line.Quantity * quantity) }),
                ],
                Crew = checked(quote.Crew * quantity),
                Credits = checked(quote.Credits * quantity),
                Materials = quote.Materials.ToDictionary(cost => cost.Key, cost => checked(cost.Value * quantity)),
            };
        }
        catch (OverflowException)
        {
            throw new GameException(FleetErrorCodes.QuantityInvalid, "The batch is too large.");
        }
    }

    private static void RefuseStructure(AssemblyQuote quote)
    {
        if (quote.RefusalCode is not null)
        {
            throw new GameException(quote.RefusalCode, "The project cannot be assembled.");
        }
    }

    private async Task RefuseHullAsync(string playerId, long planetId, AssemblyQuote quote, CancellationToken cancellationToken)
    {
        AvailabilityDto availability = await _planets.CheckRequirementsAsync(
            RequirementSubjects.Module,
            quote.Hull!.Id,
            1,
            planetId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        if (availability.IsAvailable)
        {
            return;
        }

        RequirementDto unmet = availability.UnmetRequirements[0];

        throw new GameException(
            unmet.ReasonCode,
            "The shipyard has not opened this hull.",
            new Dictionary<string, object?>
            {
                ["requirement_name_key"] = unmet.TargetNameKey,
                ["requirement_level"] = unmet.TargetLevel,
            });
    }

    private async Task RefuseStockAsync(
        string playerId,
        long planetId,
        AssemblyQuote quote,
        ResourceKinds kinds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ShipPartStack> stock = await _parts.ReadAsync(
            planetId,
            [.. quote.PartLines.Select(line => line.PartTypeId)],
            cancellationToken).ConfigureAwait(false);

        if (quote.PartLines.Any(line => Available(stock, line) < line.Quantity))
        {
            throw new GameException(FleetErrorCodes.PartMissing, "The body lacks the parts of the project.");
        }

        if (quote.BaseSeconds < 1)
        {
            throw new GameException(FleetErrorCodes.AssemblyTimeMissing, "The hull has no assembly time.");
        }

        string? reason = await StockReasonAsync(playerId, planetId, quote, kinds, cancellationToken).ConfigureAwait(false);

        if (reason is not null)
        {
            throw new GameException(reason, "The assembly cannot be paid.");
        }
    }

    private async Task<string?> StockReasonAsync(
        string playerId,
        long planetId,
        AssemblyQuote quote,
        ResourceKinds kinds,
        CancellationToken cancellationToken)
    {
        long inhabitants = 0;

        if (quote.Crew > 0)
        {
            IReadOnlyList<PopulationFacts> populations = await _economy.GetPopulationsAsync(playerId, cancellationToken).ConfigureAwait(false);
            inhabitants = populations.FirstOrDefault(entry => entry.PlanetId == planetId)?.Amount ?? 0;
        }

        IReadOnlyDictionary<int, long> planetBalance = quote.Materials.Count > 0
            ? await _resources.GetBalanceAsync(planetId, playerId, cancellationToken).ConfigureAwait(false)
            : new Dictionary<int, long>();
        IReadOnlyDictionary<int, long> currencyBalance = quote.Credits > 0
            ? await _economy.GetBalancesAsync(playerId, cancellationToken).ConfigureAwait(false)
            : new Dictionary<int, long>();

        return StockReason(quote, kinds, inhabitants, planetBalance, currencyBalance);
    }

    private static string? StockReason(
        AssemblyQuote quote,
        ResourceKinds kinds,
        long inhabitants,
        IReadOnlyDictionary<int, long> planetBalance,
        IReadOnlyDictionary<int, long> currencyBalance)
    {
        if (quote.Crew > 0 && kinds.PopulationId is null)
        {
            return FleetErrorCodes.CrewShort;
        }

        if (quote.Credits > 0 && kinds.CreditId is null)
        {
            return FleetErrorCodes.ResourcesShort;
        }

        if (quote.Crew > 0 && inhabitants < quote.Crew)
        {
            return FleetErrorCodes.CrewShort;
        }

        if (quote.Materials.Any(cost => planetBalance.GetValueOrDefault(cost.Key) < cost.Value))
        {
            return FleetErrorCodes.ResourcesShort;
        }

        if (quote.Credits > 0 && kinds.CreditId is int creditId && currencyBalance.GetValueOrDefault(creditId) < quote.Credits)
        {
            return FleetErrorCodes.ResourcesShort;
        }

        return null;
    }

    private async Task<Yard> YardAsync(
        string playerId,
        long planetId,
        long? planetBuildingId,
        bool required,
        CancellationToken cancellationToken)
    {
        int? shipyardTypeId = await _planets.FindBuildingTypeIdByClassAsync(BuildingClasses.Shipyard, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PlanetBuildingLevelById> yards = shipyardTypeId is int typeId
            ? await _planets.GetBuildingsByTypesAsync(planetId, [typeId], playerId, cancellationToken).ConfigureAwait(false)
            : [];
        PlanetBuildingLevelById? building = planetBuildingId is long asked
            ? yards.FirstOrDefault(entry => entry.PlanetBuildingId == asked)
            : yards.OrderBy(entry => entry.PlanetBuildingId).FirstOrDefault();

        if (planetBuildingId is not null && building is null)
        {
            throw new GameException(FleetErrorCodes.ShipyardMismatch, "The building is not the shipyard of this body.");
        }

        if (building is null)
        {
            if (required)
            {
                throw new GameException(FleetErrorCodes.ShipyardMissing, "The body has no shipyard.");
            }

            return new Yard(null, new Dictionary<string, decimal>());
        }

        if (building.Level < 1)
        {
            if (required)
            {
                throw new GameException(FleetErrorCodes.ShipyardMissing, "The body has no shipyard.");
            }

            return new Yard(null, new Dictionary<string, decimal>());
        }

        IReadOnlyDictionary<string, decimal> effects = await _planets
            .GetBuildingEffectsAsync(planetId, building.PlanetBuildingId, playerId, cancellationToken)
            .ConfigureAwait(false);

        return new Yard(building, effects);
    }

    private async Task<PlanetFacts> OwnedPlanetAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(FleetErrorCodes.PlanetUnknown, "The body does not exist.");

        if (!string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(FleetErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }

        return facts;
    }

    private async Task<List<ShipEntity>> ShipsOfAsync(string playerId, long? planetId, CancellationToken cancellationToken)
    {
        IQueryable<ShipEntity> ships = _dbContext.Ships.AsNoTracking()
            .Include(ship => ship.ModuleIds)
            .Where(ship => ship.PlayerId == playerId);

        if (planetId is long bodyId)
        {
            ships = ships.Where(ship => ship.PlanetId == bodyId);
        }

        return await ships
            .OrderBy(ship => ship.PlanetId)
            .ThenBy(ship => ship.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the fleet of each ship that is in one, in one query (task 00040).
    /// </summary>
    private async Task<Dictionary<long, long>> FleetOfShipsAsync(IReadOnlyCollection<ShipEntity> ships, CancellationToken cancellationToken)
    {
        if (ships.Count == 0)
        {
            return [];
        }

        List<long> shipIds = [.. ships.Select(ship => ship.Id)];

        return await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => shipIds.Contains(entry.ShipId))
            .ToDictionaryAsync(entry => entry.ShipId, entry => entry.FleetId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<long, string>> DesignNamesAsync(
        string playerId,
        IReadOnlyCollection<long> designIds,
        CancellationToken cancellationToken)
    {
        if (designIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => design.OwnerPlayerId == playerId && designIds.Contains(design.Id))
            .ToDictionaryAsync(design => design.Id, design => design.Name, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Dictionary<int, string>> PartNamesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<int, PartRecipeFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

        return facts.ToDictionary(entry => entry.Key, entry => entry.Value.NameKey);
    }

    private async Task<ResourceKinds> KindsAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        int? populationId = catalog.Resources.Where(resource => resource.PaidByPopulation).OrderBy(resource => resource.Id).Select(resource => (int?)resource.Id).FirstOrDefault();
        int? creditId = catalog.Resources
            .Where(resource => resource.IsCivilizationScope && resource.EarnsPopulationIncome && !resource.AllowsDebt)
            .OrderBy(resource => resource.Id)
            .Select(resource => (int?)resource.Id)
            .FirstOrDefault();
        HashSet<int> currencies = [.. catalog.Resources.Where(resource => resource.IsCivilizationScope).Select(resource => resource.Id)];
        int? fuelGroupId = catalog.Groups
            .Where(group => string.Equals(group.Code, FuelGroupCode, StringComparison.Ordinal))
            .Select(group => (int?)group.Id)
            .FirstOrDefault();
        HashSet<int> fuelIds = fuelGroupId is int groupId
            ? [.. catalog.Resources.Where(resource => resource.ResourceGroupId == groupId).Select(resource => resource.Id)]
            : [];
        Dictionary<int, string> names = catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.NameKey);

        return new ResourceKinds(populationId, creditId, currencies, fuelIds, names);
    }

    private static int Places(FleetSettings settings, IReadOnlyDictionary<string, decimal> effects)
    {
        int places = effects.TryGetValue(BuildingEffectTypes.ShipyardQueueSlots, out decimal effect)
            ? (int)decimal.Floor(effect)
            : settings.QueueBaseSlots;

        if (places < 1)
        {
            places = settings.QueueBaseSlots;
        }

        if (places > settings.QueueMaxSlots)
        {
            places = settings.QueueMaxSlots;
        }

        return places < 1 ? 1 : places;
    }

    /// <summary>
    /// How many full parts of the line the body has. The constructor does not choose the
    /// manufacturing bonus, so a line that does not ask for it counts the plain stack and the
    /// stack with the bonus of the race. A line that does ask for the bonus counts only that stack.
    /// </summary>
    private static long Available(IReadOnlyList<ShipPartStack> stock, QuotedPart line) =>
        stock
            .Where(stack => stack.PartTypeId == line.PartTypeId
                && stack.MarkLevel == line.MarkLevel
                && stack.ConditionPercent == PartConditions.Full
                && (!line.HasRaceBonus || stack.HasRaceBonus))
            .Sum(stack => stack.Quantity);

    /// <summary>
    /// The stacks an assembly spends. A line that names the bonus takes only that stack. A line
    /// that does not takes the plain stack first and the bonus stack when the plain one is short.
    /// The spent stack keeps its own bonus, so a cancellation returns it to the same row.
    /// </summary>
    private static List<TakenPart> PartsToTake(IReadOnlyList<ShipPartStack> stock, IReadOnlyList<QuotedPart> lines)
    {
        Dictionary<(int PartTypeId, int MarkLevel, bool HasRaceBonus), long> onHand = [];

        foreach (ShipPartStack stack in stock)
        {
            if (stack.ConditionPercent != PartConditions.Full || stack.Quantity <= 0)
            {
                continue;
            }

            (int PartTypeId, int MarkLevel, bool HasRaceBonus) stackKey = (stack.PartTypeId, stack.MarkLevel, stack.HasRaceBonus);
            onHand[stackKey] = onHand.GetValueOrDefault(stackKey) + stack.Quantity;
        }

        List<TakenPart> taken = [];

        foreach (QuotedPart line in lines)
        {
            long stillNeeded = line.Quantity;
            bool[] bonusOrder = line.HasRaceBonus ? [true] : [false, true];

            foreach (bool hasRaceBonus in bonusOrder)
            {
                (int PartTypeId, int MarkLevel, bool HasRaceBonus) stackKey = (line.PartTypeId, line.MarkLevel, hasRaceBonus);
                long have = onHand.GetValueOrDefault(stackKey);
                long take = Math.Min(have, stillNeeded);

                if (take <= 0)
                {
                    continue;
                }

                taken.Add(new TakenPart(line.PartTypeId, line.MarkLevel, hasRaceBonus, take, line.IsHull));
                onHand[stackKey] = have - take;
                stillNeeded -= take;
            }

            if (stillNeeded > 0)
            {
                throw new GameException(FleetErrorCodes.PartMissing, "The body lacks the parts of the project.");
            }
        }

        return taken;
    }

    /// <summary>
    /// The parts of one ship of a batch. Earlier ships of the same batch already took the plain
    /// stacks first, so this ship takes the next slice of the same rows.
    /// </summary>
    private static List<ShipModuleEntity> ModulesOf(ShipyardOrderEntity order, long batch)
    {
        List<ShipModuleEntity> modules = [];

        foreach (IGrouping<(int PartTypeId, int MarkLevel, bool IsHull), ShipyardOrderPartEntity> group in order.PartIds
            .GroupBy(part => (part.PartTypeId, part.MarkLevel, part.IsHull))
            .OrderBy(group => group.Key.IsHull ? 0 : 1)
            .ThenBy(group => group.Key.PartTypeId)
            .ThenBy(group => group.Key.MarkLevel))
        {
            long unit = group.Sum(part => part.Quantity) / batch;
            long skip = unit * order.ProducedQuantity;
            long need = unit;

            foreach (ShipyardOrderPartEntity row in group.OrderBy(part => part.HasRaceBonus).ThenBy(part => part.Id))
            {
                long afterSkip = Math.Max(0, row.Quantity - skip);
                skip = Math.Max(0, skip - row.Quantity);
                long take = Math.Min(afterSkip, need);

                if (take <= 0)
                {
                    continue;
                }

                modules.Add(new ShipModuleEntity
                {
                    PartTypeId = row.PartTypeId,
                    MarkLevel = row.MarkLevel,
                    HasRaceBonus = row.HasRaceBonus,
                    ConditionPercent = row.ConditionPercent,
                    ConditionCode = ShipModuleConditions.Intact,
                    Quantity = take,
                    IsHull = row.IsHull,
                });
                need -= take;
            }
        }

        return modules;
    }

    /// <summary>
    /// The parts of the ships a cancelled batch has not finished. The ships already finished
    /// took the plain stacks first.
    /// </summary>
    private static List<ShipPartStack> UnusedParts(ShipyardOrderEntity order, long batch)
    {
        List<ShipPartStack> stacks = [];

        foreach (IGrouping<(int PartTypeId, int MarkLevel, bool IsHull), ShipyardOrderPartEntity> group in order.PartIds
            .GroupBy(part => (part.PartTypeId, part.MarkLevel, part.IsHull)))
        {
            long total = group.Sum(part => part.Quantity);
            long used = total - Unused(total, batch, order.ProducedQuantity);

            foreach (ShipyardOrderPartEntity row in group.OrderBy(part => part.HasRaceBonus).ThenBy(part => part.Id))
            {
                long consumed = Math.Min(row.Quantity, used);
                used -= consumed;
                long back = row.Quantity - consumed;

                if (back > 0)
                {
                    stacks.Add(new ShipPartStack(row.PartTypeId, row.MarkLevel, row.HasRaceBonus, row.ConditionPercent, back));
                }
            }
        }

        return stacks;
    }

    /// <summary>The part of a paid amount that belongs to the ships a batch has not finished.</summary>
    private static long Unused(long amount, long quantity, long produced) =>
        quantity <= 0 ? 0 : amount / quantity * (quantity - produced);

    /// <summary>The wait of the ships still left, when the number fits.</summary>
    private static long WaitingSeconds(long unitSeconds, long remaining)
    {
        if (remaining < 1)
        {
            return unitSeconds;
        }

        try
        {
            return checked(unitSeconds * remaining);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private static List<ShipyardOrderCostEntity> CostsOf(AssemblyQuote quote, ResourceKinds kinds)
    {
        List<ShipyardOrderCostEntity> costs = [];

        if (quote.Crew > 0 && kinds.PopulationId is int populationId)
        {
            costs.Add(new ShipyardOrderCostEntity { ResourceTypeId = populationId, Amount = quote.Crew });
        }

        if (quote.Credits > 0 && kinds.CreditId is int creditId)
        {
            costs.Add(new ShipyardOrderCostEntity { ResourceTypeId = creditId, Amount = quote.Credits });
        }

        costs.AddRange(quote.Materials
            .OrderBy(cost => cost.Key)
            .Select(cost => new ShipyardOrderCostEntity { ResourceTypeId = cost.Key, Amount = cost.Value }));

        return costs;
    }

    private static List<ShipyardCostDto> CostDtos(AssemblyQuote quote, AssemblyBoard board)
    {
        ResourceKinds kinds = board.Kinds;
        List<ShipyardCostDto> costs = [];

        if (quote.Crew > 0 && kinds.PopulationId is int populationId)
        {
            costs.Add(new ShipyardCostDto(
                populationId,
                quote.Crew,
                kinds.NameKeys.GetValueOrDefault(populationId),
                board.Inhabitants));
        }

        if (quote.Credits > 0 && kinds.CreditId is int creditId)
        {
            costs.Add(new ShipyardCostDto(
                creditId,
                quote.Credits,
                kinds.NameKeys.GetValueOrDefault(creditId),
                board.CurrencyBalance.GetValueOrDefault(creditId)));
        }

        costs.AddRange(quote.Materials
            .OrderBy(cost => cost.Key)
            .Select(cost => new ShipyardCostDto(
                cost.Key,
                cost.Value,
                kinds.NameKeys.GetValueOrDefault(cost.Key),
                board.PlanetBalance.GetValueOrDefault(cost.Key))));

        return costs;
    }

    private static void Renumber(List<ShipyardOrderEntity> queue)
    {
        for (int index = 0; index < queue.Count; index++)
        {
            queue[index].Position = index + 1;
        }
    }

    private async Task<AssemblyBoard> LoadBoardAsync(
        string playerId,
        long planetId,
        IReadOnlyList<ShipDesignEntity> designs,
        CancellationToken cancellationToken)
    {
        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, PartRecipeFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);
        ResourceKinds kinds = await KindsAsync(cancellationToken).ConfigureAwait(false);
        FleetSettings settings = FleetSettings.From(_config);
        long[] designIds = [.. designs.Select(design => design.Id)];
        List<ShipDesignSlotEntity> slots = designIds.Length == 0
            ? []
            : await _dbContext.ShipDesignSlots.AsNoTracking()
                .Where(slot => designIds.Contains(slot.DesignId))
                .OrderBy(slot => slot.SlotIndex)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        Dictionary<long, List<ShipDesignSlotEntity>> slotsByDesign = slots
            .GroupBy(slot => slot.DesignId)
            .ToDictionary(group => group.Key, group => group.ToList());
        Dictionary<long, AssemblyQuote> quotes = designs.ToDictionary(
            design => design.Id,
            design => QuoteOf(design, slotsByDesign.GetValueOrDefault(design.Id) ?? [], catalog, facts, kinds, settings));
        int[] partTypeIds = [.. quotes.Values.SelectMany(quote => quote.PartLines).Select(line => line.PartTypeId).Distinct()];
        IReadOnlyList<ShipPartStack> stock = partTypeIds.Length == 0
            ? []
            : await _parts.ReadAsync(planetId, partTypeIds, cancellationToken).ConfigureAwait(false);
        List<ShipEntity> ships = await ShipsOfAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
        long inhabitants = 0;

        if (quotes.Values.Any(quote => quote.Crew > 0))
        {
            IReadOnlyList<PopulationFacts> populations = await _economy.GetPopulationsAsync(playerId, cancellationToken).ConfigureAwait(false);
            inhabitants = populations.FirstOrDefault(entry => entry.PlanetId == planetId)?.Amount ?? 0;
        }

        IReadOnlyDictionary<int, long> planetBalance = quotes.Values.Any(quote => quote.Materials.Count > 0)
            ? await _resources.GetBalanceAsync(planetId, playerId, cancellationToken).ConfigureAwait(false)
            : new Dictionary<int, long>();
        IReadOnlyDictionary<int, long> currencyBalance = quotes.Values.Any(quote => quote.Credits > 0)
            ? await _economy.GetBalancesAsync(playerId, cancellationToken).ConfigureAwait(false)
            : new Dictionary<int, long>();

        return new AssemblyBoard(quotes, facts, kinds, stock, ships, inhabitants, planetBalance, currencyBalance, catalog);
    }

    private async Task<Dictionary<int, AvailabilityDto>> OpenedHullsAsync(
        string playerId,
        long planetId,
        IEnumerable<AssemblyQuote> quotes,
        CancellationToken cancellationToken)
    {
        Dictionary<int, AvailabilityDto> opened = [];

        foreach (int hullId in quotes.Where(quote => quote.RefusalCode is null && quote.Hull is not null).Select(quote => quote.Hull!.Id).Distinct())
        {
            opened[hullId] = await _planets.CheckRequirementsAsync(
                RequirementSubjects.Module,
                hullId,
                1,
                planetId,
                playerId,
                cancellationToken).ConfigureAwait(false);
        }

        return opened;
    }

    private sealed record Yard(PlanetBuildingLevelById? Building, IReadOnlyDictionary<string, decimal> Effects);

    private sealed record AssemblyBoard(
        Dictionary<long, AssemblyQuote> Quotes,
        IReadOnlyDictionary<int, PartRecipeFacts> Facts,
        ResourceKinds Kinds,
        IReadOnlyList<ShipPartStack> Stock,
        List<ShipEntity> Ships,
        long Inhabitants,
        IReadOnlyDictionary<int, long> PlanetBalance,
        IReadOnlyDictionary<int, long> CurrencyBalance,
        ShipCatalogDto Catalog);

    private sealed record ResourceKinds(int? PopulationId, int? CreditId, HashSet<int> CurrencyIds, HashSet<int> FuelIds, Dictionary<int, string> NameKeys);

    private sealed record QuotedPart(int PartTypeId, int MarkLevel, bool HasRaceBonus, long Quantity, bool IsHull);

    /// <summary>A stack the assembly spends, with the bonus of the row it came from.</summary>
    private sealed record TakenPart(int PartTypeId, int MarkLevel, bool HasRaceBonus, long Quantity, bool IsHull);

    private sealed record AssemblyQuote(
        ShipDesignEntity? Design,
        ShipHullDto? Hull,
        string? RefusalCode,
        IReadOnlyList<QuotedPart> PartLines,
        long Crew,
        long Credits,
        decimal UpkeepPerHour,
        long BaseSeconds,
        Dictionary<int, long> Materials)
    {
        public static AssemblyQuote Refused(long designId, string code) =>
            new(new ShipDesignEntity { Id = designId }, null, code, [], 0, 0, 0m, 1, []);

        public static AssemblyQuote Refused(ShipDesignEntity design, string code) =>
            new(design, null, code, [], 0, 0, 0m, 1, []);

        public static AssemblyQuote Refused(ShipDesignEntity design, ShipHullDto hull, string code) =>
            new(design, hull, code, [], 0, 0, 0m, 1, []);
    }

    /// <summary>A paid resource, whether it sits on an order or on a ship.</summary>
    private readonly record struct CostRow(int ResourceTypeId, long Amount);
}

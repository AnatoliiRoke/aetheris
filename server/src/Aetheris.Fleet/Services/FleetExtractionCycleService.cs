using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The cycle of extraction of a fleet (task 00050).
/// </summary>
/// <remarks>
/// <para>
/// A fleet with a plan under way that has an order of extraction works every quarter of an hour:
/// the event of the cycle comes, the fleet takes what it can from the place of the order, and the
/// yield goes to the holds of its ships. The event of the next cycle is planned right away.
/// </para>
/// <para>
/// The hold of a ship is the room of its hull and of its container modules. The yield goes to the
/// ships in the order of their numbers, and what does not fit into the hold of a ship goes to the
/// next one. What fits into none of them stays in the place: it is the empty rock of the cycle.
/// </para>
/// </remarks>
public sealed class FleetExtractionCycleService
{
    /// <summary>
    /// The event type of the cycle of extraction.
    /// </summary>
    public const string CycleEventType = "fleet.extraction_cycle";

    /// <summary>
    /// The length of a cycle of extraction, in seconds of game time (a quarter of an hour).
    /// </summary>
    public const int CycleSeconds = 900;

    /// <summary>
    /// The group of the crystals (task 00050): the crystal extractor takes only these.
    /// </summary>
    public const string CrystalGroupCode = "resource_group_4";

    /// <summary>
    /// The code of the resource of the helium (task 00050).
    /// </summary>
    public const string HeliumResourceCode = "resource_13";

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly FleetExtractionService _extraction;
    private readonly IProductionModule _production;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetExtractionCycleService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="extraction">The rules of the extraction.</param>
    /// <param name="production">The catalog of the modules.</param>
    /// <param name="world">The world module.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="clock">The game clock.</param>
    public FleetExtractionCycleService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        FleetExtractionService extraction,
        IProductionModule production,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _extraction = extraction;
        _production = production;
        _world = world;
        _scheduler = scheduler;
        _clock = clock;
    }

    /// <summary>
    /// The body of the event of a cycle (task 00050).
    /// </summary>
    /// <param name="FleetId">The fleet that works.</param>
    /// <param name="OrderId">The order of extraction.</param>
    public sealed record CyclePayload(long FleetId, long OrderId);

    /// <summary>
    /// Plans the first cycle of the extraction of a fleet (task 00050).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="orderId">The order of extraction.</param>
    /// <param name="sectorId">The sector of the place of the order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event is planned.</returns>
    public async Task ScheduleAsync(long fleetId, long orderId, int sectorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(_scheduler);

        DateTimeOffset runAt = _clock.GameNow + TimeSpan.FromSeconds(CycleSeconds);
        string dedupe = CycleEventType + ":" + fleetId.ToString(CultureInfo.InvariantCulture) + ":" + orderId.ToString(CultureInfo.InvariantCulture);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                sectorId,
                CycleEventType,
                runAt,
                JsonSerializer.Serialize(new CyclePayload(fleetId, orderId)),
                dedupe),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Does one cycle of the extraction of a fleet and plans the next one (task 00050).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="orderId">The order of extraction.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the cycle is done.</returns>
    public async Task CycleAsync(long fleetId, long orderId, CancellationToken cancellationToken = default)
    {
        int sectorId = await _unitOfWork.RunAsync(
            async () =>
            {
                FlightPlanOrderEntity? order = await _dbContext.FlightPlanOrders
                    .FirstOrDefaultAsync(entry => entry.Id == orderId, cancellationToken).ConfigureAwait(false);

                if (order is null)
                {
                    return -1;
                }

                // The plan of the order must be under way: a plan that was stopped, deleted or
                // finished changes nothing.
                bool underWay = await _dbContext.FlightPlans
                    .AnyAsync(plan => plan.Id == order.FlightPlanId && plan.StateCode == FlightPlanStates.Active, cancellationToken)
                    .ConfigureAwait(false);

                if (!underWay)
                {
                    return -1;
                }

                IReadOnlyList<FleetExtractionService.ExtractionModules> modules =
                    await _extraction.ModulesAsync(fleetId, cancellationToken).ConfigureAwait(false);

                if (modules.Count == 0)
                {
                    return -1;
                }

                // The extraction lasts as long as the player asked for: the moment it began is kept
                // in the parameters of the order, and the order stops when the time is up.
                DateTimeOffset now = _clock.GameNow;
                DateTimeOffset? startedAt = ExtractionOrderParameters.ReadStartedAt(order.Parameters);

                if (startedAt is null)
                {
                    order.Parameters = ExtractionOrderParameters.WriteStartedAt(order.Parameters, now);
                    startedAt = now;
                }
                else if (order.DurationSeconds > 0 && now - startedAt.Value >= TimeSpan.FromSeconds(order.DurationSeconds))
                {
                    return -1;
                }

                await TakeAsync(fleetId, modules, order, cancellationToken).ConfigureAwait(false);

                return order.TargetSectorId;
            },
            cancellationToken).ConfigureAwait(false);

        if (sectorId < 0)
        {
            return;
        }

        await ScheduleAsync(fleetId, orderId, sectorId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes what the modules of the fleet take from the place of the order and puts it into the
    /// holds of the ships (task 00050).
    /// </summary>
    private async Task TakeAsync(
        long fleetId,
        IReadOnlyList<FleetExtractionService.ExtractionModules> modules,
        FlightPlanOrderEntity order,
        CancellationToken cancellationToken)
    {
        PlaceContentDto? place = await _world.GetPlaceContentAsync(order.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (place is null)
        {
            return;
        }

        IReadOnlyList<ShipHold> holds = await HoldsAsync(fleetId, cancellationToken).ConfigureAwait(false);

        if (holds.Count == 0)
        {
            return;
        }

        long free = holds.Sum(hold => hold.Free);

        if (free <= 0)
        {
            return;
        }

        List<ResourceAmount> yields = [];

        if (place.Object.AsteroidFieldId is { } fieldId)
        {
            long mass = FleetExtractionService.CycleMass(modules, FleetExtractionService.ExtractionSource.Field);
            ExtractionResult result = await _world.ExtractFromFieldAsync(fieldId, mass, free, cancellationToken).ConfigureAwait(false);
            yields.AddRange(result.YieldIds);

            // The crystal extractor takes only the crystals of the field, and they share its yield
            // evenly.
            IReadOnlyList<int> crystals = await CrystalsAsync(fieldId, place.Compositions, cancellationToken).ConfigureAwait(false);
            yields.AddRange(FleetExtractionService.CrystalCycle(modules, crystals));
        }
        else if (place.Object.NebulaId is { } nebulaId)
        {
            long mass = FleetExtractionService.CycleMass(modules, FleetExtractionService.ExtractionSource.Nebula);
            ExtractionResult result = await _world.ExtractFromNebulaAsync(nebulaId, mass, free, cancellationToken).ConfigureAwait(false);
            yields.AddRange(result.YieldIds);
        }
        else if (place.Object.ObjectClass == WorldObjectClasses.GasGiant)
        {
            int helium = await HeliumResourceIdAsync(cancellationToken).ConfigureAwait(false);
            ResourceAmount? amount = FleetExtractionService.HeliumCycle(modules, helium);

            if (amount is not null && amount.Amount > 0)
            {
                yields.Add(amount);
            }
        }
        else
        {
            return;
        }

        if (yields.Count == 0)
        {
            return;
        }

        await StoreAsync(fleetId, holds, yields, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The holds of the ships of the fleet with the room that is left in them (task 00050).
    /// </summary>
    private async Task<IReadOnlyList<ShipHold>> HoldsAsync(long fleetId, CancellationToken cancellationToken)
    {
        List<FleetShipEntity> members = await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .OrderBy(entry => entry.Position)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (members.Count == 0)
        {
            return [];
        }

        long[] shipIds = [.. members.Select(entry => entry.ShipId)];
        List<ShipEntity> ships = await _dbContext.Ships.AsNoTracking()
            .Where(ship => shipIds.Contains(ship.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ShipModuleEntity> modules = await _dbContext.ShipModules.AsNoTracking()
            .Where(module => shipIds.Contains(module.ShipId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<long, long> carried = await _dbContext.ShipCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId))
            .GroupBy(cargo => cargo.ShipId)
            .Select(group => new { ShipId = group.Key, Amount = group.Sum(cargo => cargo.Amount) })
            .ToDictionaryAsync(row => row.ShipId, row => row.Amount, cancellationToken).ConfigureAwait(false);

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ShipHullDto> hulls = catalog.Hulls.ToDictionary(hull => hull.Id);
        Dictionary<int, ShipModuleDto> byType = catalog.Modules.ToDictionary(module => module.Id);

        long[] designIds = [.. ships.Select(ship => ship.ShipDesignId).Distinct()];
        Dictionary<long, int> hullOfDesign = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => designIds.Contains(design.Id))
            .Select(design => new { design.Id, design.HullPartTypeId })
            .ToDictionaryAsync(row => row.Id, row => row.HullPartTypeId, cancellationToken).ConfigureAwait(false);

        List<ShipHold> holds = [];

        foreach (ShipEntity ship in ships.OrderBy(ship => Array.IndexOf(shipIds, ship.Id)))
        {
            int hullTypeId = hullOfDesign.GetValueOrDefault(ship.ShipDesignId);
            decimal capacity = hulls.TryGetValue(hullTypeId, out ShipHullDto? hull) ? hull.HoldCapacity : 0m;

            foreach (ShipModuleEntity module in modules.Where(entry => entry.ShipId == ship.Id))
            {
                if (byType.TryGetValue(module.PartTypeId, out ShipModuleDto? known))
                {
                    capacity += known.CargoCapacity;
                }
            }

            holds.Add(new ShipHold(ship.Id, (long)Math.Floor(capacity), carried.GetValueOrDefault(ship.Id)));
        }

        return holds;
    }

    /// <summary>
    /// Puts the yield of the cycle into the holds of the ships of the fleet (task 00050): the ships
    /// take the yield in the order of their numbers, and what does not fit into one goes to the next.
    /// </summary>
    private async Task StoreAsync(long fleetId, IReadOnlyList<ShipHold> holds, IReadOnlyList<ResourceAmount> yields, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.GameNow;

        foreach (ResourceAmount yield in yields)
        {
            long left = yield.Amount;

            foreach (ShipHold hold in holds)
            {
                if (left <= 0)
                {
                    break;
                }

                long taken = hold.Take(left);

                if (taken <= 0)
                {
                    continue;
                }

                await AddAsync(hold.ShipId, yield.ResourceId, taken, now, cancellationToken).ConfigureAwait(false);
                left -= taken;
            }

            // What fits into none of the holds stays in the place: it is the empty rock of the
            // cycle, and the place loses the mass of it all.
        }

        await RecountAsync(fleetId, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the cargo of the fleet again from the holds of its ships (task 00050): the fleet
    /// carries what its ships carry.
    /// </summary>
    private async Task RecountAsync(long fleetId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return;
        }

        Dictionary<int, long> carried = await _dbContext.ShipCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId))
            .GroupBy(cargo => cargo.ResourceId)
            .Select(group => new { ResourceId = group.Key, Amount = group.Sum(cargo => cargo.Amount) })
            .ToDictionaryAsync(row => row.ResourceId, row => row.Amount, cancellationToken).ConfigureAwait(false);

        List<FleetCargoEntity> rows = await _dbContext.FleetCargos
            .Where(cargo => cargo.FleetId == fleetId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (FleetCargoEntity row in rows)
        {
            row.Amount = carried.GetValueOrDefault(row.ResourceId);
            row.UpdatedAt = now;
        }

        foreach ((int resourceId, long amount) in carried)
        {
            if (rows.Any(row => row.ResourceId == resourceId))
            {
                continue;
            }

            _dbContext.FleetCargos.Add(new FleetCargoEntity
            {
                FleetId = fleetId,
                ResourceId = resourceId,
                Amount = amount,
                UpdatedAt = now,
            });
        }
    }

    /// <summary>
    /// Adds an amount of a resource to the hold of a ship (task 00050).
    /// </summary>
    private async Task AddAsync(long shipId, int resourceId, long amount, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ShipCargoEntity? cargo = await _dbContext.ShipCargos
            .FirstOrDefaultAsync(entry => entry.ShipId == shipId && entry.ResourceId == resourceId, cancellationToken).ConfigureAwait(false);

        if (cargo is null)
        {
            cargo = new ShipCargoEntity { ShipId = shipId, ResourceId = resourceId, Amount = 0 };
            _dbContext.ShipCargos.Add(cargo);
        }

        cargo.Amount += amount;
        cargo.UpdatedAt = now;
    }

    /// <summary>
    /// The crystals of a field: the resources of the group of crystals among the shares of the field
    /// (task 00050).
    /// </summary>
    private async Task<IReadOnlyList<int>> CrystalsAsync(long fieldId, IReadOnlyList<CompositionDto> compositions, CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        HashSet<int> crystalIds = [.. catalog.Resources
            .Where(resource => catalog.Groups.FirstOrDefault(group => group.Id == resource.ResourceGroupId)?.Code == CrystalGroupCode)
            .Select(resource => resource.Id)];

        return
        [
            .. compositions
                .Where(share => share.AsteroidFieldId == fieldId && crystalIds.Contains(share.ResourceId))
                .Select(share => share.ResourceId)
                .Distinct()
                .OrderBy(id => id),
        ];
    }

    /// <summary>
    /// The identifier of the resource of the helium (task 00050).
    /// </summary>
    private async Task<int> HeliumResourceIdAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        return catalog.Resources.FirstOrDefault(resource => resource.Code == HeliumResourceCode)?.Id ?? 0;
    }
}

/// <summary>
/// The hold of a ship of a fleet (task 00050).
/// </summary>
/// <param name="ShipId">The ship.</param>
/// <param name="Capacity">The room of the hold, in tonnes.</param>
/// <param name="Carried">What the hold carries now, in tonnes.</param>
public sealed class ShipHold(long shipId, long capacity, long carried)
{
    /// <summary>
    /// Gets the ship the hold belongs to.
    /// </summary>
    public long ShipId { get; } = shipId;

    /// <summary>
    /// Gets the room of the hold, in tonnes.
    /// </summary>
    public long Capacity { get; } = capacity;

    /// <summary>
    /// Gets what the hold carries now, in tonnes.
    /// </summary>
    public long Carried { get; } = carried;

    /// <summary>
    /// Gets the room that is left in the hold, in tonnes.
    /// </summary>
    public long Free { get; private set; } = Math.Max(0, capacity - carried);

    /// <summary>
    /// Puts an amount of a resource into the hold and takes the room it took.
    /// </summary>
    /// <param name="amount">The amount, in tonnes.</param>
    /// <returns>How much of the amount fitted into the hold.</returns>
    public long Take(long amount)
    {
        long taken = Math.Min(Free, Math.Max(0, amount));
        Free -= taken;

        return taken;
    }
}

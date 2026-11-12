using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The cycle of extraction of a fleet (task 00050, task 00053).
/// </summary>
/// <remarks>
/// <para>
/// A fleet with a plan under way whose current order is an order of extraction works in cycles: the
/// event of the cycle comes, the fleet takes what it can from the place of the order, and the yield
/// goes to the holds of its ships. The length of a cycle is a parameter of the configuration (a quarter
/// of an hour at the start); the last cycle of an order with a time is shortened to what is left of it
/// and takes the matching part of the yield.
/// </para>
/// <para>
/// The hold of a ship is the room of its hull and of its container modules. The yield goes to the
/// ships in the order of their numbers, and what does not fit into the hold of a ship goes to the
/// next one. What fits into none of them stays in the place: it is the empty rock of the cycle.
/// </para>
/// <para>
/// The order ends when its time is up, when the holds are full or when the place has nothing left; the
/// plan then goes on to its next order. The service changes the data only, it does not open a
/// transaction: the caller does, together with the plan.
/// </para>
/// </remarks>
public sealed class FleetExtractionCycleService
{
    /// <summary>
    /// The event type of the cycle of extraction.
    /// </summary>
    public const string CycleEventType = "fleet.extraction_cycle";

    private readonly FleetDbContext _dbContext;
    private readonly FleetExtractionService _extraction;
    private readonly FleetHoldService _holds;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetExtractionCycleService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="extraction">The rules of the extraction.</param>
    /// <param name="holds">The holds of the ships of the fleets.</param>
    /// <param name="world">The world module.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="config">The game configuration.</param>
    public FleetExtractionCycleService(
        FleetDbContext dbContext,
        FleetExtractionService extraction,
        FleetHoldService holds,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _extraction = extraction;
        _holds = holds;
        _world = world;
        _scheduler = scheduler;
        _config = config;
    }

    /// <summary>
    /// The body of the event of a cycle (task 00050, task 00053).
    /// </summary>
    /// <param name="FleetId">The fleet that works.</param>
    /// <param name="OrderId">The order of extraction.</param>
    /// <param name="PlanVersion">The version of the plan when the cycle was planned: an event of a plan that was stopped and started again is not the event of the new run.</param>
    /// <param name="PeriodSeconds">How long the cycle lasts, in game seconds.</param>
    public sealed record CyclePayload(long FleetId, long OrderId, int PlanVersion, int PeriodSeconds);

    /// <summary>
    /// What a cycle ended with.
    /// </summary>
    public enum CycleResult
    {
        /// <summary>The event does not belong to the plan that is under way: nothing was done.</summary>
        Ignored,

        /// <summary>The cycle was done and the order goes on.</summary>
        Continue,

        /// <summary>The cycle was done and the order is over: the plan goes to its next order.</summary>
        Finished,
    }

    /// <summary>
    /// The outcome of a cycle.
    /// </summary>
    /// <param name="Result">What the cycle ended with.</param>
    /// <param name="NextPeriodSeconds">The length of the next cycle, when the order goes on.</param>
    /// <param name="Yields">The resources the fleet took in the cycle, by their type, for the report of
    /// the fleet (task 00060).</param>
    public sealed record CycleOutcome(
        FleetExtractionCycleService.CycleResult Result,
        int NextPeriodSeconds,
        IReadOnlyList<FleetYieldDto> Yields)
    {
        /// <summary>
        /// The outcome of a cycle that took nothing.
        /// </summary>
        /// <param name="result">What the cycle ended with.</param>
        /// <param name="nextPeriodSeconds">The length of the next cycle, when the order goes on.</param>
        /// <returns>An outcome without a yield.</returns>
        public static CycleOutcome Empty(CycleResult result, int nextPeriodSeconds = 0) =>
            new(result, nextPeriodSeconds, NoYields);

        /// <summary>
        /// An empty list of the resources the fleet took.
        /// </summary>
        public static readonly IReadOnlyList<FleetYieldDto> NoYields = [];
    }

    /// <summary>
    /// Gets the length of a cycle of extraction, in game seconds.
    /// </summary>
    /// <returns>The length of a cycle.</returns>
    public int CycleSeconds() => FleetSettings.From(_config).ExtractionCycleSeconds;

    /// <summary>
    /// Counts the length of the next cycle of an order: a full cycle, or what is left of the time of
    /// the order when that is shorter (task 00053).
    /// </summary>
    /// <param name="order">The order of extraction.</param>
    /// <param name="startedAt">The moment the order began.</param>
    /// <param name="now">The moment the previous cycle ended.</param>
    /// <returns>The length of the next cycle, in game seconds.</returns>
    public int NextPeriodSeconds(FlightPlanOrderEntity order, DateTimeOffset startedAt, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(order);

        int cycle = CycleSeconds();

        if (order.DurationSeconds <= 0)
        {
            return cycle;
        }

        long left = order.DurationSeconds - (long)(now - startedAt).TotalSeconds;

        return (int)Math.Clamp(left, 1L, cycle);
    }

    /// <summary>
    /// Plans a cycle of the extraction of a fleet (task 00050, task 00053).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="orderId">The order of extraction.</param>
    /// <param name="planVersion">The version of the plan.</param>
    /// <param name="sectorId">The sector the fleet works in.</param>
    /// <param name="now">The moment the cycle begins.</param>
    /// <param name="periodSeconds">How long the cycle lasts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event is planned.</returns>
    public async Task ScheduleAsync(
        long fleetId,
        long orderId,
        int planVersion,
        int sectorId,
        DateTimeOffset now,
        int periodSeconds,
        CancellationToken cancellationToken = default)
    {
        // The cycle that is planned from inside the handler of the previous cycle is planned while that
        // one is still pending, so the key names the moment of the cycle too: one key per cycle.
        DateTimeOffset runAt = now + TimeSpan.FromSeconds(periodSeconds);
        string dedupe = CycleEventType
            + ":" + fleetId.ToString(CultureInfo.InvariantCulture)
            + ":" + orderId.ToString(CultureInfo.InvariantCulture)
            + ":" + planVersion.ToString(CultureInfo.InvariantCulture)
            + ":" + runAt.UtcTicks.ToString(CultureInfo.InvariantCulture);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                sectorId,
                CycleEventType,
                runAt,
                JsonSerializer.Serialize(new CyclePayload(fleetId, orderId, planVersion, periodSeconds)),
                dedupe),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Does one cycle of the extraction of a fleet (task 00050, task 00053).
    /// </summary>
    /// <param name="payload">The body of the event.</param>
    /// <param name="now">The moment the cycle ends, in game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What the cycle ended with.</returns>
    public async Task<CycleOutcome> CycleAsync(CyclePayload payload, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        FlightPlanOrderEntity? order = await _dbContext.FlightPlanOrders.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == payload.OrderId, cancellationToken).ConfigureAwait(false);

        if (order is null)
        {
            return CycleOutcome.Empty(CycleResult.Ignored);
        }

        // The plan of the order must be under way and be the run this cycle was planned for: a plan
        // that was stopped, deleted, finished or started anew changes nothing.
        bool underWay = await _dbContext.FlightPlans.AsNoTracking()
            .AnyAsync(
                plan => plan.Id == order.FlightPlanId
                    && plan.FleetId == payload.FleetId
                    && plan.StateCode == FlightPlanStates.Active
                    && plan.Version == payload.PlanVersion
                    && plan.CurrentPosition == order.Position,
                cancellationToken)
            .ConfigureAwait(false);

        if (!underWay)
        {
            return CycleOutcome.Empty(CycleResult.Ignored);
        }

        IReadOnlyList<FleetExtractionService.ExtractionModules> modules =
            await _extraction.ModulesAsync(payload.FleetId, cancellationToken).ConfigureAwait(false);

        if (modules.Count == 0)
        {
            return CycleOutcome.Empty(CycleResult.Finished);
        }

        DateTimeOffset startedAt = ExtractionOrderParameters.ReadStartedAt(order.Parameters) ?? now - TimeSpan.FromSeconds(payload.PeriodSeconds);
        TakeResult taken = await TakeAsync(payload.FleetId, modules, order, payload.PeriodSeconds, now, cancellationToken).ConfigureAwait(false);

        // The order ends when its time is up, when the holds are full or when the place has nothing
        // more to give. A cycle that took nothing means the fleet cannot take anything here.
        bool timeUp = order.DurationSeconds > 0 && now - startedAt >= TimeSpan.FromSeconds(order.DurationSeconds);

        if (timeUp || taken.HoldsFull || taken.PlaceGone || !taken.Taken)
        {
            return new CycleOutcome(CycleResult.Finished, 0, taken.Yields);
        }

        return new CycleOutcome(CycleResult.Continue, NextPeriodSeconds(order, startedAt, now), taken.Yields);
    }

    /// <summary>
    /// Tells whether the modules of a fleet take anything from a place (task 00054, task 00055): by the rows
    /// of the modules, a place that holds none of the resources they take gives nothing, so the order is
    /// skipped at its start and no cycle is planned.
    /// </summary>
    /// <param name="objectId">The object of the place.</param>
    /// <param name="modules">The modules of the fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one module of the fleet takes a resource of the place.</returns>
    public async Task<bool> YieldsAsync(
        long objectId,
        IReadOnlyList<FleetExtractionService.ExtractionModules> modules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modules);

        PlaceContentDto? place = await _world.GetPlaceContentAsync(objectId, cancellationToken).ConfigureAwait(false);

        if (place is null)
        {
            return false;
        }

        string placeKind = place.Object.ObjectClass;
        IReadOnlyList<CompositionDto>? shares = SharesOf(place);
        IReadOnlyDictionary<int, IReadOnlyList<int>> groups = await GroupResourceIdsAsync(modules, placeKind, cancellationToken).ConfigureAwait(false);

        // The drilling of a field takes by the shares of the field, so it yields where the field has any
        // share. An hour is long enough for any yield above zero to show: the rounding of a short period
        // does not hide a resource the place has.
        bool drills = FleetExtractionService.CycleMass(modules, placeKind, FleetExtractionService.SecondsPerHour) > 0
            && shares is not null
            && shares.Any(share => share.Coefficient > 0m);

        return drills
            || FleetExtractionService.RuleYields(modules, placeKind, shares, groups, FleetExtractionService.SecondsPerHour).Count > 0;
    }

    /// <summary>
    /// Takes what the modules of the fleet take from the place of the order in a period and puts it
    /// into the holds of the ships (task 00050, task 00053).
    /// </summary>
    private async Task<TakeResult> TakeAsync(
        long fleetId,
        IReadOnlyList<FleetExtractionService.ExtractionModules> modules,
        FlightPlanOrderEntity order,
        int periodSeconds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PlaceContentDto? place = await _world.GetPlaceContentAsync(order.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (place is null)
        {
            return TakeResult.Nothing(holdsFull: false, placeGone: true);
        }

        IReadOnlyList<ShipHold> holds = await _holds.ReadAsync(fleetId, cancellationToken).ConfigureAwait(false);
        long free = holds.Sum(hold => hold.Free);

        if (free <= 0)
        {
            return TakeResult.Nothing(holdsFull: true, placeGone: false);
        }

        string placeKind = place.Object.ObjectClass;
        List<ResourceAmount> yields = [];
        bool placeGone = false;

        // The modules that take the mass of the place (the drilling of a field) are served by the world,
        // which splits the mass by the shares of the place, loses it and says whether the place is gone.
        long mass = FleetExtractionService.CycleMass(modules, placeKind, periodSeconds);

        if (mass > 0 && place.Object.AsteroidFieldId is { } fieldId)
        {
            try
            {
                ExtractionResult result = await _world.ExtractFromFieldAsync(fieldId, mass, free, cancellationToken).ConfigureAwait(false);
                yields.AddRange(result.YieldIds);
                placeGone = result.Depleted;
            }
            catch (GameException exception) when (exception.Code == GameErrorCodes.NotFound)
            {
                // The field was emptied by another fleet since the place was read.
                return TakeResult.Nothing(holdsFull: false, placeGone: true);
            }
        }

        // The other rows of the modules are the yield of the period turned into resources by their way: a
        // nebula never runs out, so what the modules take from it is only the yield times the share.
        IReadOnlyDictionary<int, IReadOnlyList<int>> groups = await GroupResourceIdsAsync(modules, placeKind, cancellationToken).ConfigureAwait(false);

        foreach (ResourceAmount amount in FleetExtractionService.RuleYields(modules, placeKind, SharesOf(place), groups, periodSeconds))
        {
            Merge(yields, amount);
        }

        if (yields.Count == 0)
        {
            return TakeResult.Nothing(holdsFull: false, placeGone);
        }

        await StoreAsync(fleetId, holds, yields, now, cancellationToken).ConfigureAwait(false);

        // The resources the fleet took are the report of the fleet (task 00060).
        List<FleetYieldDto> taken = [.. yields.Select(yield => new FleetYieldDto(yield.ResourceId, yield.Amount))];

        return new TakeResult(true, holds.Sum(hold => hold.Free) <= 0, placeGone, taken);
    }

    /// <summary>
    /// Puts the yield of the cycle into the holds of the ships of the fleet (task 00050): the ships
    /// take the yield in the order of their numbers, and what does not fit into one goes to the next.
    /// </summary>
    private async Task StoreAsync(
        long fleetId,
        IReadOnlyList<ShipHold> holds,
        IReadOnlyList<ResourceAmount> yields,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
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

        // The sum of the fleet is read from the rows of the ships, so the rows are written first.
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

        Dictionary<int, long> carried = await _dbContext.ShipCargos
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
    /// The shares of the composition of a place (task 00055): the shares of its field or of its nebula, or
    /// <see langword="null"/> when the place has no composition, as a gas giant.
    /// </summary>
    private static IReadOnlyList<CompositionDto>? SharesOf(PlaceContentDto place)
    {
        if (place.Object.AsteroidFieldId is { } fieldId)
        {
            return [.. place.Compositions.Where(share => share.AsteroidFieldId == fieldId)];
        }

        if (place.Object.NebulaId is { } nebulaId)
        {
            return [.. place.Compositions.Where(share => share.NebulaId == nebulaId)];
        }

        return null;
    }

    /// <summary>
    /// The resources of the groups of resources that the rows of the modules name for a kind of place
    /// (task 00055), from the catalog of the resources of the world, which the world keeps in memory.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> GroupResourceIdsAsync(
        IReadOnlyList<FleetExtractionService.ExtractionModules> modules,
        string placeKind,
        CancellationToken cancellationToken)
    {
        HashSet<int> groupIds =
        [
            .. modules
                .SelectMany(module => module.Rules)
                .Where(rule => rule.PlaceKind == placeKind && rule.ResourceGroupId is not null)
                .Select(rule => rule.ResourceGroupId!.Value),
        ];

        if (groupIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<int>>();
        }

        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        return groupIds.ToDictionary(
            groupId => groupId,
            groupId => (IReadOnlyList<int>)[.. catalog.Resources.Where(resource => resource.ResourceGroupId == groupId).Select(resource => resource.Id)]);
    }

    /// <summary>
    /// Adds an amount to the yield of the cycle, summing it with the amount of the same resource that is
    /// already there.
    /// </summary>
    private static void Merge(List<ResourceAmount> yields, ResourceAmount amount)
    {
        int index = yields.FindIndex(entry => entry.ResourceId == amount.ResourceId);

        if (index < 0)
        {
            yields.Add(amount);
        }
        else
        {
            yields[index] = new ResourceAmount(amount.ResourceId, yields[index].Amount + amount.Amount);
        }
    }

    /// <summary>
    /// What the fleet took in a cycle.
    /// </summary>
    /// <param name="Taken">Whether anything went to the holds.</param>
    /// <param name="HoldsFull">Whether the holds of the fleet are full now.</param>
    /// <param name="PlaceGone">Whether the place has nothing left or is gone.</param>
    /// <summary>
    /// What one cycle of extraction took from the place.
    /// </summary>
    /// <param name="Taken">Whether the fleet took anything.</param>
    /// <param name="HoldsFull">Whether the holds of the fleet are full.</param>
    /// <param name="PlaceGone">Whether the place is gone.</param>
    /// <param name="Yields">The resources the fleet took, for the report of the fleet (task 00060).</param>
    private sealed record TakeResult(
        bool Taken,
        bool HoldsFull,
        bool PlaceGone,
        IReadOnlyList<FleetYieldDto> Yields)
    {
        /// <summary>
        /// A result of a cycle that took nothing.
        /// </summary>
        /// <param name="holdsFull">Whether the holds of the fleet are full.</param>
        /// <param name="placeGone">Whether the place is gone.</param>
        /// <returns>A result without a yield.</returns>
        public static TakeResult Nothing(bool holdsFull, bool placeGone) =>
            new(false, holdsFull, placeGone, CycleOutcome.NoYields);
    }
}

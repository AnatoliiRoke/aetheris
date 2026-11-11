using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// Carries out the flight plan of a fleet order after order (task 00053).
/// </summary>
/// <remarks>
/// <para>
/// The orders of a plan are carried out one after another: the next begins when the previous is over.
/// A flight is over when the fleet arrives, an extraction when its time is up or the holds are full
/// or the place has nothing left, a wait when its time has passed. Nothing polls: every step is an
/// event of the scheduler (the arrival, the cycle of the extraction, the end of the wait), and the
/// event of a plan that was stopped or started anew is told apart by the version of the plan and
/// changes nothing.
/// </para>
/// <para>
/// After the last order the plan is deleted (decision of task 00050: a plan is kept until it is carried
/// out and is deleted after). An order that cannot begin at its turn, because its target is gone or
/// the fleet is not where the order needs it, stops the plan with the reason, and the fleet waits for
/// the player where it is.
/// </para>
/// <para>
/// The time inside the runner is the logical time of the step: the moment an event was planned for,
/// so a step that the scheduler runs late does not shift the steps after it.
/// </para>
/// </remarks>
public sealed class FlightPlanRunner
{
    /// <summary>
    /// The event type of the arrival of a fleet.
    /// </summary>
    public const string ArrivalEventType = "fleet.flight_arrival";

    /// <summary>
    /// The event type of the end of an order of waiting.
    /// </summary>
    public const string WaitEventType = "fleet.order_wait";

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly FleetFlightService _flights;
    private readonly FleetSpeedService _speeds;
    private readonly FleetExtractionService _extraction;
    private readonly FleetExtractionCycleService _cycles;
    private readonly FleetHoldService _holds;
    private readonly FleetMinePlaceService _minePlaces;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlanRunner"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="flights">The places and the times of the flights.</param>
    /// <param name="speeds">The speeds of the fleets.</param>
    /// <param name="extraction">The rules of the extraction.</param>
    /// <param name="cycles">The cycles of the extraction.</param>
    /// <param name="holds">The holds of the ships.</param>
    /// <param name="minePlaces">Finds the place an extraction takes from.</param>
    /// <param name="world">The world module.</param>
    /// <param name="scheduler">The event scheduler.</param>
    public FlightPlanRunner(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        FleetFlightService flights,
        FleetSpeedService speeds,
        FleetExtractionService extraction,
        FleetExtractionCycleService cycles,
        FleetHoldService holds,
        FleetMinePlaceService minePlaces,
        IWorldModule world,
        IEventScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(cycles);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(minePlaces);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _flights = flights;
        _speeds = speeds;
        _extraction = extraction;
        _cycles = cycles;
        _holds = holds;
        _minePlaces = minePlaces;
        _world = world;
        _scheduler = scheduler;
    }

    /// <summary>
    /// The body of the event of the arrival of a fleet.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="FlightId">The flight that ends.</param>
    public sealed record ArrivalPayload(long FleetId, long FlightId);

    /// <summary>
    /// The body of the event of the end of an order of waiting.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="OrderId">The order of waiting.</param>
    /// <param name="PlanVersion">The version of the plan when the wait began.</param>
    public sealed record WaitPayload(long FleetId, long OrderId, int PlanVersion);

    /// <summary>
    /// Checks that the fleet can carry out every order of the plan, one after another, before the
    /// plan begins (task 00053): the types are known, the targets exist and the fleet has a drive.
    /// </summary>
    /// <remarks>
    /// An order of extraction is not checked here (task 00054): it names no target and takes from the
    /// place where the fleet stands when the order begins, so the place and the modules are looked at
    /// then, and an order with nothing to take is skipped, not refused.
    /// </remarks>
    /// <param name="fleet">The fleet.</param>
    /// <param name="orders">The orders of the plan.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the plan is checked.</returns>
    /// <exception cref="GameException">An order cannot be carried out.</exception>
    public async Task ValidateAsync(
        FleetEntity fleet,
        IReadOnlyCollection<FlightPlanOrderEntity> orders,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(orders);

        if (orders.Count == 0)
        {
            throw new GameException(FleetErrorCodes.FlightPlanEmpty, "The plan has no orders.");
        }

        FleetSpeeds? speeds = null;

        foreach (FlightPlanOrderEntity order in orders.OrderBy(entry => entry.Position))
        {
            switch (order.TypeCode)
            {
                case FlightOrderTypes.Fly:
                case FlightOrderTypes.Return:
                    speeds ??= await _speeds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

                    if (speeds.Empty <= 0m)
                    {
                        throw new GameException(FleetErrorCodes.FlightNoDrive, "A ship of the fleet has no drive.");
                    }

                    _ = await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false)
                        ?? throw new GameException(FleetErrorCodes.FlightTargetUnknown, "The target of the flight is not a place of the map.");

                    break;

                case FlightOrderTypes.Mine:
                case FlightOrderTypes.Wait:
                    break;

                default:
                    throw new GameException(FleetErrorCodes.FlightOrderNotSupported, "The fleet cannot carry out this order yet.");
            }
        }
    }

    /// <summary>
    /// Begins the next order of the plan, and the ones after it that need no time, until an order
    /// takes time, the plan is carried out or an order cannot begin (task 00053).
    /// </summary>
    /// <param name="plan">The plan with its orders; the entity is changed.</param>
    /// <param name="fleet">The fleet of the plan; the entity is changed.</param>
    /// <param name="now">The logical moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when an order is under way or the plan is over.</returns>
    public async Task AdvanceAsync(FlightPlanEntity plan, FleetEntity fleet, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fleet);

        while (true)
        {
            FlightPlanOrderEntity? next = plan.OrderIds
                .OrderBy(order => order.Position)
                .FirstOrDefault(order => order.Position > plan.CurrentPosition);

            if (next is null)
            {
                // The last order is carried out: the plan is deleted, and the fleet waits where it is.
                _dbContext.FlightPlans.Remove(plan);

                return;
            }

            plan.CurrentPosition = next.Position;
            (BeginState state, string? code) = await BeginAsync(plan, fleet, next, now, cancellationToken).ConfigureAwait(false);

            if (state == BeginState.Started)
            {
                plan.UpdatedAt = now;

                return;
            }

            if (state == BeginState.Blocked)
            {
                // The order cannot begin: the plan stops with the reason and the fleet waits. The same
                // order begins again when the player carries the plan out anew.
                plan.StateCode = FlightPlanStates.Paused;
                plan.PauseCode = code;
                plan.UpdatedAt = now;
                plan.Version++;

                return;
            }
        }
    }

    /// <summary>
    /// Puts a fleet that is under way at the point of its route that the time has reached and ends
    /// the flight, when the plan is stopped (task 00053).
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the fleet stands.</returns>
    public async Task SettleAsync(FleetEntity fleet, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        FleetFlightEntity? flight = await _dbContext.FleetFlights
            .SingleOrDefaultAsync(entry => entry.FleetId == fleet.Id, cancellationToken).ConfigureAwait(false);

        if (flight is null)
        {
            return;
        }

        FleetFlightService.Place(fleet, FleetFlightService.PlaceOnRoute(flight, now));
        _dbContext.FleetFlights.Remove(flight);
    }

    /// <summary>
    /// Handles the arrival of a fleet: the fleet stands at the place, and the plan goes on.
    /// </summary>
    /// <param name="payload">The body of the event.</param>
    /// <param name="now">The moment the fleet arrives, in game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the arrival is handled.</returns>
    public Task HandleArrivalAsync(ArrivalPayload payload, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return _unitOfWork.RunAsync(
            async () =>
            {
                if (!await LockFleetAsync(payload.FleetId, cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                // A plan that was stopped removed its flight, so an arrival without a flight changes nothing.
                FleetFlightEntity? flight = await _dbContext.FleetFlights
                    .SingleOrDefaultAsync(entry => entry.Id == payload.FlightId && entry.FleetId == payload.FleetId, cancellationToken)
                    .ConfigureAwait(false);

                if (flight is null)
                {
                    return 0;
                }

                FleetEntity? fleet = await _dbContext.Fleets
                    .SingleOrDefaultAsync(entry => entry.Id == payload.FleetId, cancellationToken).ConfigureAwait(false);

                if (fleet is null)
                {
                    return 0;
                }

                WorldPlaceDto place = await DestinationAsync(flight, cancellationToken).ConfigureAwait(false);
                await ArriveAsync(fleet, place, cancellationToken).ConfigureAwait(false);
                _dbContext.FleetFlights.Remove(flight);

                FlightPlanEntity? plan = await _dbContext.FlightPlans
                    .Include(entry => entry.OrderIds)
                    .SingleOrDefaultAsync(entry => entry.FleetId == payload.FleetId, cancellationToken).ConfigureAwait(false);

                if (plan is not null
                    && plan.StateCode == FlightPlanStates.Active
                    && plan.OrderIds.Any(order => order.Id == flight.FlightPlanOrderId && order.Position == plan.CurrentPosition))
                {
                    await AdvanceAsync(plan, fleet, now, cancellationToken).ConfigureAwait(false);
                }

                return 0;
            },
            cancellationToken);
    }

    /// <summary>
    /// Handles the end of an order of waiting: the plan goes on.
    /// </summary>
    /// <param name="payload">The body of the event.</param>
    /// <param name="now">The moment the wait ends, in game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the end of the wait is handled.</returns>
    public Task HandleWaitAsync(WaitPayload payload, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return _unitOfWork.RunAsync(
            async () =>
            {
                if (!await LockFleetAsync(payload.FleetId, cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                FlightPlanEntity? plan = await _dbContext.FlightPlans
                    .Include(entry => entry.OrderIds)
                    .SingleOrDefaultAsync(entry => entry.FleetId == payload.FleetId, cancellationToken).ConfigureAwait(false);

                // The wait belongs to the run of the plan it was planned in: a plan that was stopped,
                // changed or started anew has another version.
                if (plan is null
                    || plan.StateCode != FlightPlanStates.Active
                    || plan.Version != payload.PlanVersion
                    || !plan.OrderIds.Any(order => order.Id == payload.OrderId && order.Position == plan.CurrentPosition))
                {
                    return 0;
                }

                FleetEntity? fleet = await _dbContext.Fleets
                    .SingleOrDefaultAsync(entry => entry.Id == payload.FleetId, cancellationToken).ConfigureAwait(false);

                if (fleet is not null)
                {
                    await AdvanceAsync(plan, fleet, now, cancellationToken).ConfigureAwait(false);
                }

                return 0;
            },
            cancellationToken);
    }

    /// <summary>
    /// Handles a cycle of an extraction: the fleet takes its yield, and the order goes on or ends.
    /// </summary>
    /// <param name="payload">The body of the event.</param>
    /// <param name="now">The moment the cycle ends, in game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the cycle is handled.</returns>
    public Task HandleCycleAsync(FleetExtractionCycleService.CyclePayload payload, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return _unitOfWork.RunAsync(
            async () =>
            {
                if (!await LockFleetAsync(payload.FleetId, cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                FleetExtractionCycleService.CycleOutcome outcome =
                    await _cycles.CycleAsync(payload, now, cancellationToken).ConfigureAwait(false);

                if (outcome.Result == FleetExtractionCycleService.CycleResult.Ignored)
                {
                    return 0;
                }

                FleetEntity? fleet = await _dbContext.Fleets
                    .SingleOrDefaultAsync(entry => entry.Id == payload.FleetId, cancellationToken).ConfigureAwait(false);

                if (fleet is null)
                {
                    return 0;
                }

                if (outcome.Result == FleetExtractionCycleService.CycleResult.Continue)
                {
                    await _cycles
                        .ScheduleAsync(payload.FleetId, payload.OrderId, payload.PlanVersion, fleet.SectorId, now, outcome.NextPeriodSeconds, cancellationToken)
                        .ConfigureAwait(false);

                    return 0;
                }

                FlightPlanEntity plan = await _dbContext.FlightPlans
                    .Include(entry => entry.OrderIds)
                    .SingleAsync(entry => entry.FleetId == payload.FleetId, cancellationToken).ConfigureAwait(false);

                await AdvanceAsync(plan, fleet, now, cancellationToken).ConfigureAwait(false);

                return 0;
            },
            cancellationToken);
    }

    /// <summary>
    /// Locks the fleets of the owner of a fleet before the event changes anything, as the commands do.
    /// </summary>
    /// <returns><see langword="false"/> when the fleet is gone.</returns>
    private async Task<bool> LockFleetAsync(long fleetId, CancellationToken cancellationToken)
    {
        string? playerId = await _dbContext.Fleets.AsNoTracking()
            .Where(fleet => fleet.Id == fleetId)
            .Select(fleet => fleet.PlayerId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (playerId is null)
        {
            return false;
        }

        await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Begins one order. A flight or a wait that takes time is under way when the call returns; an order
    /// that takes none is done; an order that cannot begin is blocked with the reason.
    /// </summary>
    private async Task<(BeginState State, string? Code)> BeginAsync(
        FlightPlanEntity plan,
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (order.TypeCode)
        {
            case FlightOrderTypes.Fly:
            case FlightOrderTypes.Return:
                return await BeginFlightAsync(fleet, order, now, cancellationToken).ConfigureAwait(false);

            case FlightOrderTypes.Wait:
                if (order.DurationSeconds <= 0)
                {
                    return (BeginState.Done, null);
                }

                order.Parameters = ExtractionOrderParameters.WriteStartedAt(order.Parameters, now);

                await _scheduler.ScheduleAsync(
                    new ScheduledEventRequest(
                        fleet.SectorId,
                        WaitEventType,
                        now + TimeSpan.FromSeconds(order.DurationSeconds),
                        JsonSerializer.Serialize(new WaitPayload(fleet.Id, order.Id, plan.Version)),
                        WaitEventType + ":" + fleet.Id.ToString(CultureInfo.InvariantCulture) + ":" + order.Id.ToString(CultureInfo.InvariantCulture) + ":" + plan.Version.ToString(CultureInfo.InvariantCulture)),
                    cancellationToken).ConfigureAwait(false);

                return (BeginState.Started, null);

            case FlightOrderTypes.Mine:
                return await BeginMineAsync(plan, fleet, order, now, cancellationToken).ConfigureAwait(false);

            default:
                return (BeginState.Blocked, FleetErrorCodes.FlightOrderNotSupported);
        }
    }

    /// <summary>
    /// Begins an order of flight: the fleet leaves for the target at the speed of its slowest ship.
    /// </summary>
    private async Task<(BeginState State, string? Code)> BeginFlightAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        WorldPlaceDto? destination = await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false);

        if (destination is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightTargetUnknown);
        }

        WorldPlaceDto? origin = await _flights.CurrentPlaceAsync(fleet, null, now, cancellationToken).ConfigureAwait(false);

        if (origin is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightTargetUnknown);
        }

        double distance = _world.GetTravelDistance(origin, destination);

        // The fleet is at the place already: it arrives at once and the plan goes on.
        if (distance <= 0d)
        {
            await ArriveAsync(fleet, destination, cancellationToken).ConfigureAwait(false);

            return (BeginState.Done, null);
        }

        FleetSpeeds speeds = await _speeds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

        if (speeds.Current <= 0m)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightNoDrive);
        }

        // The mass of the fleet is the mass of its ships and what their holds carry now.
        long seconds = FleetSpeedService.FlightSeconds(distance, speeds.Current);
        DateTimeOffset arrivesAt = now + TimeSpan.FromSeconds(seconds);

        FleetFlightEntity flight = new()
        {
            FleetId = fleet.Id,
            FlightPlanOrderId = order.Id,
            StartedAt = now,
            ArrivesAt = arrivesAt,
            Distance = distance,
            Speed = speeds.Current,
            FromSectorId = origin.SectorId,
            FromSystemId = origin.SystemId,
            FromOrbit = origin.Orbit,
            FromX = origin.X,
            FromY = origin.Y,
            FromZ = origin.Z,
            ToSectorId = destination.SectorId,
            ToSystemId = destination.SystemId,
            ToObjectId = destination.ObjectId,
            ToPlanetId = destination.PlanetId,
            ToOrbit = destination.Orbit,
            ToX = destination.X,
            ToY = destination.Y,
            ToZ = destination.Z,
        };

        // The fleet leaves its place: it is on no orbit and at no object until it arrives.
        fleet.StateCode = FleetStates.Flying;
        fleet.PlanetId = 0;
        fleet.ObjectId = null;
        fleet.SystemId = null;
        fleet.Orbit = null;
        fleet.PositionX = origin.X;
        fleet.PositionY = origin.Y;
        fleet.PositionZ = origin.Z;

        _dbContext.FleetFlights.Add(flight);

        // The event of the arrival names the flight, so the flight gets its identifier first.
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                destination.SectorId,
                ArrivalEventType,
                arrivesAt,
                JsonSerializer.Serialize(new ArrivalPayload(fleet.Id, flight.Id)),
                ArrivalEventType + ":" + flight.Id.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);

        return (BeginState.Started, null);
    }

    /// <summary>
    /// Begins an order of extraction (task 00054): the place is the one where the fleet stands, it is
    /// written into the order, and the first cycle is planned. An order with no place or no suitable
    /// module is skipped, so the plan goes on to its next order and does not stop.
    /// </summary>
    private async Task<(BeginState State, string? Code)> BeginMineAsync(
        FlightPlanEntity plan,
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The order names no target: a target a saved plan may still carry is dropped, and the place
        // that is found below is written in its stead for the cycles to read.
        ClearTarget(order);

        IReadOnlyList<FleetExtractionService.ExtractionModules> modules =
            await _extraction.ModulesAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

        // A fleet without a module of extraction takes nothing anywhere, so the world is not asked.
        if (modules.Count == 0)
        {
            return (BeginState.Done, null);
        }

        WorldPlaceDto? position = await _flights.CurrentPlaceAsync(fleet, null, now, cancellationToken).ConfigureAwait(false);
        WorldPlaceDto? place = position is null
            ? null
            : await _minePlaces.FindAsync(position, FleetExtractionService.PlaceKinds(modules), cancellationToken).ConfigureAwait(false);

        // Nothing to take where the fleet stands, or no module of the fleet that takes anything from this
        // place (by the rows of the modules, task 00055): the order is skipped and the plan goes on with the
        // next one (task 00054).
        if (place?.ObjectId is not { } objectId
            || !FleetExtractionService.CanExtract(modules, place.ObjectClass)
            || !await _cycles.YieldsAsync(objectId, modules, cancellationToken).ConfigureAwait(false))
        {
            return (BeginState.Done, null);
        }

        order.TargetKind = FlightTargetKinds.Object;
        order.TargetObjectId = objectId;
        order.TargetSectorId = place.SectorId;
        order.TargetSystemId = place.SystemId ?? 0;

        // Holds that are full already end the order at once: there is nothing to take them into.
        long free = (await _holds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false)).Sum(hold => hold.Free);

        if (free <= 0)
        {
            return (BeginState.Done, null);
        }

        order.Parameters = ExtractionOrderParameters.WriteStartedAt(order.Parameters, now);
        int period = _cycles.NextPeriodSeconds(order, now, now);
        await _cycles.ScheduleAsync(fleet.Id, order.Id, plan.Version, place.SectorId, now, period, cancellationToken).ConfigureAwait(false);

        return (BeginState.Started, null);
    }

    /// <summary>
    /// Takes the target out of an order of extraction: the order has the time only.
    /// </summary>
    private static void ClearTarget(FlightPlanOrderEntity order)
    {
        order.TargetKind = FlightTargetKinds.None;
        order.TargetSectorId = 0;
        order.TargetSystemId = 0;
        order.TargetObjectId = 0;
        order.TargetX = 0m;
        order.TargetY = 0m;
        order.TargetZ = 0m;
    }

    /// <summary>
    /// Puts the fleet at the place it arrived at; the ships live on the body of the player when the
    /// place is such a body.
    /// </summary>
    private async Task ArriveAsync(FleetEntity fleet, WorldPlaceDto place, CancellationToken cancellationToken)
    {
        FleetFlightService.Place(fleet, place);

        // A fleet that comes home brings its ships to the body: the ships stand where the fleet is.
        // A body of another player or a place that is no body does not take the ships.
        if (place.PlanetId is { } planetId && string.Equals(place.OwnerPlayerId, fleet.PlayerId, StringComparison.Ordinal))
        {
            long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
                .Where(entry => entry.FleetId == fleet.Id)
                .Select(entry => entry.ShipId)
                .ToListAsync(cancellationToken).ConfigureAwait(false)];

            await _dbContext.Ships
                .Where(ship => shipIds.Contains(ship.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(ship => ship.PlanetId, planetId), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The place a flight ends at: the object of the map as it is now, so a body that changed its
    /// owner on the way is told apart, or the point that was stored when the object is gone.
    /// </summary>
    private async Task<WorldPlaceDto> DestinationAsync(FleetFlightEntity flight, CancellationToken cancellationToken)
    {
        if (flight.ToObjectId is { } objectId)
        {
            WorldPlaceDto? place = await _world.GetObjectPlaceAsync(objectId, cancellationToken).ConfigureAwait(false);

            if (place is not null)
            {
                return place;
            }
        }

        return new WorldPlaceDto(flight.ToSectorId, flight.ToSystemId, null, null, flight.ToOrbit, string.Empty, null, flight.ToX, flight.ToY, flight.ToZ);
    }

    private enum BeginState
    {
        /// <summary>The order is under way and the plan waits for its event.</summary>
        Started,

        /// <summary>The order took no time and is done: the plan goes on.</summary>
        Done,

        /// <summary>The order cannot begin: the plan stops.</summary>
        Blocked,
    }
}

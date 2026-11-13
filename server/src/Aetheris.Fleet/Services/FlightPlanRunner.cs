using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Configuration;
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

    /// <summary>The event type of the end of a scan.</summary>
    public const string ScanEventType = "fleet.order_scan";

    /// <summary>
    /// The event type of the end of the deployment of a colony module (task 00073).
    /// </summary>
    public const string ColonizeEventType = "fleet.colonize_complete";

    /// <summary>The key of the details of a refusal that lists the ships it names (task 00072).</summary>
    public const string ShipIdsDetail = "ship_ids";

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly FleetFlightService _flights;
    private readonly FleetSpeedService _speeds;
    private readonly FleetExtractionService _extraction;
    private readonly FleetExtractionCycleService _cycles;
    private readonly FleetHoldService _holds;
    private readonly FleetMinePlaceService _minePlaces;
    private readonly FleetCargoService _cargo;
    private readonly FleetColonizeService _colonize;
    private readonly FleetReportService _reports;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly FleetScanService _scans;
    private readonly EngineKindsReader _engines;
    private readonly FlightLegPlanner _legs;
    private readonly FuelService _fuel;
    private readonly IGameConfig _config;

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
    /// <param name="cargo">The load and the unload of the holds (task 00057).</param>
    /// <param name="colonize">The colonization of a planet (task 00073).</param>
    /// <param name="reports">The reports of the fleets to the players (task 00060).</param>
    /// <param name="world">The world module.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="scans">The scanners of the fleet.</param>
    /// <param name="engines">The drives of the ships of the fleet (task 00072).</param>
    /// <param name="legs">Plans the legs of a command (task 00072).</param>
    /// <param name="propellant">The fuel of the fleet (task 00072).</param>
    /// <param name="config">The game configuration.</param>
    public FlightPlanRunner(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        FleetFlightService flights,
        FleetSpeedService speeds,
        FleetExtractionService extraction,
        FleetExtractionCycleService cycles,
        FleetHoldService holds,
        FleetMinePlaceService minePlaces,
        FleetCargoService cargo,
        FleetColonizeService colonize,
        FleetReportService reports,
        IWorldModule world,
        IEventScheduler scheduler,
        FleetScanService scans,
        EngineKindsReader engines,
        FlightLegPlanner legs,
        FuelService propellant,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(cycles);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(minePlaces);
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(colonize);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(scans);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(propellant);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _flights = flights;
        _speeds = speeds;
        _extraction = extraction;
        _cycles = cycles;
        _holds = holds;
        _minePlaces = minePlaces;
        _cargo = cargo;
        _colonize = colonize;
        _reports = reports;
        _world = world;
        _scheduler = scheduler;
        _scans = scans;
        _engines = engines;
        _legs = legs;
        _fuel = propellant;
        _config = config;
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

    /// <summary>The scheduled end of a scan.</summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="OrderId">The order.</param>
    /// <param name="PlanVersion">The plan version.</param>
    public sealed record ScanPayload(long FleetId, long OrderId, int PlanVersion);

    /// <summary>
    /// The scheduled end of the deployment of a colony module (task 00073).
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="OrderId">The order of the colonization.</param>
    /// <param name="PlanetId">The planet the fleet colonizes.</param>
    /// <param name="PlanVersion">The version of the plan when the deployment began.</param>
    public sealed record ColonizePayload(long FleetId, long OrderId, long PlanetId, int PlanVersion);

    /// <summary>The scan parameters fixed at the beginning of an order.</summary>
    /// <param name="StartedAt">The start time.</param>
    /// <param name="SectorId">The centre sector.</param>
    /// <param name="X">The absolute x coordinate.</param>
    /// <param name="Y">The absolute y coordinate.</param>
    /// <param name="Z">The absolute z coordinate.</param>
    /// <param name="Radius">The scan radius.</param>
    public sealed record ScanOrderState(DateTimeOffset StartedAt, int SectorId, double X, double Y, double Z, double Radius);

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

                    if (speeds.Normal.Empty <= 0m)
                    {
                        throw new GameException(FleetErrorCodes.FlightNoDrive, "A ship of the fleet has no drive.");
                    }

                    _ = await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false)
                        ?? throw new GameException(FleetErrorCodes.FlightTargetUnknown, "The target of the flight is not a place of the map.");

                    break;

                case FlightOrderTypes.Hyperfly:
                case FlightOrderTypes.Jump:
                    await ValidateFtlAsync(fleet, order, cancellationToken).ConfigureAwait(false);

                    break;

                case FlightOrderTypes.Mine:
                case FlightOrderTypes.Wait:
                    break;

                case FlightOrderTypes.Scan:
                    FleetScanModules available = await _scans.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
                    if (available.Systems is null)
                    {
                        throw new GameException(available.HasFleetScanner ? FleetErrorCodes.FlightOrderNotSupported : FleetErrorCodes.ScanNoModule, "The fleet has no working scanner of systems.");
                    }

                    break;

                case FlightOrderTypes.Load:
                case FlightOrderTypes.Unload:
                    // The list of what to carry is read when the plan is written, so a plan never holds an order the fleet cannot read (task 00057).
                    if (CargoOrderParameters.ReadItems(order.Parameters) is null)
                    {
                        throw new GameException(FleetErrorCodes.CargoItemsInvalid, "The order names nothing to carry, or an amount that is not above zero.");
                    }

                    break;

                case FlightOrderTypes.Colonize:
                    // The fleet must carry a colony module and the player must have room for one more
                    // planet (task 00073). The colonization names no target: the planet is the one the
                    // fleet stands on when the order begins, so it is checked then.
                    if (await _colonize.FindModuleAsync(fleet.Id, cancellationToken).ConfigureAwait(false) is null)
                    {
                        throw new GameException(FleetErrorCodes.ColonizeNoModule, "The fleet carries no colony module.");
                    }

                    await _colonize.CheckLimitAsync(fleet, cancellationToken).ConfigureAwait(false);

                    break;

                default:
                    throw new GameException(FleetErrorCodes.FlightOrderNotSupported, "The fleet cannot carry out this order yet.");
            }
        }
    }

    /// <summary>
    /// Checks that the fleet can carry out a hyperflight or a jump (task 00072).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every ship of the fleet needs a drive of the class the command uses: a ship without a
    /// hyperdrive grounds a hyperflight, and a ship without a jump drive grounds a jump. The ships
    /// that ground the fleet are named in the answer.
    /// </para>
    /// <para>
    /// The fleet also needs an ordinary drive, because the legs to the edge of the system and from
    /// the edge to the target are flown with the ordinary drives.
    /// </para>
    /// </remarks>
    /// <param name="fleet">The fleet.</param>
    /// <param name="order">The order of the command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the command is checked.</returns>
    /// <exception cref="GameException">The fleet cannot carry out the command.</exception>
    private async Task ValidateFtlAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(order);

        string kind = string.Equals(order.TypeCode, FlightOrderTypes.Hyperfly, StringComparison.Ordinal)
            ? FlightEngineKinds.Hyper
            : FlightEngineKinds.Jump;

        await CheckFtlDrivesAsync(fleet, kind, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks that every ship of a fleet can fly a hyperflight or a jump (task 00072): it has a
    /// drive of the class of the command and an ordinary drive for the legs inside the systems.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="kind">The class of the command, <see cref="FlightEngineKinds.Hyper"/> or <see cref="FlightEngineKinds.Jump"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the check has passed.</returns>
    /// <exception cref="GameException">A ship lacks a drive; the details list the ships.</exception>
    public async Task CheckFtlDrivesAsync(FleetEntity fleet, string kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        FleetEngines engines = await _engines.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

        // The fleet flies at the speed of its slowest ship, so every ship of it needs a drive of
        // the class of the command (task 00072, 2.2.5).
        IReadOnlyList<long> withoutClass = engines.ShipsWithout(kind);

        if (withoutClass.Count > 0)
        {
            throw new GameException(
                FleetErrorCodes.FtlShipMissing,
                "A ship of the fleet has no drive of the class the flight needs.",
                new Dictionary<string, object?> { [ShipIdsDetail] = withoutClass });
        }

        // Every ship needs an ordinary drive for the legs inside the systems (task 00072, 2.1.2).
        IReadOnlyList<long> withoutNormal = engines.ShipsWithout(FlightEngineKinds.Normal);

        if (withoutNormal.Count > 0)
        {
            throw new GameException(
                FleetErrorCodes.FtlNeedsNormalDrive,
                "A ship of the fleet has no ordinary drive, so it cannot fly inside a system.",
                new Dictionary<string, object?> { [ShipIdsDetail] = withoutNormal });
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
    /// Handles the end of the deployment of a colony module (task 00073): the planet belongs to the
    /// player, the colony is founded on it, the module is gone and the plan goes on.
    /// </summary>
    /// <remarks>
    /// The colonization is one transaction: the body is taken under the lock of its row, the
    /// colony is founded and the module is taken out of the hold of the fleet. A planet that was
    /// taken in the meantime stops the plan with the code of a taken planet, and the module stays
    /// in the hold of the fleet.
    /// </remarks>
    /// <param name="payload">The body of the event.</param>
    /// <param name="now">The moment the deployment ends, in game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the end of the deployment is handled.</returns>
    public Task HandleColonizeCompleteAsync(ColonizePayload payload, DateTimeOffset now, CancellationToken cancellationToken)
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

                // The deployment belongs to the run of the plan it was planned in: a plan that was
                // stopped, changed or started anew has another version.
                if (plan is null
                    || plan.StateCode != FlightPlanStates.Active
                    || plan.Version != payload.PlanVersion
                    || !plan.OrderIds.Any(order => order.Id == payload.OrderId && order.Position == plan.CurrentPosition))
                {
                    return 0;
                }

                FleetEntity? fleet = await _dbContext.Fleets
                    .SingleOrDefaultAsync(entry => entry.Id == payload.FleetId, cancellationToken).ConfigureAwait(false);

                if (fleet is null)
                {
                    return 0;
                }

                ColonyModule? module = await _colonize.FindModuleAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

                if (module is null)
                {
                    plan.StateCode = FlightPlanStates.Paused;
                    plan.PauseCode = FleetErrorCodes.ColonizeNoModule;
                    plan.UpdatedAt = now;
                    plan.Version++;

                    return 0;
                }

                try
                {
                    string planetName = await _colonize.ColonizeAsync(fleet, payload.PlanetId, module, now, cancellationToken).ConfigureAwait(false);

                    await _reports.WriteColonizationReportAsync(fleet.Id, planetName, module.Colonists, cancellationToken).ConfigureAwait(false);
                }
                catch (GameException exception)
                {
                    // The planet was taken, or the player has no room for one more body: the plan
                    // stops with the reason and the fleet waits. The module stays in the hold.
                    plan.StateCode = FlightPlanStates.Paused;
                    plan.PauseCode = exception.Code;
                    plan.UpdatedAt = now;
                    plan.Version++;

                    return 0;
                }

                await AdvanceAsync(plan, fleet, now, cancellationToken).ConfigureAwait(false);

                return 0;
            },
            cancellationToken);
    }

    /// <summary>Completes a scan if its plan has not been interrupted.</summary>
    /// <param name="payload">The scheduled scan.</param>
    /// <param name="now">The scheduled completion time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the work.</returns>
    public Task HandleScanAsync(ScanPayload payload, DateTimeOffset now, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(async () =>
        {
            if (!await LockFleetAsync(payload.FleetId, cancellationToken).ConfigureAwait(false)) return 0;
            FlightPlanEntity? plan = await _dbContext.FlightPlans.Include(entry => entry.OrderIds)
                .SingleOrDefaultAsync(entry => entry.FleetId == payload.FleetId, cancellationToken).ConfigureAwait(false);
            if (plan is null || plan.StateCode != FlightPlanStates.Active || plan.Version != payload.PlanVersion) return 0;
            FlightPlanOrderEntity? order = plan.OrderIds.SingleOrDefault(entry => entry.Id == payload.OrderId && entry.Position == plan.CurrentPosition && entry.TypeCode == FlightOrderTypes.Scan);
            if (order is null) return 0;
            FleetEntity? fleet = await _dbContext.Fleets.SingleOrDefaultAsync(entry => entry.Id == payload.FleetId, cancellationToken).ConfigureAwait(false);
            if (fleet is null) return 0;
            ScanOrderState state = JsonSerializer.Deserialize<ScanOrderState>(order.Parameters) ?? throw new InvalidOperationException("The scan has no parameters.");
            long lifetime = FleetSettings.From(_config).ScanSnapshotLifetimeSeconds;
            ScanSnapshotResult result = await _world.TakeScanSnapshotAsync(fleet.PlayerId, state.SectorId, state.X, state.Y, state.Z, state.Radius, now, now + TimeSpan.FromSeconds(lifetime), cancellationToken).ConfigureAwait(false);
            await _reports.WriteScanReportAsync(fleet.PlayerId, result, cancellationToken).ConfigureAwait(false);
            await AdvanceAsync(plan, fleet, now, cancellationToken).ConfigureAwait(false);
            return 0;
        }, cancellationToken);

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

                // The report of the fleet is written to the player: what the fleet took from the place
                // (task 00060).
                await _reports
                    .WriteExtractionReportAsync(payload.FleetId, outcome.Yields, cancellationToken)
                    .ConfigureAwait(false);

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
            case FlightOrderTypes.Scan:
                FleetScanModules available = await _scans.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
                ScanCapabilities? scan = available.Systems;
                if (scan is null) return (BeginState.Blocked, available.HasFleetScanner ? FleetErrorCodes.FlightOrderNotSupported : FleetErrorCodes.ScanNoModule);
                WorldPlaceDto? place = await _flights.CurrentPlaceAsync(fleet, null, now, cancellationToken).ConfigureAwait(false);
                if (place is null) return (BeginState.Blocked, FleetErrorCodes.FlightTargetUnknown);
                order.DurationSeconds = (long)decimal.Ceiling(scan.DurationSeconds);
                order.Parameters = JsonSerializer.Serialize(new ScanOrderState(now, place.SectorId, place.X, place.Y, place.Z, (double)scan.Radius));
                await _scheduler.ScheduleAsync(new ScheduledEventRequest(place.SectorId, ScanEventType, now + TimeSpan.FromSeconds(order.DurationSeconds),
                    JsonSerializer.Serialize(new ScanPayload(fleet.Id, order.Id, plan.Version)),
                    ScanEventType + ":" + fleet.Id.ToString(CultureInfo.InvariantCulture) + ":" + order.Id.ToString(CultureInfo.InvariantCulture) + ":" + plan.Version.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false);
                return (BeginState.Started, null);

            case FlightOrderTypes.Fly:
            case FlightOrderTypes.Return:
                return await BeginFlightAsync(fleet, order, now, cancellationToken).ConfigureAwait(false);

            case FlightOrderTypes.Hyperfly:
            case FlightOrderTypes.Jump:
                return await BeginFtlAsync(fleet, order, now, cancellationToken).ConfigureAwait(false);

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

            case FlightOrderTypes.Load:
            case FlightOrderTypes.Unload:
                return await BeginCargoAsync(fleet, order, now, cancellationToken).ConfigureAwait(false);

            case FlightOrderTypes.Colonize:
                return await BeginColonizeAsync(plan, fleet, order, now, cancellationToken).ConfigureAwait(false);

            default:
                return (BeginState.Blocked, FleetErrorCodes.FlightOrderNotSupported);
        }
    }

    /// <summary>
    /// Begins an order of a load or an unload (task 00057). The order takes no time: it is done when the call
    /// returns, or it is blocked with the reason, and the same order begins again when the player carries
    /// the plan out anew.
    /// </summary>
    private async Task<(BeginState State, string? Code)> BeginCargoAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CargoItem>? items = CargoOrderParameters.ReadItems(order.Parameters);

        if (items is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.CargoItemsInvalid);
        }

        CargoOutcome outcome = order.TypeCode == FlightOrderTypes.Load
            ? await _cargo.LoadAsync(fleet, items, now, cancellationToken).ConfigureAwait(false)
            : await _cargo.UnloadAsync(fleet, items, now, cancellationToken).ConfigureAwait(false);

        return outcome.Done ? (BeginState.Done, null) : (BeginState.Blocked, outcome.Code);
    }

    /// <summary>
    /// Begins an order of a colonization (task 00073): the fleet stands at the planet it is to
    /// colonize, and the deployment of the colony module begins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order takes no fuel and burns none: only the flight to the planet costs fuel. The
    /// deployment lasts as long as the module says, and the planet belongs to the player when the
    /// deployment is over.
    /// </para>
    /// <para>
    /// The checks are made again here, because the world may have changed while the fleet flew to
    /// the planet: a planet that was taken in the meantime stops the plan with the code of a taken
    /// planet, and the module stays in the hold of the fleet.
    /// </para>
    /// </remarks>
    /// <param name="plan">The plan of the fleet.</param>
    /// <param name="fleet">The fleet.</param>
    /// <param name="order">The order of the colonization.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The state of the order and the reason it is blocked.</returns>
    private async Task<(BeginState State, string? Code)> BeginColonizeAsync(
        FlightPlanEntity plan,
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(order);

        WorldPlaceDto? place = await _flights.CurrentPlaceAsync(fleet, null, now, cancellationToken).ConfigureAwait(false);

        // The colonization names no target (task 00073): the planet is the body the fleet stands on.
        if (place?.PlanetId is not { } planetId)
        {
            return (BeginState.Blocked, FleetErrorCodes.ColonizeNotOnPlanet);
        }

        ColonyModule? module;

        try
        {
            module = await _colonize.FindModuleAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

            if (module is null)
            {
                return (BeginState.Blocked, FleetErrorCodes.ColonizeNoModule);
            }

            await _colonize.CheckLimitAsync(fleet, cancellationToken).ConfigureAwait(false);
            await _colonize.CheckPlanetAsync(fleet, planetId, cancellationToken).ConfigureAwait(false);
        }
        catch (GameException exception)
        {
            return (BeginState.Blocked, exception.Code);
        }

        // The deployment takes the time of the module (char_deploy_time, data). A module without a
        // time is deployed at once: the colonization happens now, in the step of the plan.
        long seconds = Math.Max(0, module.DeploySeconds);
        order.DurationSeconds = seconds;

        if (seconds <= 0)
        {
            try
            {
                string planetName = await _colonize.ColonizeAsync(fleet, planetId, module, now, cancellationToken).ConfigureAwait(false);

                await _reports.WriteColonizationReportAsync(fleet.Id, planetName, module.Colonists, cancellationToken).ConfigureAwait(false);
            }
            catch (GameException exception)
            {
                return (BeginState.Blocked, exception.Code);
            }

            return (BeginState.Done, null);
        }

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                place.SectorId,
                ColonizeEventType,
                now + TimeSpan.FromSeconds(seconds),
                JsonSerializer.Serialize(new ColonizePayload(fleet.Id, order.Id, planetId, plan.Version)),
                ColonizeEventType + ":" + fleet.Id.ToString(CultureInfo.InvariantCulture) + ":" + order.Id.ToString(CultureInfo.InvariantCulture) + ":" + plan.Version.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);

        return (BeginState.Started, null);
    }

    /// <summary>
    /// Begins an order of a flight: the fleet leaves for the target at the speed of its slowest ship.
    /// </summary>
    /// <remarks>
    /// The flight burns the fuel of its class (task 00072): the fuel is taken from the hold of the
    /// fleet at the start of the flight, so a flight never begins without the fuel for it.
    /// </remarks>
    private async Task<(BeginState State, string? Code)> BeginFlightAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        WorldPlaceDto? origin = await _flights.CurrentPlaceAsync(fleet, null, now, cancellationToken).ConfigureAwait(false);

        if (origin is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightTargetUnknown);
        }

        // The flight to the edge of a system names the system (task 00072): its target resolves to
        // the last orbit of that system, as the data of the world give it.
        WorldPlaceDto? destination = await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false);

        if (destination is null)
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

        // The gravitational drives work only inside a star system (task 00072, 2.4.7).
        bool inSystem = FuelPlanner.IsInSystem(origin, destination);
        decimal speed = inSystem ? speeds.Normal.Current : speeds.NormalOpenSpace.Current;

        if (speed <= 0m)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightNoDrive);
        }

        // The mass of the fleet is the mass of its ships and what their holds carry now.
        long seconds = FleetSpeedService.FlightSeconds(distance, speed);
        DateTimeOffset arrivesAt = now + TimeSpan.FromSeconds(seconds);

        // The fuel of the leg is burned at the start of the flight: a flight that begins without
        // the fuel for it is not begun at all.
        IReadOnlyDictionary<int, decimal>? propellant = await BurnFuelAsync(
            fleet,
            FlightEngineKinds.Normal,
            seconds,
            inSystem,
            now,
            cancellationToken).ConfigureAwait(false);

        if (propellant is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FuelShort);
        }

        return await StartFlightAsync(fleet, order, origin, destination, distance, speed, seconds, arrivesAt, FlightEngineKinds.Normal, null, propellant, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Begins a leg of a hyperflight or of a jump (task 00072).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hyperflight or a jump is spread over three legs of a plan, and every leg begins like an
    /// ordinary flight: the fleet leaves for the place of the leg at the speed of the drives of
    /// the class of the leg.
    /// </para>
    /// <para>
    /// A jump is prepared before it happens: the fleet stands on the edge of its system while the
    /// drives charge, and the jump itself is instant. The moment the preparation is over is
    /// written into the flight, and the fleet is at the place of the target from that moment on.
    /// </para>
    /// </remarks>
    private async Task<(BeginState State, string? Code)> BeginFtlAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(order);

        string kind = string.Equals(order.TypeCode, FlightOrderTypes.Hyperfly, StringComparison.Ordinal)
            ? FlightEngineKinds.Hyper
            : FlightEngineKinds.Jump;

        // The place the leg starts at is the place the fleet stands at, and the place it ends at
        // is the place of the leg: the edge of the system or the target.
        WorldPlaceDto? origin = await _flights.CurrentPlaceAsync(fleet, null, now, cancellationToken).ConfigureAwait(false);

        if (origin is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightTargetUnknown);
        }

        WorldPlaceDto? destination = await ResolveLegAsync(fleet, order, origin, kind, cancellationToken).ConfigureAwait(false);

        if (destination is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FlightTargetUnknown);
        }

        double distance = _world.GetTravelDistance(origin, destination);

        if (distance <= 0d)
        {
            await ArriveAsync(fleet, destination, cancellationToken).ConfigureAwait(false);

            return (BeginState.Done, null);
        }

        FleetSpeeds speeds = await _speeds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
        FleetSettings settings = FleetSettings.From(_config);

        long seconds;
        decimal speed;
        DateTimeOffset arrivesAt;

        if (string.Equals(kind, FlightEngineKinds.Jump, StringComparison.Ordinal))
        {
            // A jump takes the time of its preparation: the drives charge and the jump itself is
            // instant. The fleet stands at the place of the leg until the preparation is over.
            seconds = (long)Math.Ceiling(speeds.JumpPrepSeconds * JumpFactorOf(distance, settings));
            speed = 0m;
            arrivesAt = now + TimeSpan.FromSeconds(seconds);
        }
        else
        {
            speed = speeds.Hyper.Current * settings.HyperSpeedMultiplier;

            if (speed <= 0m)
            {
                return (BeginState.Blocked, FleetErrorCodes.FlightNoDrive);
            }

            seconds = FleetSpeedService.FlightSeconds(distance, speed);
            arrivesAt = now + TimeSpan.FromSeconds(seconds);
        }

        IReadOnlyDictionary<int, decimal>? propellant = await BurnFuelAsync(fleet, kind, seconds, inSystem: false, now, cancellationToken).ConfigureAwait(false);

        if (propellant is null)
        {
            return (BeginState.Blocked, FleetErrorCodes.FuelShort);
        }

        return await StartFlightAsync(
            fleet,
            order,
            origin,
            destination,
            distance,
            speed,
            seconds,
            arrivesAt,
            kind,
            string.Equals(kind, FlightEngineKinds.Jump, StringComparison.Ordinal) ? arrivesAt : null,
            propellant,
            now,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the place a leg of a hyperflight or of a jump ends at (task 00072).
    /// </summary>
    /// <remarks>
    /// The plan holds the legs of a command (task 00072), so the place of the leg is taken from the
    /// role of the leg: the leg to the edge ends at the edge of the system of the fleet, the leg
    /// between the systems ends at the edge of the system of the target, and the leg from the edge
    /// ends at the target itself.
    /// </remarks>
    /// <param name="fleet">The fleet.</param>
    /// <param name="order">The order of the leg.</param>
    /// <param name="origin">The place the fleet stands at.</param>
    /// <param name="kind">The class of the drives of the command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when it is not on the map.</returns>
    private async Task<WorldPlaceDto?> ResolveLegAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        WorldPlaceDto origin,
        string kind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(origin);

        // A command that is not yet spread over its legs keeps its target, so the whole command
        // is planned at once.
        if (order.Leg is null)
        {
            return await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false);
        }

        if (string.Equals(order.Leg, FlightLegKinds.ToEdge, StringComparison.Ordinal))
        {
            return origin.SystemId is { } systemId
                ? await _world.GetSystemEdgePlaceAsync(systemId, cancellationToken).ConfigureAwait(false)
                : null;
        }

        if (string.Equals(order.Leg, FlightLegKinds.FromEdge, StringComparison.Ordinal))
        {
            return await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false);
        }

        // The leg between the systems: it ends at the edge of the system of the target.
        WorldPlaceDto? target = await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false);

        if (target?.SystemId is not { } targetSystemId)
        {
            return target;
        }

        return await _world.GetSystemEdgePlaceAsync(targetSystemId, cancellationToken).ConfigureAwait(false) ?? target;
    }

    /// <summary>
    /// Burns the fuel of a leg from the hold of the fleet (task 00072).
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="kind">The class of the drives of the leg.</param>
    /// <param name="seconds">The time of the leg, in game seconds.</param>
    /// <param name="inSystem">Whether the leg lies inside one star system, where the gravitational drives work.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fuel that was burned, or <see langword="null"/> when the hold holds less.</returns>
    private async Task<IReadOnlyDictionary<int, decimal>?> BurnFuelAsync(
        FleetEntity fleet,
        string kind,
        long seconds,
        bool inSystem,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        FleetEngines engines = await _engines.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<int, decimal> propellant = FuelPlanner.BurnOf(engines, kind, seconds, inSystem);

        if (propellant.Count == 0)
        {
            return propellant;
        }

        try
        {
            await _fuel.ConsumeAsync(fleet.Id, propellant, now, cancellationToken).ConfigureAwait(false);
        }
        catch (GameException exception) when (string.Equals(exception.Code, FleetErrorCodes.FuelShort, StringComparison.Ordinal))
        {
            return null;
        }

        return propellant;
    }

    /// <summary>
    /// The factor of the time of the preparation of a jump: the preparation lasts the same time up
    /// to the reference distance and grows past it (task 00072).
    /// </summary>
    /// <param name="distance">The distance of the jump, in the units of the map.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <returns>The factor.</returns>
    private static decimal JumpFactorOf(double distance, FleetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        double reference = Math.Max(1d, settings.JumpReferenceDistance);

        return (decimal)Math.Max(1d, distance / reference);
    }

    /// <summary>
    /// Writes the flight of the fleet and plans its arrival.
    /// </summary>
    /// <param name="fleet">The fleet; the entity is changed.</param>
    /// <param name="order">The order of the plan that is the flight.</param>
    /// <param name="origin">The place the fleet leaves.</param>
    /// <param name="destination">The place the fleet flies to.</param>
    /// <param name="distance">The distance of the flight.</param>
    /// <param name="speed">The speed of the fleet.</param>
    /// <param name="seconds">The time of the flight, in game seconds.</param>
    /// <param name="arrivesAt">The moment the fleet arrives.</param>
    /// <param name="legKind">The class of the drives of the flight.</param>
    /// <param name="prepEndsAt">The moment the preparation of a jump is over at.</param>
    /// <param name="propellant">The fuel the flight burns.</param>
    /// <param name="now">The moment the fleet leaves.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The state of the order and the reason it is blocked.</returns>
    private async Task<(BeginState State, string? Code)> StartFlightAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        WorldPlaceDto origin,
        WorldPlaceDto destination,
        double distance,
        decimal speed,
        long seconds,
        DateTimeOffset arrivesAt,
        string legKind,
        DateTimeOffset? prepEndsAt,
        IReadOnlyDictionary<int, decimal> propellant,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(propellant);

        FleetFlightEntity flight = new()
        {
            FleetId = fleet.Id,
            FlightPlanOrderId = order.Id,
            StartedAt = now,
            ArrivesAt = arrivesAt,
            Distance = distance,
            Speed = speed,
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
            LegKind = legKind,
            PrepEndsAt = prepEndsAt,
            FuelJson = JsonSerializer.Serialize(propellant),
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

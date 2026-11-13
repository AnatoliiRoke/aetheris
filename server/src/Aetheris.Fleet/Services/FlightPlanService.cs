using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The flight plans of the fleets of a player: writing a plan, changing it, stopping it and
/// deleting it (task 00050).
/// </summary>
/// <remarks>
/// <para>
/// A plan belongs to the player and to the fleet at once. The player comes from the session and
/// the fleet is looked up by its owner, so a plan of another player is never read and never
/// changed. A fleet has one plan at most, so the plan of a fleet that already has one is
/// refused.
/// </para>
/// <para>
/// Every change locks the row of the fleets of the player first, as the commands of the fleets
/// do, so twenty requests at once do not make two plans of one fleet.
/// </para>
/// <para>
/// The orders of a plan are carried out one after another by <see cref="FlightPlanRunner"/> (task
/// 00053); this service keeps the model and the commands around it: writing, changing, deleting,
/// stopping and starting a plan.
/// </para>
/// </remarks>
public sealed class FlightPlanService
{
    /// <summary>The key of the details of a refusal for the shortage of the fuel (task 00072).</summary>
    public const string ShortagesDetail = "shortages";

    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly FlightPlanRunner _runner;
    private readonly FleetFlightService _flights;
    private readonly FlightLegPlanner _legs;
    private readonly FlightChainResolver _chain;
    private readonly EngineKindsReader _engines;
    private readonly FleetSpeedService _speeds;
    private readonly FuelService _fuel;
    private readonly IWorldModule _world;
    private readonly IGameClock _clock;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlanService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="runner">Carries the orders of a plan out.</param>
    /// <param name="flights">Finds the places of the map.</param>
    /// <param name="legs">Plans the legs of a command (task 00072).</param>
    /// <param name="chain">Walks the route of the plan (task 00072).</param>
    /// <param name="engines">The drives of the ships of the fleet (task 00072).</param>
    /// <param name="speeds">The speeds of the fleet (task 00072).</param>
    /// <param name="propellant">The fuel of the fleet (task 00072).</param>
    /// <param name="world">The world module.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="config">The game configuration.</param>
    public FlightPlanService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        FlightPlanRunner runner,
        FleetFlightService flights,
        FlightLegPlanner legs,
        FlightChainResolver chain,
        EngineKindsReader engines,
        FleetSpeedService speeds,
        FuelService propellant,
        IWorldModule world,
        IGameClock clock,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(propellant);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _runner = runner;
        _flights = flights;
        _legs = legs;
        _chain = chain;
        _engines = engines;
        _speeds = speeds;
        _fuel = propellant;
        _world = world;
        _clock = clock;
        _config = config;
    }

    /// <summary>
    /// Reads the plans of the player with their orders.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The plans of the player.</returns>
    public async Task<IReadOnlyList<FlightPlanDto>> ReadAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        List<FlightPlanEntity> plans = await _dbContext.FlightPlans.AsNoTracking()
            .Include(plan => plan.OrderIds)
            .Where(plan => plan.PlayerId == playerId)
            .OrderBy(plan => plan.CreatedAt)
            .ThenBy(plan => plan.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. plans.Select(ToDto)];
    }

    /// <summary>
    /// Reads the plan of one fleet of the player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The plan of the fleet, or <see langword="null"/> when it has none.</returns>
    public async Task<FlightPlanDto?> ReadForFleetAsync(string playerId, long fleetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        FlightPlanEntity? plan = await _dbContext.FlightPlans.AsNoTracking()
            .Include(entry => entry.OrderIds)
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId && entry.FleetId == fleetId, cancellationToken)
            .ConfigureAwait(false);

        return plan is null ? null : ToDto(plan);
    }

    /// <summary>
    /// Writes a plan for a fleet of the player. The fleet has no plan yet: a plan that is
    /// written for a fleet that has one is refused.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet the plan is made for.</param>
    /// <param name="orders">The orders of the plan, in the order they are carried out in.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The plan that was written.</returns>
    public Task<FlightPlanDto> CreateAsync(
        string playerId,
        long fleetId,
        IReadOnlyList<FlightPlanOrderInput> orders,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(orders);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                if (await _dbContext.FlightPlans.AnyAsync(plan => plan.FleetId == fleetId, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanExists, "The fleet already has a flight plan.");
                }

                WorldPlaceDto start = await CurrentPlaceAsync(fleet, cancellationToken).ConfigureAwait(false);
                List<FlightPlanOrderEntity> written = await BuildLegsAsync(fleet, orders, start, cancellationToken).ConfigureAwait(false);

                FlightPlanEntity plan = new()
                {
                    PlayerId = playerId,
                    FleetId = fleetId,
                    StateCode = FlightPlanStates.Draft,
                    CreatedAt = _clock.GameNow,
                    UpdatedAt = _clock.GameNow,
                    OrderIds = written,
                };

                _dbContext.FlightPlans.Add(plan);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return ToDto(plan);
            },
            cancellationToken);
    }

    /// <summary>
    /// Changes the orders of the plan of a fleet. The plan of a fleet that is under way is not
    /// changed: it is stopped first.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="orders">The orders of the plan, in the order they are carried out in.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The plan after the change.</returns>
    public Task<FlightPlanDto> SaveAsync(
        string playerId,
        long fleetId,
        IReadOnlyList<FlightPlanOrderInput> orders,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(orders);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FlightPlanEntity plan = await LoadPlanAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                if (string.Equals(plan.StateCode, FlightPlanStates.Active, StringComparison.Ordinal))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanUnderWay, "The plan of the fleet is under way and cannot be changed.");
                }

                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                WorldPlaceDto start = await CurrentPlaceAsync(fleet, cancellationToken).ConfigureAwait(false);
                List<FlightPlanOrderEntity> written = await BuildLegsAsync(fleet, orders, start, cancellationToken).ConfigureAwait(false);

                _dbContext.FlightPlanOrders.RemoveRange(plan.OrderIds);
                plan.OrderIds = written;
                plan.UpdatedAt = _clock.GameNow;
                plan.Version++;

                // The orders are new, so the progress through the old ones means nothing.
                plan.CurrentPosition = 0;
                plan.PauseCode = null;

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return ToDto(plan);
            },
            cancellationToken);
    }

    /// <summary>
    /// Deletes the plan of a fleet. A plan that is under way is not deleted: it is stopped
    /// first.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the plan is gone.</returns>
    public Task DeleteAsync(string playerId, long fleetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FlightPlanEntity plan = await LoadPlanAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                if (string.Equals(plan.StateCode, FlightPlanStates.Active, StringComparison.Ordinal))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanUnderWay, "The plan of the fleet is under way and cannot be deleted.");
                }

                _dbContext.FlightPlans.Remove(plan);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return 0;
            },
            cancellationToken);
    }

    /// <summary>
    /// Stops the plan of a fleet: the fleet waits where it is, and the player may carry the
    /// plan out again or write a new one.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The plan after the change.</returns>
    public Task<FlightPlanDto> StopAsync(string playerId, long fleetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FlightPlanEntity plan = await LoadPlanAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                if (!string.Equals(plan.StateCode, FlightPlanStates.Active, StringComparison.Ordinal))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanNotUnderWay, "The plan of the fleet is not under way.");
                }

                plan.StateCode = FlightPlanStates.Paused;
                plan.PauseCode = null;
                plan.UpdatedAt = _clock.GameNow;
                plan.Version++;

                // A fleet that is under way hangs at the point of its route it has reached and waits
                // there (decision 4 of task 00050); the events of the run that was stopped change
                // nothing, because the plan has another version now.
                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                await _runner.SettleAsync(fleet, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return ToDto(plan);
            },
            cancellationToken);
    }

    /// <summary>
    /// Starts the plan of a fleet (task 00053): the orders are carried out one after another, the first
    /// begins now and every next one begins when the previous is over. A plan that was stopped goes on
    /// with the order it was stopped at.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The plan after the change; a plan whose orders all took no time is already carried out.</returns>
    public Task<FlightPlanDto> ExecuteAsync(string playerId, long fleetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(_dbContext, playerId, cancellationToken).ConfigureAwait(false);
                FlightPlanEntity plan = await LoadPlanAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                if (string.Equals(plan.StateCode, FlightPlanStates.Active, StringComparison.Ordinal))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanUnderWay, "The plan of the fleet is under way already.");
                }

                FleetEntity fleet = await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);
                DateTimeOffset now = _clock.GameNow;

                if (!string.Equals(fleet.StateCode, FleetStates.Stationed, StringComparison.Ordinal))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanUnderWay, "The fleet is under way.");
                }

                await _runner.ValidateAsync(fleet, plan.OrderIds, cancellationToken).ConfigureAwait(false);

                // The fuel of the whole route is counted before the fleet leaves (task 00072): the
                // fleet does not leave when the hold holds less than the route needs.
                await CheckFuelAsync(fleet, plan.OrderIds, cancellationToken).ConfigureAwait(false);

                // A plan that was stopped begins again with the order it was stopped at; a plan that
                // never began starts with its first order.
                plan.CurrentPosition = Math.Max(0, plan.CurrentPosition - 1);
                plan.StateCode = FlightPlanStates.Active;
                plan.PauseCode = null;
                plan.UpdatedAt = now;
                plan.Version++;

                // The events of the plan carry its version, so they are the events of this run.
                await _runner.AdvanceAsync(plan, fleet, now, cancellationToken).ConfigureAwait(false);

                // A plan that is carried out at once is deleted by the runner; the answer says so.
                if (_dbContext.Entry(plan).State == EntityState.Deleted)
                {
                    plan.StateCode = FlightPlanStates.Completed;
                }

                return ToDto(plan);
            },
            cancellationToken);
    }

    /// <summary>
    /// Counts the fuel the route of a plan needs and what the hold of the fleet holds (task 00072).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The need is counted with the full holds of the fleet, because that is the heaviest and the
    /// most expensive flight. The flight home is counted as well, in the mode of the last flight
    /// command of the plan, so the fleet comes home with the fuel for the way back.
    /// </para>
    /// <para>
    /// The orders of the plan are already the legs of the flights (task 00072), so the need is
    /// counted over them and not over the commands of the player.
    /// </para>
    /// </remarks>
    /// <param name="fleet">The fleet of the plan.</param>
    /// <param name="orders">The orders of the plan.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The estimate of the fuel of the route.</returns>
    public async Task<FlightFuelEstimateDto> EstimateFuelAsync(
        FleetEntity fleet,
        IReadOnlyList<FlightPlanOrderEntity> orders,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(orders);

        FleetSettings settings = FleetSettings.From(_config);
        FleetEngines engines = await _engines.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
        FleetSpeeds speeds = await _speeds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, decimal> have = await _fuel.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

        WorldPlaceDto start = await CurrentPlaceAsync(fleet, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FlightLeg> legs = await _chain.ResolveAsync(fleet, orders, start, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<FlightLeg> returnLegs = settings.FuelCheckReturnHome
            ? await PlanReturnAsync(fleet, orders, legs, cancellationToken).ConfigureAwait(false)
            : [];

        FuelEstimate estimate = FuelPlanner.EstimateWithReturn(engines, speeds, legs, returnLegs, have, settings);

        return ToFuelDto(estimate);
    }

    /// <summary>
    /// Adds the legs and the fuel to the estimate of a flight (task 00072, 4.7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The legs are the legs of the flight the request names: a hyperflight or a jump is spread
    /// over its two or three legs, and the times of the estimate are the sums of the legs; an
    /// ordinary flight is one leg. Every leg carries the fuel it burns with full holds.
    /// </para>
    /// <para>
    /// The need, the shortage and the flight home are those of the saved plan of the fleet: the
    /// same check that runs when the player carries the plan out.
    /// </para>
    /// </remarks>
    /// <param name="fleet">The fleet.</param>
    /// <param name="typeCode">The type of the flight the request names.</param>
    /// <param name="target">The target of the flight.</param>
    /// <param name="from">The place the flight starts at, or <see langword="null"/> for the place of the fleet.</param>
    /// <param name="estimate">The estimate of an ordinary flight to the target.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The estimate with the legs and the fuel.</returns>
    public async Task<FlightEstimateDto> ExtendEstimateAsync(
        FleetEntity fleet,
        string? typeCode,
        FlightTarget? target,
        FlightTarget? from,
        FlightEstimateDto estimate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(estimate);

        FleetSettings settings = FleetSettings.From(_config);
        FleetEngines engines = await _engines.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);
        FleetSpeeds speeds = await _speeds.ReadAsync(fleet.Id, cancellationToken).ConfigureAwait(false);

        WorldPlaceDto? origin = from is null
            ? await CurrentPlaceAsync(fleet, cancellationToken).ConfigureAwait(false)
            : await _flights.ResolveAsync(from, cancellationToken).ConfigureAwait(false);
        WorldPlaceDto? destination = typeCode == FlightOrderTypes.Return
            ? await _world.GetPlanetPlaceAsync(fleet.HomePlanetId, cancellationToken).ConfigureAwait(false)
            : target is null ? null : await _flights.ResolveAsync(target, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<FlightLeg> legs = [];
        FlightEstimateDto extended = estimate;

        if (origin is not null && destination is not null)
        {
            bool isFtl = typeCode == FlightOrderTypes.Hyperfly || typeCode == FlightOrderTypes.Jump;

            if (isFtl && !(origin.SystemId is { } originSystemId && originSystemId == destination.SystemId))
            {
                string kind = typeCode == FlightOrderTypes.Hyperfly ? FlightEngineKinds.Hyper : FlightEngineKinds.Jump;

                legs = await _legs.PlanAsync(origin, destination, kind, cancellationToken).ConfigureAwait(false);

                extended = extended with
                {
                    Distance = legs.Sum(leg => leg.Distance),
                    EmptySeconds = legs.Sum(leg => FuelPlanner.SecondsOf(leg, speeds, settings, set => set.Empty)),
                    CurrentSeconds = legs.Sum(leg => FuelPlanner.SecondsOf(leg, speeds, settings, set => set.Current)),
                    FullSeconds = legs.Sum(leg => FuelPlanner.SecondsOf(leg, speeds, settings, set => set.Full)),
                    CanFly = engines.ShipsWithout(kind).Count == 0 && engines.ShipsWithout(FlightEngineKinds.Normal).Count == 0,
                };
            }
            else if (!isFtl)
            {
                legs =
                [
                    new FlightLeg(
                        FlightLegKinds.Normal,
                        FlightEngineKinds.Normal,
                        origin,
                        destination,
                        _world.GetTravelDistance(origin, destination)),
                ];
            }
        }

        FuelEstimate counted = FuelPlanner.Estimate(engines, speeds, legs, settings);

        // The need and the shortage of the saved plan: the same check as the one before the fleet leaves.
        List<FlightPlanOrderEntity> orders = await _dbContext.FlightPlanOrders.AsNoTracking()
            .Where(order => _dbContext.FlightPlans.Any(plan => plan.Id == order.FlightPlanId && plan.FleetId == fleet.Id))
            .OrderBy(order => order.Position)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        FlightFuelEstimateDto? plan = orders.Count == 0
            ? null
            : await EstimateFuelAsync(fleet, orders, cancellationToken).ConfigureAwait(false);

        return extended with
        {
            Legs = [.. counted.Legs.Select(ToFuelLegDto)],
            ReturnLegs = plan?.ReturnLegs ?? [],
            FuelNeed = plan?.FuelNeed ?? [],
            Shortages = plan?.Shortages ?? [],
        };
    }

    /// <summary>
    /// Checks that the hold of the fleet holds the fuel the route of a plan needs (task 00072).
    /// </summary>
    /// <param name="fleet">The fleet of the plan.</param>
    /// <param name="orders">The orders of the plan.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the fleet has the fuel it needs.</returns>
    /// <exception cref="GameException">The hold holds less fuel than the route needs.</exception>
    private async Task CheckFuelAsync(
        FleetEntity fleet,
        IReadOnlyList<FlightPlanOrderEntity> orders,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(orders);

        FlightFuelEstimateDto estimate = await EstimateFuelAsync(fleet, orders, cancellationToken).ConfigureAwait(false);

        if (estimate.Shortages.Count == 0)
        {
            return;
        }

        string shortages = string.Join(
            ", ",
            estimate.Shortages.Select(shortage => $"{shortage.ResourceTypeId}: {shortage.Need} of {shortage.Have}"));

        throw new GameException(
            FleetErrorCodes.FuelShort,
            $"The fleet does not have the fuel its route needs ({shortages}).",
            new Dictionary<string, object?> { [ShortagesDetail] = estimate.Shortages });
    }

    /// <summary>
    /// Plans the legs of the flight home (task 00072).
    /// </summary>
    /// <remarks>
    /// The flight home is flown in the mode of the last flight command of the plan: a plan that
    /// ends with a hyperflight comes home with a hyperflight, and a plan that ends with a jump
    /// comes home with a jump.
    /// </remarks>
    /// <param name="fleet">The fleet of the plan.</param>
    /// <param name="orders">The orders of the plan.</param>
    /// <param name="legs">The legs of the route of the plan.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The legs of the flight home.</returns>
    private async Task<IReadOnlyList<FlightLeg>> PlanReturnAsync(
        FleetEntity fleet,
        IReadOnlyList<FlightPlanOrderEntity> orders,
        IReadOnlyList<FlightLeg> legs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(legs);

        // A plan without a flight does not come home, so it needs no fuel for the way back.
        if (legs.Count == 0)
        {
            return [];
        }

        WorldPlaceDto? home = await _world.GetPlanetPlaceAsync(fleet.HomePlanetId, cancellationToken).ConfigureAwait(false);

        if (home is null)
        {
            return [];
        }

        WorldPlaceDto end = legs[^1].To;

        // The mode of the way home is the mode of the last flying order of the plan (task 00072):
        // an ordinary flight after a hyperflight brings the fleet home the ordinary way.
        string kind = FlightEngineKinds.Normal;

        foreach (FlightPlanOrderEntity order in orders.OrderBy(entry => entry.Position))
        {
            string? command = FlightChainResolver.CommandOf(order);

            if (command is not null)
            {
                kind = string.Equals(command, FlightOrderTypes.Hyperfly, StringComparison.Ordinal)
                    ? FlightEngineKinds.Hyper
                    : FlightEngineKinds.Jump;
            }
            else if (order.TypeCode == FlightOrderTypes.Fly || order.TypeCode == FlightOrderTypes.Return)
            {
                kind = FlightEngineKinds.Normal;
            }
        }

        // The way home inside one system, or the ordinary way home, is one ordinary flight; a
        // hyperflight or a jump home is spread over its legs.
        if (end.SystemId == home.SystemId || string.Equals(kind, FlightEngineKinds.Normal, StringComparison.Ordinal))
        {
            return
            [
                new FlightLeg(
                    FlightLegKinds.Normal,
                    FlightEngineKinds.Normal,
                    end,
                    home,
                    _world.GetTravelDistance(end, home)),
            ];
        }

        return await _legs.PlanAsync(end, home, kind, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the estimate of the fuel into the answer of the server.
    /// </summary>
    /// <param name="estimate">The estimate.</param>
    /// <returns>The answer.</returns>
    private static FlightFuelEstimateDto ToFuelDto(FuelEstimate estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        return new FlightFuelEstimateDto(
            [.. estimate.Legs.Select(ToFuelLegDto)],
            [.. estimate.ReturnLegs.Select(ToFuelLegDto)],
            [
                .. estimate.NeedByResource
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new FlightFuelResourceDto(
                        pair.Key,
                        pair.Value,
                        estimate.HaveByResource.GetValueOrDefault(pair.Key))),
            ],
            [
                .. estimate.Shortages()
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new FlightFuelShortageDto(
                        pair.Key,
                        estimate.NeedByResource.GetValueOrDefault(pair.Key),
                        estimate.HaveByResource.GetValueOrDefault(pair.Key))),
            ]);
    }

    /// <summary>
    /// Turns one leg with its fuel into the answer of the server.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <returns>The answer.</returns>
    private static FlightFuelLegDto ToFuelLegDto(FuelLeg leg)
    {
        ArgumentNullException.ThrowIfNull(leg);

        return new FlightFuelLegDto(
            leg.Leg,
            leg.Kind,
            leg.From.SectorId,
            leg.From.SystemId,
            leg.From.Orbit,
            leg.To.SectorId,
            leg.To.SystemId,
            leg.To.Orbit,
            leg.Distance,
            leg.Seconds,
            [
                .. leg.Fuel
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new FlightFuelResourceDto(pair.Key, pair.Value)),
            ]);
    }

    private async Task<FleetEntity> LoadFleetAsync(string playerId, long fleetId, CancellationToken cancellationToken) =>
        await _dbContext.Fleets
            .SingleOrDefaultAsync(fleet => fleet.Id == fleetId && fleet.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet does not exist.");

    /// <summary>
    /// Finds the place the fleet stands at now.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place of the fleet.</returns>
    private async Task<WorldPlaceDto> CurrentPlaceAsync(FleetEntity fleet, CancellationToken cancellationToken)
    {
        WorldPlaceDto? place = await _flights.CurrentPlaceAsync(fleet, null, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        return place ?? throw new GameException(FleetErrorCodes.FlightTargetUnknown, "The fleet stands nowhere.");
    }

    private async Task<FlightPlanEntity> LoadPlanAsync(string playerId, long fleetId, CancellationToken cancellationToken) =>
        await _dbContext.FlightPlans
            .Include(plan => plan.OrderIds)
            .SingleOrDefaultAsync(plan => plan.PlayerId == playerId && plan.FleetId == fleetId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new GameException(FleetErrorCodes.FlightPlanUnknown, "The fleet has no flight plan.");

    /// <summary>
    /// Spreads the commands of the player over the legs of a flight (task 00072).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hyperflight or a jump is one command of the player and three legs of a flight: to the edge
    /// of the system, through the space between the systems, and from the edge of the system of the
    /// target to the target itself. The legs share one key, so the client shows them as one row
    /// with a list under it, and the command that made them, so the server knows what they are.
    /// </para>
    /// <para>
    /// The legs are counted over the route of the whole plan, so the place a command starts at is
    /// the place the command before it ends at. The plan is written with the legs, and the order
    /// of the player is replaced by them.
    /// </para>
    /// </remarks>
    /// <param name="fleet">The fleet of the plan.</param>
    /// <param name="orders">The commands of the player.</param>
    /// <param name="start">The place the fleet stands at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The orders of the plan, the commands of the player spread over their legs.</returns>
    private async Task<List<FlightPlanOrderEntity>> BuildLegsAsync(
        FleetEntity fleet,
        IReadOnlyList<FlightPlanOrderInput> orders,
        WorldPlaceDto start,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(start);

        int limit = FleetSettings.From(_config).FlightPlanMaxOrders;

        // The limit counts the commands of the player, so a hyperflight or a jump that is spread
        // over three legs counts as one command (task 00072).
        if (orders.Count > limit)
        {
            throw new GameException(FleetErrorCodes.FlightPlanTooManyOrders, "The plan has more orders than a plan may hold.");
        }

        List<FlightPlanOrderEntity> written = [];
        HashSet<string> checkedKinds = [];
        int position = 1;

        foreach (FlightPlanOrderInput order in orders)
        {
            if (order.TypeCode != FlightOrderTypes.Hyperfly && order.TypeCode != FlightOrderTypes.Jump)
            {
                written.Add(new FlightPlanOrderEntity
                {
                    Position = position++,
                    TypeCode = order.TypeCode,
                    TargetKind = order.TypeCode == FlightOrderTypes.Mine ? FlightTargetKinds.None : order.TargetKind,
                    TargetSectorId = order.TypeCode == FlightOrderTypes.Mine ? 0 : order.TargetSectorId,
                    TargetSystemId = order.TypeCode == FlightOrderTypes.Mine ? 0 : order.TargetSystemId,
                    TargetObjectId = order.TypeCode == FlightOrderTypes.Mine ? 0 : order.TargetObjectId,
                    TargetX = order.TypeCode == FlightOrderTypes.Mine ? 0m : order.TargetX,
                    TargetY = order.TypeCode == FlightOrderTypes.Mine ? 0m : order.TargetY,
                    TargetZ = order.TypeCode == FlightOrderTypes.Mine ? 0m : order.TargetZ,
                    DurationSeconds = order.DurationSeconds,
                    Parameters = order.Parameters,
                });

                if (order.TypeCode == FlightOrderTypes.Fly || order.TypeCode == FlightOrderTypes.Return)
                {
                    // The order of the return flies to the home planet of the fleet, whatever it names.
                    WorldPlaceDto? target = order.TypeCode == FlightOrderTypes.Return
                        ? await _world.GetPlanetPlaceAsync(fleet.HomePlanetId, cancellationToken).ConfigureAwait(false)
                        : await _flights.ResolveAsync(FlightTarget.Of(order), cancellationToken).ConfigureAwait(false);

                    if (target is not null)
                    {
                        start = target;
                    }
                }

                continue;
            }

            WorldPlaceDto? destination = await _flights.ResolveAsync(FlightTarget.Of(order), cancellationToken).ConfigureAwait(false);

            if (destination is null)
            {
                throw new GameException(FleetErrorCodes.FlightTargetUnknown, "The target of the flight is not a place of the map.");
            }

            // A hyperflight or a jump inside one system makes no sense: inside a system only the
            // ordinary drives fly.
            if (start.SystemId is { } startSystemId && startSystemId == destination.SystemId)
            {
                throw new GameException(FleetErrorCodes.FtlSameSystem, "The target of the flight is in the system the fleet stands in.");
            }

            string kind = order.TypeCode == FlightOrderTypes.Hyperfly
                ? FlightEngineKinds.Hyper
                : FlightEngineKinds.Jump;

            // Every ship of the fleet needs the drive of the class and an ordinary drive (task 00072).
            if (checkedKinds.Add(kind))
            {
                await _runner.CheckFtlDrivesAsync(fleet, kind, cancellationToken).ConfigureAwait(false);
            }

            IReadOnlyList<FlightLeg> legs = await _legs.PlanAsync(start, destination, kind, cancellationToken).ConfigureAwait(false);
            Guid groupKey = Guid.NewGuid();

            // Every leg keeps the target of the command: the leg from the edge flies to it, the leg
            // between the systems ends at the edge of its system and the leg to the edge ends at the
            // edge of the system the fleet stands in when the leg begins. The legs inside a system
            // are ordinary flights; only the leg between the systems is a hyperflight or a jump.
            foreach (FlightLeg leg in legs)
            {
                bool isFtl = string.Equals(leg.Leg, FlightLegKinds.Ftl, StringComparison.Ordinal);

                written.Add(new FlightPlanOrderEntity
                {
                    Position = position++,
                    TypeCode = isFtl ? order.TypeCode : FlightOrderTypes.Fly,
                    TargetKind = order.TargetKind,
                    TargetSectorId = order.TargetSectorId,
                    TargetSystemId = order.TargetSystemId,
                    TargetObjectId = order.TargetObjectId,
                    TargetX = order.TargetX,
                    TargetY = order.TargetY,
                    TargetZ = order.TargetZ,
                    DurationSeconds = 0,
                    Parameters = "{}",
                    GroupKey = groupKey,
                    Command = order.TypeCode,
                    Leg = leg.Leg,
                });
            }

            start = legs.Count > 0 ? legs[^1].To : destination;
        }

        return written;
    }

    private static FlightPlanDto ToDto(FlightPlanEntity plan) =>
        new(
            plan.Id,
            plan.FleetId,
            plan.StateCode,
            plan.CreatedAt,
            plan.UpdatedAt,
            plan.Version,
            [
                .. plan.OrderIds
                    .OrderBy(order => order.Position)
                    .Select(order => new FlightPlanOrderDto(
                        order.Id,
                        order.Position,
                        order.TypeCode,
                        order.TargetKind,
                        order.TargetSectorId,
                        order.TargetSystemId,
                        order.TargetObjectId,
                        order.TargetX,
                        order.TargetY,
                        order.TargetZ,
                        order.DurationSeconds,
                        order.Parameters,
                        order.GroupKey,
                        order.Command,
                        order.Leg)),
            ],
            plan.CurrentPosition,
            plan.PauseCode);
}

/// <summary>
/// One order of a flight plan as the player sends it (task 00050).
/// </summary>
/// <param name="TypeCode">The type of the order, one of <see cref="FlightOrderTypes"/>.</param>
/// <param name="TargetKind">The kind of the target, one of <see cref="FlightTargetKinds"/>.</param>
/// <param name="TargetSectorId">The sector of the target.</param>
/// <param name="TargetSystemId">The system of the target.</param>
/// <param name="TargetObjectId">The object of the target.</param>
/// <param name="TargetX">The first axis of the target.</param>
/// <param name="TargetY">The second axis of the target.</param>
/// <param name="TargetZ">The third axis of the target.</param>
/// <param name="DurationSeconds">How long the order lasts, in game seconds.</param>
/// <param name="Parameters">The parameters of the order as a document.</param>
public sealed record FlightPlanOrderInput(
    string TypeCode,
    string TargetKind,
    int TargetSectorId,
    long TargetSystemId,
    long TargetObjectId,
    decimal TargetX,
    decimal TargetY,
    decimal TargetZ,
    long DurationSeconds,
    string Parameters);

using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
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
    private readonly FleetDbContext _dbContext;
    private readonly FleetUnitOfWork _unitOfWork;
    private readonly FlightPlanRunner _runner;
    private readonly IGameClock _clock;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlanService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="runner">Carries the orders of a plan out.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="config">The game configuration.</param>
    public FlightPlanService(
        FleetDbContext dbContext,
        FleetUnitOfWork unitOfWork,
        FlightPlanRunner runner,
        IGameClock clock,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _runner = runner;
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
                await LoadFleetAsync(playerId, fleetId, cancellationToken).ConfigureAwait(false);

                if (await _dbContext.FlightPlans.AnyAsync(plan => plan.FleetId == fleetId, cancellationToken).ConfigureAwait(false))
                {
                    throw new GameException(FleetErrorCodes.FlightPlanExists, "The fleet already has a flight plan.");
                }

                List<FlightPlanOrderEntity> written = BuildOrders(orders);

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

                List<FlightPlanOrderEntity> written = BuildOrders(orders);

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

    private async Task<FleetEntity> LoadFleetAsync(string playerId, long fleetId, CancellationToken cancellationToken) =>
        await _dbContext.Fleets
            .SingleOrDefaultAsync(fleet => fleet.Id == fleetId && fleet.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet does not exist.");

    private async Task<FlightPlanEntity> LoadPlanAsync(string playerId, long fleetId, CancellationToken cancellationToken) =>
        await _dbContext.FlightPlans
            .Include(plan => plan.OrderIds)
            .SingleOrDefaultAsync(plan => plan.PlayerId == playerId && plan.FleetId == fleetId, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new GameException(FleetErrorCodes.FlightPlanUnknown, "The fleet has no flight plan.");

    private List<FlightPlanOrderEntity> BuildOrders(IReadOnlyList<FlightPlanOrderInput> orders)
    {
        int limit = FleetSettings.From(_config).FlightPlanMaxOrders;

        if (orders.Count > limit)
        {
            throw new GameException(FleetErrorCodes.FlightPlanTooManyOrders, "The plan has more orders than a plan may hold.");
        }

        List<FlightPlanOrderEntity> written = [];
        int position = 1;

        foreach (FlightPlanOrderInput order in orders)
        {
            // An order of extraction has the time only, and the time is optional (task 00054): zero means
            // no limit. The place is where the fleet is when the order begins, so a target the request
            // names is dropped.
            bool isMine = order.TypeCode == FlightOrderTypes.Mine;

            written.Add(new FlightPlanOrderEntity
            {
                Position = position++,
                TypeCode = order.TypeCode,
                TargetKind = isMine ? FlightTargetKinds.None : order.TargetKind,
                TargetSectorId = isMine ? 0 : order.TargetSectorId,
                TargetSystemId = isMine ? 0 : order.TargetSystemId,
                TargetObjectId = isMine ? 0 : order.TargetObjectId,
                TargetX = isMine ? 0m : order.TargetX,
                TargetY = isMine ? 0m : order.TargetY,
                TargetZ = isMine ? 0m : order.TargetZ,
                DurationSeconds = order.DurationSeconds,
                Parameters = order.Parameters,
            });
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
                        order.Parameters)),
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

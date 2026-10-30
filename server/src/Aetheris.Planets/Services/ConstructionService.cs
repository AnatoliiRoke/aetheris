using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Services;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Requirements;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The changes of a body: the construction queue and the balance (task 00011, decisions 5–8).
/// </summary>
/// <remarks>
/// <para>
/// Every change locks the state row of the body first, so the commands of one body run one
/// after another and never spend the same resources twice. Then the due constructions are
/// completed at their own moments and the balance is fixed at the moment of the change; only
/// after that the change itself is checked and applied.
/// </para>
/// <para>
/// The queue is sequential: the first record is under way, the others wait. Its end is a
/// scheduled event; the handler and every later change complete what is due, so the result
/// does not depend on when the scheduler wakes up.
/// </para>
/// </remarks>
public sealed class ConstructionService
{
    /// <summary>
    /// The event type of the end of a construction.
    /// </summary>
    public const string CompleteEventType = "planets.construction_complete";

    private readonly PlanetsDbContext _dbContext;
    private readonly PlanetsUnitOfWork _unitOfWork;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly ITechnologyLevels _technologies;
    private readonly IEconomyModule _economy;
    private readonly CostSplitter _costs;
    private readonly IRequirementsModule _requirements;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructionService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="catalogCache">The building catalog.</param>
    /// <param name="world">The world, for the deposits.</param>
    /// <param name="technologies">The technologies of the players.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The game clock.</param>
    public ConstructionService(
        PlanetsDbContext dbContext,
        PlanetsUnitOfWork unitOfWork,
        BuildingCatalogCache catalogCache,
        IWorldModule world,
        ITechnologyLevels technologies,
        IEconomyModule economy,
        CostSplitter costs,
        IRequirementsModule requirements,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _world = world;
        _technologies = technologies;
        _economy = economy;
        _costs = costs;
        _requirements = requirements;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Puts the next level of a building into the queue of a body. The cost is taken at once,
    /// in one change; the construction starts at once when the queue is empty.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The body after the change.</returns>
    public Task<PlanetOverviewDto> EnqueueAsync(long planetId, int buildingTypeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        now = Normalize(now);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetSession session = await OpenAsync(planetId, now, cancellationToken).ConfigureAwait(false);
                BuildingTypeInfo buildingType = session.Catalog.FindType(buildingTypeId)
                    ?? throw new GameException(PlanetsErrorCodes.BuildingUnknown, "The building type does not exist.");

                PlanetFigures figures = session.Figures(now);
                NextLevelCheck check = ConstructionRules.CheckNext(session.Catalog, session.Settings, figures, buildingType);

                if (check.ReasonCodes.Count > 0)
                {
                    throw new GameException(check.ReasonCodes[0], "The building cannot be queued.", new Dictionary<string, object?>
                    {
                        ["reason_codes"] = check.ReasonCodes.ToArray(),
                        ["unmet_requirements"] = JsonSerializer.SerializeToElement(PlanetOverviewBuilder.Requirements(session.Catalog, check.Unmet), PlanetsJson.Options),
                        ["missing_resources"] = JsonSerializer.SerializeToElement(
                            check.Missing.Select(entry => new ResourceAmountDto(entry.Key, entry.Value)).ToArray(),
                            PlanetsJson.Options),
                    });
                }

                // The costs of a civilization are paid from its balance and the rest from the
                // balance of the body (task 00015).
                SplitCosts split = await _costs.SplitAsync(check.Info!.Costs, cancellationToken).ConfigureAwait(false);

                foreach ((int resourceTypeId, long amount) in split.PlanetResources)
                {
                    session.Take(resourceTypeId, amount);
                }

                // The credits belong to the civilization of the owner of the body.
                string ownerPlayerId = session.State.OwnerPlayerId
                    ?? throw new GameException(PlanetsErrorCodes.PlanetUnknown, "The body has no owner.");

                await _economy.TakeAsync(ownerPlayerId, split.Currencies, cancellationToken).ConfigureAwait(false);

                ConstructionQueueItemEntity item = new()
                {
                    SectorId = session.State.SectorId,
                    PlanetId = planetId,
                    BuildingTypeId = buildingTypeId,
                    TargetLevel = check.Level,
                    Position = session.Queue.Count + 1,
                    DurationSeconds = check.DurationSeconds,
                    CreatedAt = now,
                    CostIds = [.. check.Info.Costs.Select(cost => new ConstructionQueueCostEntity { ResourceTypeId = cost.Key, Amount = cost.Value })],
                };

                _dbContext.QueueItems.Add(item);
                session.Queue.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if (session.Queue.Count == 1)
                {
                    await StartAsync(session, item, now, cancellationToken).ConfigureAwait(false);
                }

                session.RefreshStorage(now);

                return session.Overview(now, _clock.Multiplier);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a record from the queue of a body and returns its whole cost. The later records
    /// of the same building go with it, since their levels rest on it; the next record starts
    /// at once when the one under way is removed.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="queueItemId">The record.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The body after the change.</returns>
    public Task<PlanetOverviewDto> CancelAsync(long planetId, long queueItemId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        now = Normalize(now);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetSession session = await OpenAsync(planetId, now, cancellationToken).ConfigureAwait(false);
                ConstructionQueueItemEntity cancelled = session.Queue.FirstOrDefault(entry => entry.Id == queueItemId)
                    ?? throw new GameException(PlanetsErrorCodes.QueueItemUnknown, "The queue record does not exist on the body.");

                bool headRemoved = ReferenceEquals(session.Queue[0], cancelled);
                List<ConstructionQueueItemEntity> removed =
                [
                    .. session.Queue.Where(entry => entry.BuildingTypeId == cancelled.BuildingTypeId && entry.TargetLevel >= cancelled.TargetLevel),
                ];

                // The cost returns to the balances it was paid from: the resources of the body to
                // the body and the currencies to the balance of the civilization (task 00015).
                Dictionary<int, long> refund = removed
                    .SelectMany(item => item.CostIds)
                    .GroupBy(cost => cost.ResourceTypeId)
                    .ToDictionary(group => group.Key, group => group.Sum(cost => cost.Amount));

                foreach (ConstructionQueueItemEntity item in removed)
                {
                    session.Queue.Remove(item);
                    _dbContext.QueueItems.Remove(item);
                }

                SplitCosts split = await _costs.SplitAsync(refund, cancellationToken).ConfigureAwait(false);

                foreach ((int resourceTypeId, long amount) in split.PlanetResources)
                {
                    session.Give(resourceTypeId, amount);
                }

                session.Renumber();
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                // The balance of the civilization is the last lock of the request, after the body.
                if (split.Currencies.Count > 0)
                {
                    string ownerPlayerId = session.State.OwnerPlayerId
                        ?? throw new GameException(PlanetsErrorCodes.PlanetUnknown, "The body has no owner.");

                    await _economy.GiveAsync(ownerPlayerId, split.Currencies, cancellationToken).ConfigureAwait(false);
                }

                if (headRemoved && session.Queue.Count > 0)
                {
                    await StartAsync(session, session.Queue[0], now, cancellationToken).ConfigureAwait(false);
                }

                session.RefreshStorage(now);

                return session.Overview(now, _clock.Multiplier);
            },
            cancellationToken);
    }

    /// <summary>
    /// Completes every construction of a body that is due at a moment, each at its own end.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of completed constructions.</returns>
    /// <remarks>
    /// The scheduled event of a construction calls this; so does the read of a body that finds
    /// a due construction. A body without a state, or an event of a cancelled record, changes
    /// nothing.
    /// </remarks>
    public Task<int> CompleteDueAsync(long planetId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        now = Normalize(now);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetSession? session = await TryOpenAsync(planetId, cancellationToken).ConfigureAwait(false);

                return session is null ? 0 : await CompleteDueAsync(session, now, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Takes amounts from the balance of a body or gives them back, for a module that pays for
    /// something on the body (task 00012).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="amounts">The amounts by resource.</param>
    /// <param name="give">Whether the amounts are given back rather than taken.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the balance is changed.</returns>
    /// <remarks>
    /// The body is opened like for a construction: the due constructions are completed and the
    /// balance is brought to the moment first, so the payment sees what the body has produced
    /// and the production after the change starts from the new amounts.
    /// </remarks>
    public Task ChangeBalanceAsync(
        long planetId,
        IReadOnlyDictionary<int, long> amounts,
        bool give,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        now = Normalize(now);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetSession session = await OpenAsync(planetId, now, cancellationToken).ConfigureAwait(false);

                foreach ((int resourceTypeId, long amount) in amounts.OrderBy(entry => entry.Key))
                {
                    if (amount < 0)
                    {
                        throw new GameException(PlanetsErrorCodes.ResourcesShort, "The amount of a resource cannot be negative.");
                    }

                    if (give)
                    {
                        session.Give(resourceTypeId, amount);
                    }
                    else
                    {
                        session.Take(resourceTypeId, amount);
                    }
                }

                session.RefreshStorage(now);

                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Brings the balance of every body of a player to a moment with the rates as they are, so
    /// a change of the bonuses of the player applies only from that moment on (documentation/tech/09,
    /// section 5.2).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="moment">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of bodies of the player.</returns>
    /// <remarks>
    /// The bodies are locked in the order of their identifiers, so two changes of one player
    /// never wait for each other in a circle. A moment before the last fix of a body changes no
    /// amount there and only computes again when its storage is full.
    /// </remarks>
    public Task<int> FixPlayerAsync(string playerId, DateTimeOffset moment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        moment = Normalize(moment);

        return _unitOfWork.RunAsync(
            async () =>
            {
                List<long> planetIds = await _dbContext.PlanetStates.AsNoTracking()
                    .Where(state => state.OwnerPlayerId == playerId)
                    .OrderBy(state => state.PlanetId)
                    .Select(state => state.PlanetId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // Every body is locked before any of them is changed: a completion calls the economy,
                // which locks the balances of the civilization, and the balances are the last lock
                // of a request, so a body is never locked after them.
                List<PlanetSession> sessions = [];

                foreach (long planetId in planetIds)
                {
                    if (await TryOpenAsync(planetId, cancellationToken).ConfigureAwait(false) is { } session)
                    {
                        sessions.Add(session);
                    }
                }

                foreach (PlanetSession session in sessions)
                {
                    await CompleteDueAsync(session, moment, cancellationToken).ConfigureAwait(false);
                    session.FixTo(moment);
                }

                return planetIds.Count;
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets the answer about a body inside the current change.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The answer, or <see langword="null"/> when the body has no state.</returns>
    public async Task<PlanetOverviewDto?> ReadAsync(long planetId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        PlanetSession? session = await TryOpenAsync(planetId, cancellationToken, forUpdate: false).ConfigureAwait(false);

        return session?.Overview(Normalize(now), _clock.Multiplier);
    }

    /// <summary>
    /// Cuts a moment to the microseconds the database keeps, so a moment read back equals the
    /// moment written and the answers of a change and of a later read agree.
    /// </summary>
    /// <param name="moment">The moment.</param>
    /// <returns>The moment without the ticks below a microsecond.</returns>
    public static DateTimeOffset Normalize(DateTimeOffset moment) =>
        moment.AddTicks(-(moment.Ticks % TimeSpan.TicksPerMicrosecond));

    private async Task<PlanetSession> OpenAsync(long planetId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        PlanetSession session = await TryOpenAsync(planetId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(PlanetsErrorCodes.PlanetUnknown, "The body is not a colony.");

        await CompleteDueAsync(session, now, cancellationToken).ConfigureAwait(false);
        session.FixTo(now);

        return session;
    }

    private async Task<PlanetSession?> TryOpenAsync(long planetId, CancellationToken cancellationToken, bool forUpdate = true)
    {
        PlanetStateEntity? state = forUpdate
            ? (await _dbContext.PlanetStates
                .FromSql($"SELECT * FROM planet_state WHERE planet_id = {planetId} FOR UPDATE")
                .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
            : await _dbContext.PlanetStates.AsNoTracking().SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken).ConfigureAwait(false);

        if (state is null)
        {
            return null;
        }

        IQueryable<PlanetBuildingEntity> buildings = _dbContext.PlanetBuildings.Where(entry => entry.PlanetId == planetId);
        IQueryable<ConstructionQueueItemEntity> queue = _dbContext.QueueItems.Include(entry => entry.CostIds).Where(entry => entry.PlanetId == planetId);
        IQueryable<PlanetResourceEntity> resources = _dbContext.PlanetResources.Where(entry => entry.PlanetId == planetId);
        IQueryable<PlanetStorageGroupEntity> groups = _dbContext.StorageGroups.Where(entry => entry.PlanetId == planetId);

        if (!forUpdate)
        {
            buildings = buildings.AsNoTracking();
            queue = queue.AsNoTracking();
            resources = resources.AsNoTracking();
            groups = groups.AsNoTracking();
        }

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        PlanetSession session = new(
            _dbContext,
            await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false),
            PlanetsSettings.From(_config),
            state,
            await buildings.ToListAsync(cancellationToken).ConfigureAwait(false),
            [.. (await queue.ToListAsync(cancellationToken).ConfigureAwait(false)).OrderBy(entry => entry.Position).ThenBy(entry => entry.Id)],
            await resources.ToListAsync(cancellationToken).ConfigureAwait(false),
            await groups.ToListAsync(cancellationToken).ConfigureAwait(false),
            (facts?.Deposits ?? []).ToDictionary(deposit => deposit.ResourceTypeId, deposit => deposit.Coefficient),
            await _technologies.GetLevelsAsync(state.OwnerPlayerId, cancellationToken).ConfigureAwait(false),
            await _technologies.GetBonusesAsync(state.OwnerPlayerId, cancellationToken).ConfigureAwait(false));

        // The costs in a currency are checked against the balance of the civilization.
        if (state.OwnerPlayerId is { } ownerPlayerId)
        {
            session.Currencies = await _economy.GetBalancesAsync(ownerPlayerId, cancellationToken).ConfigureAwait(false);
        }

        return session;
    }

    private async Task<int> CompleteDueAsync(PlanetSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        int completed = 0;

        while (session.Queue.Count > 0)
        {
            ConstructionQueueItemEntity head = session.Queue[0];

            if (head.FinishesAt is null)
            {
                // A head that never started (it should not happen) starts now.
                await StartAsync(session, head, Max(now, session.State.ResourcesUpdatedAt), cancellationToken).ConfigureAwait(false);

                continue;
            }

            if (head.FinishesAt > now)
            {
                break;
            }

            DateTimeOffset finishedAt = head.FinishesAt.Value;

            session.FixTo(finishedAt);

            // The new level may raise the largest population or the speed it grows with, so the
            // population and the credits of the civilization are counted to the end of the
            // construction with the buildings as they were (documentation/tech/09, section 5.2).
            if (session.State.OwnerPlayerId is { } ownerPlayerId)
            {
                await _economy.FixPlayerAsync(ownerPlayerId, finishedAt, cancellationToken).ConfigureAwait(false);
            }

            session.SetLevel(head.BuildingTypeId, head.TargetLevel);
            session.Queue.RemoveAt(0);
            _dbContext.QueueItems.Remove(head);
            session.Renumber();
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (session.Queue.Count > 0)
            {
                await StartAsync(session, session.Queue[0], finishedAt, cancellationToken).ConfigureAwait(false);
            }

            session.RefreshStorage(finishedAt);
            completed++;
        }

        return completed;
    }

    private async Task StartAsync(PlanetSession session, ConstructionQueueItemEntity item, DateTimeOffset startAt, CancellationToken cancellationToken)
    {
        item.StartedAt = startAt;
        item.FinishesAt = startAt.AddSeconds(item.DurationSeconds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                session.State.SectorId,
                CompleteEventType,
                item.FinishesAt.Value,
                JsonSerializer.Serialize(new CompletePayload(session.State.PlanetId, item.Id), PlanetsJson.Options),
                CompleteEventType + ":" + item.Id.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;

    /// <summary>
    /// The payload of the end of a construction.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="QueueItemId">The queue record.</param>
    public sealed record CompletePayload(long PlanetId, long QueueItemId);
}

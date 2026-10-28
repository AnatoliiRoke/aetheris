using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// The queue of the researches of a player (task 00012, decision 5a).
/// </summary>
/// <remarks>
/// <para>
/// The queue is one for the whole civilization and the researches follow one another, so the
/// record under way holds the moment it finishes and the ones that wait have none. The
/// resources are paid when a record is put in the queue and returned in full when it is
/// removed.
/// </para>
/// <para>
/// Every change locks the row of the queue of the player first, so two parallel commands of
/// one player never spend the same resources twice (task 00012, criterion 6).
/// </para>
/// </remarks>
public sealed class ResearchQueueService
{
    /// <summary>
    /// The event type of the end of a research.
    /// </summary>
    public const string CompleteEventType = "research.complete";

    private readonly ResearchDbContext _dbContext;
    private readonly ResearchUnitOfWork _unitOfWork;
    private readonly TechnologyCatalogCache _catalogCache;
    private readonly IPlanetsModule _planets;
    private readonly IPlanetResources _resources;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchQueueService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    /// <param name="planets">The bodies module, for the level of the science centre.</param>
    /// <param name="resources">The balance of a body, for the cost of the research.</param>
    /// <param name="world">The world, for the sector of the body.</param>
    /// <param name="scheduler">The scheduler of the events.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The game clock.</param>
    public ResearchQueueService(
        ResearchDbContext dbContext,
        ResearchUnitOfWork unitOfWork,
        TechnologyCatalogCache catalogCache,
        IPlanetsModule planets,
        IPlanetResources resources,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _planets = planets;
        _resources = resources;
        _world = world;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Puts a level of a technology in the queue of the researches of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the research starts from.</param>
    /// <param name="technologyId">The technology.</param>
    /// <param name="targetLevel">The level to research.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tree of the technologies and the queue after the change.</returns>
    public Task<ResearchTreeSnapshot> EnqueueAsync(
        string playerId,
        long planetId,
        int technologyId,
        int targetLevel,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                ResearchQueueStateEntity state = await LockStateAsync(playerId, cancellationToken).ConfigureAwait(false);
                List<ResearchQueueItemEntity> queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);

                TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
                TechnologyInfo technology = catalog.FindTechnology(technologyId)
                    ?? throw new GameException(ResearchErrorCodes.TechnologyUnknown, "The technology does not exist.");

                TechnologyLevelInfo level = technology.Levels.GetValueOrDefault(targetLevel)
                    ?? throw new GameException(ResearchErrorCodes.LevelUnknown, "The level of the technology does not exist.");

                await CheckAvailableAsync(playerId, planetId, technologyId, targetLevel, cancellationToken).ConfigureAwait(false);

                ResearchSettings settings = ResearchSettings.From(_config);
                int slots = settings.QueueBaseSlots + state.ExtraQueueSlots;

                if (queue.Count >= slots)
                {
                    throw new GameException(ResearchErrorCodes.QueueFull, "The queue of the researches has no free place.");
                }

                if (queue.Any(entry => entry.TechnologyId == technologyId && entry.TargetLevel == targetLevel))
                {
                    throw new GameException(ResearchErrorCodes.AlreadyQueued, "The level of the technology is already in the queue.");
                }

                // The resources are paid when the record enters the queue and returned in
                // full when it leaves it.
                await _resources.TakeAsync(planetId, playerId, level.Costs, cancellationToken).ConfigureAwait(false);

                DateTimeOffset now = _clock.GameNow;
                ResearchQueueItemEntity item = new()
                {
                    PlayerId = playerId,
                    PlanetId = planetId,
                    TechnologyId = technologyId,
                    TargetLevel = targetLevel,
                    Position = queue.Count + 1,
                    StartsAt = now,
                    FinishesAt = null,
                    CostIds = [.. level.Costs.Select(cost => new ResearchQueueCostEntity { ResourceTypeId = cost.Key, Amount = cost.Value })],
                };

                _dbContext.ResearchQueueItems.Add(item);
                queue.Add(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await StartNextAsync(queue, now, cancellationToken).ConfigureAwait(false);

                return await SnapshotAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Removes a record from the queue of the researches of a player and returns its whole
    /// cost.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="queueItemId">The record.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tree of the technologies and the queue after the change.</returns>
    public Task<ResearchTreeSnapshot> CancelAsync(string playerId, int queueItemId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await LockStateAsync(playerId, cancellationToken).ConfigureAwait(false);
                List<ResearchQueueItemEntity> queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);

                ResearchQueueItemEntity item = queue.FirstOrDefault(entry => entry.Id == queueItemId)
                    ?? throw new GameException(ResearchErrorCodes.QueueItemUnknown, "The record of the queue does not exist.");

                bool headRemoved = queue[0].Id == item.Id;
                Dictionary<int, long> refund = item.CostIds.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount);

                queue.Remove(item);
                _dbContext.ResearchQueueItems.Remove(item);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _resources.GiveAsync(item.PlanetId, playerId, refund, cancellationToken).ConfigureAwait(false);

                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                if (headRemoved)
                {
                    await StartNextAsync(queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);
                }

                return await SnapshotAsync(playerId, null, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Completes every research of a player that is due at a moment.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of completed researches.</returns>
    public Task<int> CompleteDueAsync(string playerId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await LockStateAsync(playerId, cancellationToken).ConfigureAwait(false);
                List<ResearchQueueItemEntity> queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);

                int completed = 0;

                while (queue.Count > 0)
                {
                    ResearchQueueItemEntity head = queue[0];

                    if (head.FinishesAt is null)
                    {
                        break;
                    }

                    if (head.FinishesAt > now)
                    {
                        break;
                    }

                    await LearnAsync(playerId, head, cancellationToken).ConfigureAwait(false);

                    queue.RemoveAt(0);
                    _dbContext.ResearchQueueItems.Remove(head);
                    Renumber(queue);
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    await StartNextAsync(queue, head.FinishesAt.Value, cancellationToken).ConfigureAwait(false);
                    completed++;
                }

                return completed;
            },
            cancellationToken);
    }

    /// <summary>
    /// Reads the queue of a player and the tree of the technologies for the read of the client.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the research starts from, or <see langword="null"/> to take the body with the highest science centre.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tree of the technologies and the queue.</returns>
    public async Task<ResearchTreeSnapshot> ReadAsync(string playerId, long? planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await CompleteDueAsync(playerId, _clock.GameNow, cancellationToken).ConfigureAwait(false);

        return await SnapshotAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ResearchQueueStateEntity> LockStateAsync(string playerId, CancellationToken cancellationToken)
    {
        ResearchQueueStateEntity? state = (await _dbContext.ResearchQueueStates
            .FromSql($"SELECT * FROM research_queue_state WHERE player_id = {playerId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).FirstOrDefault();

        if (state is not null)
        {
            return state;
        }

        state = new ResearchQueueStateEntity { PlayerId = playerId };
        _dbContext.ResearchQueueStates.Add(state);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return state;
    }

    private async Task<List<ResearchQueueItemEntity>> LoadQueueAsync(string playerId, CancellationToken cancellationToken) =>
        [.. await _dbContext.ResearchQueueItems
            .Include(item => item.CostIds)
            .Where(item => item.PlayerId == playerId)
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

    private async Task StartNextAsync(List<ResearchQueueItemEntity> queue, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0].FinishesAt is not null)
        {
            return;
        }

        ResearchQueueItemEntity head = queue[0];
        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        TechnologyInfo? technology = catalog.FindTechnology(head.TechnologyId);

        if (technology is null)
        {
            return;
        }

        TechnologyLevelInfo? level = technology.Levels.GetValueOrDefault(head.TargetLevel);

        if (level is null)
        {
            return;
        }

        head.StartsAt = now;
        head.FinishesAt = now.AddSeconds(level.DurationSeconds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(head.PlanetId, cancellationToken).ConfigureAwait(false);

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                facts?.SectorId ?? 0,
                CompleteEventType,
                head.FinishesAt.Value,
                JsonSerializer.Serialize(new CompletePayload(head.PlayerId, head.Id)),
                CompleteEventType + ":" + head.Id.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task LearnAsync(string playerId, ResearchQueueItemEntity item, CancellationToken cancellationToken)
    {
        PlayerTechnologyEntity? learned = await _dbContext.PlayerTechnologies
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId && entry.TechnologyId == item.TechnologyId, cancellationToken)
            .ConfigureAwait(false);

        if (learned is null)
        {
            _dbContext.PlayerTechnologies.Add(new PlayerTechnologyEntity
            {
                PlayerId = playerId,
                TechnologyId = item.TechnologyId,
                Level = item.TargetLevel,
            });
        }
        else if (item.TargetLevel > learned.Level)
        {
            learned.Level = item.TargetLevel;
        }
    }

    private async Task CheckAvailableAsync(
        string playerId,
        long planetId,
        int technologyId,
        int targetLevel,
        CancellationToken cancellationToken)
    {
        int centerLevel = await _planets.GetBuildingLevelAsync(
            BuildingClasses.ResearchCenter,
            planetId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        if (centerLevel <= 0)
        {
            throw new GameException(ResearchErrorCodes.ResearchCenterLevel, "The body has no science centre.");
        }

        IReadOnlyDictionary<int, int> learned = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId && entry.TechnologyId == technologyId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);

        int current = learned.GetValueOrDefault(technologyId);

        if (targetLevel <= current)
        {
            throw new GameException(ResearchErrorCodes.AlreadyLearned, "The player has already learned the level of the technology.");
        }
    }

    /// <summary>
    /// The payload of the end of a research.
    /// </summary>
    /// <param name="PlayerId">The player.</param>
    /// <param name="QueueItemId">The record of the queue.</param>
    public sealed record CompletePayload(string PlayerId, int QueueItemId);

    private static void Renumber(List<ResearchQueueItemEntity> queue)
    {
        for (int index = 0; index < queue.Count; index++)
        {
            queue[index].Position = index + 1;
        }
    }

    private async Task<ResearchTreeSnapshot> SnapshotAsync(string playerId, long? planetId, CancellationToken cancellationToken)
    {
        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        List<ResearchQueueItemEntity> queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, int> learned = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);

        ResearchSettings settings = ResearchSettings.From(_config);
        ResearchQueueStateEntity? state = await _dbContext.ResearchQueueStates.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        int slots = settings.QueueBaseSlots + (state?.ExtraQueueSlots ?? 0);

        return new ResearchTreeSnapshot(
            catalog,
            queue,
            learned,
            slots,
            planetId,
            await _planets.GetBuildingLevelAsync(BuildingClasses.ResearchCenter, planetId ?? 0, playerId, cancellationToken).ConfigureAwait(false));
    }
}

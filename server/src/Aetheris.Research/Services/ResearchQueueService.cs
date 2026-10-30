using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Services;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research.Api;
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
    private readonly IEconomyModule _economy;
    private readonly CostSplitter _costs;
    private readonly IRequirementsModule _requirements;
    private readonly IWorldModule _world;
    private readonly IEventScheduler _scheduler;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private readonly IReadOnlyList<ITechnologyUnlockTargets> _unlockTargets;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchQueueService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    /// <param name="planets">The bodies module, for the level of the science centre.</param>
    /// <param name="resources">The balance of a body, for the cost of the research.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="world">The world, for the sector of the body.</param>
    /// <param name="scheduler">The scheduler of the events.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="unlockTargets">The subjects of the other modules a technology may open, for their names.</param>
    public ResearchQueueService(
        ResearchDbContext dbContext,
        ResearchUnitOfWork unitOfWork,
        TechnologyCatalogCache catalogCache,
        IPlanetsModule planets,
        IPlanetResources resources,
        IEconomyModule economy,
        CostSplitter costs,
        IRequirementsModule requirements,
        IWorldModule world,
        IEventScheduler scheduler,
        IGameConfig config,
        IGameClock clock,
        IEnumerable<ITechnologyUnlockTargets> unlockTargets)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unlockTargets);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _planets = planets;
        _resources = resources;
        _economy = economy;
        _costs = costs;
        _requirements = requirements;
        _world = world;
        _scheduler = scheduler;
        _config = config;
        _clock = clock;
        _unlockTargets = [.. unlockTargets];
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

                // What is due is completed first, so a finished research never holds a place.
                await CompleteDueAsync(playerId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
                TechnologyInfo technology = catalog.FindTechnology(technologyId)
                    ?? throw new GameException(ResearchErrorCodes.TechnologyUnknown, "The technology does not exist.");

                TechnologyLevelInfo level = technology.Levels.GetValueOrDefault(targetLevel)
                    ?? throw new GameException(ResearchErrorCodes.LevelUnknown, "The level of the technology does not exist.");

                // An archived technology keeps working for its owners and its queue finishes, but
                // no new research of it starts (task 00023, decision 5).
                if (!catalog.IsOffered(technology))
                {
                    throw new GameException(ResearchErrorCodes.TechnologyArchived, "The technology is archived.");
                }

                ResearchSettings settings = ResearchSettings.From(_config);

                if (queue.Count >= Slots(settings, state))
                {
                    throw new GameException(ResearchErrorCodes.QueueFull, "The queue of the researches has no free place.");
                }

                await CheckAvailableAsync(playerId, planetId, technology, targetLevel, queue, cancellationToken).ConfigureAwait(false);

                // The resources are paid when the record enters the queue and returned in
                // full when it leaves it. The costs of a civilization are paid from its
                // balance and the rest from the balance of the body (task 00015).
                SplitCosts split = await _costs.SplitAsync(level.Costs, cancellationToken).ConfigureAwait(false);

                await _resources.TakeAsync(planetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);
                await _economy.TakeAsync(playerId, split.Currencies, cancellationToken).ConfigureAwait(false);

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
                await CompleteDueAsync(playerId, queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);

                ResearchQueueItemEntity cancelled = queue.FirstOrDefault(entry => entry.Id == queueItemId)
                    ?? throw new GameException(ResearchErrorCodes.QueueItemUnknown, "The record of the queue does not exist.");

                bool headRemoved = queue[0].Id == cancelled.Id;

                // The later levels of the same technology go with the record, since their
                // research rests on it; every record returns its whole cost to its own body.
                List<ResearchQueueItemEntity> removed =
                [
                    .. queue.Where(entry => entry.TechnologyId == cancelled.TechnologyId && entry.TargetLevel >= cancelled.TargetLevel),
                ];

                // The costs are read before the records are deleted, since a deleted record
                // loses its cost rows. The bodies are paid back in the order of their
                // identifiers, the order every change of the player locks them in.
                List<(long PlanetId, Dictionary<int, long> Amounts)> refunds =
                [
                    .. removed
                        .GroupBy(item => item.PlanetId)
                        .OrderBy(group => group.Key)
                        .Select(group => (group.Key, group
                            .SelectMany(item => item.CostIds)
                            .GroupBy(cost => cost.ResourceTypeId)
                            .ToDictionary(cost => cost.Key, cost => cost.Sum(entry => entry.Amount)))),
                ];

                foreach (ResearchQueueItemEntity item in removed)
                {
                    queue.Remove(item);
                    _dbContext.ResearchQueueItems.Remove(item);
                }

                Renumber(queue);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                foreach ((long refundPlanetId, Dictionary<int, long> amounts) in refunds)
                {
                    SplitCosts split = await _costs.SplitAsync(amounts, cancellationToken).ConfigureAwait(false);

                    await _resources.GiveAsync(refundPlanetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);
                    await _economy.GiveAsync(playerId, split.Currencies, cancellationToken).ConfigureAwait(false);
                }

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

                return await CompleteDueAsync(playerId, queue, now, cancellationToken).ConfigureAwait(false);
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

    private async Task<int> CompleteDueAsync(
        string playerId,
        List<ResearchQueueItemEntity> queue,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        int completed = 0;

        while (queue.Count > 0)
        {
            ResearchQueueItemEntity head = queue[0];

            if (head.FinishesAt is null)
            {
                // A head that never started (its technology left the catalog and came
                // back, for example) starts now.
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

            DateTimeOffset finishedAt = head.FinishesAt.Value;

            // The bonuses of the player change with the new level, so the balance of
            // every body is brought to the end of the research first, and the moment
            // its storage is full is computed again after the change.
            await _resources.FixPlayerAsync(playerId, finishedAt, cancellationToken).ConfigureAwait(false);
            await LearnAsync(playerId, head, cancellationToken).ConfigureAwait(false);

            queue.RemoveAt(0);
            _dbContext.ResearchQueueItems.Remove(head);
            Renumber(queue);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _resources.FixPlayerAsync(playerId, finishedAt, cancellationToken).ConfigureAwait(false);
            await StartNextAsync(queue, finishedAt, cancellationToken).ConfigureAwait(false);
            completed++;
        }

        return completed;
    }

    private async Task<ResearchQueueStateEntity> LockStateAsync(string playerId, CancellationToken cancellationToken)
    {
        // The row is created without a conflict first, so the first parallel commands of a
        // player wait for one row instead of failing on a duplicate key.
        await _dbContext.Database
            .ExecuteSqlAsync($"INSERT INTO research_queue_state (player_id, extra_queue_slots) VALUES ({playerId}, 0) ON CONFLICT (player_id) DO NOTHING", cancellationToken)
            .ConfigureAwait(false);

        return (await _dbContext.ResearchQueueStates
            .FromSql($"SELECT * FROM research_queue_state WHERE player_id = {playerId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).Single();
    }

    private async Task<List<ResearchQueueItemEntity>> LoadQueueAsync(string playerId, CancellationToken cancellationToken) =>
        [.. await _dbContext.ResearchQueueItems
            .Include(item => item.CostIds)
            .Where(item => item.PlayerId == playerId)
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];

    private async Task<bool> StartNextAsync(List<ResearchQueueItemEntity> queue, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (queue.Count == 0 || queue[0].FinishesAt is not null)
        {
            return false;
        }

        ResearchQueueItemEntity head = queue[0];
        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        TechnologyLevelInfo? level = catalog.FindTechnology(head.TechnologyId)?.Levels.GetValueOrDefault(head.TargetLevel);

        if (level is null)
        {
            return false;
        }

        head.StartsAt = now;
        head.FinishesAt = now.AddSeconds(level.DurationSeconds);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(head.PlanetId, cancellationToken).ConfigureAwait(false);

        // The key of the event names the record, so a repeated start never doubles the event,
        // and an event of a cancelled record finds nothing due and changes nothing.
        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                facts?.SectorId ?? 0,
                CompleteEventType,
                head.FinishesAt.Value,
                JsonSerializer.Serialize(new CompletePayload(head.PlayerId, head.Id)),
                CompleteEventType + ":" + head.Id.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);

        return true;
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
        TechnologyInfo technology,
        int targetLevel,
        IReadOnlyList<ResearchQueueItemEntity> queue,
        CancellationToken cancellationToken)
    {
        int learned = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId && entry.TechnologyId == technology.Id)
            .Select(entry => entry.Level)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (targetLevel <= learned)
        {
            throw new GameException(ResearchErrorCodes.AlreadyLearned, "The player has already learned the level of the technology.");
        }

        int queued = queue.Where(entry => entry.TechnologyId == technology.Id).Select(entry => entry.TargetLevel).DefaultIfEmpty(0).Max();

        if (targetLevel <= queued)
        {
            throw new GameException(ResearchErrorCodes.AlreadyQueued, "The level of the technology is already in the queue.");
        }

        // The levels follow one another: a level is researched after the levels below it are
        // learned or wait in the queue before it.
        if (targetLevel != Math.Max(learned, queued) + 1)
        {
            throw new GameException(ResearchErrorCodes.LevelOutOfOrder, "The level of the technology is not the next one.");
        }

        // The science centre of the chosen body must be of the level the technology needs; a
        // body of another player has none for this player.
        int centerLevel = await _planets.GetBuildingLevelAsync(
            BuildingClasses.ResearchCenter,
            planetId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        if (centerLevel <= 0 || centerLevel < technology.ResearchCenterLevel)
        {
            throw new GameException(
                ResearchErrorCodes.ResearchCenterLevel,
                "The body has no science centre of the level the technology needs.",
                new Dictionary<string, object?>
                {
                    ["required_level"] = technology.ResearchCenterLevel,
                    ["available_level"] = centerLevel,
                });
        }

        // The other requirements of the level live in the common requirements module and are
        // checked by the one check of the bodies module, on the chosen body.
        AvailabilityDto availability = await _planets.CheckRequirementsAsync(
            RequirementSubjectTypes.Technology,
            technology.Id,
            targetLevel,
            planetId,
            playerId,
            cancellationToken).ConfigureAwait(false);

        if (!availability.IsAvailable)
        {
            throw new GameException(
                ResearchErrorCodes.RequirementUnmet,
                "A requirement of the level of the technology is not met.",
                new Dictionary<string, object?>
                {
                    ["unmet_requirements"] = JsonSerializer.SerializeToElement(availability.UnmetRequirements, ResearchJson.Options),
                });
        }
    }

    private int Slots(ResearchSettings settings, ResearchQueueStateEntity? state) =>
        settings.QueueBaseSlots + Math.Clamp(state?.ExtraQueueSlots ?? 0, 0, settings.QueueMaxExtraSlots);

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

        // The research starts from a body the player chooses; without a choice the body with
        // the highest science centre is taken, and a body of another player is never chosen.
        IReadOnlyList<PlanetBuildingLevel> planets = await _planets
            .GetBuildingLevelsAsync(BuildingClasses.ResearchCenter, playerId, cancellationToken)
            .ConfigureAwait(false);

        PlanetBuildingLevel? selected = planetId is null
            ? planets.OrderByDescending(planet => planet.Level).ThenBy(planet => planet.PlanetId).FirstOrDefault()
            : planets.FirstOrDefault(planet => planet.PlanetId == planetId);

        // The requirements of the next level of every technology are read in one query.
        IReadOnlyList<SubjectRequirement> requirements = await _requirements.GetManyAsync(
            RequirementSubjectTypes.Technology,
            [.. catalog.Technologies.Select(technology => new SubjectLevel(technology.Id, Math.Min(learned.GetValueOrDefault(technology.Id) + 1, technology.MaxLevel)))],
            cancellationToken).ConfigureAwait(false);

        Dictionary<string, IReadOnlyDictionary<int, string>> unlockNameKeys = new(StringComparer.Ordinal);

        foreach (ITechnologyUnlockTargets targets in _unlockTargets)
        {
            unlockNameKeys[targets.UnlockKind] = await targets.GetNameKeysAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ResearchTreeSnapshot(
            catalog,
            queue,
            learned,
            Slots(settings, state),
            selected?.PlanetId,
            selected?.Level ?? 0,
            planets,
            requirements,
            await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false),
            _clock.GameNow,
            _clock.Multiplier,
            unlockNameKeys);
    }
}

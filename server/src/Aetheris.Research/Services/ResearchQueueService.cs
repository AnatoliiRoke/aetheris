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
    private readonly PlayerTechnologyLevelsReader _levelsReader;

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
    /// <param name="levelsReader">The levels of the technologies of a player, read once for a request.</param>
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
        IEnumerable<ITechnologyUnlockTargets> unlockTargets,
        PlayerTechnologyLevelsReader levelsReader)
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
        ArgumentNullException.ThrowIfNull(levelsReader);

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
        _levelsReader = levelsReader;
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

                if (queue.Count >= await SlotsAsync(settings, state, playerId, cancellationToken).ConfigureAwait(false))
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

                return await SnapshotAsync(playerId, planetId, null, cancellationToken).ConfigureAwait(false);
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

                // Every body is locked before the balance of the civilization, whatever the number of
                // bodies: the resources go back to the bodies in the order of their identifiers and
                // the currencies go back to the balance of the civilization in one change at the
                // end, so the economy is the last lock of the request and two cancellations never
                // wait for each other in a circle (documentation/tech/09, section 5.3).
                Dictionary<int, long> currencyRefunds = [];

                foreach ((long refundPlanetId, Dictionary<int, long> amounts) in refunds)
                {
                    SplitCosts split = await _costs.SplitAsync(amounts, cancellationToken).ConfigureAwait(false);

                    await _resources.GiveAsync(refundPlanetId, playerId, split.PlanetResources, cancellationToken).ConfigureAwait(false);

                    foreach ((int currencyId, long amount) in split.Currencies)
                    {
                        currencyRefunds[currencyId] = currencyRefunds.GetValueOrDefault(currencyId) + amount;
                    }
                }

                await _economy.GiveAsync(playerId, currencyRefunds, cancellationToken).ConfigureAwait(false);

                if (headRemoved)
                {
                    await StartNextAsync(queue, _clock.GameNow, cancellationToken).ConfigureAwait(false);
                }

                return await SnapshotAsync(playerId, null, null, cancellationToken).ConfigureAwait(false);
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

        DateTimeOffset now = _clock.GameNow;
        List<ResearchQueueItemEntity> queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);
        ResearchQueueItemEntity? head = queue.FirstOrDefault();

        // The lock of the queue and the completion are taken only when the head of the queue has
        // finished or has not started; a read of a queue that waits changes nothing, so it needs
        // neither (task 00036, section 6.3).
        if (head is not null && (head.FinishesAt is null || head.FinishesAt <= now))
        {
            await CompleteDueAsync(playerId, now, cancellationToken).ConfigureAwait(false);
            queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);
        }

        return await SnapshotAsync(playerId, planetId, queue, cancellationToken).ConfigureAwait(false);
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

            // A technology may raise the speed the population grows with and the income of the
            // credits, so the population and the credits of the civilization are counted to the end
            // of the research with the bonuses as they were.
            await _economy.FixPlayerAsync(playerId, finishedAt, cancellationToken).ConfigureAwait(false);
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

        // The queue of the researches is one for the whole civilization, so the speed of the
        // research is the one of the civilization: the largest level of its bodies.
        IReadOnlyDictionary<int, int> learnedLevels = await _levelsReader.GetAsync(head.PlayerId, cancellationToken).ConfigureAwait(false);
        head.StartsAt = now;

        // The science centres of the owner speed the research up on every body of the owner, so
        // their share divides the time of the research.
        Aetheris.Planets.OwnerBuildingBonus ownerBonus =
            await _planets.GetOwnerBuildingBonusAsync(head.PlayerId, cancellationToken).ConfigureAwait(false);

        decimal researchTime = ResearchTime.Multiplier(catalog, learnedLevels) / (1m + ownerBonus.ResearchSpeedBonus);

        head.FinishesAt = now.AddSeconds(ResearchTime.Scale(level.DurationSeconds, researchTime));
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
        // A technology is researched from a body, so the level belongs to that body: every body
        // has its own level of a technology and starts from nothing.
        PlayerTechnologyEntity? learned = await _dbContext.PlayerTechnologies
            .SingleOrDefaultAsync(
                entry => entry.PlayerId == playerId && entry.PlanetId == item.PlanetId && entry.TechnologyId == item.TechnologyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (learned is null)
        {
            _dbContext.PlayerTechnologies.Add(new PlayerTechnologyEntity
            {
                PlayerId = playerId,
                PlanetId = item.PlanetId,
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
        // The levels of a technology belong to the body that researched it, so the level of the
        // body is the one the next level of the body follows.
        int learned = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId && entry.PlanetId == planetId && entry.TechnologyId == technology.Id)
            .Select(entry => entry.Level)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (targetLevel <= learned)
        {
            throw new GameException(ResearchErrorCodes.AlreadyLearned, "The player has already learned the level of the technology.");
        }

        // The queue of the researches is one for the whole civilization, but the levels of a
        // technology follow the body they belong to, so only the records of that body block it.
        int queued = queue
            .Where(entry => entry.TechnologyId == technology.Id && entry.PlanetId == planetId)
            .Select(entry => entry.TargetLevel)
            .DefaultIfEmpty(0)
            .Max();

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
    /// Gets the places of the queue of the researches of a player: the base places, the paid ones
    /// up to their limit and the places the science centres of the player add.
    /// </summary>
    /// <param name="settings">The parameters.</param>
    /// <param name="state">The state row of the queue.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The places.</returns>
    private async Task<int> SlotsAsync(
        ResearchSettings settings,
        ResearchQueueStateEntity? state,
        string playerId,
        CancellationToken cancellationToken)
    {
        Aetheris.Planets.OwnerBuildingBonus bonus =
            await _planets.GetOwnerBuildingBonusAsync(playerId, cancellationToken).ConfigureAwait(false);

        return Slots(settings, state) + (int)decimal.Floor(bonus.ResearchQueueSlots);
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

    private async Task<ResearchTreeSnapshot> SnapshotAsync(
        string playerId,
        long? planetId,
        List<ResearchQueueItemEntity>? knownQueue,
        CancellationToken cancellationToken)
    {
        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        List<ResearchQueueItemEntity> queue = knownQueue ?? await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);

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

        // A technology is researched from a body, so the tree shows the levels of the chosen
        // body: every body has its own level of a technology and starts from nothing.
        IReadOnlyDictionary<int, int> learned = selected is null
            ? new Dictionary<int, int>()
            : await _levelsReader.GetAsync(playerId, selected.PlanetId, cancellationToken).ConfigureAwait(false);

        // The level of the civilization is the largest level of its bodies, so the subjects a
        // technology opens are open for the whole civilization and the queue of the researches,
        // which is one for the civilization, goes at the speed of the civilization.
        IReadOnlyDictionary<int, int> civilizationLevels = await _levelsReader.GetAsync(playerId, cancellationToken).ConfigureAwait(false);

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

        // The balance of the selected body is the accumulated one of this moment (one read of
        // the body), so the tree can say what a level is short of without a request per level.
        PlanetOverviewDto? selectedPlanet = selected is null
            ? null
            : await _planets.GetPlanetAsync(playerId, selected.PlanetId, cancellationToken).ConfigureAwait(false);
        // Without a selected body of the player there is no balance, and the tree says nothing
        // about a shortage.
        Dictionary<int, long>? balance = selectedPlanet?.Resources.ToDictionary(resource => resource.ResourceTypeId, resource => resource.Amount);

        // A currency of a civilization does not lie on a body, so the balance of the body says
        // nothing about it. The currencies are paid from the balance of the civilization, and
        // the tree must check the cost against the same balance (task 00015, decision 5). The
        // balances are read only when the tree has a body to be short of, so a player without
        // one costs nothing.
        HashSet<int>? currencyResourceIds = null;
        IReadOnlyDictionary<int, long>? currencyBalances = null;

        if (balance is not null)
        {
            ResourceCatalog resourceCatalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
            HashSet<int> currencies = [.. resourceCatalog.Resources.Where(resource => resource.IsCivilizationScope).Select(resource => resource.Id)];
            currencyResourceIds = currencies;
            currencyBalances = await _economy.GetBalancesAsync(playerId, cancellationToken).ConfigureAwait(false);
        }

        // The science centres of the owner add their places to the queue and take their share off
        // the time of the research of every body of the owner.
        // The levels of the science centres of every body are read above, so the bonus comes from
        // the cached catalog without another read.
        Aetheris.Planets.OwnerBuildingBonus ownerBonus = await _planets
            .GetResearchCenterBonusAsync(planets.Select(planet => planet.Level).DefaultIfEmpty(0).Max(), cancellationToken)
            .ConfigureAwait(false);

        decimal researchTime = ResearchTime.Multiplier(catalog, civilizationLevels) / (1m + ownerBonus.ResearchSpeedBonus);

        return new ResearchTreeSnapshot(
            catalog,
            queue,
            learned,
            Slots(settings, state) + (int)decimal.Floor(ownerBonus.ResearchQueueSlots),
            selected?.PlanetId,
            selected?.Level ?? 0,
            planets,
            requirements,
            await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false),
            _clock.GameNow,
            _clock.Multiplier,
            unlockNameKeys,
            balance,
            currencyBalances,
            currencyResourceIds,
            selectedPlanet?.Buildings.ToDictionary(building => building.BuildingTypeId, building => building.Level),
            selectedPlanet?.DepositRichness?.Select(deposit => deposit.ResourceTypeId).ToHashSet(),
            selectedPlanet?.ResourceTypes.ToDictionary(resource => resource.Id, resource => resource.NameKey),
            researchTime);
    }
}

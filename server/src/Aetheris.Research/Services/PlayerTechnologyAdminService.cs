using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Planets;
using Aetheris.Research.Api;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// The technologies of a player for the admin panel. A level belongs to one body.
/// </summary>
public sealed class PlayerTechnologyAdminService
{
    private readonly ResearchDbContext _dbContext;
    private readonly ResearchUnitOfWork _unitOfWork;
    private readonly ResearchQueueService _queue;
    private readonly TechnologyCatalogCache _catalogCache;
    private readonly IPlanetResources _resources;
    private readonly IEconomyModule _economy;
    private readonly IWorldModule _world;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerTechnologyAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="queue">The queue of the researches.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    /// <param name="resources">The stocks of the bodies, fixed before a level changes the rates.</param>
    /// <param name="economy">The population and the credits, fixed before a level changes the rates.</param>
    /// <param name="world">The world, for the owner of the body.</param>
    /// <param name="clock">The game clock.</param>
    public PlayerTechnologyAdminService(
        ResearchDbContext dbContext,
        ResearchUnitOfWork unitOfWork,
        ResearchQueueService queue,
        TechnologyCatalogCache catalogCache,
        IPlanetResources resources,
        IEconomyModule economy,
        IWorldModule world,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _queue = queue;
        _catalogCache = catalogCache;
        _resources = resources;
        _economy = economy;
        _world = world;
        _clock = clock;
    }

    /// <summary>
    /// Reads the levels of a body and the research queue of the player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels and the queue.</returns>
    public async Task<AdminTechnologyView> ReadAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _clock.GameNow;
        List<ResearchQueueItemEntity> queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);
        ResearchQueueItemEntity? head = queue.FirstOrDefault();

        if (head is not null && (head.FinishesAt is null || head.FinishesAt <= now))
        {
            await _queue.CompleteDueAsync(playerId, now, cancellationToken).ConfigureAwait(false);
            queue = await LoadQueueAsync(playerId, cancellationToken).ConfigureAwait(false);
        }

        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> levels = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId && entry.PlanetId == planetId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);

        List<AdminTechnologyRow> technologies =
        [
            .. catalog.Technologies
                .OrderBy(technology => catalog.FindBranch(technology.BranchId)?.SortOrder ?? int.MaxValue)
                .ThenBy(technology => technology.SortOrder)
                .ThenBy(technology => technology.Id)
                .Select(technology =>
                {
                    TechnologyBranchInfo? branch = catalog.FindBranch(technology.BranchId);

                    return new AdminTechnologyRow(
                        technology.Id,
                        technology.NameKey,
                        technology.BranchId,
                        branch?.NameKey ?? string.Empty,
                        levels.GetValueOrDefault(technology.Id),
                        technology.MaxLevel);
                }),
        ];

        List<AdminResearchQueueRow> records =
        [
            .. queue.Select(item => new AdminResearchQueueRow(
                item.Id,
                item.PlanetId,
                item.TechnologyId,
                catalog.FindTechnology(item.TechnologyId)?.NameKey ?? string.Empty,
                item.TargetLevel,
                item.Position,
                item.FinishesAt)),
        ];

        return new AdminTechnologyView(technologies, records);
    }

    /// <summary>
    /// Sets the level of a technology on a body. Level zero removes the row. A queued technology is refused.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="technologyId">The technology.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level before the change and the level after it.</returns>
    public Task<(long Before, long After)> SetLevelAsync(
        string playerId,
        long planetId,
        int technologyId,
        int level,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (level < 0)
        {
            throw new GameException(PlayerAdminCodes.LevelOutOfRange, "The level of a technology cannot be negative.");
        }

        DateTimeOffset now = _clock.GameNow;

        return _unitOfWork.RunAsync(
            async () =>
            {
                await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                await _queue.CompleteDueAsync(playerId, now, cancellationToken).ConfigureAwait(false);

                TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
                TechnologyInfo technology = catalog.FindTechnology(technologyId)
                    ?? throw new GameException(GameErrorCodes.NotFound, "The technology does not exist.");

                if (level > technology.MaxLevel)
                {
                    throw new GameException(PlayerAdminCodes.LevelOutOfRange, "The level is outside the range of the technology.");
                }

                bool queued = await _dbContext.ResearchQueueItems.AnyAsync(
                    item => item.PlayerId == playerId && item.PlanetId == planetId && item.TechnologyId == technologyId,
                    cancellationToken).ConfigureAwait(false);

                if (queued)
                {
                    throw new GameException(PlayerAdminCodes.QueueBlocks, "A research of the technology is in the queue.");
                }

                // The same order as the end of a research: the stocks first, then the economy,
                // so the old rates apply until now.
                await _resources.FixPlayerAsync(playerId, now, cancellationToken).ConfigureAwait(false);
                await _economy.FixPlayerAsync(playerId, now, cancellationToken).ConfigureAwait(false);

                PlayerTechnologyEntity? learned = await _dbContext.PlayerTechnologies
                    .SingleOrDefaultAsync(
                        entry => entry.PlayerId == playerId && entry.PlanetId == planetId && entry.TechnologyId == technologyId,
                        cancellationToken)
                    .ConfigureAwait(false);

                int before = learned?.Level ?? 0;

                if (level == 0)
                {
                    if (learned is not null)
                    {
                        _dbContext.PlayerTechnologies.Remove(learned);
                    }
                }
                else if (learned is null)
                {
                    _dbContext.PlayerTechnologies.Add(new PlayerTechnologyEntity
                    {
                        PlayerId = playerId,
                        PlanetId = planetId,
                        TechnologyId = technologyId,
                        Level = level,
                    });
                }
                else
                {
                    learned.Level = level;
                }

                return ((long)before, (long)level);
            },
            cancellationToken);
    }

    private async Task<List<ResearchQueueItemEntity>> LoadQueueAsync(string playerId, CancellationToken cancellationToken) =>
        await _dbContext.ResearchQueueItems.AsNoTracking()
            .Where(item => item.PlayerId == playerId)
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task RequireOwnerAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(GameErrorCodes.NotFound, "The body is not a colony of the player.");
        }
    }
}

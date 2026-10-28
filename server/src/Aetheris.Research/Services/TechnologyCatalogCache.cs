using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// Keeps the catalog of the technologies in the memory of the server and reads it again when
/// it is older than <c>research.catalog.cache_seconds</c>, so a change of the tables takes
/// effect without a restart or a new release.
/// </summary>
public sealed class TechnologyCatalogCache
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private volatile CachedCatalog? _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="TechnologyCatalogCache"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The clock.</param>
    public TechnologyCatalogCache(IGameConfig config, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Forgets the catalog, so the next read loads it again.
    /// </summary>
    public void Invalidate() => _cached = null;

    /// <summary>
    /// Gets the catalog, loading it when it is missing or old.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    public async Task<TechnologyCatalog> GetAsync(ResearchDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        TimeSpan lifetime = ResearchSettings.From(_config).CatalogCacheDuration;
        CachedCatalog? cached = _cached;

        if (cached is not null && _clock.UtcNow - cached.LoadedAt < lifetime)
        {
            return cached.Catalog;
        }

        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            cached = _cached;

            if (cached is not null && _clock.UtcNow - cached.LoadedAt < lifetime)
            {
                return cached.Catalog;
            }

            TechnologyCatalog catalog = await LoadAsync(dbContext, cancellationToken).ConfigureAwait(false);
            _cached = new CachedCatalog(catalog, _clock.UtcNow);

            return catalog;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static async Task<TechnologyCatalog> LoadAsync(ResearchDbContext dbContext, CancellationToken cancellationToken)
    {
        List<TechnologyBranchInfo> branches = await dbContext.TechnologyBranches.AsNoTracking()
            .OrderBy(branch => branch.SortOrder)
            .ThenBy(branch => branch.Id)
            .Select(branch => new TechnologyBranchInfo(branch.Id, branch.Code, branch.NameKey, branch.SortOrder, branch.IsArchived))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var technologies = await dbContext.Technologies.AsNoTracking()
            .Include(technology => technology.LevelIds).ThenInclude(level => level.CostIds)
            .Include(technology => technology.LevelIds).ThenInclude(level => level.EffectIds)
            .AsSplitQuery()
            .OrderBy(technology => technology.BranchId)
            .ThenBy(technology => technology.SortOrder)
            .ThenBy(technology => technology.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TechnologyCatalog(
        [
            .. technologies.Select(technology => new TechnologyInfo(
                technology.Id,
                technology.Code,
                technology.NameKey,
                technology.DescriptionKey,
                technology.BranchId,
                technology.SortOrder,
                technology.MaxLevel,
                technology.ResearchCenterLevel,
                technology.LevelIds.ToDictionary(
                    level => level.Level,
                    level => new TechnologyLevelInfo(
                        level.Level,
                        level.DurationSeconds,
                        level.CostIds.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount),
                        [.. level.EffectIds.Select(effect => new TechnologyEffectInfo(
                            effect.EffectType,
                            effect.UnlockKind,
                            effect.UnlockTargetId,
                            effect.Value))])),
                technology.IsArchived)),
        ],
        branches);
    }

    private sealed record CachedCatalog(TechnologyCatalog Catalog, DateTimeOffset LoadedAt);
}

using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Keeps the resource catalog in the memory of the server and reads it again when it is older
/// than <c>world.catalog.cache_seconds</c>. Almost every read of a player needs the catalog (the
/// currencies, the weights, the names), so one request no longer reads the two tables again for
/// every service that asks (task 00036, section 6.3).
/// </summary>
/// <remarks>
/// A change of the resources in the admin panel and the seed of the reference data drop the
/// catalog at once, so a change takes effect without a restart.
/// </remarks>
public sealed class ResourceCatalogCache
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private volatile CachedCatalog? _cached;
    private long _generation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceCatalogCache"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The clock.</param>
    public ResourceCatalogCache(IGameConfig config, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Forgets the catalog, so the next read loads it again. A read that was loading while the
    /// catalog was forgotten does not keep what it loaded, since it may hold the rows before the
    /// change.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _cached = null;
    }

    /// <summary>
    /// Gets the catalog, loading it when it is missing or old.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    public async Task<ResourceCatalog> GetAsync(WorldDbContext dbContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        TimeSpan lifetime = WorldSettings.From(_config).CatalogCacheDuration;
        CachedCatalog? cached = _cached;

        if (cached is not null && _clock.UtcNow - cached.LoadedAt < lifetime)
        {
            return cached.Catalog;
        }

        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            long generation = Interlocked.Read(ref _generation);
            cached = _cached;

            if (cached is not null && _clock.UtcNow - cached.LoadedAt < lifetime)
            {
                return cached.Catalog;
            }

            ResourceCatalog catalog = await LoadAsync(dbContext, cancellationToken).ConfigureAwait(false);

            if (generation == Interlocked.Read(ref _generation))
            {
                _cached = new CachedCatalog(catalog, _clock.UtcNow);
            }

            return catalog;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static async Task<ResourceCatalog> LoadAsync(WorldDbContext dbContext, CancellationToken cancellationToken)
    {
        List<ResourceGroupFact> groups = await dbContext.ResourceGroups.AsNoTracking()
            .OrderBy(group => group.Id)
            .Select(group => new ResourceGroupFact(group.Id, group.Code, group.NameKey, group.IsArchived, group.IsBalance))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ResourceTypeFact> resources = await dbContext.ResourceTypes.AsNoTracking()
            .OrderBy(resource => resource.Id)
            .Select(resource => new ResourceTypeFact(resource.Id, resource.Code, resource.NameKey, resource.ResourceGroupId, resource.IsArchived, resource.Scope, resource.EarnsPopulationIncome, resource.Weight, resource.AllowsDebt, resource.ImageKey, resource.PaysPortalDelivery, resource.PaidByPopulation))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new ResourceCatalog(groups, resources);
    }

    private sealed record CachedCatalog(ResourceCatalog Catalog, DateTimeOffset LoadedAt);
}

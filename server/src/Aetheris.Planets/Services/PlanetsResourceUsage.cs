using Aetheris.Planets.Data;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// Tells the world whether the stocks of the bodies and the costs of the buildings name a
/// resource (task 00015).
/// </summary>
/// <remarks>
/// The scope of a resource is the world's to change, but the stocks of the bodies and the rows of
/// the cost of a building were written for it, so the world asks before it changes the scope.
/// </remarks>
public sealed class PlanetsResourceUsage : IResourceUsage
{
    private readonly PlanetsDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetsResourceUsage"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    public PlanetsResourceUsage(PlanetsDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> IsUsedAsync(int resourceTypeId, CancellationToken cancellationToken = default) =>
        await _dbContext.PlanetResources.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false)
        || await _dbContext.BuildingLevelCosts.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false)
        || await _dbContext.BuildingLevelEffects.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false)
        || await _dbContext.QueueCosts.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false);
}

using Aetheris.Research.Data;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// Tells the world whether the costs of the technologies name a resource (task 00015).
/// </summary>
/// <remarks>
/// The rows of the cost of a technology and of its queue were written for the scope the resource
/// had, so the world asks before it changes the scope.
/// </remarks>
public sealed class ResearchResourceUsage : IResourceUsage
{
    private readonly ResearchDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchResourceUsage"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    public ResearchResourceUsage(ResearchDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> IsUsedAsync(int resourceTypeId, CancellationToken cancellationToken = default) =>
        await _dbContext.TechnologyLevelCosts.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false)
        || await _dbContext.ResearchQueueCosts.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false);
}

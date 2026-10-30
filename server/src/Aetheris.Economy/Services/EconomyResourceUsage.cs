using Aetheris.Economy.Data;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Economy.Services;

/// <summary>
/// Tells the world whether a balance of a civilization names a resource (task 00015).
/// </summary>
/// <remarks>
/// A currency that a civilization holds keeps its scope, so the world asks before it lets an
/// administrator change it.
/// </remarks>
public sealed class EconomyResourceUsage : IResourceUsage
{
    private readonly EconomyDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyResourceUsage"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    public EconomyResourceUsage(EconomyDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<bool> IsUsedAsync(int resourceTypeId, CancellationToken cancellationToken = default) =>
        _dbContext.CivilizationBalances.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken);
}

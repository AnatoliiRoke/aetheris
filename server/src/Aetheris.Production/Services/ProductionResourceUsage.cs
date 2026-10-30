using Aetheris.Production.Data;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// Tells the world whether the recipes and the batches of the production name a resource
/// (task 00015).
/// </summary>
/// <remarks>
/// The ingredients of a recipe and the cost of a batch were written for the scope the resource
/// had, so the world asks before it changes the scope.
/// </remarks>
public sealed class ProductionResourceUsage : IResourceUsage
{
    private readonly ProductionDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionResourceUsage"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    public ProductionResourceUsage(ProductionDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> IsUsedAsync(int resourceTypeId, CancellationToken cancellationToken = default) =>
        await _dbContext.Recipes.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false)
        || await _dbContext.RecipeIngredients.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false)
        || await _dbContext.ProductionOrderCosts.AsNoTracking().AnyAsync(row => row.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false);
}

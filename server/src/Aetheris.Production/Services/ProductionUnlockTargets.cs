using Aetheris.Production.Data;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research.Services;

namespace Aetheris.Production.Services;

/// <summary>
/// The recipes a technology may open with its effect «opens» (task 00023, decision 7): the
/// technology closes the recipe through the common requirements until its level is learned.
/// </summary>
public sealed class ProductionUnlockTargets : ITechnologyUnlockTargets
{
    private readonly ProductionDbContext _dbContext;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionUnlockTargets"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="requirements">The common requirements module.</param>
    public ProductionUnlockTargets(
        ProductionDbContext dbContext,
        ProductionCatalogCache catalogCache,
        IRequirementsModule requirements)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(requirements);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
        _requirements = requirements;
    }

    /// <inheritdoc />
    public string UnlockKind => TechnologyUnlockKinds.Recipe;

    /// <inheritdoc />
    public string RequirementSubjectType => RequirementSubjectTypes.Recipe;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, string>> GetNameKeysAsync(CancellationToken cancellationToken)
    {
        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        return catalog.Recipes.ToDictionary(recipe => recipe.Id, recipe => recipe.NameKey);
    }

    /// <inheritdoc />
    public void InvalidateCatalog() => _catalogCache.Invalidate();
}

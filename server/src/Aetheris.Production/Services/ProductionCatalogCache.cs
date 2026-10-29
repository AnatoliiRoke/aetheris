using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// Keeps the catalog of the production in the memory of the server and reads it again when
/// it is older than <c>production.catalog.cache_seconds</c>, so a change of the tables takes
/// effect without a restart or a new release.
/// </summary>
public sealed class ProductionCatalogCache
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private volatile CachedCatalog? _cached;
    private long _generation;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionCatalogCache"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The clock.</param>
    public ProductionCatalogCache(IGameConfig config, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _config = config;
        _clock = clock;
    }

    /// <summary>
    /// Forgets the catalog, so the next read loads it again. A read that is loading the catalog
    /// at this moment does not keep what it loaded (documentation/tech/09, section 10a.6).
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
    /// <param name="requirements">The common requirements module, for the requirements of the recipes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    public async Task<ProductionCatalog> GetAsync(
        ProductionDbContext dbContext,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(requirements);

        TimeSpan lifetime = ProductionSettings.From(_config).CatalogCacheDuration;
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

            long generation = Interlocked.Read(ref _generation);
            ProductionCatalog catalog = await LoadAsync(dbContext, requirements, cancellationToken).ConfigureAwait(false);

            // A change committed while the tables were read leaves the loaded copy behind, so it is
            // used for this read only and the next read loads the tables again.
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

    private static async Task<ProductionCatalog> LoadAsync(
        ProductionDbContext dbContext,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        List<PartGroupEntity> groups = await dbContext.PartGroups.AsNoTracking()
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<PartTypeEntity> parts = await dbContext.PartTypes.AsNoTracking()
            .Include(part => part.AttributeIds)
            .OrderBy(part => part.SortOrder)
            .ThenBy(part => part.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<RecipeEntity> recipes = await dbContext.Recipes.AsNoTracking()
            .Include(recipe => recipe.IngredientIds)
            .OrderBy(recipe => recipe.SortOrder)
            .ThenBy(recipe => recipe.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The requirements live in the common requirements module, so the catalog reads them
        // through its contract and never through the tables of that module.
        IReadOnlyList<SubjectRequirement> subjectRequirements = await requirements.GetManyAsync(
            RequirementSubjectTypes.Recipe,
            [.. recipes.Select(recipe => new SubjectLevel(recipe.Id, 1))],
            cancellationToken).ConfigureAwait(false);

        List<ProductionRequirementInfo> requirementInfos =
        [
            .. subjectRequirements.Select(subjectRequirement => new ProductionRequirementInfo(
                subjectRequirement.Requirement.Id,
                subjectRequirement.SubjectId,
                RequirementSubjectTypes.Recipe,
                subjectRequirement.Requirement.RequirementType,
                subjectRequirement.Requirement.TargetId,
                subjectRequirement.Requirement.TargetLevel)),
        ];

        return new ProductionCatalog(
            [
                .. groups.Select(group => new PartGroupInfo(
                    group.Id,
                    group.Code,
                    group.NameKey,
                    group.DescriptionKey,
                    group.ImageKey,
                    group.SortOrder,
                    group.IsArchived)),
            ],
            [
                .. parts.Select(part => new PartTypeInfo(
                    part.Id,
                    part.Code,
                    part.NameKey,
                    part.DescriptionKey,
                    part.ImageKey,
                    part.PartGroupId,
                    part.MarkLevel,
                    part.SortOrder,
                    part.IsArchived,
                    part.AttributeIds.ToDictionary(
                        attribute => attribute.AttributeKey,
                        attribute => new PartAttributeInfo(
                            attribute.AttributeKey,
                            attribute.NumberValue,
                            attribute.CodeValue,
                            attribute.TextValue)))),
            ],
            [
                .. recipes.Select(recipe => new RecipeInfo(
                    recipe.Id,
                    recipe.Code,
                    recipe.NameKey,
                    recipe.DescriptionKey,
                    recipe.ImageKey,
                    recipe.BuildingTypeId,
                    recipe.PartTypeId,
                    recipe.ResourceTypeId,
                    recipe.OutputQuantity,
                    recipe.DurationSeconds,
                    recipe.SortOrder,
                    recipe.IsArchived,
                    [
                        .. recipe.IngredientIds.Select(ingredient => new IngredientInfo(
                            ingredient.ResourceTypeId,
                            ingredient.PartTypeId,
                            ingredient.Amount)),
                    ])),
            ],
            requirementInfos);
    }

    private sealed record CachedCatalog(ProductionCatalog Catalog, DateTimeOffset LoadedAt);
}

using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Services;

/// <summary>
/// Keeps the catalog of the processes in the memory of the server and reads it again when it
/// is older than <c>processes.catalog.cache_seconds</c>, so a change of the tables takes
/// effect without a restart or a new release.
/// </summary>
public sealed class ProcessCatalogCache
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private volatile CachedCatalog? _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessCatalogCache"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The clock.</param>
    public ProcessCatalogCache(IGameConfig config, IGameClock clock)
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
    /// <param name="requirements">The common requirements module, for the requirements of the recipes.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    public async Task<ProcessCatalog> GetAsync(
        ProcessesDbContext dbContext,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(requirements);

        TimeSpan lifetime = ProcessesSettings.From(_config).CatalogCacheDuration;
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

            ProcessCatalog catalog = await LoadAsync(dbContext, requirements, cancellationToken).ConfigureAwait(false);
            _cached = new CachedCatalog(catalog, _clock.UtcNow);

            return catalog;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static async Task<ProcessCatalog> LoadAsync(
        ProcessesDbContext dbContext,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        List<ProcessTypeEntity> types = await dbContext.ProcessTypes.AsNoTracking()
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ProcessRecipeEntity> recipes = await dbContext.ProcessRecipes.AsNoTracking()
            .Include(recipe => recipe.IngredientIds)
            .OrderBy(recipe => recipe.SortOrder)
            .ThenBy(recipe => recipe.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ProcessParamEntity> parameters = await dbContext.ProcessParams.AsNoTracking()
            .OrderBy(parameter => parameter.SortOrder)
            .ThenBy(parameter => parameter.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The requirements live in the common requirements module, so the catalog reads them
        // through its contract and never through the tables of that module.
        IReadOnlyList<SubjectRequirement> subjectRequirements = await requirements.GetManyAsync(
            RequirementSubjectTypes.ProcessRecipe,
            [.. recipes.Select(recipe => new SubjectLevel(recipe.Id, 1))],
            cancellationToken).ConfigureAwait(false);

        List<ProcessRequirementInfo> requirementInfos =
        [
            .. subjectRequirements.Select(subjectRequirement => new ProcessRequirementInfo(
                subjectRequirement.Requirement.Id,
                subjectRequirement.SubjectId,
                subjectRequirement.Requirement.RequirementType,
                subjectRequirement.Requirement.TargetId,
                subjectRequirement.Requirement.TargetLevel)),
        ];

        return new ProcessCatalog(
            [
                .. types.Select(type => new ProcessTypeInfo(
                    type.Id,
                    type.Code,
                    type.NameKey,
                    type.DescriptionKey,
                    type.ImageKey,
                    type.HandlerCode,
                    type.RefundsInput,
                    type.IsArchived,
                    type.SortOrder))
            ],
            [
                .. recipes.Select(recipe => new ProcessRecipeInfo(
                    recipe.Id,
                    recipe.Code,
                    recipe.NameKey,
                    recipe.DescriptionKey,
                    recipe.ImageKey,
                    recipe.ProcessTypeId,
                    recipe.BuildingTypeId,
                    recipe.SourceGroupId,
                    recipe.TargetGroupId,
                    recipe.DurationSeconds,
                    recipe.ShipStub,
                    recipe.IsArchived,
                    recipe.SortOrder,
                    [
                        .. recipe.IngredientIds.Select(ingredient => new ProcessIngredientInfo(
                            ingredient.ResourceTypeId,
                            ingredient.PartTypeId,
                            ingredient.Amount))
                    ]))
            ],
            [
                .. parameters.Select(parameter => new ProcessParamInfo(
                    parameter.Id,
                    parameter.Code,
                    parameter.NameKey,
                    parameter.DescriptionKey,
                    parameter.ProcessTypeId,
                    parameter.ParamKind,
                    parameter.LevelSource,
                    parameter.LevelTargetId,
                    parameter.Level,
                    parameter.Value,
                    parameter.ValueMax,
                    parameter.IsArchived,
                    parameter.SortOrder))
            ],
            requirementInfos);
    }

    private sealed record CachedCatalog(ProcessCatalog Catalog, DateTimeOffset LoadedAt);
}

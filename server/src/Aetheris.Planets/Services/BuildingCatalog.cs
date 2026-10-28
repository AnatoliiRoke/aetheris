using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The building tables, the requirements and the resource catalog as they are in memory.
/// </summary>
/// <param name="Types">The building types in the order of the lists.</param>
/// <param name="Requirements">The requirements of every subject.</param>
/// <param name="Resources">The resource types.</param>
/// <param name="Groups">The resource groups.</param>
public sealed record BuildingCatalog(
    IReadOnlyList<BuildingTypeInfo> Types,
    IReadOnlyList<RequirementInfo> Requirements,
    IReadOnlyList<ResourceTypeFact> Resources,
    IReadOnlyList<ResourceGroupFact> Groups)
{
    private readonly Dictionary<int, BuildingTypeInfo> _typesById = Types.ToDictionary(type => type.Id);
    private readonly Dictionary<int, ResourceTypeFact> _resourcesById = Resources.ToDictionary(resource => resource.Id);

    /// <summary>
    /// Finds a building type.
    /// </summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <returns>The type, or <see langword="null"/>.</returns>
    public BuildingTypeInfo? FindType(int buildingTypeId) => _typesById.GetValueOrDefault(buildingTypeId);

    /// <summary>
    /// Finds the building type of a class: what a building is for (task 00012).
    /// </summary>
    /// <param name="buildingClass">The class, one of <see cref="BuildingClasses"/>.</param>
    /// <returns>The type, or <see langword="null"/> when no type has the class.</returns>
    public BuildingTypeInfo? FindByClass(string buildingClass)
    {
        ArgumentNullException.ThrowIfNull(buildingClass);

        return Types.FirstOrDefault(type => string.Equals(type.BuildingClass, buildingClass, StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the group of a resource.
    /// </summary>
    /// <param name="resourceTypeId">The resource.</param>
    /// <returns>The group, or <see langword="null"/> for an unknown resource.</returns>
    public int? GroupOf(int resourceTypeId) => _resourcesById.TryGetValue(resourceTypeId, out ResourceTypeFact? resource) ? resource.ResourceGroupId : null;

    /// <summary>
    /// Gets the requirements of a subject that apply to a level.
    /// </summary>
    /// <param name="subjectType">The subject type.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <returns>The requirements.</returns>
    public IEnumerable<RequirementInfo> RequirementsOf(string subjectType, int subjectId, int level) =>
        Requirements.Where(requirement => requirement.SubjectId == subjectId
            && requirement.SubjectLevel <= level
            && string.Equals(requirement.SubjectType, subjectType, StringComparison.Ordinal));
}

/// <summary>A building type with its levels.</summary>
/// <param name="Id">The type.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="BuildingClass">The class of the type: what the building is for.</param>
/// <param name="Levels">The levels by number.</param>
public sealed record BuildingTypeInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    int SortOrder,
    string BuildingClass,
    IReadOnlyDictionary<int, BuildingLevelInfo> Levels)
{
    /// <summary>
    /// Gets a value indicating whether a building of this type limits the levels of the others;
    /// such a building is not limited by them itself.
    /// </summary>
    public bool CapsLevels { get; } = Levels.Values.Any(level => level.Effects.Any(effect => effect.EffectType == BuildingEffectTypes.LevelCap));
}

/// <summary>A level of a building type.</summary>
/// <param name="Level">The level.</param>
/// <param name="DurationSeconds">The time to build it, in game seconds.</param>
/// <param name="Costs">The costs by resource.</param>
/// <param name="Effects">The effects of a building of this level.</param>
public sealed record BuildingLevelInfo(
    int Level,
    long DurationSeconds,
    IReadOnlyDictionary<int, long> Costs,
    IReadOnlyList<BuildingEffectInfo> Effects);

/// <summary>An effect of a building level.</summary>
/// <param name="EffectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
/// <param name="ResourceTypeId">The resource of a production effect.</param>
/// <param name="ResourceGroupId">The resource group of a storage effect.</param>
/// <param name="Value">The value at the level.</param>
public sealed record BuildingEffectInfo(string EffectType, int? ResourceTypeId, int? ResourceGroupId, long Value);

/// <summary>A requirement of a subject.</summary>
/// <param name="Id">The requirement.</param>
/// <param name="SubjectType">The subject type.</param>
/// <param name="SubjectId">The subject.</param>
/// <param name="SubjectLevel">The first level of the subject it applies to.</param>
/// <param name="RequirementType">The requirement type.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The required level of the target.</param>
public sealed record RequirementInfo(
    int Id,
    string SubjectType,
    int SubjectId,
    int SubjectLevel,
    string RequirementType,
    int TargetId,
    int TargetLevel);

/// <summary>
/// Keeps the building catalog in the memory of the server and reads it again when it is older
/// than <c>planets.catalog.cache_seconds</c>, so a change of the tables takes effect without a
/// restart or a new release.
/// </summary>
public sealed class BuildingCatalogCache
{
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;
    private volatile CachedCatalog? _cached;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildingCatalogCache"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <param name="clock">The clock.</param>
    public BuildingCatalogCache(IGameConfig config, IGameClock clock)
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
    /// <param name="world">The world, for the resources.</param>
    /// <param name="requirements">The common requirements module, for the requirements of the subjects.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    public async Task<BuildingCatalog> GetAsync(
        PlanetsDbContext dbContext,
        IWorldModule world,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(requirements);

        TimeSpan lifetime = PlanetsSettings.From(_config).CatalogCacheDuration;
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

            BuildingCatalog catalog = await LoadAsync(dbContext, world, requirements, cancellationToken).ConfigureAwait(false);
            _cached = new CachedCatalog(catalog, _clock.UtcNow);

            return catalog;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static async Task<BuildingCatalog> LoadAsync(
        PlanetsDbContext dbContext,
        IWorldModule world,
        IRequirementsModule requirements,
        CancellationToken cancellationToken)
    {
        var types = await dbContext.BuildingTypes.AsNoTracking()
            .Include(type => type.LevelIds).ThenInclude(level => level.CostIds)
            .Include(type => type.LevelIds).ThenInclude(level => level.EffectIds)
            .AsSplitQuery()
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The requirements live in the common requirements module, so the catalog reads them
        // through its contract and never through the tables of that module.
        IReadOnlyList<SubjectRequirement> subjectRequirements = await requirements.GetManyAsync(
            RequirementSubjectTypes.BuildingType,
            [.. types.Select(type => new SubjectLevel(type.Id, HighestLevel(type)))],
            cancellationToken).ConfigureAwait(false);

        List<RequirementInfo> requirementInfos =
        [
            .. subjectRequirements.Select(subjectRequirement => new RequirementInfo(
                subjectRequirement.Requirement.Id,
                RequirementSubjectTypes.BuildingType,
                subjectRequirement.SubjectId,
                subjectRequirement.SubjectLevel,
                subjectRequirement.Requirement.RequirementType,
                subjectRequirement.Requirement.TargetId,
                subjectRequirement.Requirement.TargetLevel)),
        ];

        ResourceCatalog resources = await world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        return new BuildingCatalog(
            [
                .. types.Select(type => new BuildingTypeInfo(
                    type.Id,
                    type.Code,
                    type.NameKey,
                    type.DescriptionKey,
                    type.SortOrder,
                    type.BuildingClass,
                    type.LevelIds.ToDictionary(
                        level => level.Level,
                        level => new BuildingLevelInfo(
                            level.Level,
                            level.DurationSeconds,
                            level.CostIds.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount),
                            [.. level.EffectIds.Select(effect => new BuildingEffectInfo(effect.EffectType, effect.ResourceTypeId, effect.ResourceGroupId, effect.Value))])))),
            ],
            requirementInfos,
            resources.Resources,
            resources.Groups);
    }

    /// <summary>
    /// Gets the largest level of a building type: the requirements of a type are read for it,
    /// so the catalog holds every requirement of the type.
    /// </summary>
    /// <param name="type">The building type.</param>
    /// <returns>The largest level of the type.</returns>
    private static int HighestLevel(BuildingTypeEntity type) =>
        type.LevelIds.Count == 0 ? 1 : type.LevelIds.Max(level => level.Level);

    private sealed record CachedCatalog(BuildingCatalog Catalog, DateTimeOffset LoadedAt);
}

using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
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
/// The building types in the admin panel: the list, the form, the creation, the change and the
/// archive (task 00023, section 4.4). A building has a mandatory category (task 00020).
/// </summary>
/// <remarks>
/// <para>
/// A building is data: the server gives the neutral code, the administrator gives the class, the
/// texts, the picture and the levels with their time, costs, effects and requirements. The row, its
/// texts and its requirements are committed together, and the catalog in memory is dropped after
/// the commit, so a player sees the change at once.
/// </para>
/// <para>
/// The rules of the levels (decision 6): at least one level, the levels from one without a gap,
/// every level with a cost and with requirements; only the first level of the start building may
/// have none. The requirements apply from their level upwards, like in the game.
/// </para>
/// </remarks>
public sealed class PlanetsReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the building types.</summary>
    public const string BuildingKind = "building";

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.Planets.admin-reference-texts.json";

    private const string LabelPrefix = "admin.reference.";

    private const string ClassField = "building_class";

    private const string CategoryField = "category_id";

    private const string SortOrderField = "sort_order";

    private const string StartField = "is_start_building";

    private const string LevelsField = "levels";

    private const string CostsField = "costs";

    private const string EffectsField = "effects";

    private const string RequirementsField = "requirements";

    /// <summary>The place of the buildings in the menu of the reference kinds.</summary>
    private const int BuildingSortOrder = 30;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(PlanetsReferenceAdminService).Assembly, LabelTextsResource);

    private readonly PlanetsDbContext _dbContext;
    private readonly PlanetsUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IWorldModule _world;
    private readonly IRequirementsModule _requirements;
    private readonly ITechnologyLevels _technologies;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetsReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="world">The world, for the resources.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="technologies">The technologies, for the requirements on them.</param>
    /// <param name="catalogCache">The building catalog in memory.</param>
    /// <param name="config">The game configuration.</param>
    public PlanetsReferenceAdminService(
        PlanetsDbContext dbContext,
        PlanetsUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        IWorldModule world,
        IRequirementsModule requirements,
        ITechnologyLevels technologies,
        BuildingCatalogCache catalogCache,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
        _world = world;
        _requirements = requirements;
        _technologies = technologies;
        _catalogCache = catalogCache;
        _config = config;
    }

    /// <summary>
    /// Gets the kind of the building types.
    /// </summary>
    public static AdminReferenceDescriptor BuildingDescriptor { get; } = new(
        BuildingKind,
        PlanetsTextKeys.CodeGroup,
        PlanetsTextKeys.Building,
        LabelPrefix + BuildingKind + ".title",
        BuildingSortOrder,
        "/planets/buildings",
        new AdminReferencePermissions(
            "admin.planets_buildings_read",
            "admin.planets_building_read",
            "admin.planets_building_added",
            "admin.planets_building_updated",
            "admin.planets_building_archived",
            "admin.planets_building_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                ClassField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.building_class",
                InList: true,
                Options: [.. BuildingClassValues.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "building_class." + (value.Length == 0 ? "none" : value)))]),
            new(CategoryField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.category", IsRequired: true, InList: true, ReferenceKind: BuildingCategoryAdminService.CategoryKind),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
            new(StartField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.is_start_building", InList: true),
            new(
                LevelsField,
                AdminReferenceFieldTypes.Levels,
                LabelPrefix + "field.levels",
                IsRequired: true,
                Fields:
                [
                    new("level", AdminReferenceFieldTypes.Number, LabelPrefix + "field.level", IsRequired: true, Minimum: 1),
                    new("duration_seconds", AdminReferenceFieldTypes.Number, LabelPrefix + "field.duration_seconds", IsRequired: true, Minimum: 0),
                    new(
                        CostsField,
                        AdminReferenceFieldTypes.Costs,
                        LabelPrefix + "field.costs",
                        IsRequired: true,
                        Fields:
                        [
                            new("resource_type_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource", IsRequired: true, ReferenceKind: "resource"),
                            new("amount", AdminReferenceFieldTypes.Number, LabelPrefix + "field.amount", IsRequired: true, Minimum: 1),
                        ]),
                    new(
                        EffectsField,
                        AdminReferenceFieldTypes.Effects,
                        LabelPrefix + "field.effects",
                        Fields:
                        [
                            new(
                                "effect_type",
                                AdminReferenceFieldTypes.Select,
                                LabelPrefix + "field.effect_type",
                                IsRequired: true,
                                Options: [.. BuildingEffectTypes.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "building_effect." + value))]),
                            new("resource_type_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource", ReferenceKind: "resource"),
                            new("resource_group_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource_group", ReferenceKind: "resource_group"),
                            new("value", AdminReferenceFieldTypes.Number, LabelPrefix + "field.value", IsRequired: true, Minimum: 0, IsDecimal: true),
                        ]),
                    RequirementAdminFields.Field,
                ]),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [BuildingDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        CheckKind(kind);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        List<BuildingTypeEntity> types = await _dbContext.BuildingTypes.AsNoTracking()
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return AdminReferenceRules.Page(
            types.Select(type => new AdminReferenceRow(
                type.Id,
                type.Code,
                type.NameKey,
                type.ImageKey,
                type.IsArchived,
                type.Version,
                new Dictionary<string, object?>
                {
                    [ClassField] = type.BuildingClass,
                    [CategoryField] = type.CategoryId,
                    [SortOrderField] = type.SortOrder,
                    [StartField] = type.IsStartBuilding,
                })),
            query,
            packs);
    }

    /// <inheritdoc />
    public async Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken)
    {
        CheckKind(kind);

        BuildingTypeEntity? type = await LoadAsync(id, false, cancellationToken).ConfigureAwait(false);

        if (type is null)
        {
            return null;
        }

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        BuildingValues values = await ValuesOfAsync(type, packs, cancellationToken).ConfigureAwait(false);

        return new AdminReferenceRecord(type.Id, type.Code, type.NameKey, type.DescriptionKey, type.ImageKey, type.IsArchived, type.Version, values.ToDictionary());
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> CreateAsync(string kind, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);
        CheckKind(kind);

        BuildingValues values = AdminReferenceJson.Read<BuildingValues>(input.Values);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                List<SubjectRequirement> requirements = await CheckAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(PlanetsTextKeys.CodeGroup, cancellationToken).ConfigureAwait(false);
                BuildingTypeEntity type = new()
                {
                    Code = code,
                    NameKey = PlanetsTextKeys.Name(code),
                    DescriptionKey = PlanetsTextKeys.Description(code),
                };

                Apply(type, values);
                _dbContext.BuildingTypes.Add(type);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _requirements.ReplaceAsync(RequirementSubjectTypes.BuildingType, type.Id, requirements, cancellationToken).ConfigureAwait(false);
                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, type.NameKey, type.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(BuildingKind, type.Id),
                    null,
                    new { code, values = values.Trimmed(_texts) },
                    type.Id,
                    type.Version);
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> UpdateAsync(string kind, int id, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);
        CheckKind(kind);

        BuildingValues values = AdminReferenceJson.Read<BuildingValues>(input.Values);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                await LockAsync(id, cancellationToken).ConfigureAwait(false);
                BuildingTypeEntity type = (await LoadAsync(id, true, cancellationToken).ConfigureAwait(false))!;
                AdminReferenceRules.CheckVersion(type.Version, input.Version);

                List<SubjectRequirement> requirements = await CheckAsync(values, type, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                BuildingValues before = await ValuesOfAsync(type, packs, cancellationToken).ConfigureAwait(false);

                Apply(type, values);
                type.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _requirements.ReplaceAsync(RequirementSubjectTypes.BuildingType, type.Id, requirements, cancellationToken).ConfigureAwait(false);
                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, type.NameKey, type.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(BuildingKind, type.Id),
                    before,
                    values.Trimmed(_texts),
                    type.Id,
                    type.Version);
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> SetArchivedAsync(string kind, int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        CheckKind(kind);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                BuildingTypeEntity type = await LockAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(type.Version, input.Version);

                if (type.IsArchived == isArchived)
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                }

                type.IsArchived = isArchived;
                type.Version++;

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(BuildingKind, type.Id),
                    new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                    new Dictionary<string, object?> { ["is_archived"] = isArchived },
                    type.Id,
                    type.Version);
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    private static void CheckKind(string kind)
    {
        if (!string.Equals(kind, BuildingKind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The bodies module has no reference kind '{kind}'.", nameof(kind));
        }
    }

    /// <summary>
    /// Writes the sent values into the row: the class, the category, the order, the flag, the picture and the
    /// levels. A kept level keeps its row; its costs and effects are written again.
    /// </summary>
    private static void Apply(BuildingTypeEntity type, BuildingValues values)
    {
        type.BuildingClass = values.BuildingClass ?? BuildingClasses.None;
        type.CategoryId = values.CategoryId!.Value;
        type.SortOrder = values.SortOrder ?? 0;
        type.IsStartBuilding = values.IsStartBuilding;
        type.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);

        List<LevelValues> levels = values.Levels!;
        type.LevelIds.RemoveAll(level => !levels.Exists(sent => sent.Level == level.Level));

        foreach (LevelValues sent in levels)
        {
            BuildingLevelEntity? level = type.LevelIds.Find(stored => stored.Level == sent.Level);

            if (level is null)
            {
                level = new BuildingLevelEntity { Level = sent.Level!.Value };
                type.LevelIds.Add(level);
            }

            level.DurationSeconds = sent.DurationSeconds ?? 0;

            // A kept resource of the cost is changed in place, so the unique index of the
            // resource of a level is never broken in between.
            level.CostIds.RemoveAll(cost => !sent.Costs!.Exists(sentCost => sentCost.ResourceTypeId == cost.ResourceTypeId));

            foreach (CostValues cost in sent.Costs!)
            {
                BuildingLevelCostEntity? kept = level.CostIds.Find(stored => stored.ResourceTypeId == cost.ResourceTypeId);

                if (kept is null)
                {
                    level.CostIds.Add(new BuildingLevelCostEntity { ResourceTypeId = cost.ResourceTypeId!.Value, Amount = cost.Amount ?? 0 });
                }
                else
                {
                    kept.Amount = cost.Amount ?? 0;
                }
            }

            level.EffectIds.Clear();
            level.EffectIds.AddRange((sent.Effects ?? []).Select(effect => new BuildingLevelEffectEntity
            {
                EffectType = effect.EffectType!,
                ResourceTypeId = effect.ResourceTypeId,
                ResourceGroupId = effect.ResourceGroupId,
                Value = effect.Value ?? 0,
            }));
        }
    }

    /// <summary>
    /// Checks the values of a building against the rules of the levels and the references, and
    /// builds its requirements.
    /// </summary>
    private async Task<List<SubjectRequirement>> CheckAsync(BuildingValues values, BuildingTypeEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        if (!BuildingClassValues.IsKnown(values.BuildingClass ?? BuildingClasses.None))
        {
            AdminReferenceRules.Fail(failures, ClassField, AdminReferenceErrors.Unknown);
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await CheckCategoryAsync(failures, values, stored, cancellationToken).ConfigureAwait(false);

        // One type is the start building of a race without one: a second flag would make the
        // choice ambiguous.
        if (values.IsStartBuilding && await _dbContext.BuildingTypes.AnyAsync(
            type => type.IsStartBuilding && !type.IsArchived && type.Id != (stored == null ? 0 : stored.Id),
            cancellationToken).ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, StartField, AdminReferenceErrors.Duplicate);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        List<LevelValues> levels = values.Levels ?? [];
        List<SubjectRequirement> requirements = [];

        if (levels.Any(level => level.Level is null))
        {
            AdminReferenceRules.Fail(failures, LevelsField, AdminReferenceErrors.LevelsGap);
        }
        else if (AdminReferenceRules.CheckLevelNumbers(failures, LevelsField, [.. levels.Select(level => level.Level!.Value)], PlanetsSettings.From(_config).BuildingMaxLevel))
        {
            await CheckLevelsInUseAsync(failures, stored, levels.Count, cancellationToken).ConfigureAwait(false);
            requirements = await CheckLevelsAsync(failures, values, stored, cancellationToken).ConfigureAwait(false);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);

        return requirements;
    }

    /// <summary>
    /// Checks the category of a building: it is mandatory, it exists, and an archived one is not
    /// offered to a new building or to a building that changes its category.
    /// </summary>
    private async Task CheckCategoryAsync(Dictionary<string, List<string>> failures, BuildingValues values, BuildingTypeEntity? stored, CancellationToken cancellationToken)
    {
        if (values.CategoryId is not { } categoryId)
        {
            AdminReferenceRules.Fail(failures, CategoryField, AdminReferenceErrors.Required);

            return;
        }

        // The row of the category is locked (shared) until the commit, so the category cannot be
        // archived between this check and the write of the building.
        BuildingCategoryEntity? category = (await _dbContext.BuildingCategories
            .FromSql($"SELECT * FROM building_category WHERE id = {categoryId} FOR SHARE")
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();

        if (category is null)
        {
            AdminReferenceRules.Fail(failures, CategoryField, AdminReferenceErrors.Unknown);
        }
        else if (category.IsArchived && category.Id != stored?.CategoryId)
        {
            AdminReferenceRules.Fail(failures, CategoryField, AdminReferenceErrors.Archived);
        }
    }

    /// <summary>
    /// Refuses to remove a level a player has or has in the queue.
    /// </summary>
    private async Task CheckLevelsInUseAsync(Dictionary<string, List<string>> failures, BuildingTypeEntity? stored, int levelCount, CancellationToken cancellationToken)
    {
        if (stored is null)
        {
            return;
        }

        int built = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.BuildingTypeId == stored.Id)
            .MaxAsync(building => (int?)building.Level, cancellationToken).ConfigureAwait(false) ?? 0;
        int queued = await _dbContext.QueueItems.AsNoTracking()
            .Where(item => item.BuildingTypeId == stored.Id)
            .MaxAsync(item => (int?)item.TargetLevel, cancellationToken).ConfigureAwait(false) ?? 0;

        if (Math.Max(built, queued) > levelCount)
        {
            AdminReferenceRules.Fail(failures, LevelsField, AdminReferenceErrors.LevelsInUse);
        }
    }

    private async Task<List<SubjectRequirement>> CheckLevelsAsync(
        Dictionary<string, List<string>> failures,
        BuildingValues values,
        BuildingTypeEntity? stored,
        CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ResourceTypeFact> resourcesById = resources.Resources.ToDictionary(resource => resource.Id);
        Dictionary<int, ResourceGroupFact> groupsById = resources.Groups.ToDictionary(group => group.Id);
        HashSet<int> buildingIds = [.. await _dbContext.BuildingTypes.AsNoTracking().Select(type => type.Id).ToListAsync(cancellationToken).ConfigureAwait(false)];
        IReadOnlySet<int> technologyIds = await _technologies.GetTechnologyIdsAsync(cancellationToken).ConfigureAwait(false);

        // An archived resource stays where the row already has it, but no level gets it anew.
        HashSet<int> storedResourceIds = [.. (stored?.LevelIds ?? []).SelectMany(level => level.CostIds.Select(cost => cost.ResourceTypeId)
            .Concat(level.EffectIds.Where(effect => effect.ResourceTypeId is not null).Select(effect => effect.ResourceTypeId!.Value)))];
        HashSet<int> storedGroupIds = [.. (stored?.LevelIds ?? []).SelectMany(level => level.EffectIds)
            .Where(effect => effect.ResourceGroupId is not null).Select(effect => effect.ResourceGroupId!.Value)];

        List<SubjectRequirement> requirements = [];
        List<LevelValues> levels = [.. values.Levels!.OrderBy(level => level.Level)];
        List<LevelValues> sent = values.Levels!;

        foreach (LevelValues level in levels)
        {
            int index = sent.IndexOf(level);

            if (level.DurationSeconds is null or < 0)
            {
                AdminReferenceRules.Fail(failures, AdminReferenceRules.PathOf(LevelsField, index, "duration_seconds"), AdminReferenceErrors.OutOfBounds);
            }

            CheckCosts(failures, index, level.Costs ?? [], resourcesById, storedResourceIds);
            CheckEffects(failures, index, level.Effects ?? [], resourcesById, groupsById, storedResourceIds, storedGroupIds);

            foreach ((RequirementValues requirement, int requirementIndex) in (level.Requirements ?? []).Select((requirement, position) => (requirement, position)))
            {
                string path = AdminReferenceRules.PathOf(LevelsField, index, RequirementsField) + "." + requirementIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (RequirementAdminFields.Check(failures, path, requirement.RequirementType, requirement.TargetId, requirement.TargetLevel, buildingIds, technologyIds, resourcesById.Keys) is { } definition)
                {
                    requirements.Add(new SubjectRequirement(stored?.Id ?? 0, level.Level!.Value, definition));
                }
            }

            // The requirements apply from their level upwards, so a level has requirements when
            // any level up to it has one; only the first level of the start building may have none.
            bool hasRequirements = levels.Where(lower => lower.Level <= level.Level).Any(lower => (lower.Requirements ?? []).Count > 0);

            if (!hasRequirements && !(level.Level == 1 && values.IsStartBuilding))
            {
                AdminReferenceRules.Fail(failures, AdminReferenceRules.PathOf(LevelsField, index, RequirementsField), AdminReferenceErrors.RequirementsMissing);
            }
        }

        if (requirements.GroupBy(requirement => requirement).Any(group => group.Count() > 1))
        {
            AdminReferenceRules.Fail(failures, LevelsField, AdminReferenceErrors.Duplicate);
        }

        return requirements;
    }

    private static void CheckCosts(
        Dictionary<string, List<string>> failures,
        int index,
        List<CostValues> costs,
        Dictionary<int, ResourceTypeFact> resourcesById,
        HashSet<int> storedResourceIds)
    {
        string path = AdminReferenceRules.PathOf(LevelsField, index, CostsField);

        if (costs.Count == 0)
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.CostMissing);
        }

        if (costs.Select(cost => cost.ResourceTypeId).Distinct().Count() != costs.Count)
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Duplicate);
        }

        foreach (CostValues cost in costs)
        {
            if (cost.ResourceTypeId is not { } resourceId || !resourcesById.TryGetValue(resourceId, out ResourceTypeFact? resource))
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
            }
            else if (resource.IsArchived && !storedResourceIds.Contains(resourceId))
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Archived);
            }

            if (cost.Amount is null or < 1)
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.OutOfBounds);
            }
        }
    }

    private static void CheckEffects(
        Dictionary<string, List<string>> failures,
        int index,
        List<EffectValues> effects,
        Dictionary<int, ResourceTypeFact> resourcesById,
        Dictionary<int, ResourceGroupFact> groupsById,
        HashSet<int> storedResourceIds,
        HashSet<int> storedGroupIds)
    {
        string path = AdminReferenceRules.PathOf(LevelsField, index, EffectsField);

        foreach (EffectValues effect in effects)
        {
            if (effect.EffectType is null || !BuildingEffectTypes.All.Contains(effect.EffectType, StringComparer.Ordinal))
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);

                continue;
            }

            bool needsResource = BuildingEffectTypes.OfResource.Contains(effect.EffectType);
            bool needsGroup = BuildingEffectTypes.OfResourceGroup.Contains(effect.EffectType);

            if (needsResource != (effect.ResourceTypeId is not null) || needsGroup != (effect.ResourceGroupId is not null))
            {
                AdminReferenceRules.Fail(failures, path, needsResource || needsGroup ? AdminReferenceErrors.Required : AdminReferenceErrors.NotApplicable);
            }

            if (effect.ResourceTypeId is { } resourceId)
            {
                if (!resourcesById.TryGetValue(resourceId, out ResourceTypeFact? resource))
                {
                    AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
                }
                else if (resource.IsArchived && !storedResourceIds.Contains(resourceId))
                {
                    AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Archived);
                }
            }

            if (effect.ResourceGroupId is { } groupId)
            {
                if (!groupsById.TryGetValue(groupId, out ResourceGroupFact? group))
                {
                    AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
                }
                else if (group.IsArchived && !storedGroupIds.Contains(groupId))
                {
                    AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Archived);
                }
            }

            if (effect.Value is null or < 0)
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.OutOfBounds);
            }
        }
    }

    private void Committed()
    {
        // The row, its texts and its requirements are committed: the copies in memory are dropped,
        // so a player sees the change without waiting for the lifetime of the cache.
        _catalogCache.Invalidate();
        _texts.Invalidate();
    }

    private async Task<BuildingTypeEntity> LockAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.BuildingTypes
            .FromSql($"SELECT * FROM building_type WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    private Task<BuildingTypeEntity?> LoadAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<BuildingTypeEntity> types = tracked ? _dbContext.BuildingTypes : _dbContext.BuildingTypes.AsNoTracking();

        return types
            .Include(type => type.LevelIds).ThenInclude(level => level.CostIds)
            .Include(type => type.LevelIds).ThenInclude(level => level.EffectIds)
            .AsSplitQuery()
            .SingleOrDefaultAsync(type => type.Id == id, cancellationToken);
    }

    private async Task<BuildingValues> ValuesOfAsync(BuildingTypeEntity type, IReadOnlyList<LocalizationPack> packs, CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectRequirement> requirements = await _requirements.GetManyAsync(
            RequirementSubjectTypes.BuildingType,
            [new SubjectLevel(type.Id, int.MaxValue)],
            cancellationToken).ConfigureAwait(false);

        return new BuildingValues(
            AdminReferenceRules.TextsOf(packs, type.NameKey),
            AdminReferenceRules.TextsOf(packs, type.DescriptionKey),
            type.BuildingClass,
            type.CategoryId,
            type.SortOrder,
            type.IsStartBuilding,
            type.ImageKey,
            [
                .. type.LevelIds.OrderBy(level => level.Level).Select(level => new LevelValues(
                    level.Level,
                    level.DurationSeconds,
                    [.. level.CostIds.OrderBy(cost => cost.ResourceTypeId).Select(cost => new CostValues(cost.ResourceTypeId, cost.Amount))],
                    [.. level.EffectIds.OrderBy(effect => effect.Id).Select(effect => new EffectValues(effect.EffectType, effect.ResourceTypeId, effect.ResourceGroupId, effect.Value))],
                    [
                        .. requirements.Where(requirement => requirement.SubjectLevel == level.Level).Select(requirement => new RequirementValues(
                            requirement.Requirement.RequirementType,
                            requirement.Requirement.TargetId,
                            requirement.Requirement.TargetLevel)),
                    ])),
            ]);
    }

    /// <summary>The values of the form of a building type.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="BuildingClass">The class, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="CategoryId">The category of the building: the group of its tile on the building page.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    /// <param name="IsStartBuilding">Whether a race without a start building gets this type.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="Levels">The levels.</param>
    private sealed record BuildingValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        string? BuildingClass,
        int? CategoryId,
        int? SortOrder,
        bool IsStartBuilding,
        string? ImageKey,
        List<LevelValues>? Levels)
    {
        public BuildingValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [ClassField] = BuildingClass,
            [CategoryField] = CategoryId,
            [SortOrderField] = SortOrder,
            [StartField] = IsStartBuilding,
            [LevelsField] = Levels,
        };
    }

    /// <summary>A level of the form.</summary>
    /// <param name="Level">The number of the level.</param>
    /// <param name="DurationSeconds">The time to build it, in game seconds.</param>
    /// <param name="Costs">The costs.</param>
    /// <param name="Effects">The effects.</param>
    /// <param name="Requirements">The requirements that apply from this level.</param>
    private sealed record LevelValues(
        int? Level,
        long? DurationSeconds,
        List<CostValues>? Costs,
        List<EffectValues>? Effects,
        List<RequirementValues>? Requirements);

    /// <summary>A cost of a level.</summary>
    /// <param name="ResourceTypeId">The resource.</param>
    /// <param name="Amount">The amount.</param>
    private sealed record CostValues(int? ResourceTypeId, long? Amount);

    /// <summary>An effect of a level.</summary>
    /// <param name="EffectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
    /// <param name="ResourceTypeId">The resource of a production effect.</param>
    /// <param name="ResourceGroupId">The resource group of a storage effect.</param>
    /// <param name="Value">The value at the level.</param>
    private sealed record EffectValues(string? EffectType, int? ResourceTypeId, int? ResourceGroupId, decimal? Value);

    /// <summary>A requirement of a level.</summary>
    /// <param name="RequirementType">The requirement type.</param>
    /// <param name="TargetId">The target.</param>
    /// <param name="TargetLevel">The required level of the target.</param>
    private sealed record RequirementValues(string? RequirementType, int? TargetId, int? TargetLevel);
}

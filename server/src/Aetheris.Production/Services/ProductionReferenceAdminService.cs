using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The reference kinds of the production in the admin panel: the groups of the parts, the
/// parts and the recipes (task 00013).
/// </summary>
/// <remarks>
/// <para>
/// Every kind is data: the server gives the neutral code, the administrator gives the texts,
/// the picture and the values of the row. The row, its texts and its requirements are
/// committed together, and the catalog in memory is dropped after the commit, so a player
/// sees the change at once.
/// </para>
/// <para>
/// A row is never deleted: it is archived and can come back from the archive (task 00023,
/// decision 5).
/// </para>
/// </remarks>
public sealed class ProductionReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the groups of the parts.</summary>
    public const string PartGroupKind = "part_group";

    /// <summary>The kind of the parts.</summary>
    public const string PartKind = "part";

    /// <summary>The kind of the recipes.</summary>
    public const string RecipeKind = "recipe";

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.Production.admin-reference-texts.json";

    private const string LabelPrefix = "admin.reference.";

    private const string GroupField = "part_group_id";

    private const string MarkLevelField = "mark_level";

    private const string SortOrderField = "sort_order";

    private const string AttributesField = "attributes";

    private const string FactoryField = "building_type_id";

    private const string PartField = "part_type_id";

    private const string ResourceField = "resource_type_id";

    private const string OutputQuantityField = "output_quantity";

    private const string DurationField = "duration_seconds";

    private const string IngredientsField = "ingredients";

    private const string RequirementsField = "requirements";

    /// <summary>
    /// The reference kind of the races, which a requirement «only the race X» picks from.
    /// </summary>
    private const string RaceReferenceKind = "race";

    /// <summary>The place of the groups of the parts in the menu of the reference kinds.</summary>
    private const int PartGroupSortOrder = 60;

    /// <summary>The place of the parts in the menu of the reference kinds.</summary>
    private const int PartSortOrder = 61;

    /// <summary>The place of the recipes in the menu of the reference kinds.</summary>
    private const int RecipeSortOrder = 62;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(ProductionReferenceAdminService).Assembly, LabelTextsResource);

    /// <summary>
    /// Gets the field of the requirements of a recipe: the shared kinds of requirement and
    /// «only the race X» (task 00013, decision 2.7).
    /// </summary>
    private static AdminReferenceField RecipeRequirementsField { get; } = RequirementAdminFields.Field with
    {
        Fields =
        [
            .. RequirementAdminFields.Field.Fields!.Select(field => field.Name == "requirement_type"
                ? field with
                {
                    Options =
                    [
                        .. field.Options!,
                        new AdminReferenceOption(ProductionRequirementTypes.Race, LabelPrefix + "requirement." + ProductionRequirementTypes.Race, RaceReferenceKind),
                    ],
                }
                : field),
        ],
    };

    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly IRequirementsModule _requirements;
    private readonly ITechnologyLevels _technologies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="world">The world, for the resources and the races.</param>
    /// <param name="planets">The bodies module, for the factories.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="technologies">The technologies, for the requirement of a recipe on a technology.</param>
    public ProductionReferenceAdminService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        ProductionCatalogCache catalogCache,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        IWorldModule world,
        IPlanetsModule planets,
        IRequirementsModule requirements,
        ITechnologyLevels technologies)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(technologies);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _codes = codes;
        _texts = texts;
        _images = images;
        _world = world;
        _planets = planets;
        _requirements = requirements;
        _technologies = technologies;
    }

    /// <summary>
    /// Gets the kind of the groups of the parts.
    /// </summary>
    public static AdminReferenceDescriptor PartGroupDescriptor { get; } = new(
        PartGroupKind,
        ProductionTextKeys.PartGroupCodeGroup,
        ProductionTextKeys.PartGroup,
        LabelPrefix + PartGroupKind + ".title",
        PartGroupSortOrder,
        "/production/part-groups",
        new AdminReferencePermissions(
            "admin.production_part_groups_read",
            "admin.production_part_group_read",
            "admin.production_part_group_added",
            "admin.production_part_group_updated",
            "admin.production_part_group_archived",
            "admin.production_part_group_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
        ]);

    /// <summary>
    /// Gets the kind of the parts.
    /// </summary>
    public static AdminReferenceDescriptor PartDescriptor { get; } = new(
        PartKind,
        ProductionTextKeys.PartTypeCodeGroup,
        ProductionTextKeys.PartType,
        LabelPrefix + PartKind + ".title",
        PartSortOrder,
        "/production/parts",
        new AdminReferencePermissions(
            "admin.production_parts_read",
            "admin.production_part_read",
            "admin.production_part_added",
            "admin.production_part_updated",
            "admin.production_part_archived",
            "admin.production_part_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                GroupField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.part_group",
                IsRequired: true,
                InList: true,
                ReferenceKind: PartGroupKind),
            new(
                MarkLevelField,
                AdminReferenceFieldTypes.Number,
                LabelPrefix + "field.mark_level",
                IsRequired: true,
                InList: true,
                Minimum: PartMarkLevels.First,
                Maximum: PartMarkLevels.Max),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
            new(
                AttributesField,
                AdminReferenceFieldTypes.Effects,
                LabelPrefix + "field.attributes",
                Fields:
                [
                    new(
                        "attribute_key",
                        AdminReferenceFieldTypes.Select,
                        LabelPrefix + "field.attribute_key",
                        IsRequired: true,
                        Options: [.. PartAttributeKeys.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "attribute." + value))]),
                    new("number_value", AdminReferenceFieldTypes.Number, LabelPrefix + "field.attribute_number"),
                    new(
                        "code_value",
                        AdminReferenceFieldTypes.Select,
                        LabelPrefix + "field.attribute_code",
                        Options:
                        [
                            .. PartDamageKinds.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "damage_kind." + value)),
                            .. PartSlotSizes.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "slot_size." + value)),
                        ]),
                    new("text_value", AdminReferenceFieldTypes.Text, LabelPrefix + "field.attribute_text"),
                ]),
        ]);

    /// <summary>
    /// Gets the kind of the recipes.
    /// </summary>
    public static AdminReferenceDescriptor RecipeDescriptor { get; } = new(
        RecipeKind,
        ProductionTextKeys.RecipeCodeGroup,
        ProductionTextKeys.Recipe,
        LabelPrefix + RecipeKind + ".title",
        RecipeSortOrder,
        "/production/recipes",
        new AdminReferencePermissions(
            "admin.production_recipes_read",
            "admin.production_recipe_read",
            "admin.production_recipe_added",
            "admin.production_recipe_updated",
            "admin.production_recipe_archived",
            "admin.production_recipe_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                FactoryField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.factory",
                IsRequired: true,
                InList: true,
                ReferenceKind: "building"),
            new(
                PartField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.part",
                ReferenceKind: PartKind),
            new(
                ResourceField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.resource",
                ReferenceKind: "resource"),
            new(OutputQuantityField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.output_quantity", IsRequired: true, InList: true, Minimum: 1),
            new(DurationField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.duration_seconds", IsRequired: true, InList: true, Minimum: 0),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, Minimum: 0),
            new(
                IngredientsField,
                AdminReferenceFieldTypes.Costs,
                LabelPrefix + "field.ingredients",
                IsRequired: true,
                Fields:
                [
                    new("resource_type_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource", ReferenceKind: "resource"),
                    new("part_type_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.part", ReferenceKind: PartKind),
                    new("amount", AdminReferenceFieldTypes.Number, LabelPrefix + "field.amount", IsRequired: true, Minimum: 1),
                ]),
            RecipeRequirementsField,
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [PartGroupDescriptor, PartDescriptor, RecipeDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        return kind switch
        {
            PartGroupKind => AdminReferenceRules.Page(
                [
                    .. (await _dbContext.PartGroups.AsNoTracking()
                        .OrderBy(group => group.SortOrder)
                        .ThenBy(group => group.Id)
                        .ToListAsync(cancellationToken).ConfigureAwait(false))
                        .Select(group => new AdminReferenceRow(
                            group.Id,
                            group.Code,
                            group.NameKey,
                            group.ImageKey,
                            group.IsArchived,
                            group.Version,
                            new Dictionary<string, object?> { [SortOrderField] = group.SortOrder })),
                ],
                query,
                packs),
            PartKind => AdminReferenceRules.Page(
                [
                    .. (await _dbContext.PartTypes.AsNoTracking()
                        .OrderBy(part => part.SortOrder)
                        .ThenBy(part => part.Id)
                        .ToListAsync(cancellationToken).ConfigureAwait(false))
                        .Select(part => new AdminReferenceRow(
                            part.Id,
                            part.Code,
                            part.NameKey,
                            part.ImageKey,
                            part.IsArchived,
                            part.Version,
                            new Dictionary<string, object?>
                            {
                                [GroupField] = part.PartGroupId,
                                [MarkLevelField] = part.MarkLevel,
                                [SortOrderField] = part.SortOrder,
                            })),
                ],
                query,
                packs),
            RecipeKind => AdminReferenceRules.Page(
                [
                    .. (await _dbContext.Recipes.AsNoTracking()
                        .OrderBy(recipe => recipe.SortOrder)
                        .ThenBy(recipe => recipe.Id)
                        .ToListAsync(cancellationToken).ConfigureAwait(false))
                        .Select(recipe => new AdminReferenceRow(
                            recipe.Id,
                            recipe.Code,
                            recipe.NameKey,
                            recipe.ImageKey,
                            recipe.IsArchived,
                            recipe.Version,
                            new Dictionary<string, object?>
                            {
                                [FactoryField] = recipe.BuildingTypeId,
                                [PartField] = recipe.PartTypeId,
                                [ResourceField] = recipe.ResourceTypeId,
                                [OutputQuantityField] = recipe.OutputQuantity,
                                [DurationField] = recipe.DurationSeconds,
                                [SortOrderField] = recipe.SortOrder,
                            })),
                ],
                query,
                packs),
            _ => throw new ArgumentException($"The production module has no reference kind '{kind}'.", nameof(kind)),
        };
    }

    /// <inheritdoc />
    public async Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken)
    {
        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        switch (kind)
        {
            case PartGroupKind:
                {
                    PartGroupEntity? group = await _dbContext.PartGroups.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (group is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        group.Id,
                        group.Code,
                        group.NameKey,
                        group.DescriptionKey,
                        group.ImageKey,
                        group.IsArchived,
                        group.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.ImageField] = group.ImageKey,
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, group.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, group.DescriptionKey),
                            [SortOrderField] = group.SortOrder,
                        });
                }

            case PartKind:
                {
                    PartTypeEntity? part = await _dbContext.PartTypes.AsNoTracking()
                        .Include(entry => entry.AttributeIds)
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (part is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        part.Id,
                        part.Code,
                        part.NameKey,
                        part.DescriptionKey,
                        part.ImageKey,
                        part.IsArchived,
                        part.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.ImageField] = part.ImageKey,
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, part.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, part.DescriptionKey),
                            [GroupField] = part.PartGroupId,
                            [MarkLevelField] = part.MarkLevel,
                            [SortOrderField] = part.SortOrder,
                            [AttributesField] = AttributesOf(part),
                        });
                }

            case RecipeKind:
                {
                    RecipeEntity? recipe = await _dbContext.Recipes.AsNoTracking()
                        .Include(entry => entry.IngredientIds)
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (recipe is null)
                    {
                        return null;
                    }

                    // The requirements are a part of the form, so saving the form keeps them.
                    IReadOnlyList<RequirementDefinition> requirements = await _requirements.GetAsync(
                        RequirementSubjectTypes.Recipe,
                        recipe.Id,
                        1,
                        cancellationToken).ConfigureAwait(false);

                    return new AdminReferenceRecord(
                        recipe.Id,
                        recipe.Code,
                        recipe.NameKey,
                        recipe.DescriptionKey,
                        recipe.ImageKey,
                        recipe.IsArchived,
                        recipe.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.ImageField] = recipe.ImageKey,
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, recipe.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, recipe.DescriptionKey),
                            [FactoryField] = recipe.BuildingTypeId,
                            [PartField] = recipe.PartTypeId,
                            [ResourceField] = recipe.ResourceTypeId,
                            [OutputQuantityField] = recipe.OutputQuantity,
                            [DurationField] = recipe.DurationSeconds,
                            [SortOrderField] = recipe.SortOrder,
                            [IngredientsField] = IngredientsOf(recipe),
                            [RequirementsField] = RequirementsOf(requirements),
                        });
                }

            default:
                throw new ArgumentException($"The production module has no reference kind '{kind}'.", nameof(kind));
        }
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> CreateAsync(string kind, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                switch (kind)
                {
                    case PartGroupKind:
                        {
                            GroupValues values = AdminReferenceJson.Read<GroupValues>(input.Values);
                            await CheckGroupAsync(values, null, cancellationToken).ConfigureAwait(false);

                            string code = await _codes.NextAsync(ProductionTextKeys.PartGroupCodeGroup, cancellationToken).ConfigureAwait(false);
                            PartGroupEntity group = new()
                            {
                                Code = code,
                                NameKey = ProductionTextKeys.GroupName(code),
                                DescriptionKey = ProductionTextKeys.GroupDescription(code),
                            };

                            Apply(group, values);
                            _dbContext.PartGroups.Add(group);
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.AddMissingAsync(
                                AdminReferenceRules.Entries(_texts, group.NameKey, group.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(PartGroupKind, group.Id),
                                null,
                                new { code, values = values.Trimmed(_texts) },
                                group.Id,
                                group.Version);
                        }

                    case PartKind:
                        {
                            PartValues values = AdminReferenceJson.Read<PartValues>(input.Values);
                            await CheckPartAsync(values, null, cancellationToken).ConfigureAwait(false);

                            string code = await _codes.NextAsync(ProductionTextKeys.PartTypeCodeGroup, cancellationToken).ConfigureAwait(false);
                            PartTypeEntity part = new()
                            {
                                Code = code,
                                NameKey = ProductionTextKeys.PartName(code),
                                DescriptionKey = ProductionTextKeys.PartDescription(code),
                            };

                            Apply(part, values);
                            _dbContext.PartTypes.Add(part);
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.AddMissingAsync(
                                AdminReferenceRules.Entries(_texts, part.NameKey, part.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(PartKind, part.Id),
                                null,
                                new { code, values = values.Trimmed(_texts) },
                                part.Id,
                                part.Version);
                        }

                    case RecipeKind:
                        {
                            RecipeValues values = AdminReferenceJson.Read<RecipeValues>(input.Values);
                            List<SubjectRequirement> requirements = await CheckRecipeAsync(values, null, cancellationToken).ConfigureAwait(false);

                            string code = await _codes.NextAsync(ProductionTextKeys.RecipeCodeGroup, cancellationToken).ConfigureAwait(false);
                            RecipeEntity recipe = new()
                            {
                                Code = code,
                                NameKey = ProductionTextKeys.RecipeName(code),
                                DescriptionKey = ProductionTextKeys.RecipeDescription(code),
                            };

                            Apply(recipe, values);
                            _dbContext.Recipes.Add(recipe);
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _requirements.ReplaceAsync(RequirementSubjectTypes.Recipe, recipe.Id, requirements, cancellationToken).ConfigureAwait(false);

                            await _texts.AddMissingAsync(
                                AdminReferenceRules.Entries(_texts, recipe.NameKey, recipe.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(RecipeKind, recipe.Id),
                                null,
                                new { code, values = values.Trimmed(_texts) },
                                recipe.Id,
                                recipe.Version);
                        }

                    default:
                        throw new ArgumentException($"The production module has no reference kind '{kind}'.", nameof(kind));
                }
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

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                switch (kind)
                {
                    case PartGroupKind:
                        {
                            GroupValues values = AdminReferenceJson.Read<GroupValues>(input.Values);
                            PartGroupEntity group = await LockGroupAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(group.Version, input.Version);
                            await CheckGroupAsync(values, group, cancellationToken).ConfigureAwait(false);

                            IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                            Dictionary<string, object?> before = new()
                            {
                                [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, group.NameKey),
                                [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, group.DescriptionKey),
                                [AdminReferenceRules.ImageField] = group.ImageKey,
                                [SortOrderField] = group.SortOrder,
                            };

                            Apply(group, values);
                            group.Version++;
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.SetAsync(
                                AdminReferenceRules.Entries(_texts, group.NameKey, group.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(PartGroupKind, group.Id),
                                before,
                                values.Trimmed(_texts),
                                group.Id,
                                group.Version);
                        }

                    case PartKind:
                        {
                            PartValues values = AdminReferenceJson.Read<PartValues>(input.Values);
                            PartTypeEntity part = await LockPartAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(part.Version, input.Version);
                            await CheckPartAsync(values, part, cancellationToken).ConfigureAwait(false);

                            IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                            Dictionary<string, object?> before = new()
                            {
                                [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, part.NameKey),
                                [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, part.DescriptionKey),
                                [AdminReferenceRules.ImageField] = part.ImageKey,
                                [GroupField] = part.PartGroupId,
                                [MarkLevelField] = part.MarkLevel,
                                [SortOrderField] = part.SortOrder,
                                [AttributesField] = AttributesOf(part),
                            };

                            Apply(part, values);
                            part.Version++;
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.SetAsync(
                                AdminReferenceRules.Entries(_texts, part.NameKey, part.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(PartKind, part.Id),
                                before,
                                values.Trimmed(_texts),
                                part.Id,
                                part.Version);
                        }

                    case RecipeKind:
                        {
                            RecipeValues values = AdminReferenceJson.Read<RecipeValues>(input.Values);
                            RecipeEntity recipe = await LockRecipeAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(recipe.Version, input.Version);
                            List<SubjectRequirement> requirements = await CheckRecipeAsync(values, recipe, cancellationToken).ConfigureAwait(false);

                            IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                            IReadOnlyList<RequirementDefinition> storedRequirements = await _requirements.GetAsync(
                                RequirementSubjectTypes.Recipe,
                                recipe.Id,
                                1,
                                cancellationToken).ConfigureAwait(false);
                            Dictionary<string, object?> before = new()
                            {
                                [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, recipe.NameKey),
                                [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, recipe.DescriptionKey),
                                [AdminReferenceRules.ImageField] = recipe.ImageKey,
                                [FactoryField] = recipe.BuildingTypeId,
                                [PartField] = recipe.PartTypeId,
                                [ResourceField] = recipe.ResourceTypeId,
                                [OutputQuantityField] = recipe.OutputQuantity,
                                [DurationField] = recipe.DurationSeconds,
                                [SortOrderField] = recipe.SortOrder,
                                [IngredientsField] = IngredientsOf(recipe),
                                [RequirementsField] = RequirementsOf(storedRequirements),
                            };

                            Apply(recipe, values);
                            recipe.Version++;
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _requirements.ReplaceAsync(RequirementSubjectTypes.Recipe, recipe.Id, requirements, cancellationToken).ConfigureAwait(false);

                            await _texts.SetAsync(
                                AdminReferenceRules.Entries(_texts, recipe.NameKey, recipe.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(RecipeKind, recipe.Id),
                                before,
                                values.Trimmed(_texts),
                                recipe.Id,
                                recipe.Version);
                        }

                    default:
                        throw new ArgumentException($"The production module has no reference kind '{kind}'.", nameof(kind));
                }
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> SetArchivedAsync(string kind, int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                switch (kind)
                {
                    case PartGroupKind:
                        {
                            PartGroupEntity group = await LockGroupAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(group.Version, input.Version);

                            if (group.IsArchived == isArchived)
                            {
                                throw new AdminReferenceException(
                                    GameErrorCodes.ValidationFailed,
                                    new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                            }

                            group.IsArchived = isArchived;
                            group.Version++;

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(PartGroupKind, group.Id),
                                new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                                new Dictionary<string, object?> { ["is_archived"] = isArchived },
                                group.Id,
                                group.Version);
                        }

                    case PartKind:
                        {
                            PartTypeEntity part = await LockPartAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(part.Version, input.Version);

                            if (part.IsArchived == isArchived)
                            {
                                throw new AdminReferenceException(
                                    GameErrorCodes.ValidationFailed,
                                    new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                            }

                            part.IsArchived = isArchived;
                            part.Version++;

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(PartKind, part.Id),
                                new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                                new Dictionary<string, object?> { ["is_archived"] = isArchived },
                                part.Id,
                                part.Version);
                        }

                    case RecipeKind:
                        {
                            RecipeEntity recipe = await LockRecipeAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(recipe.Version, input.Version);

                            if (recipe.IsArchived == isArchived)
                            {
                                throw new AdminReferenceException(
                                    GameErrorCodes.ValidationFailed,
                                    new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                            }

                            recipe.IsArchived = isArchived;
                            recipe.Version++;

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(RecipeKind, recipe.Id),
                                new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                                new Dictionary<string, object?> { ["is_archived"] = isArchived },
                                recipe.Id,
                                recipe.Version);
                        }

                    default:
                        throw new ArgumentException($"The production module has no reference kind '{kind}'.", nameof(kind));
                }
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    private async Task CheckGroupAsync(GroupValues values, PartGroupEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    private async Task CheckPartAsync(PartValues values, PartTypeEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        if (values.PartGroupId is not { } groupId)
        {
            AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Required);
        }
        else if (await _dbContext.PartGroups.AsNoTracking().SingleOrDefaultAsync(group => group.Id == groupId, cancellationToken).ConfigureAwait(false) is not { } group)
        {
            AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Unknown);
        }
        else if (group.IsArchived && stored?.PartGroupId != groupId)
        {
            // An archived group keeps its parts, but a new choice of it is refused (task 00023,
            // decision 5).
            AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Archived);
        }

        if (values.MarkLevel is null or < PartMarkLevels.First or > PartMarkLevels.Max)
        {
            AdminReferenceRules.Fail(failures, MarkLevelField, values.MarkLevel is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> seenKeys = new(StringComparer.Ordinal);
        int index = 0;

        foreach (AttributeValues attribute in values.Attributes ?? [])
        {
            string path = AdminReferenceRules.PathOf(AttributesField, index, "attribute_key");

            if (attribute.AttributeKey is null || !PartAttributeKeys.All.Contains(attribute.AttributeKey))
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
            }
            else if (!seenKeys.Add(attribute.AttributeKey))
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Duplicate);
            }

            // A logical code is one the program knows; the constructor of the ships reads it.
            if (attribute.CodeValue is { } codeValue && !PartDamageKinds.All.Contains(codeValue) && !PartSlotSizes.All.Contains(codeValue))
            {
                AdminReferenceRules.Fail(failures, AdminReferenceRules.PathOf(AttributesField, index, "code_value"), AdminReferenceErrors.Unknown);
            }

            index++;
        }

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    private async Task<List<SubjectRequirement>> CheckRecipeAsync(RecipeValues values, RecipeEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ResourceTypeFact> resourcesById = resources.Resources.ToDictionary(resource => resource.Id);
        Dictionary<int, PartTypeEntity> partsById = await _dbContext.PartTypes.AsNoTracking().ToDictionaryAsync(part => part.Id, cancellationToken).ConfigureAwait(false);
        HashSet<int> archivedGroupIds = [.. await _dbContext.PartGroups.AsNoTracking().Where(group => group.IsArchived).Select(group => group.Id).ToListAsync(cancellationToken).ConfigureAwait(false)];
        HashSet<int> buildingIds = [.. (await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false)).Keys];

        // An archived row keeps working where it is already used, but a new choice of it is
        // refused (task 00023, decision 5).
        HashSet<int> storedResourceIds = [.. stored?.IngredientIds.Select(ingredient => ingredient.ResourceTypeId ?? 0) ?? [], stored?.ResourceTypeId ?? 0];
        HashSet<int> storedPartIds = [.. stored?.IngredientIds.Select(ingredient => ingredient.PartTypeId ?? 0) ?? [], stored?.PartTypeId ?? 0];

        if (values.BuildingTypeId is not { } buildingTypeId)
        {
            AdminReferenceRules.Fail(failures, FactoryField, AdminReferenceErrors.Required);
        }
        else if (!buildingIds.Contains(buildingTypeId))
        {
            AdminReferenceRules.Fail(failures, FactoryField, AdminReferenceErrors.Unknown);
        }

        // A recipe makes one product: a part or a resource.
        if (values.PartTypeId is null && values.ResourceTypeId is null)
        {
            AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Required);
        }
        else if (values.PartTypeId is not null && values.ResourceTypeId is not null)
        {
            AdminReferenceRules.Fail(failures, ResourceField, AdminReferenceErrors.NotApplicable);
        }

        if (values.PartTypeId is { } partTypeId)
        {
            CheckPart(failures, PartField, partTypeId, partsById, archivedGroupIds, storedPartIds);
        }

        if (values.ResourceTypeId is { } resourceTypeId)
        {
            CheckResource(failures, ResourceField, resourceTypeId, resourcesById, storedResourceIds);
        }

        if (values.OutputQuantity is null or < 1)
        {
            AdminReferenceRules.Fail(failures, OutputQuantityField, values.OutputQuantity is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        if (values.DurationSeconds is null or < 0)
        {
            AdminReferenceRules.Fail(failures, DurationField, values.DurationSeconds is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        // The ingredients are the cost of a unit: at least one, each a resource or a part, each
        // once.
        HashSet<(int? ResourceTypeId, int? PartTypeId)> seenIngredients = [];
        int index = 0;

        foreach (IngredientValues ingredient in values.Ingredients ?? [])
        {
            string resourcePath = AdminReferenceRules.PathOf(IngredientsField, index, "resource_type_id");
            string partPath = AdminReferenceRules.PathOf(IngredientsField, index, "part_type_id");

            if (ingredient.Amount is null or < 1)
            {
                AdminReferenceRules.Fail(failures, AdminReferenceRules.PathOf(IngredientsField, index, "amount"), ingredient.Amount is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
            }

            if (ingredient.ResourceTypeId is null && ingredient.PartTypeId is null)
            {
                AdminReferenceRules.Fail(failures, resourcePath, AdminReferenceErrors.Required);
            }
            else if (ingredient.ResourceTypeId is not null && ingredient.PartTypeId is not null)
            {
                AdminReferenceRules.Fail(failures, partPath, AdminReferenceErrors.NotApplicable);
            }
            else if (!seenIngredients.Add((ingredient.ResourceTypeId, ingredient.PartTypeId)))
            {
                AdminReferenceRules.Fail(failures, ingredient.ResourceTypeId is null ? partPath : resourcePath, AdminReferenceErrors.Duplicate);
            }
            else if (ingredient.ResourceTypeId is { } ingredientResourceId)
            {
                CheckResource(failures, resourcePath, ingredientResourceId, resourcesById, storedResourceIds);
            }
            else if (ingredient.PartTypeId is { } ingredientPartId)
            {
                CheckPart(failures, partPath, ingredientPartId, partsById, archivedGroupIds, storedPartIds);
            }

            index++;
        }

        if ((values.Ingredients ?? []).Count == 0)
        {
            AdminReferenceRules.Fail(failures, IngredientsField, AdminReferenceErrors.CostMissing);
        }

        // The requirements of the recipe use the shared field and «only the race X», so the same
        // kinds of requirement are offered as for the buildings.
        List<SubjectRequirement> requirements = [];
        IReadOnlySet<int> technologyIds = await _technologies.GetTechnologyIdsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RaceDto> races = await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false);
        int requirementIndex = 0;

        foreach (RequirementValues requirement in values.Requirements ?? [])
        {
            string path = AdminReferenceRules.PathOf(RequirementsField, requirementIndex, "requirement_type");

            if (requirement.RequirementType == ProductionRequirementTypes.Race)
            {
                if (requirement.TargetId is not { } raceId || !races.Any(race => race.Id == raceId))
                {
                    AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
                }
                else
                {
                    requirements.Add(new SubjectRequirement(
                        stored?.Id ?? 0,
                        1,
                        new RequirementDefinition(ProductionRequirementTypes.Race, raceId, 0)));
                }
            }
            else if (RequirementAdminFields.Check(failures, path, requirement.RequirementType, requirement.TargetId, requirement.TargetLevel, buildingIds, technologyIds, resourcesById.Keys) is { } definition)
            {
                requirements.Add(new SubjectRequirement(stored?.Id ?? 0, 1, definition));
            }

            requirementIndex++;
        }

        if (requirements.GroupBy(requirement => (requirement.Requirement.RequirementType, requirement.Requirement.TargetId)).Any(group => group.Count() > 1))
        {
            AdminReferenceRules.Fail(failures, RequirementsField, AdminReferenceErrors.Duplicate);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);

        return requirements;
    }

    private static void CheckPart(
        Dictionary<string, List<string>> failures,
        string path,
        int partTypeId,
        Dictionary<int, PartTypeEntity> partsById,
        HashSet<int> archivedGroupIds,
        HashSet<int> storedPartIds)
    {
        if (!partsById.TryGetValue(partTypeId, out PartTypeEntity? part))
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
        }
        else if ((part.IsArchived || archivedGroupIds.Contains(part.PartGroupId)) && !storedPartIds.Contains(partTypeId))
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Archived);
        }
    }

    private static void CheckResource(
        Dictionary<string, List<string>> failures,
        string path,
        int resourceTypeId,
        Dictionary<int, ResourceTypeFact> resourcesById,
        HashSet<int> storedResourceIds)
    {
        if (!resourcesById.TryGetValue(resourceTypeId, out ResourceTypeFact? resource))
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
        }
        else if (resource.IsArchived && !storedResourceIds.Contains(resourceTypeId))
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Archived);
        }
    }

    private static void Apply(PartGroupEntity group, GroupValues values)
    {
        group.SortOrder = values.SortOrder ?? 0;
        group.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
    }

    private static void Apply(PartTypeEntity part, PartValues values)
    {
        part.PartGroupId = values.PartGroupId!.Value;
        part.MarkLevel = values.MarkLevel!.Value;
        part.SortOrder = values.SortOrder ?? 0;
        part.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);

        part.AttributeIds.RemoveAll(attribute => !(values.Attributes ?? []).Any(sent => sent.AttributeKey == attribute.AttributeKey));

        foreach (AttributeValues sent in values.Attributes ?? [])
        {
            PartAttributeEntity? kept = part.AttributeIds.Find(attribute => attribute.AttributeKey == sent.AttributeKey);

            if (kept is null)
            {
                part.AttributeIds.Add(new PartAttributeEntity
                {
                    AttributeKey = sent.AttributeKey!,
                    NumberValue = sent.NumberValue,
                    CodeValue = sent.CodeValue,
                    TextValue = sent.TextValue,
                });
            }
            else
            {
                kept.NumberValue = sent.NumberValue;
                kept.CodeValue = sent.CodeValue;
                kept.TextValue = sent.TextValue;
            }
        }
    }

    private static void Apply(RecipeEntity recipe, RecipeValues values)
    {
        recipe.BuildingTypeId = values.BuildingTypeId;
        recipe.PartTypeId = values.PartTypeId;
        recipe.ResourceTypeId = values.ResourceTypeId;
        recipe.OutputQuantity = values.OutputQuantity ?? 1;
        recipe.DurationSeconds = values.DurationSeconds ?? 0;
        recipe.SortOrder = values.SortOrder ?? 0;
        recipe.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);

        recipe.IngredientIds.RemoveAll(ingredient => !(values.Ingredients ?? []).Any(sent =>
            sent.ResourceTypeId == ingredient.ResourceTypeId && sent.PartTypeId == ingredient.PartTypeId));

        foreach (IngredientValues sent in values.Ingredients ?? [])
        {
            RecipeIngredientEntity? kept = recipe.IngredientIds.Find(ingredient =>
                ingredient.ResourceTypeId == sent.ResourceTypeId && ingredient.PartTypeId == sent.PartTypeId);

            if (kept is null)
            {
                recipe.IngredientIds.Add(new RecipeIngredientEntity
                {
                    ResourceTypeId = sent.ResourceTypeId,
                    PartTypeId = sent.PartTypeId,
                    Amount = sent.Amount ?? 0,
                });
            }
            else
            {
                kept.Amount = sent.Amount ?? 0;
            }
        }
    }

    private async Task<PartGroupEntity> LockGroupAsync(int id, CancellationToken cancellationToken)
    {
        // The row is locked first, so two changes of one row compare their versions one after
        // the other and the second one meets the new version (task 00023, decision 5).
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM part_group WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.PartGroups.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    private async Task<PartTypeEntity> LockPartAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM part_type WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.PartTypes.Include(entry => entry.AttributeIds).SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    private async Task<RecipeEntity> LockRecipeAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM recipe WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.Recipes.Include(entry => entry.IngredientIds).SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    private static List<object?> IngredientsOf(RecipeEntity recipe) =>
    [
        .. recipe.IngredientIds
            .OrderBy(ingredient => ingredient.Id)
            .Select(ingredient => (object?)new Dictionary<string, object?>
            {
                ["resource_type_id"] = ingredient.ResourceTypeId,
                ["part_type_id"] = ingredient.PartTypeId,
                ["amount"] = ingredient.Amount,
            }),
    ];

    private static List<object?> AttributesOf(PartTypeEntity part) =>
    [
        .. part.AttributeIds
            .OrderBy(attribute => attribute.Id)
            .Select(attribute => (object?)new Dictionary<string, object?>
            {
                ["attribute_key"] = attribute.AttributeKey,
                ["number_value"] = attribute.NumberValue,
                ["code_value"] = attribute.CodeValue,
                ["text_value"] = attribute.TextValue,
            }),
    ];

    private static List<object?> RequirementsOf(IReadOnlyList<RequirementDefinition> requirements) =>
    [
        .. requirements.Select(requirement => (object?)new Dictionary<string, object?>
        {
            ["requirement_type"] = requirement.RequirementType,
            ["target_id"] = requirement.TargetId,
            ["target_level"] = requirement.TargetLevel,
        }),
    ];

    /// <summary>
    /// Drops the copy of the catalog after the change is committed, so a player sees it at
    /// once.
    /// </summary>
    private void Committed() => _catalogCache.Invalidate();

    private sealed record GroupValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? SortOrder,
        string? ImageKey)
    {
        public GroupValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [SortOrderField] = SortOrder,
        };
    }

    private sealed record PartValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? PartGroupId,
        int? MarkLevel,
        int? SortOrder,
        string? ImageKey,
        List<AttributeValues>? Attributes)
    {
        public PartValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [GroupField] = PartGroupId,
            [MarkLevelField] = MarkLevel,
            [SortOrderField] = SortOrder,
            [AttributesField] = Attributes,
        };
    }

    private sealed record RecipeValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? BuildingTypeId,
        int? PartTypeId,
        int? ResourceTypeId,
        long? OutputQuantity,
        long? DurationSeconds,
        int? SortOrder,
        string? ImageKey,
        List<IngredientValues>? Ingredients,
        List<RequirementValues>? Requirements)
    {
        public RecipeValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [FactoryField] = BuildingTypeId,
            [PartField] = PartTypeId,
            [ResourceField] = ResourceTypeId,
            [OutputQuantityField] = OutputQuantity,
            [DurationField] = DurationSeconds,
            [SortOrderField] = SortOrder,
            [IngredientsField] = Ingredients,
            [RequirementsField] = Requirements,
        };
    }

    private sealed record AttributeValues(
        string? AttributeKey,
        decimal? NumberValue,
        string? CodeValue,
        string? TextValue);

    private sealed record IngredientValues(int? ResourceTypeId, int? PartTypeId, long? Amount);

    private sealed record RequirementValues(string? RequirementType, int? TargetId, int? TargetLevel);
}

using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Services;

/// <summary>
/// The reference kinds of the processes in the admin panel: the kinds of the processes, their
/// recipes and their parameters (task 00014).
/// </summary>
/// <remarks>
/// <para>
/// Every kind is data: the server gives the neutral code, the administrator gives the texts,
/// the picture and the values of the row. The row, its texts and the requirements of a recipe
/// are committed together, and the catalog in memory is dropped after the commit, so a player
/// sees the change at once.
/// </para>
/// <para>
/// A row is never deleted: it is archived and can come back from the archive (task 00023,
/// decision 5).
/// </para>
/// </remarks>
public sealed class ProcessReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the kinds of the processes.</summary>
    public const string ProcessTypeKind = "process_type";

    /// <summary>The kind of the recipes of the processes.</summary>
    public const string ProcessRecipeKind = "process_recipe";

    /// <summary>The kind of the parameters of the processes.</summary>
    public const string ProcessParamKind = "process_param";

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.Processes.admin-reference-texts.json";

    private const string LabelPrefix = "admin.reference.";

    private const string HandlerCodeField = "handler_code";

    private const string RefundsInputField = "refunds_input";

    private const string ProcessTypeField = "process_type_id";

    private const string BuildingField = "building_type_id";

    private const string SourceGroupField = "source_group_id";

    private const string TargetGroupField = "target_group_id";

    private const string DurationField = "duration_seconds";

    private const string ShipStubField = "ship_stub";

    private const string ParamKindField = "param_kind";

    private const string LevelSourceField = "level_source";

    private const string LevelTargetField = "level_target_id";

    private const string LevelField = "level";

    private const string ValueField = "value";

    private const string ValueMaxField = "value_max";

    private const string SortOrderField = "sort_order";

    private const string IngredientsField = "ingredients";

    private const string RequirementsField = "requirements";

    private const string ResourceField = "resource_type_id";

    private const string PartField = "part_type_id";

    private const string AmountField = "amount";

    private const string RequirementTypeField = "requirement_type";

    /// <summary>
    /// The reference kind of the races, which a requirement «only the race X» picks from.
    /// </summary>
    private const string RaceReferenceKind = "race";

    /// <summary>The place of the kinds of the processes in the menu of the reference kinds.</summary>
    private const int ProcessTypeSortOrder = 70;

    /// <summary>The place of the recipes of the processes in the menu of the reference kinds.</summary>
    private const int ProcessRecipeSortOrder = 71;

    /// <summary>The place of the parameters of the processes in the menu of the reference kinds.</summary>
    private const int ProcessParamSortOrder = 72;

    /// <summary>
    /// The kinds of the parameters of a process, in the order of the form of the admin panel.
    /// </summary>
    private static readonly IReadOnlyList<string> ParamKinds =
    [
        ProcessParamKinds.ValueByLevel,
        ProcessParamKinds.Range,
        ProcessParamKinds.Scaling,
    ];

    /// <summary>
    /// The sources of the level a parameter follows, in the order of the form of the admin panel.
    /// </summary>
    private static readonly IReadOnlyList<string> LevelSources =
    [
        ProcessLevelSources.Building,
        ProcessLevelSources.Technology,
        ProcessLevelSources.Part,
    ];

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(ProcessReferenceAdminService).Assembly, LabelTextsResource);

    /// <summary>
    /// Gets the field of the requirements of a recipe: the shared kinds of requirement and
    /// «only the race X» (task 00023, decision 2.7).
    /// </summary>
    private static AdminReferenceField RecipeRequirementsField { get; } = RequirementAdminFields.Field with
    {
        Fields =
        [
            .. RequirementAdminFields.Field.Fields!.Select(field => field.Name == RequirementTypeField
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

    private readonly ProcessesDbContext _dbContext;
    private readonly ProcessesUnitOfWork _unitOfWork;
    private readonly ProcessCatalogCache _catalogCache;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly IRequirementsModule _requirements;
    private readonly ITechnologyLevels _technologies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the processes.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="world">The world, for the resources and the groups of the resources.</param>
    /// <param name="planets">The bodies module, for the buildings the processes run in.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="technologies">The technologies, for the requirement of a recipe on a technology.</param>
    public ProcessReferenceAdminService(
        ProcessesDbContext dbContext,
        ProcessesUnitOfWork unitOfWork,
        ProcessCatalogCache catalogCache,
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
    /// Gets the kind of the kinds of the processes.
    /// </summary>
    public static AdminReferenceDescriptor ProcessTypeDescriptor { get; } = new(
        ProcessTypeKind,
        ProcessTextKeys.ProcessTypeCodeGroup,
        ProcessTextKeys.ProcessType,
        LabelPrefix + ProcessTypeKind + ".title",
        ProcessTypeSortOrder,
        "/processes/process-types",
        new AdminReferencePermissions(
            "admin.processes_process_types_read",
            "admin.processes_process_type_read",
            "admin.processes_process_type_added",
            "admin.processes_process_type_updated",
            "admin.processes_process_type_archived",
            "admin.processes_process_type_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                HandlerCodeField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.handler_code",
                IsRequired: true,
                InList: true,
                Options: [.. ProcessHandlerCodes.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "handler." + value))]),
            new(RefundsInputField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.refunds_input"),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, Minimum: 0),
        ]);

    /// <summary>
    /// Gets the kind of the recipes of the processes.
    /// </summary>
    public static AdminReferenceDescriptor ProcessRecipeDescriptor { get; } = new(
        ProcessRecipeKind,
        ProcessTextKeys.ProcessRecipeCodeGroup,
        ProcessTextKeys.ProcessRecipe,
        LabelPrefix + ProcessRecipeKind + ".title",
        ProcessRecipeSortOrder,
        "/processes/process-recipes",
        new AdminReferencePermissions(
            "admin.processes_process_recipes_read",
            "admin.processes_process_recipe_read",
            "admin.processes_process_recipe_added",
            "admin.processes_process_recipe_updated",
            "admin.processes_process_recipe_archived",
            "admin.processes_process_recipe_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                ProcessTypeField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.process_type",
                IsRequired: true,
                InList: true,
                ReferenceKind: ProcessTypeKind),
            new(
                BuildingField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.building",
                IsRequired: true,
                InList: true,
                ReferenceKind: "building"),
            new(
                SourceGroupField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.source_group",
                ReferenceKind: "resource_group"),
            new(
                TargetGroupField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.target_group",
                ReferenceKind: "resource_group"),
            new(DurationField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.duration_seconds", IsRequired: true, InList: true, Minimum: 0),
            new(ShipStubField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.ship_stub"),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, Minimum: 0),
            new(
                IngredientsField,
                AdminReferenceFieldTypes.Costs,
                LabelPrefix + "field.ingredients",
                Fields:
                [
                    new(ResourceField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource", ReferenceKind: "resource"),
                    new(PartField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.part", ReferenceKind: "part"),
                    new(AmountField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.amount", IsRequired: true, Minimum: 1),
                ]),
            RecipeRequirementsField,
        ]);

    /// <summary>
    /// Gets the kind of the parameters of the processes.
    /// </summary>
    public static AdminReferenceDescriptor ProcessParamDescriptor { get; } = new(
        ProcessParamKind,
        ProcessTextKeys.ProcessParamCodeGroup,
        ProcessTextKeys.ProcessParam,
        LabelPrefix + ProcessParamKind + ".title",
        ProcessParamSortOrder,
        "/processes/process-params",
        new AdminReferencePermissions(
            "admin.processes_process_params_read",
            "admin.processes_process_param_read",
            "admin.processes_process_param_added",
            "admin.processes_process_param_updated",
            "admin.processes_process_param_archived",
            "admin.processes_process_param_restored"),
        [
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                ProcessTypeField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.process_type",
                IsRequired: true,
                InList: true,
                ReferenceKind: ProcessTypeKind),
            new(
                ParamKindField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.param_kind",
                IsRequired: true,
                Options: [.. ParamKinds.Select(value => new AdminReferenceOption(value, LabelPrefix + "param_kind." + value))]),
            new(
                LevelSourceField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.level_source",
                IsRequired: true,
                Options: [.. LevelSources.Select(value => new AdminReferenceOption(value, LabelPrefix + "level_source." + value))]),
            new(LevelTargetField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.level_target", IsRequired: true, Minimum: 1),
            new(LevelField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.level", IsRequired: true, Minimum: 1),
            new(ValueField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.value", IsRequired: true, IsDecimal: true),
            new(ValueMaxField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.value_max", IsDecimal: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, Minimum: 0),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } =
        [ProcessTypeDescriptor, ProcessRecipeDescriptor, ProcessParamDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        List<AdminReferenceRow> rows = kind switch
        {
            ProcessTypeKind => [.. (await _dbContext.ProcessTypes.AsNoTracking()
                    .OrderBy(type => type.SortOrder)
                    .ThenBy(type => type.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(type => new AdminReferenceRow(
                    type.Id,
                    type.Code,
                    type.NameKey,
                    type.ImageKey,
                    type.IsArchived,
                    type.Version,
                    new Dictionary<string, object?>
                    {
                        [HandlerCodeField] = type.HandlerCode,
                        [RefundsInputField] = type.RefundsInput,
                        [SortOrderField] = type.SortOrder,
                    }))],
            ProcessRecipeKind => [.. (await _dbContext.ProcessRecipes.AsNoTracking()
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
                        [ProcessTypeField] = recipe.ProcessTypeId,
                        [BuildingField] = recipe.BuildingTypeId,
                        [SourceGroupField] = recipe.SourceGroupId,
                        [TargetGroupField] = recipe.TargetGroupId,
                        [DurationField] = recipe.DurationSeconds,
                        [ShipStubField] = recipe.ShipStub,
                        [SortOrderField] = recipe.SortOrder,
                    }))],
            ProcessParamKind => [.. (await _dbContext.ProcessParams.AsNoTracking()
                    .OrderBy(parameter => parameter.SortOrder)
                    .ThenBy(parameter => parameter.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(parameter => new AdminReferenceRow(
                    parameter.Id,
                    parameter.Code,
                    parameter.NameKey,
                    null,
                    parameter.IsArchived,
                    parameter.Version,
                    new Dictionary<string, object?>
                    {
                        [ProcessTypeField] = parameter.ProcessTypeId,
                        [ParamKindField] = parameter.ParamKind,
                        [LevelSourceField] = parameter.LevelSource,
                        [LevelTargetField] = parameter.LevelTargetId,
                        [LevelField] = parameter.Level,
                        [ValueField] = parameter.Value,
                        [ValueMaxField] = parameter.ValueMax,
                        [SortOrderField] = parameter.SortOrder,
                    }))],
            _ => throw UnknownKind(kind),
        };

        return AdminReferenceRules.Page(rows, query, packs);
    }

    /// <inheritdoc />
    public async Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken)
    {
        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        switch (kind)
        {
            case ProcessTypeKind:
                {
                    ProcessTypeEntity? type = await _dbContext.ProcessTypes.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (type is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        type.Id,
                        type.Code,
                        type.NameKey,
                        type.DescriptionKey,
                        type.ImageKey,
                        type.IsArchived,
                        type.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.ImageField] = type.ImageKey,
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, type.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, type.DescriptionKey),
                            [HandlerCodeField] = type.HandlerCode,
                            [RefundsInputField] = type.RefundsInput,
                            [SortOrderField] = type.SortOrder,
                        });
                }

            case ProcessRecipeKind:
                {
                    ProcessRecipeEntity? recipe = await _dbContext.ProcessRecipes.AsNoTracking()
                        .Include(entry => entry.IngredientIds)
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (recipe is null)
                    {
                        return null;
                    }

                    // The requirements are a part of the form, so saving the form keeps them.
                    IReadOnlyList<RequirementDefinition> requirements = await _requirements.GetAsync(
                        RequirementSubjectTypes.ProcessRecipe,
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
                            [ProcessTypeField] = recipe.ProcessTypeId,
                            [BuildingField] = recipe.BuildingTypeId,
                            [SourceGroupField] = recipe.SourceGroupId,
                            [TargetGroupField] = recipe.TargetGroupId,
                            [DurationField] = recipe.DurationSeconds,
                            [ShipStubField] = recipe.ShipStub,
                            [SortOrderField] = recipe.SortOrder,
                            [IngredientsField] = IngredientsOf(recipe),
                            [RequirementsField] = RequirementsOf(requirements),
                        });
                }

            case ProcessParamKind:
                {
                    ProcessParamEntity? parameter = await _dbContext.ProcessParams.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (parameter is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        parameter.Id,
                        parameter.Code,
                        parameter.NameKey,
                        parameter.DescriptionKey,
                        null,
                        parameter.IsArchived,
                        parameter.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, parameter.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, parameter.DescriptionKey),
                            [ProcessTypeField] = parameter.ProcessTypeId,
                            [ParamKindField] = parameter.ParamKind,
                            [LevelSourceField] = parameter.LevelSource,
                            [LevelTargetField] = parameter.LevelTargetId,
                            [LevelField] = parameter.Level,
                            [ValueField] = parameter.Value,
                            [ValueMaxField] = parameter.ValueMax,
                            [SortOrderField] = parameter.SortOrder,
                        });
                }

            default:
                throw UnknownKind(kind);
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
                    case ProcessTypeKind:
                        {
                            ProcessTypeValues values = AdminReferenceJson.Read<ProcessTypeValues>(input.Values);
                            await CheckProcessTypeAsync(values, null, cancellationToken).ConfigureAwait(false);

                            string code = await _codes.NextAsync(ProcessTextKeys.ProcessTypeCodeGroup, cancellationToken).ConfigureAwait(false);
                            ProcessTypeEntity type = new()
                            {
                                Code = code,
                                NameKey = ProcessTextKeys.TypeName(code),
                                DescriptionKey = ProcessTextKeys.TypeDescription(code),
                            };

                            Apply(type, values);
                            _dbContext.ProcessTypes.Add(type);
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.AddMissingAsync(
                                AdminReferenceRules.Entries(_texts, type.NameKey, type.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessTypeKind, type.Id),
                                null,
                                new { code, values = values.Trimmed(_texts) },
                                type.Id,
                                type.Version);
                        }

                    case ProcessRecipeKind:
                        {
                            ProcessRecipeValues values = AdminReferenceJson.Read<ProcessRecipeValues>(input.Values);
                            List<SubjectRequirement> requirements = await CheckProcessRecipeAsync(values, null, cancellationToken).ConfigureAwait(false);

                            string code = await _codes.NextAsync(ProcessTextKeys.ProcessRecipeCodeGroup, cancellationToken).ConfigureAwait(false);
                            ProcessRecipeEntity recipe = new()
                            {
                                Code = code,
                                NameKey = ProcessTextKeys.RecipeName(code),
                                DescriptionKey = ProcessTextKeys.RecipeDescription(code),
                            };

                            Apply(recipe, values);
                            _dbContext.ProcessRecipes.Add(recipe);
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _requirements.ReplaceAsync(RequirementSubjectTypes.ProcessRecipe, recipe.Id, requirements, cancellationToken).ConfigureAwait(false);

                            await _texts.AddMissingAsync(
                                AdminReferenceRules.Entries(_texts, recipe.NameKey, recipe.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessRecipeKind, recipe.Id),
                                null,
                                new { code, values = values.Trimmed(_texts) },
                                recipe.Id,
                                recipe.Version);
                        }

                    case ProcessParamKind:
                        {
                            ProcessParamValues values = AdminReferenceJson.Read<ProcessParamValues>(input.Values);
                            await CheckProcessParamAsync(values, null, cancellationToken).ConfigureAwait(false);

                            string code = await _codes.NextAsync(ProcessTextKeys.ProcessParamCodeGroup, cancellationToken).ConfigureAwait(false);
                            ProcessParamEntity parameter = new()
                            {
                                Code = code,
                                NameKey = ProcessTextKeys.ParamName(code),
                                DescriptionKey = ProcessTextKeys.ParamDescription(code),
                            };

                            Apply(parameter, values);
                            _dbContext.ProcessParams.Add(parameter);
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.AddMissingAsync(
                                AdminReferenceRules.Entries(_texts, parameter.NameKey, parameter.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessParamKind, parameter.Id),
                                null,
                                new { code, values = values.Trimmed(_texts) },
                                parameter.Id,
                                parameter.Version);
                        }

                    default:
                        throw UnknownKind(kind);
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
                    case ProcessTypeKind:
                        {
                            ProcessTypeValues values = AdminReferenceJson.Read<ProcessTypeValues>(input.Values);
                            ProcessTypeEntity type = await LockProcessTypeAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(type.Version, input.Version);
                            await CheckProcessTypeAsync(values, type, cancellationToken).ConfigureAwait(false);

                            IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                            Dictionary<string, object?> before = new()
                            {
                                [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, type.NameKey),
                                [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, type.DescriptionKey),
                                [AdminReferenceRules.ImageField] = type.ImageKey,
                                [HandlerCodeField] = type.HandlerCode,
                                [RefundsInputField] = type.RefundsInput,
                                [SortOrderField] = type.SortOrder,
                            };

                            Apply(type, values);
                            type.Version++;
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.SetAsync(
                                AdminReferenceRules.Entries(_texts, type.NameKey, type.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessTypeKind, type.Id),
                                before,
                                values.Trimmed(_texts),
                                type.Id,
                                type.Version);
                        }

                    case ProcessRecipeKind:
                        {
                            ProcessRecipeValues values = AdminReferenceJson.Read<ProcessRecipeValues>(input.Values);
                            ProcessRecipeEntity recipe = await LockProcessRecipeAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(recipe.Version, input.Version);
                            List<SubjectRequirement> requirements = await CheckProcessRecipeAsync(values, recipe, cancellationToken).ConfigureAwait(false);

                            IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                            IReadOnlyList<RequirementDefinition> storedRequirements = await _requirements.GetAsync(
                                RequirementSubjectTypes.ProcessRecipe,
                                recipe.Id,
                                1,
                                cancellationToken).ConfigureAwait(false);
                            Dictionary<string, object?> before = new()
                            {
                                [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, recipe.NameKey),
                                [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, recipe.DescriptionKey),
                                [AdminReferenceRules.ImageField] = recipe.ImageKey,
                                [ProcessTypeField] = recipe.ProcessTypeId,
                                [BuildingField] = recipe.BuildingTypeId,
                                [SourceGroupField] = recipe.SourceGroupId,
                                [TargetGroupField] = recipe.TargetGroupId,
                                [DurationField] = recipe.DurationSeconds,
                                [ShipStubField] = recipe.ShipStub,
                                [SortOrderField] = recipe.SortOrder,
                                [IngredientsField] = IngredientsOf(recipe),
                                [RequirementsField] = RequirementsOf(storedRequirements),
                            };

                            Apply(recipe, values);
                            recipe.Version++;
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _requirements.ReplaceAsync(RequirementSubjectTypes.ProcessRecipe, recipe.Id, requirements, cancellationToken).ConfigureAwait(false);

                            await _texts.SetAsync(
                                AdminReferenceRules.Entries(_texts, recipe.NameKey, recipe.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessRecipeKind, recipe.Id),
                                before,
                                values.Trimmed(_texts),
                                recipe.Id,
                                recipe.Version);
                        }

                    case ProcessParamKind:
                        {
                            ProcessParamValues values = AdminReferenceJson.Read<ProcessParamValues>(input.Values);
                            ProcessParamEntity parameter = await LockProcessParamAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(parameter.Version, input.Version);
                            await CheckProcessParamAsync(values, parameter, cancellationToken).ConfigureAwait(false);

                            IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                            Dictionary<string, object?> before = new()
                            {
                                [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, parameter.NameKey),
                                [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, parameter.DescriptionKey),
                                [ProcessTypeField] = parameter.ProcessTypeId,
                                [ParamKindField] = parameter.ParamKind,
                                [LevelSourceField] = parameter.LevelSource,
                                [LevelTargetField] = parameter.LevelTargetId,
                                [LevelField] = parameter.Level,
                                [ValueField] = parameter.Value,
                                [ValueMaxField] = parameter.ValueMax,
                                [SortOrderField] = parameter.SortOrder,
                            };

                            Apply(parameter, values);
                            parameter.Version++;
                            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                            await _texts.SetAsync(
                                AdminReferenceRules.Entries(_texts, parameter.NameKey, parameter.DescriptionKey, values.Name!, values.Description!),
                                adminId,
                                cancellationToken).ConfigureAwait(false);

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessParamKind, parameter.Id),
                                before,
                                values.Trimmed(_texts),
                                parameter.Id,
                                parameter.Version);
                        }

                    default:
                        throw UnknownKind(kind);
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
                    case ProcessTypeKind:
                        {
                            ProcessTypeEntity type = await LockProcessTypeAsync(id, cancellationToken).ConfigureAwait(false);
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
                                AdminReferenceEndpoints.Target(ProcessTypeKind, type.Id),
                                new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                                new Dictionary<string, object?> { ["is_archived"] = isArchived },
                                type.Id,
                                type.Version);
                        }

                    case ProcessRecipeKind:
                        {
                            ProcessRecipeEntity recipe = await LockProcessRecipeAsync(id, cancellationToken).ConfigureAwait(false);
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
                                AdminReferenceEndpoints.Target(ProcessRecipeKind, recipe.Id),
                                new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                                new Dictionary<string, object?> { ["is_archived"] = isArchived },
                                recipe.Id,
                                recipe.Version);
                        }

                    case ProcessParamKind:
                        {
                            ProcessParamEntity parameter = await LockProcessParamAsync(id, cancellationToken).ConfigureAwait(false);
                            AdminReferenceRules.CheckVersion(parameter.Version, input.Version);

                            if (parameter.IsArchived == isArchived)
                            {
                                throw new AdminReferenceException(
                                    GameErrorCodes.ValidationFailed,
                                    new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                            }

                            parameter.IsArchived = isArchived;
                            parameter.Version++;

                            return AdminReferenceRules.Change(
                                AdminReferenceEndpoints.Target(ProcessParamKind, parameter.Id),
                                new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                                new Dictionary<string, object?> { ["is_archived"] = isArchived },
                                parameter.Id,
                                parameter.Version);
                        }

                    default:
                        throw UnknownKind(kind);
                }
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    /// <summary>
    /// Checks the values of a kind of a process: the texts, the handler of the kind, the order
    /// in the lists and the picture.
    /// </summary>
    /// <param name="values">The values the administrator sent.</param>
    /// <param name="stored">The row as it is now, or <see langword="null"/> for a creation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the values are checked.</returns>
    /// <exception cref="AdminReferenceException">A value breaks a rule.</exception>
    private async Task CheckProcessTypeAsync(ProcessTypeValues values, ProcessTypeEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        if (values.HandlerCode is null)
        {
            AdminReferenceRules.Fail(failures, HandlerCodeField, AdminReferenceErrors.Required);
        }
        else if (!ProcessHandlerCodes.All.Contains(values.HandlerCode, StringComparer.Ordinal))
        {
            // A logical code is one the program knows: the engine of the processes looks the
            // handler up by it, so an unknown code would never run.
            AdminReferenceRules.Fail(failures, HandlerCodeField, AdminReferenceErrors.Unknown);
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Checks the values of a recipe of a process and builds its requirements.
    /// </summary>
    /// <param name="values">The values the administrator sent.</param>
    /// <param name="stored">The row as it is now, or <see langword="null"/> for a creation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requirements of the recipe.</returns>
    /// <exception cref="AdminReferenceException">A value breaks a rule.</exception>
    private async Task<List<SubjectRequirement>> CheckProcessRecipeAsync(
        ProcessRecipeValues values,
        ProcessRecipeEntity? stored,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        await CheckProcessTypeReferenceAsync(failures, values.ProcessTypeId, stored?.ProcessTypeId, cancellationToken).ConfigureAwait(false);

        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ResourceTypeFact> resourcesById = resources.Resources.ToDictionary(resource => resource.Id);
        Dictionary<int, ResourceGroupFact> groupsById = resources.Groups.ToDictionary(group => group.Id);
        HashSet<int> buildingIds = [.. (await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false)).Keys];

        if (values.BuildingTypeId is not { } buildingTypeId)
        {
            AdminReferenceRules.Fail(failures, BuildingField, AdminReferenceErrors.Required);
        }
        else if (!buildingIds.Contains(buildingTypeId))
        {
            AdminReferenceRules.Fail(failures, BuildingField, AdminReferenceErrors.Unknown);
        }

        CheckGroup(failures, SourceGroupField, values.SourceGroupId, groupsById);
        CheckGroup(failures, TargetGroupField, values.TargetGroupId, groupsById);

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

        // The ingredients are the cost of one unit of the process: each a resource or a part,
        // each once.
        HashSet<int> storedResourceIds = [.. (stored?.IngredientIds ?? [])
            .Where(ingredient => ingredient.ResourceTypeId is not null)
            .Select(ingredient => ingredient.ResourceTypeId!.Value)];
        HashSet<(int? ResourceTypeId, int? PartTypeId)> seenIngredients = [];
        int index = 0;

        foreach (IngredientValues ingredient in values.Ingredients ?? [])
        {
            string resourcePath = AdminReferenceRules.PathOf(IngredientsField, index, ResourceField);
            string partPath = AdminReferenceRules.PathOf(IngredientsField, index, PartField);

            if (ingredient.Amount is null or < 1)
            {
                AdminReferenceRules.Fail(
                    failures,
                    AdminReferenceRules.PathOf(IngredientsField, index, AmountField),
                    ingredient.Amount is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
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
                CheckPart(failures, partPath, ingredientPartId);
            }

            index++;
        }

        // The requirements of the recipe use the shared field and «only the race X», so the same
        // kinds of requirement are offered as for the buildings.
        List<SubjectRequirement> requirements = [];
        IReadOnlySet<int> technologyIds = await _technologies.GetTechnologyIdsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RaceDto> races = await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false);
        int requirementIndex = 0;

        foreach (RequirementValues requirement in values.Requirements ?? [])
        {
            string path = AdminReferenceRules.PathOf(RequirementsField, requirementIndex, RequirementTypeField);

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

    /// <summary>
    /// Checks the values of a parameter of a process: the texts, the kind of the parameter, the
    /// source of its level, the level and the values.
    /// </summary>
    /// <param name="values">The values the administrator sent.</param>
    /// <param name="stored">The row as it is now, or <see langword="null"/> for a creation.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the values are checked.</returns>
    /// <exception cref="AdminReferenceException">A value breaks a rule.</exception>
    private async Task CheckProcessParamAsync(ProcessParamValues values, ProcessParamEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        await CheckProcessTypeReferenceAsync(failures, values.ProcessTypeId, stored?.ProcessTypeId, cancellationToken).ConfigureAwait(false);

        if (values.ParamKind is null)
        {
            AdminReferenceRules.Fail(failures, ParamKindField, AdminReferenceErrors.Required);
        }
        else if (!ParamKinds.Contains(values.ParamKind, StringComparer.Ordinal))
        {
            AdminReferenceRules.Fail(failures, ParamKindField, AdminReferenceErrors.Unknown);
        }

        if (values.LevelSource is null)
        {
            AdminReferenceRules.Fail(failures, LevelSourceField, AdminReferenceErrors.Required);
        }
        else if (!LevelSources.Contains(values.LevelSource, StringComparer.Ordinal))
        {
            AdminReferenceRules.Fail(failures, LevelSourceField, AdminReferenceErrors.Unknown);
        }

        if (values.LevelTargetId is null)
        {
            AdminReferenceRules.Fail(failures, LevelTargetField, AdminReferenceErrors.Required);
        }
        else if (values.LevelTargetId < 1)
        {
            AdminReferenceRules.Fail(failures, LevelTargetField, AdminReferenceErrors.OutOfBounds);
        }

        if (values.Level is null)
        {
            AdminReferenceRules.Fail(failures, LevelField, AdminReferenceErrors.Required);
        }
        else if (values.Level < 1)
        {
            AdminReferenceRules.Fail(failures, LevelField, AdminReferenceErrors.OutOfBounds);
        }

        if (values.Value is null)
        {
            AdminReferenceRules.Fail(failures, ValueField, AdminReferenceErrors.Required);
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Checks the kind of a process a row names: it must exist, and an archived kind keeps its
    /// rows but is not offered for a new one (task 00023, decision 5).
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="processTypeId">The kind the administrator sent.</param>
    /// <param name="storedProcessTypeId">The kind the row has now.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the kind is checked.</returns>
    private async Task CheckProcessTypeReferenceAsync(
        Dictionary<string, List<string>> failures,
        int? processTypeId,
        int? storedProcessTypeId,
        CancellationToken cancellationToken)
    {
        if (processTypeId is null)
        {
            AdminReferenceRules.Fail(failures, ProcessTypeField, AdminReferenceErrors.Required);

            return;
        }

        ProcessTypeEntity? processType = await _dbContext.ProcessTypes.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == processTypeId, cancellationToken).ConfigureAwait(false);

        if (processType is null)
        {
            AdminReferenceRules.Fail(failures, ProcessTypeField, AdminReferenceErrors.Unknown);
        }
        else if (processType.IsArchived && processType.Id != storedProcessTypeId)
        {
            AdminReferenceRules.Fail(failures, ProcessTypeField, AdminReferenceErrors.Archived);
        }
    }

    /// <summary>
    /// Checks the group of the resources a recipe names.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="path">The path of the field.</param>
    /// <param name="groupId">The group the administrator sent.</param>
    /// <param name="groupsById">The groups of the resources that exist.</param>
    private static void CheckGroup(
        Dictionary<string, List<string>> failures,
        string path,
        int? groupId,
        Dictionary<int, ResourceGroupFact> groupsById)
    {
        if (groupId is null)
        {
            return;
        }

        if (!groupsById.TryGetValue(groupId.Value, out ResourceGroupFact? group))
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
        }
        else if (group.IsArchived)
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Archived);
        }
    }

    /// <summary>
    /// Checks the resource an ingredient takes.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="path">The path of the field.</param>
    /// <param name="resourceTypeId">The resource the administrator sent.</param>
    /// <param name="resourcesById">The resources that exist.</param>
    /// <param name="storedResourceIds">The resources the recipe already takes.</param>
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

    /// <summary>
    /// Checks the part an ingredient takes. The parts belong to the production module and it
    /// has no contract that lists them, so the module checks the identifier and the module that
    /// owns the parts checks the part itself.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="path">The path of the field.</param>
    /// <param name="partTypeId">The part the administrator sent.</param>
    private static void CheckPart(Dictionary<string, List<string>> failures, string path, int partTypeId)
    {
        if (partTypeId < 1)
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.OutOfBounds);
        }
    }

    /// <summary>
    /// Writes the sent values into a kind of a process.
    /// </summary>
    /// <param name="type">The row.</param>
    /// <param name="values">The values the administrator sent.</param>
    private static void Apply(ProcessTypeEntity type, ProcessTypeValues values)
    {
        type.HandlerCode = values.HandlerCode!;
        type.RefundsInput = values.RefundsInput ?? type.RefundsInput;
        type.SortOrder = values.SortOrder ?? 0;
        type.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
    }

    /// <summary>
    /// Writes the sent values into a recipe of a process. The ingredients belong to the recipe,
    /// so a kept ingredient keeps its row and the others are added or removed with it.
    /// </summary>
    /// <param name="recipe">The row.</param>
    /// <param name="values">The values the administrator sent.</param>
    private static void Apply(ProcessRecipeEntity recipe, ProcessRecipeValues values)
    {
        recipe.ProcessTypeId = values.ProcessTypeId!.Value;
        recipe.BuildingTypeId = values.BuildingTypeId;
        recipe.SourceGroupId = values.SourceGroupId;
        recipe.TargetGroupId = values.TargetGroupId;
        recipe.DurationSeconds = values.DurationSeconds ?? 0;
        recipe.ShipStub = string.IsNullOrWhiteSpace(values.ShipStub) ? null : values.ShipStub.Trim();
        recipe.SortOrder = values.SortOrder ?? 0;
        recipe.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);

        recipe.IngredientIds.RemoveAll(ingredient => !(values.Ingredients ?? []).Any(sent =>
            sent.ResourceTypeId == ingredient.ResourceTypeId && sent.PartTypeId == ingredient.PartTypeId));

        foreach (IngredientValues sent in values.Ingredients ?? [])
        {
            ProcessRecipeIngredientEntity? kept = recipe.IngredientIds.Find(ingredient =>
                ingredient.ResourceTypeId == sent.ResourceTypeId && ingredient.PartTypeId == sent.PartTypeId);

            if (kept is null)
            {
                recipe.IngredientIds.Add(new ProcessRecipeIngredientEntity
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

    /// <summary>
    /// Writes the sent values into a parameter of a process.
    /// </summary>
    /// <param name="parameter">The row.</param>
    /// <param name="values">The values the administrator sent.</param>
    private static void Apply(ProcessParamEntity parameter, ProcessParamValues values)
    {
        parameter.ProcessTypeId = values.ProcessTypeId!.Value;
        parameter.ParamKind = values.ParamKind!;
        parameter.LevelSource = values.LevelSource!;
        parameter.LevelTargetId = values.LevelTargetId;
        parameter.Level = values.Level!.Value;
        parameter.Value = values.Value!.Value;
        parameter.ValueMax = values.ValueMax;
        parameter.SortOrder = values.SortOrder ?? 0;
    }

    /// <summary>
    /// Locks the row of a kind of a process, so two changes of one row compare their versions
    /// one after the other and the second one meets the new version (task 00023, decision 5).
    /// </summary>
    /// <param name="id">The row.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The locked row.</returns>
    /// <exception cref="AdminReferenceException">The row does not exist.</exception>
    private async Task<ProcessTypeEntity> LockProcessTypeAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM process_type WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.ProcessTypes.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    /// <summary>
    /// Locks the row of a recipe of a process with its ingredients.
    /// </summary>
    /// <param name="id">The row.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The locked row.</returns>
    /// <exception cref="AdminReferenceException">The row does not exist.</exception>
    private async Task<ProcessRecipeEntity> LockProcessRecipeAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM process_recipe WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.ProcessRecipes.Include(entry => entry.IngredientIds).SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    /// <summary>
    /// Locks the row of a parameter of a process.
    /// </summary>
    /// <param name="id">The row.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The locked row.</returns>
    /// <exception cref="AdminReferenceException">The row does not exist.</exception>
    private async Task<ProcessParamEntity> LockProcessParamAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM process_param WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.ProcessParams.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    /// <summary>
    /// Builds the refusal of a kind the module does not own.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The refusal.</returns>
    private static ArgumentException UnknownKind(string kind) =>
        new($"The processes module has no reference kind '{kind}'.", nameof(kind));

    /// <summary>
    /// Builds the ingredients of a recipe for the form.
    /// </summary>
    /// <param name="recipe">The row.</param>
    /// <returns>The ingredients by their columns.</returns>
    private static List<object?> IngredientsOf(ProcessRecipeEntity recipe) =>
    [
        .. recipe.IngredientIds
            .OrderBy(ingredient => ingredient.Id)
            .Select(ingredient => (object?)new Dictionary<string, object?>
            {
                [ResourceField] = ingredient.ResourceTypeId,
                [PartField] = ingredient.PartTypeId,
                [AmountField] = ingredient.Amount,
            }),
    ];

    /// <summary>
    /// Builds the requirements of a recipe for the form.
    /// </summary>
    /// <param name="requirements">The requirements of the recipe.</param>
    /// <returns>The requirements by their columns.</returns>
    private static List<object?> RequirementsOf(IReadOnlyList<RequirementDefinition> requirements) =>
    [
        .. requirements.Select(requirement => (object?)new Dictionary<string, object?>
        {
            [RequirementTypeField] = requirement.RequirementType,
            ["target_id"] = requirement.TargetId,
            ["target_level"] = requirement.TargetLevel,
        }),
    ];

    /// <summary>
    /// Drops the copies of the catalog and of the texts after the change is committed, so a
    /// player and the form see it at once.
    /// </summary>
    private void Committed()
    {
        _catalogCache.Invalidate();
        _texts.Invalidate();
    }

    /// <summary>The values of the form of a kind of a process.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="HandlerCode">The logical code of the handler of the kind.</param>
    /// <param name="RefundsInput">Whether cancelling an order of the kind returns its input.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    private sealed record ProcessTypeValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        string? HandlerCode,
        bool? RefundsInput,
        int? SortOrder,
        string? ImageKey)
    {
        /// <summary>
        /// Trims the texts of the name and of the description in every language.
        /// </summary>
        /// <param name="texts">The texts of the game data.</param>
        /// <returns>The values with the trimmed texts.</returns>
        public ProcessTypeValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };
    }

    /// <summary>The values of the form of a recipe of a process.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="ProcessTypeId">The kind of the process the recipe belongs to.</param>
    /// <param name="BuildingTypeId">The building the process runs in.</param>
    /// <param name="SourceGroupId">The group of the resources the process takes.</param>
    /// <param name="TargetGroupId">The group of the resources the process gives.</param>
    /// <param name="DurationSeconds">How long one unit of the process takes, in game seconds.</param>
    /// <param name="ShipStub">The stub of the ship on the orbit of the body.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="Ingredients">The ingredients of one unit of the process.</param>
    /// <param name="Requirements">The requirements of the recipe.</param>
    private sealed record ProcessRecipeValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? ProcessTypeId,
        int? BuildingTypeId,
        int? SourceGroupId,
        int? TargetGroupId,
        long? DurationSeconds,
        string? ShipStub,
        int? SortOrder,
        string? ImageKey,
        List<IngredientValues>? Ingredients,
        List<RequirementValues>? Requirements)
    {
        /// <summary>
        /// Trims the texts of the name and of the description in every language.
        /// </summary>
        /// <param name="texts">The texts of the game data.</param>
        /// <returns>The values with the trimmed texts.</returns>
        public ProcessRecipeValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };
    }

    /// <summary>The values of the form of a parameter of a process.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="ProcessTypeId">The kind of the process the parameter belongs to.</param>
    /// <param name="ParamKind">The logical kind of the parameter.</param>
    /// <param name="LevelSource">What the level of the parameter follows.</param>
    /// <param name="LevelTargetId">The thing whose level the parameter follows.</param>
    /// <param name="Level">The level the parameter starts at.</param>
    /// <param name="Value">The value of the parameter.</param>
    /// <param name="ValueMax">The second value of the parameter, for the ranges.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    private sealed record ProcessParamValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? ProcessTypeId,
        string? ParamKind,
        string? LevelSource,
        int? LevelTargetId,
        int? Level,
        decimal? Value,
        decimal? ValueMax,
        int? SortOrder)
    {
        /// <summary>
        /// Trims the texts of the name and of the description in every language.
        /// </summary>
        /// <param name="texts">The texts of the game data.</param>
        /// <returns>The values with the trimmed texts.</returns>
        public ProcessParamValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };
    }

    /// <summary>An ingredient of a recipe of a process.</summary>
    /// <param name="ResourceTypeId">The resource the ingredient takes.</param>
    /// <param name="PartTypeId">The part the ingredient takes.</param>
    /// <param name="Amount">How many units one unit of the process takes.</param>
    private sealed record IngredientValues(int? ResourceTypeId, int? PartTypeId, long? Amount);

    /// <summary>A requirement of a recipe of a process.</summary>
    /// <param name="RequirementType">The requirement type.</param>
    /// <param name="TargetId">The target.</param>
    /// <param name="TargetLevel">The required level of the target.</param>
    private sealed record RequirementValues(string? RequirementType, int? TargetId, int? TargetLevel);
}

using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The reference kinds of the world in the admin panel: the resources and the resource groups
/// (task 00022, decision 6; task 00023, section 4.3).
/// </summary>
/// <remarks>
/// <para>
/// The server gives the neutral code, the administrator never types it. A new row comes with its
/// name and description in every language of the texts; the row and its texts are committed
/// together, so a row never exists without its name.
/// </para>
/// <para>
/// A row is never deleted: an archived resource stays in the stocks and the costs it already has,
/// but no new level, recipe or generation rule gets it; an archived group gets no new resource.
/// </para>
/// </remarks>
public sealed class WorldReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the resources.</summary>
    public const string ResourceKind = "resource";

    /// <summary>The kind of the resource groups.</summary>
    public const string ResourceGroupKind = "resource_group";

    /// <summary>The kind of the races (task 00042).</summary>
    public const string RaceKind = "race";

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.World.admin-reference-texts.json";

    private const string GroupField = "resource_group_id";

    private const string AllowedOnPlanetField = "allowed_on_planet";

    private const string ScopeField = "scope";

    private const string EarnsPopulationIncomeField = "earns_population_income";

    private const string AllowsDebtField = "allows_debt";

    private const string PaysPortalDeliveryField = "pays_portal_delivery";

    private const string PaidByPopulationField = "paid_by_population";

    private const string IsBalanceField = "is_balance";

    private const string IconShapeField = "icon_shape";

    private const string IconToneField = "icon_tone";

    private const string WeightField = "weight";

    private const string SortOrderField = "sort_order";

    private const string ColorKeyField = "color_key";

    private const string MoodField = "mood";

    private const string TraitsField = "traits";

    private const string LabelPrefix = "admin.reference.";

    /// <summary>The place of the resources in the menu of the reference kinds.</summary>
    private const int ResourceSortOrder = 10;

    /// <summary>The place of the resource groups in the menu of the reference kinds.</summary>
    private const int ResourceGroupSortOrder = 20;

    /// <summary>The place of the races in the menu of the reference kinds.</summary>
    private const int RaceSortOrder = 25;

    /// <summary>
    /// The level of the start building of a race that the admin panel creates. The building
    /// itself is given by the bodies module at the start of the world.
    /// </summary>
    private const int RaceStartBuildingLevel = 1;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(WorldReferenceAdminService).Assembly, LabelTextsResource);

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IReadOnlyList<IResourceUsage> _usages;
    private readonly IGameConfig _config;
    private readonly ResourceCatalogCache _resourceCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="usages">The modules that hold data of a resource, asked before its scope changes.</param>
    /// <param name="config">The game configuration, for the default weight of a resource.</param>
    /// <param name="resourceCatalog">The resource catalog kept in memory, dropped when a resource changes.</param>
    public WorldReferenceAdminService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        IEnumerable<IResourceUsage> usages,
        IGameConfig config,
        ResourceCatalogCache resourceCatalog)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(usages);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(resourceCatalog);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
        _usages = [.. usages];
        _config = config;
        _resourceCatalog = resourceCatalog;
    }

    /// <summary>
    /// Gets the kind of the resources.
    /// </summary>
    public static AdminReferenceDescriptor ResourceDescriptor { get; } = new(
        ResourceKind,
        WorldCodeGroups.Resource,
        WorldTextKeys.Resource,
        LabelPrefix + ResourceKind + ".title",
        ResourceSortOrder,
        "/world/resources",
        new AdminReferencePermissions(
            "admin.world_resources_read",
            "admin.world_resource_read",
            "admin.world_reference_row_added",
            "admin.world_resource_updated",
            "admin.world_resource_archived",
            "admin.world_resource_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(GroupField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource_group", InList: true, ReferenceKind: ResourceGroupKind),
            new(
                ScopeField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.scope",
                IsRequired: true,
                InList: true,
                Options: [.. ResourceScopes.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "scope." + value))]),
            new(EarnsPopulationIncomeField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.earns_population_income", InList: true),
            new(AllowsDebtField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.allows_debt", InList: true),
            new(PaysPortalDeliveryField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.pays_portal_delivery", InList: true),
            new(PaidByPopulationField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.paid_by_population", InList: true),
            new(WeightField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.weight", IsRequired: true, InList: true, Minimum: 0, IsDecimal: true),
        ]);

    /// <summary>
    /// Gets the kind of the resource groups.
    /// </summary>
    public static AdminReferenceDescriptor ResourceGroupDescriptor { get; } = new(
        ResourceGroupKind,
        WorldCodeGroups.ResourceGroup,
        WorldTextKeys.ResourceGroup,
        LabelPrefix + ResourceGroupKind + ".title",
        ResourceGroupSortOrder,
        "/world/resource-groups",
        new AdminReferencePermissions(
            "admin.world_resource_groups_read",
            "admin.world_resource_group_read",
            "admin.world_resource_group_added",
            "admin.world_resource_group_updated",
            "admin.world_resource_group_archived",
            "admin.world_resource_group_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(AllowedOnPlanetField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.allowed_on_planet", InList: true),
            new(IsBalanceField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.is_balance", InList: true),
            new(
                IconShapeField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.icon_shape",
                InList: true,
                Options: [.. ResourceGroupIcons.Shapes.Select(value => new AdminReferenceOption(value, LabelPrefix + "icon_shape." + value))]),
            new(
                IconToneField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.icon_tone",
                InList: true,
                Options: [.. ResourceGroupIcons.Tones.Select(value => new AdminReferenceOption(value, LabelPrefix + "icon_tone." + value))]),
        ]);

    /// <summary>
    /// Gets the kind of the races.
    /// </summary>
    /// <remarks>
    /// The race is a row of the catalog: a new race is a new row, and the parts of the race are
    /// bound to it in the admin panel of the parts (task 00042).
    /// </remarks>
    public static AdminReferenceDescriptor RaceDescriptor { get; } = new(
        RaceKind,
        WorldCodeGroups.Race,
        WorldTextKeys.Race,
        LabelPrefix + RaceKind + ".title",
        RaceSortOrder,
        "/world/races",
        new AdminReferencePermissions(
            "admin.world_races_read",
            "admin.world_race_read",
            "admin.world_race_added",
            "admin.world_race_updated",
            "admin.world_race_archived",
            "admin.world_race_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                MoodField,
                AdminReferenceFieldTypes.Text,
                LabelPrefix + "field.mood",
                IsRequired: true,
                HasLanguages: true),
            new(
                TraitsField,
                AdminReferenceFieldTypes.Effects,
                LabelPrefix + "field.traits",
                Fields:
                [
                    new("trait", AdminReferenceFieldTypes.Text, LabelPrefix + "field.trait", IsRequired: true, HasLanguages: true),
                ]),
            new(ColorKeyField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.color_key", InList: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [ResourceDescriptor, ResourceGroupDescriptor, RaceDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        List<AdminReferenceRow> rows = kind switch
        {
            ResourceKind => [.. (await _dbContext.ResourceTypes.AsNoTracking().OrderBy(resource => resource.Id).ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(resource => new AdminReferenceRow(
                    resource.Id,
                    resource.Code,
                    resource.NameKey,
                    resource.ImageKey,
                    resource.IsArchived,
                    resource.Version,
                    new Dictionary<string, object?>
                    {
                        [GroupField] = resource.ResourceGroupId,
                        [ScopeField] = resource.Scope,
                        [EarnsPopulationIncomeField] = resource.EarnsPopulationIncome,
                        [AllowsDebtField] = resource.AllowsDebt,
                        [PaysPortalDeliveryField] = resource.PaysPortalDelivery,
                        [PaidByPopulationField] = resource.PaidByPopulation,
                        [WeightField] = resource.Weight,
                    }))],
            ResourceGroupKind => [.. (await _dbContext.ResourceGroups.AsNoTracking().OrderBy(group => group.Id).ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(group => new AdminReferenceRow(
                    group.Id,
                    group.Code,
                    group.NameKey,
                    group.ImageKey,
                    group.IsArchived,
                    group.Version,
                    new Dictionary<string, object?>
                    {
                        [AllowedOnPlanetField] = group.AllowedOnPlanet,
                        [IsBalanceField] = group.IsBalance,
                        [IconShapeField] = group.IconShape,
                        [IconToneField] = group.IconTone,
                    }))],
            RaceKind => [.. (await _dbContext.Races.AsNoTracking().OrderBy(race => race.Id).ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(race => new AdminReferenceRow(
                    race.Id,
                    race.Code,
                    race.NameKey,
                    race.ImageKey,
                    race.IsArchived,
                    race.Version,
                    new Dictionary<string, object?>
                    {
                        [ColorKeyField] = race.ColorKey,
                        [SortOrderField] = race.SortOrder,
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
            case ResourceKind:
                ResourceTypeEntity? resource = await _dbContext.ResourceTypes.AsNoTracking()
                    .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                return resource is null ? null : new AdminReferenceRecord(
                    resource.Id,
                    resource.Code,
                    resource.NameKey,
                    resource.DescriptionKey,
                    resource.ImageKey,
                    resource.IsArchived,
                    resource.Version,
                    ResourceValues.Of(resource, packs).ToDictionary());

            case ResourceGroupKind:
                ResourceGroupEntity? group = await _dbContext.ResourceGroups.AsNoTracking()
                    .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                return group is null ? null : new AdminReferenceRecord(
                    group.Id,
                    group.Code,
                    group.NameKey,
                    group.DescriptionKey,
                    group.ImageKey,
                    group.IsArchived,
                    group.Version,
                    ResourceGroupValues.Of(group, packs).ToDictionary());

            case RaceKind:
                RaceEntity? race = await _dbContext.Races.AsNoTracking()
                    .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                return race is null ? null : new AdminReferenceRecord(
                    race.Id,
                    race.Code,
                    race.NameKey,
                    race.DescriptionKey,
                    race.ImageKey,
                    race.IsArchived,
                    race.Version,
                    RaceValues.Of(race, packs).ToDictionary());

            default:
                throw UnknownKind(kind);
        }
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> CreateAsync(string kind, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);

        AdminReferenceChange change = kind switch
        {
            ResourceKind => await CreateResourceAsync(AdminReferenceJson.Read<ResourceValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            ResourceGroupKind => await CreateGroupAsync(AdminReferenceJson.Read<ResourceGroupValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            RaceKind => await CreateRaceAsync(AdminReferenceJson.Read<RaceValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        // The row and its texts are committed: the copy of the texts in memory is dropped.
        _texts.Invalidate();
        _resourceCatalog.Invalidate();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> UpdateAsync(string kind, int id, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);

        AdminReferenceChange change = kind switch
        {
            ResourceKind => await UpdateResourceAsync(id, input.Version, AdminReferenceJson.Read<ResourceValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            ResourceGroupKind => await UpdateGroupAsync(id, input.Version, AdminReferenceJson.Read<ResourceGroupValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            RaceKind => await UpdateRaceAsync(id, input.Version, AdminReferenceJson.Read<RaceValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        _texts.Invalidate();
        _resourceCatalog.Invalidate();

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
                    case ResourceKind:
                        ResourceTypeEntity resource = await LockResourceAsync(id, cancellationToken).ConfigureAwait(false);
                        CheckState(resource.Version, resource.IsArchived, input.Version, isArchived);
                        resource.IsArchived = isArchived;
                        resource.Version++;

                        return Archived(kind, resource.Id, resource.Version, isArchived);

                    case ResourceGroupKind:
                        ResourceGroupEntity group = await LockGroupAsync(id, cancellationToken).ConfigureAwait(false);
                        CheckState(group.Version, group.IsArchived, input.Version, isArchived);
                        group.IsArchived = isArchived;
                        group.Version++;

                        return Archived(kind, group.Id, group.Version, isArchived);

                    case RaceKind:
                        RaceEntity race = await LockRaceAsync(id, cancellationToken).ConfigureAwait(false);
                        CheckState(race.Version, race.IsArchived, input.Version, isArchived);
                        race.IsArchived = isArchived;
                        race.Version++;

                        return Archived(kind, race.Id, race.Version, isArchived);

                    default:
                        throw UnknownKind(kind);
                }
            },
            cancellationToken).ConfigureAwait(false);

        _resourceCatalog.Invalidate();

        return change;
    }

    /// <summary>
    /// Refuses a change from an older version and an archiving of an archived row (or a return of
    /// an active one).
    /// </summary>
    /// <param name="storedVersion">The version of the row.</param>
    /// <param name="storedArchived">Whether the row is archived.</param>
    /// <param name="sentVersion">The version the administrator read.</param>
    /// <param name="isArchived">The asked state.</param>
    internal static void CheckState(int storedVersion, bool storedArchived, int sentVersion, bool isArchived)
    {
        AdminReferenceRules.CheckVersion(storedVersion, sentVersion);

        if (storedArchived == isArchived)
        {
            throw new AdminReferenceException(
                GameErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
        }
    }

    private static AdminReferenceChange Archived(string kind, int id, int version, bool isArchived) =>
        AdminReferenceRules.Change(
            AdminReferenceEndpoints.Target(kind, id),
            new Dictionary<string, object?> { ["is_archived"] = !isArchived },
            new Dictionary<string, object?> { ["is_archived"] = isArchived },
            id,
            version);

    private static ArgumentException UnknownKind(string kind) => new($"The world has no reference kind '{kind}'.", nameof(kind));

    private Task<AdminReferenceChange> CreateResourceAsync(ResourceValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckResourceAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(WorldCodeGroups.Resource, cancellationToken).ConfigureAwait(false);
                ResourceTypeEntity resource = new()
                {
                    Code = code,
                    ResourceGroupId = values.ResourceGroupId,
                    NameKey = WorldTextKeys.Name(WorldTextKeys.Resource, code),
                    DescriptionKey = WorldTextKeys.Description(WorldTextKeys.Resource, code),
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                    Scope = values.Scope,
                    EarnsPopulationIncome = values.EarnsPopulationIncome,
                    AllowsDebt = values.AllowsDebt,
                    PaysPortalDelivery = values.PaysPortalDelivery,
                    PaidByPopulation = values.PaidByPopulation,
                    Weight = values.Weight ?? DefaultWeight(),
                };

                _dbContext.ResourceTypes.Add(resource);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, resource.NameKey, resource.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(ResourceKind, resource.Id),
                    null,
                    new { code, values = ResourceValues.Of(resource, values.Trimmed(_texts)) },
                    resource.Id,
                    resource.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateResourceAsync(int id, int version, ResourceValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ResourceTypeEntity resource = await LockResourceAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(resource.Version, version);
                await CheckResourceAsync(values, resource, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                ResourceValues before = ResourceValues.Of(resource, packs);

                resource.ResourceGroupId = values.ResourceGroupId;
                resource.Scope = values.Scope;
                resource.EarnsPopulationIncome = values.EarnsPopulationIncome;
                resource.AllowsDebt = values.AllowsDebt;
                resource.PaysPortalDelivery = values.PaysPortalDelivery;
                resource.PaidByPopulation = values.PaidByPopulation;
                resource.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                resource.Weight = values.Weight ?? DefaultWeight();
                resource.Version++;

                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, resource.NameKey, resource.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(ResourceKind, resource.Id),
                    before,
                    ResourceValues.Of(resource, values.Trimmed(_texts)),
                    resource.Id,
                    resource.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreateGroupAsync(ResourceGroupValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                Dictionary<string, List<string>> failures = [];
                AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);
                await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), null, cancellationToken).ConfigureAwait(false);
                CheckGroupIcon(failures, values);
                AdminReferenceRules.ThrowIfInvalid(failures);

                string code = await _codes.NextAsync(WorldCodeGroups.ResourceGroup, cancellationToken).ConfigureAwait(false);
                ResourceGroupEntity group = new()
                {
                    Code = code,
                    NameKey = WorldTextKeys.Name(WorldTextKeys.ResourceGroup, code),
                    DescriptionKey = WorldTextKeys.Description(WorldTextKeys.ResourceGroup, code),
                    AllowedOnPlanet = values.AllowedOnPlanet,
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                    IsBalance = values.IsBalance,
                    IconShape = Blank(values.IconShape),
                    IconTone = Blank(values.IconTone),
                };

                _dbContext.ResourceGroups.Add(group);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, group.NameKey, group.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(ResourceGroupKind, group.Id),
                    null,
                    new { code, values = ResourceGroupValues.Of(group, values.Trimmed(_texts)) },
                    group.Id,
                    group.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateGroupAsync(int id, int version, ResourceGroupValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ResourceGroupEntity group = await LockGroupAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(group.Version, version);

                Dictionary<string, List<string>> failures = [];
                AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);
                await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), group.ImageKey, cancellationToken).ConfigureAwait(false);
                CheckGroupIcon(failures, values);
                AdminReferenceRules.ThrowIfInvalid(failures);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                ResourceGroupValues before = ResourceGroupValues.Of(group, packs);

                group.AllowedOnPlanet = values.AllowedOnPlanet;
                group.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                group.IsBalance = values.IsBalance;
                group.IconShape = Blank(values.IconShape);
                group.IconTone = Blank(values.IconTone);
                group.Version++;

                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, group.NameKey, group.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(ResourceGroupKind, group.Id),
                    before,
                    ResourceGroupValues.Of(group, values.Trimmed(_texts)),
                    group.Id,
                    group.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreateRaceAsync(RaceValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                Dictionary<string, List<string>> failures = [];
                AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);
                AdminReferenceRules.CheckTexts(failures, _texts, values.Mood, values.Mood);
                CheckRaceTraits(failures, values);
                await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), null, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.ThrowIfInvalid(failures);

                string code = await _codes.NextAsync(WorldCodeGroups.Race, cancellationToken).ConfigureAwait(false);
                RaceEntity race = new()
                {
                    Code = code,
                    NameKey = WorldTextKeys.Name(WorldTextKeys.Race, code),
                    DescriptionKey = WorldTextKeys.Description(WorldTextKeys.Race, code),
                    MoodKey = WorldTextKeys.Of(WorldTextKeys.Race, code, WorldTextKeys.MoodPart),
                    TraitKeys = [.. (values.Traits ?? []).Select((_, index) => TraitKey(code, index))],
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                    ColorKey = Blank(values.ColorKey),
                    SortOrder = values.SortOrder,
                    StartBuildingLevel = RaceStartBuildingLevel,
                };

                _dbContext.Races.Add(race);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    RaceValues.Entries(_texts, race, values),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(RaceKind, race.Id),
                    null,
                    new { code, values = RaceValues.Of(race, values.Trimmed(_texts)) },
                    race.Id,
                    race.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateRaceAsync(int id, int version, RaceValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                RaceEntity race = await LockRaceAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(race.Version, version);

                Dictionary<string, List<string>> failures = [];
                AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);
                AdminReferenceRules.CheckTexts(failures, _texts, values.Mood, values.Mood);
                CheckRaceTraits(failures, values);
                await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), race.ImageKey, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.ThrowIfInvalid(failures);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                RaceValues before = RaceValues.Of(race, packs);

                race.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                race.ColorKey = Blank(values.ColorKey);
                race.SortOrder = values.SortOrder;
                race.MoodKey = WorldTextKeys.Of(WorldTextKeys.Race, race.Code, WorldTextKeys.MoodPart);
                race.TraitKeys = [.. (values.Traits ?? []).Select((_, index) => TraitKey(race.Code, index))];
                race.Version++;

                await _texts.SetAsync(
                    RaceValues.Entries(_texts, race, values),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(RaceKind, race.Id),
                    before,
                    RaceValues.Of(race, values.Trimmed(_texts)),
                    race.Id,
                    race.Version);
            },
            cancellationToken);

    /// <summary>
    /// Checks the strengths of a race: every one of them has a text in every language.
    /// </summary>
    private void CheckRaceTraits(Dictionary<string, List<string>> failures, RaceValues values)
    {
        foreach (RaceTraitValues trait in values.Traits ?? [])
        {
            AdminReferenceRules.CheckTexts(failures, _texts, trait.Trait, trait.Trait);
        }
    }

    /// <summary>
    /// The key of the text of one trait of a race.
    /// </summary>
    private static string TraitKey(string raceCode, int index) =>
        WorldTextKeys.Of(WorldTextKeys.Race, raceCode, WorldTextKeys.TraitPart + (index + 1).ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Checks the values of a resource: the texts, a known group that is not archived (the group
    /// it already has is kept even when it is archived) and a stored picture.
    /// </summary>
    private async Task CheckResourceAsync(ResourceValues values, ResourceTypeEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        if (!ResourceScopes.IsKnown(values.Scope))
        {
            AdminReferenceRules.Fail(failures, ScopeField, AdminReferenceErrors.Unknown);
        }

        if (values.Weight is < 0)
        {
            AdminReferenceRules.Fail(failures, WeightField, AdminReferenceErrors.OutOfBounds);
        }

        ResourceGroupEntity? group = values.ResourceGroupId is { } groupId
            ? await _dbContext.ResourceGroups.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == groupId, cancellationToken).ConfigureAwait(false)
            : null;

        if (group is null)
        {
            if (values.ResourceGroupId is not null)
            {
                AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Unknown);
            }
            else if (string.Equals(values.Scope, ResourceScopes.Planet, StringComparison.Ordinal))
            {
                // A resource that lies on a body belongs to a group; a currency of a
                // civilization has no group, because it does not lie on a body (task 00015).
                AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Required);
            }
        }
        else if (string.Equals(values.Scope, ResourceScopes.Civilization, StringComparison.Ordinal))
        {
            // The groups sort the resources of the bodies for the storage and the generation of the
            // world; a currency of a civilization in a group would reach their input.
            AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.CurrencyHasNoGroup);
        }
        else if (group.IsArchived && group.Id != stored?.ResourceGroupId)
        {
            AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Archived);
        }

        // The stocks, the balances and the costs of a resource were written for its scope, so a
        // resource that is in use keeps it.
        if (stored is not null
            && ResourceScopes.IsKnown(values.Scope)
            && !string.Equals(stored.Scope, values.Scope, StringComparison.Ordinal)
            && await IsResourceInUseAsync(stored.Id, cancellationToken).ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, ScopeField, AdminReferenceErrors.ScopeInUse);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    private async Task<bool> IsResourceInUseAsync(int resourceTypeId, CancellationToken cancellationToken)
    {
        // The data of the world itself: the deposits of the bodies, the generation rules and the
        // starting amounts of the races.
        if (await _dbContext.Deposits.AsNoTracking().AnyAsync(entry => entry.ResourceId == resourceTypeId, cancellationToken).ConfigureAwait(false)
            || await _dbContext.Rules.AsNoTracking().AnyAsync(entry => entry.ResourceId == resourceTypeId, cancellationToken).ConfigureAwait(false)
            || await _dbContext.RaceStartResources.AsNoTracking().AnyAsync(entry => entry.ResourceTypeId == resourceTypeId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        foreach (IResourceUsage usage in _usages)
        {
            if (await usage.IsUsedAsync(resourceTypeId, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<ResourceTypeEntity> LockResourceAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.ResourceTypes
            .FromSql($"SELECT * FROM resource_type WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    private async Task<ResourceGroupEntity> LockGroupAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.ResourceGroups
            .FromSql($"SELECT * FROM resource_group WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    private async Task<RaceEntity> LockRaceAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.Races
            .FromSql($"SELECT * FROM race WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    /// <summary>
    /// The values of the form of a resource.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="ResourceGroupId">The group.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="Scope">Where the resource lies.</param>
    /// <param name="EarnsPopulationIncome">Whether the population of a civilization brings this currency.</param>
    /// <param name="Weight">The weight of one unit of the resource in tonnes (task 00027).</param>
    /// <param name="AllowsDebt">Whether the balance of this currency may go below zero (task 00024).</param>
    /// <param name="PaysPortalDelivery">Whether the buyer pays the delivery of a lot through a portal with this resource.</param>
    /// <param name="PaidByPopulation">Whether a cost in this resource is paid by the inhabitants of the body.</param>
    private sealed record ResourceValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? ResourceGroupId,
        string? ImageKey,
        string Scope,
        bool EarnsPopulationIncome = false,
        decimal? Weight = null,
        bool AllowsDebt = false,
        bool PaysPortalDelivery = false,
        bool PaidByPopulation = false)
    {
        public static ResourceValues Of(ResourceTypeEntity resource, IReadOnlyList<LocalizationPack> packs) =>
            new(AdminReferenceRules.TextsOf(packs, resource.NameKey), AdminReferenceRules.TextsOf(packs, resource.DescriptionKey), resource.ResourceGroupId, resource.ImageKey, resource.Scope, resource.EarnsPopulationIncome, resource.Weight, resource.AllowsDebt, resource.PaysPortalDelivery, resource.PaidByPopulation);

        public static ResourceValues Of(ResourceTypeEntity resource, (Dictionary<string, string> Name, Dictionary<string, string> Description) texts) =>
            new(texts.Name, texts.Description, resource.ResourceGroupId, resource.ImageKey, resource.Scope, resource.EarnsPopulationIncome, resource.Weight, resource.AllowsDebt, resource.PaysPortalDelivery, resource.PaidByPopulation);

        public (Dictionary<string, string> Name, Dictionary<string, string> Description) Trimmed(ILocalizationTexts texts) =>
            (Trim(texts, Name), Trim(texts, Description));

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [GroupField] = ResourceGroupId,
            [ScopeField] = Scope,
            [EarnsPopulationIncomeField] = EarnsPopulationIncome,
            [AllowsDebtField] = AllowsDebt,
            [PaysPortalDeliveryField] = PaysPortalDelivery,
            [PaidByPopulationField] = PaidByPopulation,
            [WeightField] = Weight,
        };
    }

    /// <summary>
    /// The values of the form of a resource group.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="AllowedOnPlanet">Whether a resource of the group may lie on a body.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="IsBalance">Whether the resources of the group are balances and not a stock of the storage.</param>
    /// <param name="IconShape">The shape of the icon of the group.</param>
    /// <param name="IconTone">The tone of the icon of the group.</param>
    private sealed record ResourceGroupValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        bool AllowedOnPlanet,
        string? ImageKey = null,
        bool IsBalance = false,
        string? IconShape = null,
        string? IconTone = null)
    {
        public static ResourceGroupValues Of(ResourceGroupEntity group, IReadOnlyList<LocalizationPack> packs) =>
            new(AdminReferenceRules.TextsOf(packs, group.NameKey), AdminReferenceRules.TextsOf(packs, group.DescriptionKey), group.AllowedOnPlanet, group.ImageKey, group.IsBalance, group.IconShape, group.IconTone);

        public static ResourceGroupValues Of(ResourceGroupEntity group, (Dictionary<string, string> Name, Dictionary<string, string> Description) texts) =>
            new(texts.Name, texts.Description, group.AllowedOnPlanet, group.ImageKey, group.IsBalance, group.IconShape, group.IconTone);

        public (Dictionary<string, string> Name, Dictionary<string, string> Description) Trimmed(ILocalizationTexts texts) =>
            (Trim(texts, Name), Trim(texts, Description));

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [AllowedOnPlanetField] = AllowedOnPlanet,
            [IsBalanceField] = IsBalance,
            [IconShapeField] = IconShape,
            [IconToneField] = IconTone,
        };
    }

    /// <summary>
    /// The values of the form of a race.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="Mood">The mood shown on the card of the race, by language.</param>
    /// <param name="Traits">The strengths shown on the card of the race, each by language.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="ColorKey">The key of the colour of the race in the style of the client.</param>
    /// <param name="SortOrder">The order of the race in the list of the client.</param>
    private sealed record RaceValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        Dictionary<string, string>? Mood,
        IReadOnlyList<RaceTraitValues>? Traits,
        string? ImageKey,
        string? ColorKey,
        int SortOrder)
    {
        /// <summary>
        /// The values of the race with the texts of the packs of the languages.
        /// </summary>
        public static RaceValues Of(RaceEntity race, IReadOnlyList<LocalizationPack> packs) =>
            new(
                AdminReferenceRules.TextsOf(packs, race.NameKey),
                AdminReferenceRules.TextsOf(packs, race.DescriptionKey),
                AdminReferenceRules.TextsOf(packs, race.MoodKey),
                [.. race.TraitKeys.Select(key => new RaceTraitValues(AdminReferenceRules.TextsOf(packs, key)))],
                race.ImageKey,
                race.ColorKey,
                race.SortOrder);

        /// <summary>
        /// The values of the race with the texts the form sent.
        /// </summary>
        public static RaceValues Of(RaceEntity race, (Dictionary<string, string> Name, Dictionary<string, string> Description) texts) =>
            new(
                texts.Name,
                texts.Description,
                new Dictionary<string, string>(),
                [],
                race.ImageKey,
                race.ColorKey,
                race.SortOrder);

        /// <summary>
        /// The values of the race with the texts trimmed and with the texts of the mood and of the
        /// traits kept.
        /// </summary>
        public (Dictionary<string, string> Name, Dictionary<string, string> Description) Trimmed(ILocalizationTexts texts) =>
            (Trim(texts, Name), Trim(texts, Description));

        /// <summary>
        /// The texts of the race: the name, the description, the mood and the strengths.
        /// </summary>
        public static IEnumerable<LocalizationTextEntry> Entries(
            ILocalizationTexts texts,
            RaceEntity race,
            RaceValues values)
        {
            foreach (LocalizationTextEntry entry in AdminReferenceRules.Entries(
                texts,
                race.NameKey,
                race.DescriptionKey,
                values.Name!,
                values.Description!))
            {
                yield return entry;
            }

            foreach (LocalizationTextEntry entry in TextsOf(texts, race.MoodKey, values.Mood))
            {
                yield return entry;
            }

            int index = 0;
            foreach (RaceTraitValues trait in values.Traits ?? [])
            {
                string key = index < race.TraitKeys.Count
                    ? race.TraitKeys[index]
                    : WorldTextKeys.Of(WorldTextKeys.Race, race.Code, WorldTextKeys.TraitPart + (index + 1).ToString(CultureInfo.InvariantCulture));

                foreach (LocalizationTextEntry entry in TextsOf(texts, key, trait.Trait))
                {
                    yield return entry;
                }

                index++;
            }
        }

        /// <summary>
        /// The texts of one key by language.
        /// </summary>
        private static IEnumerable<LocalizationTextEntry> TextsOf(
            ILocalizationTexts texts,
            string key,
            Dictionary<string, string>? values) =>
            texts.Languages.Select(language => new LocalizationTextEntry(key, language, values?.GetValueOrDefault(language)?.Trim() ?? string.Empty));

        /// <summary>
        /// The values of the race for the form.
        /// </summary>
        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [MoodField] = Mood,
            [TraitsField] = Traits is null
                ? null
                : new List<object>([.. Traits.Select(trait => (object)new Dictionary<string, object?> { ["trait"] = trait.Trait })]),
            [ColorKeyField] = ColorKey,
            [SortOrderField] = SortOrder,
        };
    }

    /// <summary>
    /// One trait of a race: the text of the trait by language.
    /// </summary>
    /// <param name="Trait">The text of the trait.</param>
    private sealed record RaceTraitValues(Dictionary<string, string>? Trait);

    /// <summary>
    /// Checks the shape and the tone of the icon of a group: each is empty or one of the known
    /// values of the design.
    /// </summary>
    private static void CheckGroupIcon(Dictionary<string, List<string>> failures, ResourceGroupValues values)
    {
        if (!ResourceGroupIcons.IsKnownShape(Blank(values.IconShape)))
        {
            AdminReferenceRules.Fail(failures, IconShapeField, AdminReferenceErrors.Unknown);
        }

        if (!ResourceGroupIcons.IsKnownTone(Blank(values.IconTone)))
        {
            AdminReferenceRules.Fail(failures, IconToneField, AdminReferenceErrors.Unknown);
        }
    }

    /// <summary>
    /// Turns an empty choice of a select into no value.
    /// </summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string> Trim(ILocalizationTexts texts, Dictionary<string, string>? values) =>
        texts.Languages.ToDictionary(language => language, language => values?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal);

    /// <summary>
    /// Gets the weight of one unit of a resource for the resources that have no weight of their
    /// own yet. The value comes from the configuration, so the code carries no number of the
    /// game (task 00027, section 3.1).
    /// </summary>
    /// <returns>The default weight in tonnes.</returns>
    private decimal DefaultWeight() =>
        decimal.TryParse(_config.GetRaw(WorldConfigKeys.DefaultResourceWeight), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : decimal.Parse(Default(WorldConfigKeys.DefaultResourceWeight), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string Default(string key) =>
        WorldConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

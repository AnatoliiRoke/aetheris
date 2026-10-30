using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
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

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.World.admin-reference-texts.json";

    private const string GroupField = "resource_group_id";

    private const string AllowedOnPlanetField = "allowed_on_planet";

    private const string ScopeField = "scope";

    private const string EarnsPopulationIncomeField = "earns_population_income";

    private const string LabelPrefix = "admin.reference.";

    /// <summary>The place of the resources in the menu of the reference kinds.</summary>
    private const int ResourceSortOrder = 10;

    /// <summary>The place of the resource groups in the menu of the reference kinds.</summary>
    private const int ResourceGroupSortOrder = 20;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(WorldReferenceAdminService).Assembly, LabelTextsResource);

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IReadOnlyList<IResourceUsage> _usages;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="usages">The modules that hold data of a resource, asked before its scope changes.</param>
    public WorldReferenceAdminService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        IEnumerable<IResourceUsage> usages)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(usages);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
        _usages = [.. usages];
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
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(AllowedOnPlanetField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.allowed_on_planet", InList: true),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [ResourceDescriptor, ResourceGroupDescriptor];

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
                    new Dictionary<string, object?> { [GroupField] = resource.ResourceGroupId, [ScopeField] = resource.Scope, [EarnsPopulationIncomeField] = resource.EarnsPopulationIncome }))],
            ResourceGroupKind => [.. (await _dbContext.ResourceGroups.AsNoTracking().OrderBy(group => group.Id).ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(group => new AdminReferenceRow(
                    group.Id,
                    group.Code,
                    group.NameKey,
                    null,
                    group.IsArchived,
                    group.Version,
                    new Dictionary<string, object?> { [AllowedOnPlanetField] = group.AllowedOnPlanet }))],
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
                    null,
                    group.IsArchived,
                    group.Version,
                    ResourceGroupValues.Of(group, packs).ToDictionary());

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
            _ => throw UnknownKind(kind),
        };

        // The row and its texts are committed: the copy of the texts in memory is dropped.
        _texts.Invalidate();

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
            _ => throw UnknownKind(kind),
        };

        _texts.Invalidate();

        return change;
    }

    /// <inheritdoc />
    public Task<AdminReferenceChange> SetArchivedAsync(string kind, int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        return _unitOfWork.RunAsync(
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

                    default:
                        throw UnknownKind(kind);
                }
            },
            cancellationToken);
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
                resource.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
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
                AdminReferenceRules.ThrowIfInvalid(failures);

                string code = await _codes.NextAsync(WorldCodeGroups.ResourceGroup, cancellationToken).ConfigureAwait(false);
                ResourceGroupEntity group = new()
                {
                    Code = code,
                    NameKey = WorldTextKeys.Name(WorldTextKeys.ResourceGroup, code),
                    DescriptionKey = WorldTextKeys.Description(WorldTextKeys.ResourceGroup, code),
                    AllowedOnPlanet = values.AllowedOnPlanet,
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
                AdminReferenceRules.ThrowIfInvalid(failures);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                ResourceGroupValues before = ResourceGroupValues.Of(group, packs);

                group.AllowedOnPlanet = values.AllowedOnPlanet;
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

    /// <summary>
    /// The values of the form of a resource.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="ResourceGroupId">The group.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="Scope">Where the resource lies.</param>
    /// <param name="EarnsPopulationIncome">Whether the population of a civilization brings this currency.</param>
    private sealed record ResourceValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? ResourceGroupId,
        string? ImageKey,
        string Scope,
        bool EarnsPopulationIncome = false)
    {
        public static ResourceValues Of(ResourceTypeEntity resource, IReadOnlyList<LocalizationPack> packs) =>
            new(AdminReferenceRules.TextsOf(packs, resource.NameKey), AdminReferenceRules.TextsOf(packs, resource.DescriptionKey), resource.ResourceGroupId, resource.ImageKey, resource.Scope, resource.EarnsPopulationIncome);

        public static ResourceValues Of(ResourceTypeEntity resource, (Dictionary<string, string> Name, Dictionary<string, string> Description) texts) =>
            new(texts.Name, texts.Description, resource.ResourceGroupId, resource.ImageKey, resource.Scope, resource.EarnsPopulationIncome);

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
        };
    }

    /// <summary>
    /// The values of the form of a resource group.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="AllowedOnPlanet">Whether a resource of the group may lie on a body.</param>
    private sealed record ResourceGroupValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        bool AllowedOnPlanet)
    {
        public static ResourceGroupValues Of(ResourceGroupEntity group, IReadOnlyList<LocalizationPack> packs) =>
            new(AdminReferenceRules.TextsOf(packs, group.NameKey), AdminReferenceRules.TextsOf(packs, group.DescriptionKey), group.AllowedOnPlanet);

        public static ResourceGroupValues Of(ResourceGroupEntity group, (Dictionary<string, string> Name, Dictionary<string, string> Description) texts) =>
            new(texts.Name, texts.Description, group.AllowedOnPlanet);

        public (Dictionary<string, string> Name, Dictionary<string, string> Description) Trimmed(ILocalizationTexts texts) =>
            (Trim(texts, Name), Trim(texts, Description));

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [AllowedOnPlanetField] = AllowedOnPlanet,
        };
    }

    private static Dictionary<string, string> Trim(ILocalizationTexts texts, Dictionary<string, string>? values) =>
        texts.Languages.ToDictionary(language => language, language => values?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal);
}

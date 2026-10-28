using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// The technologies and the branches of the tree in the admin panel: the list, the form, the
/// creation, the change and the archive (task 00023, sections 4.5 and 4.6).
/// </summary>
/// <remarks>
/// <para>
/// A technology is data: the server gives the neutral code, the administrator gives the branch,
/// the texts, the picture and the levels with their time, costs, effects and requirements. The
/// largest level is the number of the levels. The requirements live in the common requirements
/// module, and a level that opens a building closes that building until the level is learned,
/// as the starting data does.
/// </para>
/// <para>
/// A branch is a row of its own table: a new branch is added here, without a change of the code.
/// </para>
/// </remarks>
public sealed class ResearchReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the technologies.</summary>
    public const string TechnologyKind = "technology";

    /// <summary>The kind of the branches of the tree.</summary>
    public const string BranchKind = "technology_branch";

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.Research.admin-reference-texts.json";

    private const string LabelPrefix = "admin.reference.";

    private const string BranchField = "branch_id";

    private const string SortOrderField = "sort_order";

    private const string CenterLevelField = "research_center_level";

    private const string LevelsField = "levels";

    private const string CostsField = "costs";

    private const string EffectsField = "effects";

    private const string RequirementsField = "requirements";

    /// <summary>The place of the technologies in the menu of the reference kinds.</summary>
    private const int TechnologySortOrder = 40;

    /// <summary>The place of the branches in the menu of the reference kinds.</summary>
    private const int BranchSortOrder = 50;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(ResearchReferenceAdminService).Assembly, LabelTextsResource);

    private readonly ResearchDbContext _dbContext;
    private readonly ResearchUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly IRequirementsModule _requirements;
    private readonly TechnologyCatalogCache _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="world">The world, for the resources.</param>
    /// <param name="planets">The bodies module, for the buildings.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="catalogCache">The catalog of the technologies in memory.</param>
    public ResearchReferenceAdminService(
        ResearchDbContext dbContext,
        ResearchUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        IWorldModule world,
        IPlanetsModule planets,
        IRequirementsModule requirements,
        TechnologyCatalogCache catalogCache)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(catalogCache);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
        _world = world;
        _planets = planets;
        _requirements = requirements;
        _catalogCache = catalogCache;
    }

    /// <summary>
    /// Gets the kind of the technologies.
    /// </summary>
    public static AdminReferenceDescriptor TechnologyDescriptor { get; } = new(
        TechnologyKind,
        ResearchTextKeys.CodeGroup,
        ResearchTextKeys.Technology,
        LabelPrefix + TechnologyKind + ".title",
        TechnologySortOrder,
        "/research/technologies",
        new AdminReferencePermissions(
            "admin.research_technologies_read",
            "admin.research_technology_read",
            "admin.research_technology_added",
            "admin.research_technology_updated",
            "admin.research_technology_archived",
            "admin.research_technology_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(BranchField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.branch", IsRequired: true, InList: true, ReferenceKind: BranchKind),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
            new(CenterLevelField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.research_center_level", IsRequired: true, InList: true, Minimum: 1),
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
                                Options: [.. TechnologyEffectTypes.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "technology_effect." + value))]),
                            new(
                                "unlock_kind",
                                AdminReferenceFieldTypes.Select,
                                LabelPrefix + "field.unlock_kind",
                                Options: [new(TechnologyUnlockKinds.BuildingType, LabelPrefix + "unlock." + TechnologyUnlockKinds.BuildingType, RequirementReferenceKinds.Building)]),
                            new("unlock_target_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.unlock_target", ReferenceKindFrom: "unlock_kind"),
                            new("value", AdminReferenceFieldTypes.Number, LabelPrefix + "field.value", IsRequired: true, Minimum: 0, IsDecimal: true),
                        ]),
                    RequirementAdminFields.Field,
                ]),
        ]);

    /// <summary>
    /// Gets the kind of the branches of the tree.
    /// </summary>
    public static AdminReferenceDescriptor BranchDescriptor { get; } = new(
        BranchKind,
        ResearchBranchTextKeys.CodeGroup,
        ResearchBranchTextKeys.Branch,
        LabelPrefix + BranchKind + ".title",
        BranchSortOrder,
        "/research/branches",
        new AdminReferencePermissions(
            "admin.research_branches_read",
            "admin.research_branch_read",
            "admin.research_branch_added",
            "admin.research_branch_updated",
            "admin.research_branch_archived",
            "admin.research_branch_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [TechnologyDescriptor, BranchDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        List<AdminReferenceRow> rows = kind switch
        {
            TechnologyKind => [.. (await _dbContext.Technologies.AsNoTracking()
                    .OrderBy(technology => technology.BranchId).ThenBy(technology => technology.SortOrder).ThenBy(technology => technology.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(technology => new AdminReferenceRow(
                    technology.Id,
                    technology.Code,
                    technology.NameKey,
                    technology.ImageKey,
                    technology.IsArchived,
                    technology.Version,
                    new Dictionary<string, object?>
                    {
                        [BranchField] = technology.BranchId,
                        [SortOrderField] = technology.SortOrder,
                        [CenterLevelField] = technology.ResearchCenterLevel,
                    }))],
            BranchKind => [.. (await _dbContext.TechnologyBranches.AsNoTracking()
                    .OrderBy(branch => branch.SortOrder).ThenBy(branch => branch.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(branch => new AdminReferenceRow(
                    branch.Id,
                    branch.Code,
                    branch.NameKey,
                    branch.ImageKey,
                    branch.IsArchived,
                    branch.Version,
                    new Dictionary<string, object?> { [SortOrderField] = branch.SortOrder }))],
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
            case TechnologyKind:
                TechnologyEntity? technology = await LoadAsync(id, false, cancellationToken).ConfigureAwait(false);

                return technology is null ? null : new AdminReferenceRecord(
                    technology.Id,
                    technology.Code,
                    technology.NameKey,
                    technology.DescriptionKey,
                    technology.ImageKey,
                    technology.IsArchived,
                    technology.Version,
                    (await ValuesOfAsync(technology, packs, cancellationToken).ConfigureAwait(false)).ToDictionary());

            case BranchKind:
                TechnologyBranchEntity? branch = await _dbContext.TechnologyBranches.AsNoTracking()
                    .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                return branch is null ? null : new AdminReferenceRecord(
                    branch.Id,
                    branch.Code,
                    branch.NameKey,
                    branch.DescriptionKey,
                    branch.ImageKey,
                    branch.IsArchived,
                    branch.Version,
                    BranchValues.Of(branch, packs).ToDictionary());

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
            TechnologyKind => await CreateTechnologyAsync(AdminReferenceJson.Read<TechnologyValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            BranchKind => await CreateBranchAsync(AdminReferenceJson.Read<BranchValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        Committed();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> UpdateAsync(string kind, int id, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);

        AdminReferenceChange change = kind switch
        {
            TechnologyKind => await UpdateTechnologyAsync(id, input.Version, AdminReferenceJson.Read<TechnologyValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            BranchKind => await UpdateBranchAsync(id, input.Version, AdminReferenceJson.Read<BranchValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

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
                (int rowId, int version, bool stored, Action<bool> set) = kind switch
                {
                    TechnologyKind => await LockTechnologyStateAsync(id, cancellationToken).ConfigureAwait(false),
                    BranchKind => await LockBranchStateAsync(id, cancellationToken).ConfigureAwait(false),
                    _ => throw UnknownKind(kind),
                };

                AdminReferenceRules.CheckVersion(version, input.Version);

                if (stored == isArchived)
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                }

                set(isArchived);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(kind, rowId),
                    new Dictionary<string, object?> { ["is_archived"] = stored },
                    new Dictionary<string, object?> { ["is_archived"] = isArchived },
                    rowId,
                    version + 1);
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    private static ArgumentException UnknownKind(string kind) => new($"The research module has no reference kind '{kind}'.", nameof(kind));

    private static Dictionary<string, string> Trim(ILocalizationTexts texts, Dictionary<string, string>? values) =>
        texts.Languages.ToDictionary(language => language, language => values?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal);

    private async Task<(int Id, int Version, bool IsArchived, Action<bool> Set)> LockTechnologyStateAsync(int id, CancellationToken cancellationToken)
    {
        TechnologyEntity technology = await LockTechnologyAsync(id, cancellationToken).ConfigureAwait(false);

        void Set(bool value)
        {
            technology.IsArchived = value;
            technology.Version++;
        }

        return (technology.Id, technology.Version, technology.IsArchived, Set);
    }

    private async Task<(int Id, int Version, bool IsArchived, Action<bool> Set)> LockBranchStateAsync(int id, CancellationToken cancellationToken)
    {
        TechnologyBranchEntity branch = await LockBranchAsync(id, cancellationToken).ConfigureAwait(false);

        void Set(bool value)
        {
            branch.IsArchived = value;
            branch.Version++;
        }

        return (branch.Id, branch.Version, branch.IsArchived, Set);
    }

    private Task<AdminReferenceChange> CreateTechnologyAsync(TechnologyValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                List<SubjectRequirement> requirements = await CheckTechnologyAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(ResearchTextKeys.CodeGroup, cancellationToken).ConfigureAwait(false);
                TechnologyEntity technology = new()
                {
                    Code = code,
                    NameKey = ResearchTextKeys.Name(code),
                    DescriptionKey = ResearchTextKeys.Description(code),
                };

                Apply(technology, values);
                _dbContext.Technologies.Add(technology);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await WriteRequirementsAsync(technology, requirements, cancellationToken).ConfigureAwait(false);
                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, technology.NameKey, technology.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(TechnologyKind, technology.Id),
                    null,
                    new { code, values = values with { Name = Trim(_texts, values.Name), Description = Trim(_texts, values.Description) } },
                    technology.Id,
                    technology.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateTechnologyAsync(int id, int version, TechnologyValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await LockTechnologyAsync(id, cancellationToken).ConfigureAwait(false);
                TechnologyEntity technology = (await LoadAsync(id, true, cancellationToken).ConfigureAwait(false))!;
                AdminReferenceRules.CheckVersion(technology.Version, version);

                List<SubjectRequirement> requirements = await CheckTechnologyAsync(values, technology, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                TechnologyValues before = await ValuesOfAsync(technology, packs, cancellationToken).ConfigureAwait(false);

                Apply(technology, values);
                technology.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await WriteRequirementsAsync(technology, requirements, cancellationToken).ConfigureAwait(false);
                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, technology.NameKey, technology.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(TechnologyKind, technology.Id),
                    before,
                    values with { Name = Trim(_texts, values.Name), Description = Trim(_texts, values.Description) },
                    technology.Id,
                    technology.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreateBranchAsync(BranchValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckBranchAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(ResearchBranchTextKeys.CodeGroup, cancellationToken).ConfigureAwait(false);
                TechnologyBranchEntity branch = new()
                {
                    Code = code,
                    NameKey = ResearchBranchTextKeys.Name(code),
                    DescriptionKey = ResearchBranchTextKeys.Description(code),
                    SortOrder = values.SortOrder ?? 0,
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                };

                _dbContext.TechnologyBranches.Add(branch);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, branch.NameKey, branch.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(BranchKind, branch.Id),
                    null,
                    new { code, values = values with { Name = Trim(_texts, values.Name), Description = Trim(_texts, values.Description) } },
                    branch.Id,
                    branch.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateBranchAsync(int id, int version, BranchValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                TechnologyBranchEntity branch = await LockBranchAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(branch.Version, version);
                await CheckBranchAsync(values, branch, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                BranchValues before = BranchValues.Of(branch, packs);

                branch.SortOrder = values.SortOrder ?? 0;
                branch.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                branch.Version++;

                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, branch.NameKey, branch.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(BranchKind, branch.Id),
                    before,
                    values with { Name = Trim(_texts, values.Name), Description = Trim(_texts, values.Description) },
                    branch.Id,
                    branch.Version);
            },
            cancellationToken);

    private async Task CheckBranchAsync(BranchValues values, TechnologyBranchEntity? stored, CancellationToken cancellationToken)
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

    /// <summary>
    /// Writes the sent values into the row: the branch, the order, the level of the science centre,
    /// the picture and the levels. A kept level keeps its row; its costs and effects are written
    /// again.
    /// </summary>
    private static void Apply(TechnologyEntity technology, TechnologyValues values)
    {
        List<TechnologyLevelValues> levels = values.Levels!;

        technology.BranchId = values.BranchId!.Value;
        technology.SortOrder = values.SortOrder ?? 0;
        technology.ResearchCenterLevel = values.ResearchCenterLevel!.Value;
        technology.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
        technology.MaxLevel = levels.Count;
        technology.LevelIds.RemoveAll(level => !levels.Exists(sent => sent.Level == level.Level));

        foreach (TechnologyLevelValues sent in levels)
        {
            TechnologyLevelEntity? level = technology.LevelIds.Find(stored => stored.Level == sent.Level);

            if (level is null)
            {
                level = new TechnologyLevelEntity { Level = sent.Level!.Value };
                technology.LevelIds.Add(level);
            }

            level.DurationSeconds = sent.DurationSeconds ?? 0;

            // A kept resource of the cost is changed in place, so the unique index of the
            // resource of a level is never broken in between.
            level.CostIds.RemoveAll(cost => !sent.Costs!.Exists(sentCost => sentCost.ResourceTypeId == cost.ResourceTypeId));

            foreach (CostValues cost in sent.Costs!)
            {
                TechnologyLevelCostEntity? kept = level.CostIds.Find(stored => stored.ResourceTypeId == cost.ResourceTypeId);

                if (kept is null)
                {
                    level.CostIds.Add(new TechnologyLevelCostEntity { ResourceTypeId = cost.ResourceTypeId!.Value, Amount = cost.Amount ?? 0 });
                }
                else
                {
                    kept.Amount = cost.Amount ?? 0;
                }
            }

            level.EffectIds.Clear();
            level.EffectIds.AddRange((sent.Effects ?? []).Select(effect => new TechnologyLevelEffectEntity
            {
                EffectType = effect.EffectType!,
                UnlockKind = effect.UnlockKind,
                UnlockTargetId = effect.UnlockTargetId,
                Value = effect.Value ?? 0,
            }));
        }
    }

    /// <summary>
    /// Checks the values of a technology and builds its requirements.
    /// </summary>
    private async Task<List<SubjectRequirement>> CheckTechnologyAsync(TechnologyValues values, TechnologyEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        TechnologyBranchEntity? branch = values.BranchId is { } branchId
            ? await _dbContext.TechnologyBranches.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == branchId, cancellationToken).ConfigureAwait(false)
            : null;

        if (values.BranchId is null)
        {
            AdminReferenceRules.Fail(failures, BranchField, AdminReferenceErrors.Required);
        }
        else if (branch is null)
        {
            AdminReferenceRules.Fail(failures, BranchField, AdminReferenceErrors.Unknown);
        }
        else if (branch.IsArchived && branch.Id != stored?.BranchId)
        {
            AdminReferenceRules.Fail(failures, BranchField, AdminReferenceErrors.Archived);
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        if (values.ResearchCenterLevel is null or < 1)
        {
            AdminReferenceRules.Fail(failures, CenterLevelField, values.ResearchCenterLevel is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        List<TechnologyLevelValues> levels = values.Levels ?? [];
        List<SubjectRequirement> requirements = [];

        if (levels.Any(level => level.Level is null))
        {
            AdminReferenceRules.Fail(failures, LevelsField, AdminReferenceErrors.LevelsGap);
        }
        else if (AdminReferenceRules.CheckLevelNumbers(failures, LevelsField, [.. levels.Select(level => level.Level!.Value)], null))
        {
            await CheckLevelsInUseAsync(failures, stored, levels.Count, cancellationToken).ConfigureAwait(false);
            requirements = await CheckLevelsAsync(failures, levels, stored, cancellationToken).ConfigureAwait(false);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);

        return requirements;
    }

    /// <summary>
    /// Refuses to remove a level a player has learned or has in the queue.
    /// </summary>
    private async Task CheckLevelsInUseAsync(Dictionary<string, List<string>> failures, TechnologyEntity? stored, int levelCount, CancellationToken cancellationToken)
    {
        if (stored is null)
        {
            return;
        }

        int learned = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.TechnologyId == stored.Id)
            .MaxAsync(entry => (int?)entry.Level, cancellationToken).ConfigureAwait(false) ?? 0;
        int queued = await _dbContext.ResearchQueueItems.AsNoTracking()
            .Where(item => item.TechnologyId == stored.Id)
            .MaxAsync(item => (int?)item.TargetLevel, cancellationToken).ConfigureAwait(false) ?? 0;

        if (Math.Max(learned, queued) > levelCount)
        {
            AdminReferenceRules.Fail(failures, LevelsField, AdminReferenceErrors.LevelsInUse);
        }
    }

    private async Task<List<SubjectRequirement>> CheckLevelsAsync(
        Dictionary<string, List<string>> failures,
        List<TechnologyLevelValues> levels,
        TechnologyEntity? stored,
        CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ResourceTypeFact> resourcesById = resources.Resources.ToDictionary(resource => resource.Id);
        HashSet<int> buildingIds = [.. (await _planets.GetBuildingNameKeysAsync(cancellationToken).ConfigureAwait(false)).Keys];
        HashSet<int> technologyIds = [.. await _dbContext.Technologies.AsNoTracking().Select(technology => technology.Id).ToListAsync(cancellationToken).ConfigureAwait(false)];

        // A technology is never its own requirement.
        if (stored is not null)
        {
            technologyIds.Remove(stored.Id);
        }

        HashSet<int> storedResourceIds = [.. (stored?.LevelIds ?? []).SelectMany(level => level.CostIds.Select(cost => cost.ResourceTypeId))];
        List<SubjectRequirement> requirements = [];

        foreach ((TechnologyLevelValues level, int index) in levels.Select((level, position) => (level, position)))
        {
            if (level.DurationSeconds is null or < 0)
            {
                AdminReferenceRules.Fail(failures, AdminReferenceRules.PathOf(LevelsField, index, "duration_seconds"), AdminReferenceErrors.OutOfBounds);
            }

            CheckCosts(failures, AdminReferenceRules.PathOf(LevelsField, index, CostsField), level.Costs ?? [], resourcesById, storedResourceIds);
            CheckEffects(failures, AdminReferenceRules.PathOf(LevelsField, index, EffectsField), level.Effects ?? [], buildingIds);

            foreach ((RequirementValues requirement, int requirementIndex) in (level.Requirements ?? []).Select((requirement, position) => (requirement, position)))
            {
                string path = AdminReferenceRules.PathOf(LevelsField, index, RequirementsField) + "." + requirementIndex.ToString(CultureInfo.InvariantCulture);

                if (RequirementAdminFields.Check(failures, path, requirement.RequirementType, requirement.TargetId, requirement.TargetLevel, buildingIds, technologyIds, resourcesById.Keys) is { } definition)
                {
                    requirements.Add(new SubjectRequirement(stored?.Id ?? 0, level.Level!.Value, definition));
                }
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
        string path,
        List<CostValues> costs,
        Dictionary<int, ResourceTypeFact> resourcesById,
        HashSet<int> storedResourceIds)
    {
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

    private static void CheckEffects(Dictionary<string, List<string>> failures, string path, List<TechnologyEffectValues> effects, HashSet<int> buildingIds)
    {
        foreach (TechnologyEffectValues effect in effects)
        {
            if (effect.EffectType is null || !TechnologyEffectTypes.All.Contains(effect.EffectType, StringComparer.Ordinal))
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);

                continue;
            }

            if (effect.EffectType == TechnologyEffectTypes.Unlocks)
            {
                // Only the buildings exist yet; the modules, the recipes and the ship classes come
                // with their own tasks and their own reference kinds.
                if (effect.UnlockKind != TechnologyUnlockKinds.BuildingType)
                {
                    AdminReferenceRules.Fail(failures, path, effect.UnlockKind is null ? AdminReferenceErrors.Required : AdminReferenceErrors.NotApplicable);
                }
                else if (effect.UnlockTargetId is not { } buildingId || !buildingIds.Contains(buildingId))
                {
                    AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);
                }
            }
            else if (effect.UnlockKind is not null || effect.UnlockTargetId is not null)
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.NotApplicable);
            }

            if (effect.Value is null or < 0)
            {
                AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.OutOfBounds);
            }
        }
    }

    /// <summary>
    /// Writes the requirements of a technology, and closes every building a level opens until the
    /// level is learned, like the starting data does.
    /// </summary>
    private async Task WriteRequirementsAsync(TechnologyEntity technology, List<SubjectRequirement> requirements, CancellationToken cancellationToken)
    {
        await _requirements.ReplaceAsync(RequirementSubjectTypes.Technology, technology.Id, requirements, cancellationToken).ConfigureAwait(false);

        foreach ((int buildingTypeId, int level) in technology.LevelIds
            .SelectMany(level => level.EffectIds
                .Where(effect => effect.EffectType == TechnologyEffectTypes.Unlocks && effect.UnlockKind == TechnologyUnlockKinds.BuildingType && effect.UnlockTargetId is not null)
                .Select(effect => (BuildingTypeId: effect.UnlockTargetId!.Value, level.Level)))
            .GroupBy(entry => entry.BuildingTypeId)
            .Select(group => (group.Key, group.Min(entry => entry.Level))))
        {
            IReadOnlyList<RequirementDefinition> existing = await _requirements.GetAsync(RequirementSubjectTypes.BuildingType, buildingTypeId, int.MaxValue, cancellationToken)
                .ConfigureAwait(false);

            if (!existing.Any(requirement => requirement.RequirementType == RequirementTypes.TechnologyLevel && requirement.TargetId == technology.Id))
            {
                await _requirements.AddAsync(
                    RequirementSubjectTypes.BuildingType,
                    buildingTypeId,
                    1,
                    [new RequirementDefinition(RequirementTypes.TechnologyLevel, technology.Id, level)],
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Committed()
    {
        // The rows, their texts and their requirements are committed: the copies in memory are
        // dropped, so a player sees the change without waiting for the lifetime of the caches. The
        // building catalog holds the requirements a technology adds to the buildings it opens.
        _catalogCache.Invalidate();
        _planets.InvalidateCatalog();
        _texts.Invalidate();
    }

    private async Task<TechnologyEntity> LockTechnologyAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.Technologies
            .FromSql($"SELECT * FROM technology WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    private async Task<TechnologyBranchEntity> LockBranchAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.TechnologyBranches
            .FromSql($"SELECT * FROM technology_branch WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    private Task<TechnologyEntity?> LoadAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<TechnologyEntity> technologies = tracked ? _dbContext.Technologies : _dbContext.Technologies.AsNoTracking();

        return technologies
            .Include(technology => technology.LevelIds).ThenInclude(level => level.CostIds)
            .Include(technology => technology.LevelIds).ThenInclude(level => level.EffectIds)
            .AsSplitQuery()
            .SingleOrDefaultAsync(technology => technology.Id == id, cancellationToken);
    }

    private async Task<TechnologyValues> ValuesOfAsync(TechnologyEntity technology, IReadOnlyList<LocalizationPack> packs, CancellationToken cancellationToken)
    {
        IReadOnlyList<SubjectRequirement> requirements = await _requirements.GetManyAsync(
            RequirementSubjectTypes.Technology,
            [new SubjectLevel(technology.Id, int.MaxValue)],
            cancellationToken).ConfigureAwait(false);

        return new TechnologyValues(
            AdminReferenceRules.TextsOf(packs, technology.NameKey),
            AdminReferenceRules.TextsOf(packs, technology.DescriptionKey),
            technology.BranchId,
            technology.SortOrder,
            technology.ResearchCenterLevel,
            technology.ImageKey,
            [
                .. technology.LevelIds.OrderBy(level => level.Level).Select(level => new TechnologyLevelValues(
                    level.Level,
                    level.DurationSeconds,
                    [.. level.CostIds.OrderBy(cost => cost.ResourceTypeId).Select(cost => new CostValues(cost.ResourceTypeId, cost.Amount))],
                    [.. level.EffectIds.OrderBy(effect => effect.Id).Select(effect => new TechnologyEffectValues(effect.EffectType, effect.UnlockKind, effect.UnlockTargetId, effect.Value))],
                    [
                        .. requirements.Where(requirement => requirement.SubjectLevel == level.Level).Select(requirement => new RequirementValues(
                            requirement.Requirement.RequirementType,
                            requirement.Requirement.TargetId,
                            requirement.Requirement.TargetLevel)),
                    ])),
            ]);
    }

    /// <summary>The values of the form of a technology.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="BranchId">The branch.</param>
    /// <param name="SortOrder">The order in the lists of the branch.</param>
    /// <param name="ResearchCenterLevel">The level of the science centre the research needs.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    /// <param name="Levels">The levels.</param>
    private sealed record TechnologyValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? BranchId,
        int? SortOrder,
        int? ResearchCenterLevel,
        string? ImageKey,
        List<TechnologyLevelValues>? Levels)
    {
        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [BranchField] = BranchId,
            [SortOrderField] = SortOrder,
            [CenterLevelField] = ResearchCenterLevel,
            [LevelsField] = Levels,
        };
    }

    /// <summary>A level of the form of a technology.</summary>
    /// <param name="Level">The number of the level.</param>
    /// <param name="DurationSeconds">The time of the research, in game seconds.</param>
    /// <param name="Costs">The costs.</param>
    /// <param name="Effects">The effects.</param>
    /// <param name="Requirements">The requirements that apply from this level.</param>
    private sealed record TechnologyLevelValues(
        int? Level,
        long? DurationSeconds,
        List<CostValues>? Costs,
        List<TechnologyEffectValues>? Effects,
        List<RequirementValues>? Requirements);

    /// <summary>A cost of a level.</summary>
    /// <param name="ResourceTypeId">The resource.</param>
    /// <param name="Amount">The amount.</param>
    private sealed record CostValues(int? ResourceTypeId, long? Amount);

    /// <summary>An effect of a level of a technology.</summary>
    /// <param name="EffectType">The kind of the effect, one of <see cref="TechnologyEffectTypes"/>.</param>
    /// <param name="UnlockKind">The kind of the subject the effect opens.</param>
    /// <param name="UnlockTargetId">The subject the effect opens.</param>
    /// <param name="Value">The value of the effect.</param>
    private sealed record TechnologyEffectValues(string? EffectType, string? UnlockKind, int? UnlockTargetId, decimal? Value);

    /// <summary>A requirement of a level.</summary>
    /// <param name="RequirementType">The requirement type.</param>
    /// <param name="TargetId">The target.</param>
    /// <param name="TargetLevel">The required level of the target.</param>
    private sealed record RequirementValues(string? RequirementType, int? TargetId, int? TargetLevel);

    /// <summary>The values of the form of a branch.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="SortOrder">The order in the tree.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    private sealed record BranchValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? SortOrder,
        string? ImageKey)
    {
        public static BranchValues Of(TechnologyBranchEntity branch, IReadOnlyList<LocalizationPack> packs) =>
            new(AdminReferenceRules.TextsOf(packs, branch.NameKey), AdminReferenceRules.TextsOf(packs, branch.DescriptionKey), branch.SortOrder, branch.ImageKey);

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [SortOrderField] = SortOrder,
        };
    }
}

using Aetheris.Combat.Api;
using Aetheris.Combat.Data;
using Aetheris.Combat.Data.Entities;
using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Combat.Services;

/// <summary>
/// The target rules and the behaviors in the admin panel (task 00062).
/// </summary>
/// <remarks>
/// A row is never deleted. Archiving is always allowed: nothing references the rows yet.
/// The logical code is typed by the administrator and is not a closed list.
/// </remarks>
public sealed class CombatReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the target rules.</summary>
    public const string TargetRuleKind = "target_rule";

    /// <summary>The kind of the behaviors.</summary>
    public const string BehaviorKind = "behavior";

    private const string LabelPrefix = "admin.reference.";

    private const string LabelTextsResource = "Aetheris.Combat.admin-reference-texts.json";

    private const string LogicCodeField = "logic_code";

    private const string SortOrderField = "sort_order";

    private const string ArchivedField = "is_archived";

    private const int TargetRuleSortOrder = 80;

    private const int BehaviorSortOrder = 81;

    private readonly CombatDbContext _dbContext;
    private readonly CombatUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    public CombatReferenceAdminService(
        CombatDbContext dbContext,
        CombatUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
    }

    /// <summary>Gets the kind of the target rules.</summary>
    public static AdminReferenceDescriptor TargetRuleDescriptor { get; } = Describe(
        TargetRuleKind,
        CombatTextKeys.TargetRuleGroup,
        CombatTextKeys.TargetRule,
        TargetRuleSortOrder,
        "/combat/target-rules",
        new AdminReferencePermissions(
            "admin.target_rules_read",
            "admin.target_rule_read",
            "admin.target_rule_added",
            "admin.target_rule_updated",
            "admin.target_rule_archived",
            "admin.target_rule_restored"));

    /// <summary>Gets the kind of the behaviors.</summary>
    public static AdminReferenceDescriptor BehaviorDescriptor { get; } = Describe(
        BehaviorKind,
        CombatTextKeys.BehaviorGroup,
        CombatTextKeys.Behavior,
        BehaviorSortOrder,
        "/combat/behaviors",
        new AdminReferencePermissions(
            "admin.behaviors_read",
            "admin.behavior_read",
            "admin.behavior_added",
            "admin.behavior_updated",
            "admin.behavior_archived",
            "admin.behavior_restored"));

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [TargetRuleDescriptor, BehaviorDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts { get; } =
        AdminReferenceRules.LoadLabelTexts(typeof(CombatReferenceAdminService).Assembly, LabelTextsResource);

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        CheckKind(kind);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        if (IsTarget(kind))
        {
            List<TargetRuleEntity> targets = await _dbContext.TargetRules.AsNoTracking()
                .OrderBy(entry => entry.SortOrder)
                .ThenBy(entry => entry.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Page(targets, query, packs);
        }

        List<BehaviorEntity> behaviors = await _dbContext.Behaviors.AsNoTracking()
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Page(behaviors, query, packs);
    }

    /// <inheritdoc />
    public async Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken)
    {
        CheckKind(kind);

        ICombatRuleRow? row = IsTarget(kind)
            ? await _dbContext.TargetRules.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            : await _dbContext.Behaviors.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        return RecordOf(row, packs);
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> CreateAsync(string kind, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);
        CheckKind(kind);

        RuleValues form = AdminReferenceJson.Read<RuleValues>(input.Values);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                if (IsTarget(kind))
                {
                    return await InsertAsync(_dbContext.TargetRules, TargetRuleDescriptor, form, adminId, cancellationToken).ConfigureAwait(false);
                }

                return await InsertAsync(_dbContext.Behaviors, BehaviorDescriptor, form, adminId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        _texts.Invalidate();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> UpdateAsync(string kind, int id, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);
        CheckKind(kind);

        RuleValues form = AdminReferenceJson.Read<RuleValues>(input.Values);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                if (IsTarget(kind))
                {
                    TargetRuleEntity target = await LockTargetAsync(id, cancellationToken).ConfigureAwait(false);

                    return await ApplyAsync(kind, target, _dbContext.TargetRules, form, input.Version, adminId, cancellationToken).ConfigureAwait(false);
                }

                BehaviorEntity behavior = await LockBehaviorAsync(id, cancellationToken).ConfigureAwait(false);

                return await ApplyAsync(kind, behavior, _dbContext.Behaviors, form, input.Version, adminId, cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        _texts.Invalidate();

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
                ICombatRuleRow row = IsTarget(kind)
                    ? await LockTargetAsync(id, cancellationToken).ConfigureAwait(false)
                    : await LockBehaviorAsync(id, cancellationToken).ConfigureAwait(false);

                return Archive(kind, row, isArchived, input.Version);
            },
            cancellationToken).ConfigureAwait(false);

        _texts.Invalidate();

        return change;
    }

    private static AdminReferenceDescriptor Describe(
        string kind,
        string codeGroup,
        string prefix,
        int sortOrder,
        string route,
        AdminReferencePermissions permissions) =>
        new(
            kind,
            codeGroup,
            prefix,
            LabelPrefix + kind + ".title",
            sortOrder,
            route,
            permissions,
            [
                new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
                new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
                new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
                new(LogicCodeField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.logic_code", IsRequired: true, InList: true),
                new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
            ]);

    private static void CheckKind(string kind)
    {
        if (!IsTarget(kind) && !string.Equals(kind, BehaviorKind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The combat reference has no kind '{kind}'.", nameof(kind));
        }
    }

    private static bool IsTarget(string kind) => string.Equals(kind, TargetRuleKind, StringComparison.Ordinal);

    private static PageResult<AdminReferenceRow> Page<TRow>(
        IEnumerable<TRow> rows,
        AdminReferenceQuery query,
        IReadOnlyList<LocalizationPack> packs)
        where TRow : ICombatRuleRow =>
        AdminReferenceRules.Page(
            rows.Select(row => new AdminReferenceRow(
                row.Id,
                row.Code,
                row.NameKey,
                row.ImageKey,
                row.IsArchived,
                row.Version,
                new Dictionary<string, object?>
                {
                    [AdminReferenceRules.ImageField] = row.ImageKey,
                    [LogicCodeField] = row.LogicCode,
                    [SortOrderField] = row.SortOrder,
                })),
            query,
            packs);

    private static AdminReferenceRecord RecordOf(ICombatRuleRow row, IReadOnlyList<LocalizationPack> packs) =>
        new(
            row.Id,
            row.Code,
            row.NameKey,
            row.DescriptionKey,
            row.ImageKey,
            row.IsArchived,
            row.Version,
            RuleValues.Of(row, packs).ToDictionary());

    private async Task<AdminReferenceChange> InsertAsync<TRow>(
        DbSet<TRow> rows,
        AdminReferenceDescriptor descriptor,
        RuleValues form,
        string adminId,
        CancellationToken cancellationToken)
        where TRow : class, ICombatRuleRow, new()
    {
        await CheckAsync(form, null, null, rows, cancellationToken).ConfigureAwait(false);

        string code = await _codes.NextAsync(descriptor.CodeGroup, cancellationToken).ConfigureAwait(false);
        TRow row = new()
        {
            Code = code,
            LogicCode = form.LogicCode!.Trim(),
            NameKey = CombatTextKeys.Name(descriptor.NameKeyPrefix, code),
            DescriptionKey = CombatTextKeys.Description(descriptor.NameKeyPrefix, code),
            ImageKey = AdminReferenceRules.ImageKey(form.ImageKey),
            SortOrder = form.SortOrder ?? 0,
        };

        rows.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _texts.AddMissingAsync(
            AdminReferenceRules.Entries(_texts, row.NameKey, row.DescriptionKey, form.Name!, form.Description!),
            adminId,
            cancellationToken).ConfigureAwait(false);

        return AdminReferenceRules.Change(
            AdminReferenceEndpoints.Target(descriptor.Kind, row.Id),
            null,
            new { code, form = form.Trimmed(_texts) },
            row.Id,
            row.Version);
    }

    private async Task<AdminReferenceChange> ApplyAsync<TRow>(
        string kind,
        TRow row,
        DbSet<TRow> rows,
        RuleValues form,
        int version,
        string adminId,
        CancellationToken cancellationToken)
        where TRow : class, ICombatRuleRow
    {
        AdminReferenceRules.CheckVersion(row.Version, version);
        await CheckAsync(form, row.Id, row.ImageKey, rows, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        RuleValues before = RuleValues.Of(row, packs);

        row.LogicCode = form.LogicCode!.Trim();
        row.ImageKey = AdminReferenceRules.ImageKey(form.ImageKey);
        row.SortOrder = form.SortOrder ?? 0;
        row.Version++;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _texts.SetAsync(
            AdminReferenceRules.Entries(_texts, row.NameKey, row.DescriptionKey, form.Name!, form.Description!),
            adminId,
            cancellationToken).ConfigureAwait(false);

        return AdminReferenceRules.Change(
            AdminReferenceEndpoints.Target(kind, row.Id),
            before,
            form.Trimmed(_texts),
            row.Id,
            row.Version);
    }

    private static AdminReferenceChange Archive(string kind, ICombatRuleRow row, bool isArchived, int version)
    {
        AdminReferenceRules.CheckVersion(row.Version, version);

        if (row.IsArchived == isArchived)
        {
            throw new AdminReferenceException(
                GameErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { [ArchivedField] = [AdminReferenceErrors.StateUnchanged] });
        }

        row.IsArchived = isArchived;
        row.Version++;

        return AdminReferenceRules.Change(
            AdminReferenceEndpoints.Target(kind, row.Id),
            new Dictionary<string, object?> { [ArchivedField] = !isArchived },
            new Dictionary<string, object?> { [ArchivedField] = isArchived },
            row.Id,
            row.Version);
    }

    private async Task CheckAsync<TRow>(
        RuleValues form,
        int? exceptId,
        string? storedImageKey,
        DbSet<TRow> rows,
        CancellationToken cancellationToken)
        where TRow : class, ICombatRuleRow
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, form.Name, form.Description);
        await AdminReferenceRules.CheckImageAsync(
            failures,
            _images,
            AdminReferenceRules.ImageKey(form.ImageKey),
            storedImageKey,
            cancellationToken).ConfigureAwait(false);

        string logicCode = form.LogicCode?.Trim() ?? string.Empty;

        if (logicCode.Length == 0)
        {
            AdminReferenceRules.Fail(failures, LogicCodeField, AdminReferenceErrors.Required);
        }
        else if (logicCode.Length > CombatLogicCodes.MaxLength || !CombatLogicCodes.Accepts(logicCode))
        {
            AdminReferenceRules.Fail(
                failures,
                LogicCodeField,
                logicCode.Length > CombatLogicCodes.MaxLength ? AdminReferenceErrors.OutOfBounds : AdminReferenceErrors.Unknown);
        }
        else if (await LogicTakenAsync(rows, logicCode, exceptId, cancellationToken).ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, LogicCodeField, AdminReferenceErrors.Duplicate);
        }

        if (form.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, form.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    private static async Task<bool> LogicTakenAsync<TRow>(DbSet<TRow> rows, string logicCode, int? exceptId, CancellationToken cancellationToken)
        where TRow : class, ICombatRuleRow
    {
        IQueryable<TRow> query = rows.AsNoTracking().Where(row => row.LogicCode == logicCode);

        if (exceptId is int id)
        {
            query = query.Where(row => row.Id != id);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TargetRuleEntity> LockTargetAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.TargetRules
            .FromSql($"SELECT * FROM target_rule WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    private async Task<BehaviorEntity> LockBehaviorAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.Behaviors
            .FromSql($"SELECT * FROM behavior WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    /// <summary>The values of the form of a target rule or a behavior.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="LogicCode">The logical code.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    private sealed record RuleValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        string? LogicCode,
        int? SortOrder,
        string? ImageKey)
    {
        public static RuleValues Of(ICombatRuleRow row, IReadOnlyList<LocalizationPack> packs) =>
            new(
                AdminReferenceRules.TextsOf(packs, row.NameKey),
                AdminReferenceRules.TextsOf(packs, row.DescriptionKey),
                row.LogicCode,
                row.SortOrder,
                row.ImageKey);

        public RuleValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            LogicCode = LogicCode?.Trim(),
        };

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [AdminReferenceRules.ImageField] = ImageKey,
            [LogicCodeField] = LogicCode,
            [SortOrderField] = SortOrder,
        };
    }
}

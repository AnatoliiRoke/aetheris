using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The reference kinds of the characteristics of the parts in the admin panel: the
/// characteristics themselves, the characteristics of a group of the parts and the values of the
/// characteristics of a part (task 00027, section 3.3).
/// </summary>
/// <remarks>
/// <para>
/// Every kind is data: the server gives the neutral code of a characteristic, the administrator
/// gives its texts, its unit of measure and its values, and the links between a group of the
/// parts, a characteristic and a part are rows of their own tables. So a new characteristic and
/// a new group of the parts get their characteristics without a release of the server
/// (documentation/tech/09, section 2.2).
/// </para>
/// <para>
/// A characteristic is archived and comes back from the archive, and the links to it keep
/// working. A link has no archive of its own: its tables hold no flag, and a row of the admin
/// panel is never deleted (documentation/tech/09, section 10a.4), so the administrator changes
/// the link or archives the characteristic it points at.
/// </para>
/// </remarks>
public sealed class CharacteristicReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the characteristics of the parts.</summary>
    public const string CharacteristicKind = "characteristic";

    /// <summary>The kind of the characteristics of a group of the parts.</summary>
    public const string PartGroupCharacteristicKind = "part_group_characteristic";

    /// <summary>The kind of the values of a characteristic of a part.</summary>
    public const string PartCharacteristicKind = "part_characteristic";

    private const string LabelPrefix = "admin.reference.";

    /// <summary>The name of the field of the unit of measure of a characteristic.</summary>
    private const string UnitField = "unit";

    /// <summary>The name of the field of the order in the lists.</summary>
    private const string SortOrderField = "sort_order";

    /// <summary>The name of the field of the group of the parts.</summary>
    private const string GroupField = "part_group_id";

    /// <summary>The name of the field of the characteristic.</summary>
    private const string CharacteristicField = "characteristic_id";

    /// <summary>The name of the field of the part.</summary>
    private const string PartField = "part_type_id";

    /// <summary>The name of the field of the value of a characteristic.</summary>
    private const string NumberValueField = "number_value";

    /// <summary>The name of the field of the obligation of a characteristic of a group.</summary>
    private const string IsRequiredField = "is_required";

    /// <summary>The place of the characteristics in the menu of the reference kinds.</summary>
    private const int CharacteristicSortOrder = 63;

    /// <summary>The place of the characteristics of the groups of the parts in the menu.</summary>
    private const int PartGroupCharacteristicSortOrder = 64;

    /// <summary>The place of the values of the characteristics of the parts in the menu.</summary>
    private const int PartCharacteristicSortOrder = 65;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(CharacteristicReferenceAdminService).Assembly, ProductionReferenceAdminService.LabelTextsResource);

    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;

    /// <summary>
    /// Initializes a new instance of the <see cref="CharacteristicReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    public CharacteristicReferenceAdminService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        ProductionCatalogCache catalogCache,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _codes = codes;
        _texts = texts;
        _images = images;
    }

    /// <summary>
    /// Gets the kind of the characteristics of the parts.
    /// </summary>
    public static AdminReferenceDescriptor CharacteristicDescriptor { get; } = new(
        CharacteristicKind,
        ProductionTextKeys.CharacteristicCodeGroup,
        ProductionTextKeys.Characteristic,
        LabelPrefix + CharacteristicKind + ".title",
        CharacteristicSortOrder,
        "/production/characteristics",
        new AdminReferencePermissions(
            "admin.production_characteristics_read",
            "admin.production_characteristic_read",
            "admin.production_characteristic_added",
            "admin.production_characteristic_updated",
            "admin.production_characteristic_archived",
            "admin.production_characteristic_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(UnitField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.unit", HasLanguages: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
        ]);

    /// <summary>
    /// Gets the kind of the characteristics of a group of the parts.
    /// </summary>
    public static AdminReferenceDescriptor PartGroupCharacteristicDescriptor { get; } = new(
        PartGroupCharacteristicKind,
        ProductionTextKeys.CharacteristicCodeGroup,
        ProductionTextKeys.Characteristic,
        LabelPrefix + PartGroupCharacteristicKind + ".title",
        PartGroupCharacteristicSortOrder,
        "/production/part-group-characteristics",
        new AdminReferencePermissions(
            "admin.production_part_group_characteristics_read",
            "admin.production_part_group_characteristic_read",
            "admin.production_part_group_characteristic_added",
            "admin.production_part_group_characteristic_updated",
            "admin.production_part_group_characteristic_archived",
            "admin.production_part_group_characteristic_restored"),
        [
            new(
                GroupField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.part_group",
                IsRequired: true,
                InList: true,
                ReferenceKind: ProductionReferenceAdminService.PartGroupKind),
            new(
                CharacteristicField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.characteristic",
                IsRequired: true,
                InList: true,
                ReferenceKind: CharacteristicKind),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
            new(IsRequiredField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.is_required", InList: true),
        ]);

    /// <summary>
    /// Gets the kind of the values of a characteristic of a part.
    /// </summary>
    public static AdminReferenceDescriptor PartCharacteristicDescriptor { get; } = new(
        PartCharacteristicKind,
        ProductionTextKeys.CharacteristicCodeGroup,
        ProductionTextKeys.Characteristic,
        LabelPrefix + PartCharacteristicKind + ".title",
        PartCharacteristicSortOrder,
        "/production/part-characteristics",
        new AdminReferencePermissions(
            "admin.production_part_characteristics_read",
            "admin.production_part_characteristic_read",
            "admin.production_part_characteristic_added",
            "admin.production_part_characteristic_updated",
            "admin.production_part_characteristic_archived",
            "admin.production_part_characteristic_restored"),
        [
            new(
                PartField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.part",
                IsRequired: true,
                InList: true,
                ReferenceKind: ProductionReferenceAdminService.PartKind),
            new(
                CharacteristicField,
                AdminReferenceFieldTypes.Reference,
                LabelPrefix + "field.characteristic",
                IsRequired: true,
                InList: true,
                ReferenceKind: CharacteristicKind),
            new(NumberValueField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.number_value", IsRequired: true, InList: true, IsDecimal: true),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } =
        [CharacteristicDescriptor, PartGroupCharacteristicDescriptor, PartCharacteristicDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        List<AdminReferenceRow> rows = kind switch
        {
            CharacteristicKind => [.. (await _dbContext.Characteristics.AsNoTracking()
                .OrderBy(characteristic => characteristic.SortOrder)
                .ThenBy(characteristic => characteristic.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(characteristic => new AdminReferenceRow(
                    characteristic.Id,
                    characteristic.Code,
                    characteristic.NameKey,
                    characteristic.ImageKey,
                    characteristic.IsArchived,
                    characteristic.Version,
                    new Dictionary<string, object?> { [SortOrderField] = characteristic.SortOrder }))],
            PartGroupCharacteristicKind => await GroupCharacteristicRowsAsync(cancellationToken).ConfigureAwait(false),
            PartCharacteristicKind => await PartCharacteristicRowsAsync(cancellationToken).ConfigureAwait(false),
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
            case CharacteristicKind:
                {
                    CharacteristicEntity? characteristic = await _dbContext.Characteristics.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (characteristic is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        characteristic.Id,
                        characteristic.Code,
                        characteristic.NameKey,
                        characteristic.DescriptionKey,
                        characteristic.ImageKey,
                        characteristic.IsArchived,
                        characteristic.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.ImageField] = characteristic.ImageKey,
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, characteristic.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, characteristic.DescriptionKey),
                            [UnitField] = UnitTextsOf(packs, characteristic.UnitKey),
                            [SortOrderField] = characteristic.SortOrder,
                        });
                }

            case PartGroupCharacteristicKind:
                {
                    PartGroupCharacteristicEntity? link = await _dbContext.PartGroupCharacteristics.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (link is null)
                    {
                        return null;
                    }

                    CharacteristicEntity? characteristic = await _dbContext.Characteristics.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == link.CharacteristicId, cancellationToken).ConfigureAwait(false);

                    if (characteristic is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        link.Id,
                        characteristic.Code,
                        characteristic.NameKey,
                        characteristic.DescriptionKey,
                        null,
                        false,
                        link.Version,
                        new Dictionary<string, object?>
                        {
                            [GroupField] = link.PartGroupId,
                            [CharacteristicField] = link.CharacteristicId,
                            [SortOrderField] = link.SortOrder,
                            [IsRequiredField] = link.IsRequired,
                        });
                }

            case PartCharacteristicKind:
                {
                    PartCharacteristicValueEntity? value = await _dbContext.PartCharacteristicValues.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (value is null)
                    {
                        return null;
                    }

                    CharacteristicEntity? characteristic = await _dbContext.Characteristics.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == value.CharacteristicId, cancellationToken).ConfigureAwait(false);

                    if (characteristic is null)
                    {
                        return null;
                    }

                    return new AdminReferenceRecord(
                        value.Id,
                        characteristic.Code,
                        characteristic.NameKey,
                        characteristic.DescriptionKey,
                        null,
                        false,
                        value.Version,
                        new Dictionary<string, object?>
                        {
                            [PartField] = value.PartTypeId,
                            [CharacteristicField] = value.CharacteristicId,
                            [NumberValueField] = value.NumberValue,
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

        AdminReferenceChange change = kind switch
        {
            CharacteristicKind => await CreateCharacteristicAsync(AdminReferenceJson.Read<CharacteristicValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            PartGroupCharacteristicKind => await CreateGroupCharacteristicAsync(AdminReferenceJson.Read<GroupCharacteristicValues>(input.Values), cancellationToken).ConfigureAwait(false),
            PartCharacteristicKind => await CreatePartCharacteristicAsync(AdminReferenceJson.Read<PartCharacteristicValues>(input.Values), cancellationToken).ConfigureAwait(false),
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
            CharacteristicKind => await UpdateCharacteristicAsync(id, input.Version, AdminReferenceJson.Read<CharacteristicValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            PartGroupCharacteristicKind => await UpdateGroupCharacteristicAsync(id, input.Version, AdminReferenceJson.Read<GroupCharacteristicValues>(input.Values), cancellationToken).ConfigureAwait(false),
            PartCharacteristicKind => await UpdatePartCharacteristicAsync(id, input.Version, AdminReferenceJson.Read<PartCharacteristicValues>(input.Values), cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        Committed();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> SetArchivedAsync(string kind, int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (kind is PartGroupCharacteristicKind)
        {
            return await SetGroupLinkArchivedAsync(id, isArchived, input, cancellationToken).ConfigureAwait(false);
        }

        if (kind is PartCharacteristicKind)
        {
            return await SetPartValueArchivedAsync(id, isArchived, input, cancellationToken).ConfigureAwait(false);
        }

        if (kind != CharacteristicKind)
        {
            throw UnknownKind(kind);
        }

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                CharacteristicEntity characteristic = await LockCharacteristicAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(characteristic.Version, input.Version);

                if (characteristic.IsArchived == isArchived)
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                }

                characteristic.IsArchived = isArchived;
                characteristic.Version++;

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(CharacteristicKind, characteristic.Id),
                    new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                    new Dictionary<string, object?> { ["is_archived"] = isArchived },
                    characteristic.Id,
                    characteristic.Version);
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    /// <summary>
    /// Archives or restores the link between a group of the parts and a characteristic. The link
    /// stays where it already is, but no new part of the group gets the characteristic
    /// (documentation/tech/09, section 10a.4).
    /// </summary>
    private Task<AdminReferenceChange> SetGroupLinkArchivedAsync(int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                PartGroupCharacteristicEntity link = await LockGroupCharacteristicAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(link.Version, input.Version);

                if (link.IsArchived == isArchived)
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                }

                link.IsArchived = isArchived;
                link.Version++;

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(PartGroupCharacteristicKind, link.Id),
                    new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                    new Dictionary<string, object?> { ["is_archived"] = isArchived },
                    link.Id,
                    link.Version);
            },
            cancellationToken);

    /// <summary>
    /// Archives or restores the value of a characteristic of a part. The value stays where it
    /// already is, but no new part gets it (documentation/tech/09, section 10a.4).
    /// </summary>
    private Task<AdminReferenceChange> SetPartValueArchivedAsync(int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                PartCharacteristicValueEntity value = await LockPartCharacteristicAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(value.Version, input.Version);

                if (value.IsArchived == isArchived)
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { ["is_archived"] = [AdminReferenceErrors.StateUnchanged] });
                }

                value.IsArchived = isArchived;
                value.Version++;

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(PartCharacteristicKind, value.Id),
                    new Dictionary<string, object?> { ["is_archived"] = !isArchived },
                    new Dictionary<string, object?> { ["is_archived"] = isArchived },
                    value.Id,
                    value.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreateCharacteristicAsync(CharacteristicValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckCharacteristicAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(ProductionTextKeys.CharacteristicCodeGroup, cancellationToken).ConfigureAwait(false);
                CharacteristicEntity characteristic = new()
                {
                    Code = code,
                    NameKey = ProductionTextKeys.CharacteristicName(code),
                    DescriptionKey = ProductionTextKeys.CharacteristicDescription(code),
                    UnitKey = UnitKeyOf(code, values.Unit),
                    SortOrder = values.SortOrder ?? 0,
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                };

                _dbContext.Characteristics.Add(characteristic);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    [
                        .. AdminReferenceRules.Entries(_texts, characteristic.NameKey, characteristic.DescriptionKey, values.Name!, values.Description!),
                        .. UnitEntriesOf(_texts, characteristic.UnitKey, values.Unit),
                    ],
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(CharacteristicKind, characteristic.Id),
                    null,
                    new { code, values = values.Trimmed(_texts) },
                    characteristic.Id,
                    characteristic.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateCharacteristicAsync(int id, int version, CharacteristicValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                CharacteristicEntity characteristic = await LockCharacteristicAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(characteristic.Version, version);
                await CheckCharacteristicAsync(values, characteristic, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                Dictionary<string, object?> before = new()
                {
                    [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, characteristic.NameKey),
                    [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, characteristic.DescriptionKey),
                    [AdminReferenceRules.ImageField] = characteristic.ImageKey,
                    [UnitField] = UnitTextsOf(packs, characteristic.UnitKey),
                    [SortOrderField] = characteristic.SortOrder,
                };

                characteristic.SortOrder = values.SortOrder ?? 0;
                characteristic.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                characteristic.UnitKey = UnitKeyOf(characteristic.Code, values.Unit);
                characteristic.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.SetAsync(
                    [
                        .. AdminReferenceRules.Entries(_texts, characteristic.NameKey, characteristic.DescriptionKey, values.Name!, values.Description!),
                        .. UnitEntriesOf(_texts, characteristic.UnitKey, values.Unit),
                    ],
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(CharacteristicKind, characteristic.Id),
                    before,
                    values.Trimmed(_texts),
                    characteristic.Id,
                    characteristic.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreateGroupCharacteristicAsync(GroupCharacteristicValues values, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckGroupCharacteristicAsync(values, null, cancellationToken).ConfigureAwait(false);

                PartGroupCharacteristicEntity link = new()
                {
                    PartGroupId = values.PartGroupId!.Value,
                    CharacteristicId = values.CharacteristicId!.Value,
                    SortOrder = values.SortOrder ?? 0,
                    IsRequired = values.IsRequired,
                };

                _dbContext.PartGroupCharacteristics.Add(link);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(PartGroupCharacteristicKind, link.Id),
                    null,
                    values.ToDictionary(),
                    link.Id,
                    link.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateGroupCharacteristicAsync(int id, int version, GroupCharacteristicValues values, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                PartGroupCharacteristicEntity link = await LockGroupCharacteristicAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(link.Version, version);
                await CheckGroupCharacteristicAsync(values, link, cancellationToken).ConfigureAwait(false);

                Dictionary<string, object?> before = new()
                {
                    [GroupField] = link.PartGroupId,
                    [CharacteristicField] = link.CharacteristicId,
                    [SortOrderField] = link.SortOrder,
                    [IsRequiredField] = link.IsRequired,
                };

                link.PartGroupId = values.PartGroupId!.Value;
                link.CharacteristicId = values.CharacteristicId!.Value;
                link.SortOrder = values.SortOrder ?? 0;
                link.IsRequired = values.IsRequired;
                link.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(PartGroupCharacteristicKind, link.Id),
                    before,
                    values.ToDictionary(),
                    link.Id,
                    link.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreatePartCharacteristicAsync(PartCharacteristicValues values, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckPartCharacteristicAsync(values, null, cancellationToken).ConfigureAwait(false);

                PartCharacteristicValueEntity value = new()
                {
                    PartTypeId = values.PartTypeId!.Value,
                    CharacteristicId = values.CharacteristicId!.Value,
                    NumberValue = values.NumberValue!.Value,
                };

                _dbContext.PartCharacteristicValues.Add(value);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(PartCharacteristicKind, value.Id),
                    null,
                    values.ToDictionary(),
                    value.Id,
                    value.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdatePartCharacteristicAsync(int id, int version, PartCharacteristicValues values, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                PartCharacteristicValueEntity value = await LockPartCharacteristicAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(value.Version, version);
                await CheckPartCharacteristicAsync(values, value, cancellationToken).ConfigureAwait(false);

                Dictionary<string, object?> before = new()
                {
                    [PartField] = value.PartTypeId,
                    [CharacteristicField] = value.CharacteristicId,
                    [NumberValueField] = value.NumberValue,
                };

                value.PartTypeId = values.PartTypeId!.Value;
                value.CharacteristicId = values.CharacteristicId!.Value;
                value.NumberValue = values.NumberValue!.Value;
                value.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(PartCharacteristicKind, value.Id),
                    before,
                    values.ToDictionary(),
                    value.Id,
                    value.Version);
            },
            cancellationToken);

    /// <summary>
    /// Checks the values of a characteristic: the texts in every language, the unit of measure
    /// (it may be missing), the order in the lists and the picture.
    /// </summary>
    private async Task CheckCharacteristicAsync(CharacteristicValues values, CharacteristicEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);
        CheckUnit(failures, values.Unit);

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Checks the values of a link between a group of the parts and a characteristic: both rows
    /// exist and are not archived, the order in the lists is right and the pair of the group and
    /// the characteristic is free.
    /// </summary>
    private async Task CheckGroupCharacteristicAsync(GroupCharacteristicValues values, PartGroupCharacteristicEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];

        if (values.PartGroupId is not { } groupId)
        {
            AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Required);
        }
        else
        {
            PartGroupEntity? group = await _dbContext.PartGroups.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.Id == groupId, cancellationToken).ConfigureAwait(false);

            if (group is null)
            {
                AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Unknown);
            }
            else if (group.IsArchived && stored?.PartGroupId != groupId)
            {
                // An archived group keeps its characteristics, but a new choice of it is refused
                // (task 00023, decision 5).
                AdminReferenceRules.Fail(failures, GroupField, AdminReferenceErrors.Archived);
            }
        }

        if (values.CharacteristicId is not { } characteristicId)
        {
            AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Required);
        }
        else
        {
            CharacteristicEntity? characteristic = await _dbContext.Characteristics.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.Id == characteristicId, cancellationToken).ConfigureAwait(false);

            if (characteristic is null)
            {
                AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Unknown);
            }
            else if (characteristic.IsArchived && stored?.CharacteristicId != characteristicId)
            {
                AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Archived);
            }
        }

        if (values.SortOrder is null or < 0)
        {
            AdminReferenceRules.Fail(failures, SortOrderField, values.SortOrder is null ? AdminReferenceErrors.Required : AdminReferenceErrors.OutOfBounds);
        }

        // A group has a characteristic once, so the pair is free.
        int storedId = stored?.Id ?? 0;

        if (values.PartGroupId is { } storedGroupId
            && values.CharacteristicId is { } storedCharacteristicId
            && await _dbContext.PartGroupCharacteristics.AsNoTracking()
                .AnyAsync(
                    link => link.Id != storedId && link.PartGroupId == storedGroupId && link.CharacteristicId == storedCharacteristicId,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Duplicate);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Checks the values of a link between a part and a characteristic: both rows exist and are
    /// not archived, the value is a number and the pair of the part and the characteristic is
    /// free.
    /// </summary>
    private async Task CheckPartCharacteristicAsync(PartCharacteristicValues values, PartCharacteristicValueEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];

        if (values.PartTypeId is not { } partTypeId)
        {
            AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Required);
        }
        else
        {
            PartTypeEntity? part = await _dbContext.PartTypes.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.Id == partTypeId, cancellationToken).ConfigureAwait(false);

            if (part is null)
            {
                AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Unknown);
            }
            else if (part.IsArchived && stored?.PartTypeId != partTypeId)
            {
                AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Archived);
            }
        }

        if (values.CharacteristicId is not { } characteristicId)
        {
            AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Required);
        }
        else
        {
            CharacteristicEntity? characteristic = await _dbContext.Characteristics.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.Id == characteristicId, cancellationToken).ConfigureAwait(false);

            if (characteristic is null)
            {
                AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Unknown);
            }
            else if (characteristic.IsArchived && stored?.CharacteristicId != characteristicId)
            {
                AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Archived);
            }
        }

        if (values.NumberValue is null)
        {
            AdminReferenceRules.Fail(failures, NumberValueField, AdminReferenceErrors.Required);
        }

        // A part has a value of a characteristic once, so the pair is free.
        int storedId = stored?.Id ?? 0;

        if (values.PartTypeId is { } storedPartId
            && values.CharacteristicId is { } storedCharacteristicId
            && await _dbContext.PartCharacteristicValues.AsNoTracking()
                .AnyAsync(
                    value => value.Id != storedId && value.PartTypeId == storedPartId && value.CharacteristicId == storedCharacteristicId,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, CharacteristicField, AdminReferenceErrors.Duplicate);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Checks the unit of measure of a characteristic: it may be missing, but a text that is
    /// there is not longer than the largest text of the texts.
    /// </summary>
    private void CheckUnit(Dictionary<string, List<string>> failures, Dictionary<string, string>? unit)
    {
        foreach (string language in _texts.Languages)
        {
            string text = unit?.GetValueOrDefault(language)?.Trim() ?? string.Empty;

            if (text.Length > _texts.MaxLength)
            {
                AdminReferenceRules.Fail(failures, UnitField + "." + language, AdminReferenceErrors.TooLong);
            }
        }
    }

    /// <summary>
    /// Gets the rows of the links between the groups of the parts and the characteristics. The
    /// name and the code of a link are the ones of its characteristic, so the list shows what the
    /// link is about.
    /// </summary>
    private async Task<List<AdminReferenceRow>> GroupCharacteristicRowsAsync(CancellationToken cancellationToken)
    {
        List<PartGroupCharacteristicEntity> links = await _dbContext.PartGroupCharacteristics.AsNoTracking()
            .OrderBy(link => link.PartGroupId)
            .ThenBy(link => link.SortOrder)
            .ThenBy(link => link.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<int, CharacteristicEntity> characteristics = await _dbContext.Characteristics.AsNoTracking()
            .ToDictionaryAsync(characteristic => characteristic.Id, cancellationToken).ConfigureAwait(false);

        return
        [
            .. links
                .Where(link => characteristics.ContainsKey(link.CharacteristicId))
                .Select(link =>
                {
                    CharacteristicEntity characteristic = characteristics[link.CharacteristicId];

                    return new AdminReferenceRow(
                        link.Id,
                        characteristic.Code,
                        characteristic.NameKey,
                        null,
                        false,
                        link.Version,
                        new Dictionary<string, object?>
                        {
                            [GroupField] = link.PartGroupId,
                            [CharacteristicField] = link.CharacteristicId,
                            [SortOrderField] = link.SortOrder,
                            [IsRequiredField] = link.IsRequired,
                        });
                }),
        ];
    }

    /// <summary>
    /// Gets the rows of the values of the characteristics of the parts. The name and the code of
    /// a value are the ones of its characteristic, so the list shows what the value is about.
    /// </summary>
    private async Task<List<AdminReferenceRow>> PartCharacteristicRowsAsync(CancellationToken cancellationToken)
    {
        List<PartCharacteristicValueEntity> values = await _dbContext.PartCharacteristicValues.AsNoTracking()
            .OrderBy(value => value.PartTypeId)
            .ThenBy(value => value.CharacteristicId)
            .ThenBy(value => value.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<int, CharacteristicEntity> characteristics = await _dbContext.Characteristics.AsNoTracking()
            .ToDictionaryAsync(characteristic => characteristic.Id, cancellationToken).ConfigureAwait(false);

        return
        [
            .. values
                .Where(value => characteristics.ContainsKey(value.CharacteristicId))
                .Select(value =>
                {
                    CharacteristicEntity characteristic = characteristics[value.CharacteristicId];

                    return new AdminReferenceRow(
                        value.Id,
                        characteristic.Code,
                        characteristic.NameKey,
                        null,
                        false,
                        value.Version,
                        new Dictionary<string, object?>
                        {
                            [PartField] = value.PartTypeId,
                            [CharacteristicField] = value.CharacteristicId,
                            [NumberValueField] = value.NumberValue,
                        });
                }),
        ];
    }

    private async Task<CharacteristicEntity> LockCharacteristicAsync(int id, CancellationToken cancellationToken)
    {
        // The row is locked first, so two changes of one row compare their versions one after
        // the other and the second one meets the new version (task 00023, decision 5).
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM characteristic WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.Characteristics.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    private async Task<PartGroupCharacteristicEntity> LockGroupCharacteristicAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM part_group_characteristic WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.PartGroupCharacteristics.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    private async Task<PartCharacteristicValueEntity> LockPartCharacteristicAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM part_characteristic_value WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.PartCharacteristicValues.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    /// <summary>
    /// Drops the copies of the catalog and of the texts after the change is committed, so a
    /// player and the form see it at once.
    /// </summary>
    private void Committed()
    {
        _catalogCache.Invalidate();
        _texts.Invalidate();
    }

    private static ArgumentException UnknownKind(string kind) => new($"The production module has no reference kind '{kind}'.", nameof(kind));

    /// <summary>
    /// Gets the key of the unit of measure of a characteristic; a characteristic without a unit
    /// has none.
    /// </summary>
    private static string? UnitKeyOf(string code, Dictionary<string, string>? unit) =>
        unit is not null && unit.Values.Any(value => !string.IsNullOrWhiteSpace(value))
            ? ProductionTextKeys.CharacteristicUnit(code)
            : null;

    /// <summary>
    /// Gets the texts of the unit of measure of a characteristic by language; a characteristic
    /// without a unit has an empty text in every language.
    /// </summary>
    private static Dictionary<string, string> UnitTextsOf(IReadOnlyList<LocalizationPack> packs, string? unitKey) =>
        unitKey is null
            ? packs.ToDictionary(pack => pack.Language, _ => string.Empty, StringComparer.Ordinal)
            : AdminReferenceRules.TextsOf(packs, unitKey);

    /// <summary>
    /// Gets the texts of the unit of measure of a characteristic; a language without a text has
    /// none.
    /// </summary>
    private static List<LocalizationTextEntry> UnitEntriesOf(ILocalizationTexts texts, string? unitKey, Dictionary<string, string>? unit)
    {
        if (unitKey is null || unit is null)
        {
            return [];
        }

        return
        [
            .. texts.Languages
                .Where(language => !string.IsNullOrWhiteSpace(unit.GetValueOrDefault(language)))
                .Select(language => new LocalizationTextEntry(unitKey, language, unit[language].Trim())),
        ];
    }

    /// <summary>
    /// The values of the form of a characteristic.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="Unit">The unit of measure by language; it may be missing.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    private sealed record CharacteristicValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        Dictionary<string, string>? Unit,
        int? SortOrder,
        string? ImageKey)
    {
        public CharacteristicValues Trimmed(ILocalizationTexts texts) => this with
        {
            Name = texts.Languages.ToDictionary(language => language, language => Name?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Description = texts.Languages.ToDictionary(language => language, language => Description?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
            Unit = texts.Languages.ToDictionary(language => language, language => Unit?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal),
        };

        public Dictionary<string, object?> ToDictionary() => new()
        {
            [AdminReferenceRules.ImageField] = ImageKey,
            [AdminReferenceRules.NameField] = Name,
            [AdminReferenceRules.DescriptionField] = Description,
            [UnitField] = Unit,
            [SortOrderField] = SortOrder,
        };
    }

    /// <summary>
    /// The values of the form of a link between a group of the parts and a characteristic.
    /// </summary>
    /// <param name="PartGroupId">The group of the parts.</param>
    /// <param name="CharacteristicId">The characteristic.</param>
    /// <param name="SortOrder">The order in the list of the group.</param>
    /// <param name="IsRequired">Whether a part of the group must have the value of the characteristic.</param>
    private sealed record GroupCharacteristicValues(
        int? PartGroupId,
        int? CharacteristicId,
        int? SortOrder,
        bool IsRequired)
    {
        public Dictionary<string, object?> ToDictionary() => new()
        {
            [GroupField] = PartGroupId,
            [CharacteristicField] = CharacteristicId,
            [SortOrderField] = SortOrder,
            [IsRequiredField] = IsRequired,
        };
    }

    /// <summary>
    /// The values of the form of a value of a characteristic of a part.
    /// </summary>
    /// <param name="PartTypeId">The part.</param>
    /// <param name="CharacteristicId">The characteristic.</param>
    /// <param name="NumberValue">The value of the characteristic.</param>
    private sealed record PartCharacteristicValues(
        int? PartTypeId,
        int? CharacteristicId,
        decimal? NumberValue)
    {
        public Dictionary<string, object?> ToDictionary() => new()
        {
            [PartField] = PartTypeId,
            [CharacteristicField] = CharacteristicId,
            [NumberValueField] = NumberValue,
        };
    }
}

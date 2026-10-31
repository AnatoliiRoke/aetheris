using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Services;

/// <summary>
/// The reference kinds of the market in the admin panel: the levels of the market and the
/// reference prices of the items (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// <para>
/// Every kind is data: the server gives the neutral code of a level, the administrator gives
/// its texts, its scope, its commission and whether it takes lots. The row and its texts are
/// committed together, and the copy of the texts in memory is dropped after the commit, so a
/// player sees the change at once.
/// </para>
/// <para>
/// A price is the price of one unit of a resource of a body or of a part, so it names one of
/// them and never both. A row is never deleted: it is archived and can come back from the
/// archive (documentation/tech/09, section 10a.4).
/// </para>
/// </remarks>
public sealed class MarketReferenceAdminService : IAdminReferenceService
{
    /// <summary>The kind of the levels of the market.</summary>
    public const string MarketLevelKind = "market_level";

    /// <summary>The kind of the reference prices of the items.</summary>
    public const string ReferencePriceKind = "market_reference_price";

    /// <summary>The name of the embedded texts of the labels.</summary>
    public const string LabelTextsResource = "Aetheris.Market.admin-reference-texts.json";

    private const string LabelPrefix = "admin.reference.";

    /// <summary>The name of the field of the scope of a level.</summary>
    private const string VisibilityField = "visibility";

    /// <summary>The name of the field of the commission of a level.</summary>
    private const string CommissionField = "commission_percent";

    /// <summary>The name of the field of the activity of a level.</summary>
    private const string IsActiveField = "is_active";

    /// <summary>The name of the field of the order in the lists.</summary>
    private const string SortOrderField = "sort_order";

    /// <summary>The name of the field of the resource of a price.</summary>
    private const string ResourceField = "resource_type_id";

    /// <summary>The name of the field of the part of a price.</summary>
    private const string PartField = "part_type_id";

    /// <summary>The name of the field of the price of one unit.</summary>
    private const string PriceField = "price";

    /// <summary>
    /// The kind of the resources of the world, which the price of a resource picks from.
    /// </summary>
    private const string ResourceReferenceKind = "resource";

    /// <summary>
    /// The kind of the parts of the production, which the price of a part picks from.
    /// </summary>
    private const string PartReferenceKind = "part";

    /// <summary>The place of the levels of the market in the menu of the reference kinds.</summary>
    private const int MarketLevelSortOrder = 66;

    /// <summary>The place of the reference prices in the menu of the reference kinds.</summary>
    private const int ReferencePriceSortOrder = 67;

    /// <summary>The texts of the labels, read once from the embedded file.</summary>
    private static readonly IReadOnlyList<LocalizationTextEntry> Labels =
        AdminReferenceRules.LoadLabelTexts(typeof(MarketReferenceAdminService).Assembly, LabelTextsResource);

    private readonly MarketDbContext _dbContext;
    private readonly MarketUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly IWorldModule _world;
    private readonly IProductionModule _production;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketReferenceAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="world">The world, for the resources.</param>
    /// <param name="production">The production, for the parts.</param>
    public MarketReferenceAdminService(
        MarketDbContext dbContext,
        MarketUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        IWorldModule world,
        IProductionModule production)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(production);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
        _world = world;
        _production = production;
    }

    /// <summary>
    /// Gets the kind of the levels of the market.
    /// </summary>
    public static AdminReferenceDescriptor MarketLevelDescriptor { get; } = new(
        MarketLevelKind,
        MarketCodeGroups.MarketLevel,
        MarketTextKeys.MarketLevel,
        LabelPrefix + MarketLevelKind + ".title",
        MarketLevelSortOrder,
        "/market/levels",
        new AdminReferencePermissions(
            "admin.market_levels_read",
            "admin.market_level_read",
            "admin.market_level_added",
            "admin.market_level_updated",
            "admin.market_level_archived",
            "admin.market_level_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(
                VisibilityField,
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.visibility",
                IsRequired: true,
                InList: true,
                Options: [.. MarketVisibilities.All.Select(value => new AdminReferenceOption(value, LabelPrefix + "visibility." + value))]),
            new(CommissionField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.commission_percent", IsRequired: true, InList: true, Minimum: 0, IsDecimal: true),
            new(IsActiveField, AdminReferenceFieldTypes.Flag, LabelPrefix + "field.is_active", IsRequired: true, InList: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", InList: true),
        ]);

    /// <summary>
    /// Gets the kind of the reference prices of the items.
    /// </summary>
    /// <remarks>
    /// The rows of this kind have no code and no texts of their own: they name the item they
    /// price, so the list shows the name of the resource or of the part.
    /// </remarks>
    public static AdminReferenceDescriptor ReferencePriceDescriptor { get; } = new(
        ReferencePriceKind,
        MarketCodeGroups.ReferencePrice,
        MarketTextKeys.ReferencePrice,
        LabelPrefix + ReferencePriceKind + ".title",
        ReferencePriceSortOrder,
        "/market/reference-prices",
        new AdminReferencePermissions(
            "admin.market_reference_prices_read",
            "admin.market_reference_price_read",
            "admin.market_reference_price_added",
            "admin.market_reference_price_updated",
            "admin.market_reference_price_archived",
            "admin.market_reference_price_restored"),
        [
            new(ResourceField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.resource", InList: true, ReferenceKind: ResourceReferenceKind),
            new(PartField, AdminReferenceFieldTypes.Reference, LabelPrefix + "field.part", InList: true, ReferenceKind: PartReferenceKind),
            new(PriceField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.price", IsRequired: true, InList: true, Minimum: 0),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", InList: true),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [MarketLevelDescriptor, ReferencePriceDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts => Labels;

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        List<AdminReferenceRow> rows = kind switch
        {
            MarketLevelKind => [.. (await _dbContext.MarketLevels.AsNoTracking()
                .OrderBy(level => level.SortOrder)
                .ThenBy(level => level.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .Select(level => new AdminReferenceRow(
                    level.Id,
                    level.Code,
                    level.NameKey,
                    level.ImageKey,
                    level.IsArchived,
                    level.Version,
                    new Dictionary<string, object?>
                    {
                        [VisibilityField] = level.Visibility,
                        [CommissionField] = level.CommissionPercent,
                        [IsActiveField] = level.IsActive,
                        [SortOrderField] = level.SortOrder,
                    }))],
            ReferencePriceKind => await PriceRowsAsync(cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        return AdminReferenceRules.Page(rows, query, packs);
    }

    /// <inheritdoc />
    public async Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case MarketLevelKind:
                {
                    MarketLevelEntity? level = await _dbContext.MarketLevels.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (level is null)
                    {
                        return null;
                    }

                    IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

                    return new AdminReferenceRecord(
                        level.Id,
                        level.Code,
                        level.NameKey,
                        level.DescriptionKey,
                        level.ImageKey,
                        level.IsArchived,
                        level.Version,
                        new Dictionary<string, object?>
                        {
                            [AdminReferenceRules.ImageField] = level.ImageKey,
                            [AdminReferenceRules.NameField] = AdminReferenceRules.TextsOf(packs, level.NameKey),
                            [AdminReferenceRules.DescriptionField] = AdminReferenceRules.TextsOf(packs, level.DescriptionKey),
                            [VisibilityField] = level.Visibility,
                            [CommissionField] = level.CommissionPercent,
                            [IsActiveField] = level.IsActive,
                            [SortOrderField] = level.SortOrder,
                        });
                }

            case ReferencePriceKind:
                {
                    ReferencePriceEntity? price = await _dbContext.ReferencePrices.AsNoTracking()
                        .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

                    if (price is null)
                    {
                        return null;
                    }

                    (IReadOnlyDictionary<int, ResourceTypeFact> resources, IReadOnlyDictionary<int, ProductionPartSummaryDto> parts) =
                        await ItemsAsync(cancellationToken).ConfigureAwait(false);

                    (string code, string nameKey, string descriptionKey) = SubjectOf(price, resources, parts);

                    return new AdminReferenceRecord(
                        price.Id,
                        code,
                        nameKey,
                        descriptionKey,
                        null,
                        price.IsArchived,
                        price.Version,
                        new Dictionary<string, object?>
                        {
                            [ResourceField] = price.ResourceTypeId,
                            [PartField] = price.PartTypeId,
                            [PriceField] = price.Price,
                            [SortOrderField] = price.SortOrder,
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
            MarketLevelKind => await CreateLevelAsync(AdminReferenceJson.Read<LevelValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            ReferencePriceKind => await CreatePriceAsync(AdminReferenceJson.Read<PriceValues>(input.Values), cancellationToken).ConfigureAwait(false),
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
            MarketLevelKind => await UpdateLevelAsync(id, input.Version, AdminReferenceJson.Read<LevelValues>(input.Values), adminId, cancellationToken).ConfigureAwait(false),
            ReferencePriceKind => await UpdatePriceAsync(id, input.Version, AdminReferenceJson.Read<PriceValues>(input.Values), cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        Committed();

        return change;
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> SetArchivedAsync(string kind, int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        AdminReferenceChange change = kind switch
        {
            MarketLevelKind => await SetLevelArchivedAsync(id, isArchived, input, cancellationToken).ConfigureAwait(false),
            ReferencePriceKind => await SetPriceArchivedAsync(id, isArchived, input, cancellationToken).ConfigureAwait(false),
            _ => throw UnknownKind(kind),
        };

        Committed();

        return change;
    }

    private Task<AdminReferenceChange> CreateLevelAsync(LevelValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckLevelAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(MarketCodeGroups.MarketLevel, cancellationToken).ConfigureAwait(false);
                MarketLevelEntity level = new()
                {
                    Code = code,
                    Visibility = values.Visibility!,
                    CommissionPercent = values.CommissionPercent!.Value,
                    IsActive = values.IsActive!.Value,
                    SortOrder = values.SortOrder ?? 0,
                    NameKey = MarketTextKeys.Name(MarketTextKeys.MarketLevel, code),
                    DescriptionKey = MarketTextKeys.Description(MarketTextKeys.MarketLevel, code),
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                };

                _dbContext.MarketLevels.Add(level);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, level.NameKey, level.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(MarketLevelKind, level.Id),
                    null,
                    new { code, values = LevelValues.Of(level, values.Trimmed(_texts)) },
                    level.Id,
                    level.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdateLevelAsync(int id, int version, LevelValues values, string adminId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                MarketLevelEntity level = await LockLevelAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(level.Version, version);
                await CheckLevelAsync(values, level, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                LevelValues before = LevelValues.Of(level, packs);

                level.Visibility = values.Visibility!;
                level.CommissionPercent = values.CommissionPercent!.Value;
                level.IsActive = values.IsActive!.Value;
                level.SortOrder = values.SortOrder ?? 0;
                level.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                level.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, level.NameKey, level.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(MarketLevelKind, level.Id),
                    before,
                    LevelValues.Of(level, values.Trimmed(_texts)),
                    level.Id,
                    level.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> CreatePriceAsync(PriceValues values, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await CheckPriceAsync(values, null, cancellationToken).ConfigureAwait(false);

                ReferencePriceEntity price = new()
                {
                    ResourceTypeId = values.ResourceTypeId,
                    PartTypeId = values.PartTypeId,
                    Price = values.Price!.Value,
                    SortOrder = values.SortOrder ?? 0,
                };

                _dbContext.ReferencePrices.Add(price);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(ReferencePriceKind, price.Id),
                    null,
                    values.ToDictionary(),
                    price.Id,
                    price.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> UpdatePriceAsync(int id, int version, PriceValues values, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ReferencePriceEntity price = await LockPriceAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(price.Version, version);
                await CheckPriceAsync(values, price, cancellationToken).ConfigureAwait(false);

                Dictionary<string, object?> before = new()
                {
                    [ResourceField] = price.ResourceTypeId,
                    [PartField] = price.PartTypeId,
                    [PriceField] = price.Price,
                    [SortOrderField] = price.SortOrder,
                };

                price.ResourceTypeId = values.ResourceTypeId;
                price.PartTypeId = values.PartTypeId;
                price.Price = values.Price!.Value;
                price.SortOrder = values.SortOrder ?? 0;
                price.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(ReferencePriceKind, price.Id),
                    before,
                    values.ToDictionary(),
                    price.Id,
                    price.Version);
            },
            cancellationToken);

    private Task<AdminReferenceChange> SetLevelArchivedAsync(int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                MarketLevelEntity level = await LockLevelAsync(id, cancellationToken).ConfigureAwait(false);
                CheckState(level.Version, level.IsArchived, input.Version, isArchived);

                level.IsArchived = isArchived;
                level.Version++;

                return Archived(MarketLevelKind, level.Id, level.Version, isArchived);
            },
            cancellationToken);

    private Task<AdminReferenceChange> SetPriceArchivedAsync(int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                ReferencePriceEntity price = await LockPriceAsync(id, cancellationToken).ConfigureAwait(false);
                CheckState(price.Version, price.IsArchived, input.Version, isArchived);

                price.IsArchived = isArchived;
                price.Version++;

                return Archived(ReferencePriceKind, price.Id, price.Version, isArchived);
            },
            cancellationToken);

    /// <summary>
    /// Checks the values of a level: the texts in every language, a known scope, a commission that
    /// is not negative, whether the level takes lots and a stored picture.
    /// </summary>
    private async Task CheckLevelAsync(LevelValues values, MarketLevelEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];
        AdminReferenceRules.CheckTexts(failures, _texts, values.Name, values.Description);

        if (values.Visibility is not { } visibility)
        {
            AdminReferenceRules.Fail(failures, VisibilityField, AdminReferenceErrors.Required);
        }
        else if (!MarketVisibilities.All.Contains(visibility, StringComparer.Ordinal))
        {
            AdminReferenceRules.Fail(failures, VisibilityField, AdminReferenceErrors.Unknown);
        }

        if (values.CommissionPercent is null)
        {
            AdminReferenceRules.Fail(failures, CommissionField, AdminReferenceErrors.Required);
        }
        else if (values.CommissionPercent < 0)
        {
            AdminReferenceRules.Fail(failures, CommissionField, AdminReferenceErrors.OutOfBounds);
        }

        if (values.IsActive is null)
        {
            AdminReferenceRules.Fail(failures, IsActiveField, AdminReferenceErrors.Required);
        }

        await AdminReferenceRules.CheckImageAsync(failures, _images, AdminReferenceRules.ImageKey(values.ImageKey), stored?.ImageKey, cancellationToken)
            .ConfigureAwait(false);

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Checks the values of a price: the item is a resource or a part and never both, it exists
    /// and is not archived, the price is a number that is not negative and the item has no other
    /// price.
    /// </summary>
    private async Task CheckPriceAsync(PriceValues values, ReferencePriceEntity? stored, CancellationToken cancellationToken)
    {
        Dictionary<string, List<string>> failures = [];

        (IReadOnlyDictionary<int, ResourceTypeFact> resources, IReadOnlyDictionary<int, ProductionPartSummaryDto> parts) =
            await ItemsAsync(cancellationToken).ConfigureAwait(false);

        if (values.ResourceTypeId is not null && values.PartTypeId is not null)
        {
            // A price is of a resource or of a part, so both of them is a value that appears
            // twice.
            AdminReferenceRules.Fail(failures, ResourceField, AdminReferenceErrors.Duplicate);
            AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Duplicate);
        }
        else if (values.ResourceTypeId is null && values.PartTypeId is null)
        {
            AdminReferenceRules.Fail(failures, ResourceField, AdminReferenceErrors.Required);
            AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Required);
        }
        else if (values.ResourceTypeId is { } resourceTypeId)
        {
            if (!resources.TryGetValue(resourceTypeId, out ResourceTypeFact? resource))
            {
                AdminReferenceRules.Fail(failures, ResourceField, AdminReferenceErrors.Unknown);
            }
            else if (resource.IsArchived && stored?.ResourceTypeId != resourceTypeId)
            {
                // An archived resource keeps the price it has, but no new price takes it.
                AdminReferenceRules.Fail(failures, ResourceField, AdminReferenceErrors.Archived);
            }
        }
        else if (values.PartTypeId is { } partTypeId)
        {
            if (!parts.TryGetValue(partTypeId, out ProductionPartSummaryDto? part))
            {
                AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Unknown);
            }
            else if (part.IsArchived && stored?.PartTypeId != partTypeId)
            {
                AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Archived);
            }
        }

        if (values.Price is null)
        {
            AdminReferenceRules.Fail(failures, PriceField, AdminReferenceErrors.Required);
        }
        else if (values.Price < 0)
        {
            AdminReferenceRules.Fail(failures, PriceField, AdminReferenceErrors.OutOfBounds);
        }

        // An item has one price, so the pair is free.
        int storedId = stored?.Id ?? 0;

        if (values.ResourceTypeId is { } storedResourceId
            && await _dbContext.ReferencePrices.AsNoTracking()
                .AnyAsync(price => price.Id != storedId && price.ResourceTypeId == storedResourceId, cancellationToken)
                .ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, ResourceField, AdminReferenceErrors.Duplicate);
        }

        if (values.PartTypeId is { } storedPartId
            && await _dbContext.ReferencePrices.AsNoTracking()
                .AnyAsync(price => price.Id != storedId && price.PartTypeId == storedPartId, cancellationToken)
                .ConfigureAwait(false))
        {
            AdminReferenceRules.Fail(failures, PartField, AdminReferenceErrors.Duplicate);
        }

        AdminReferenceRules.ThrowIfInvalid(failures);
    }

    /// <summary>
    /// Gets the rows of the prices. The name and the code of a row are the ones of the item it
    /// prices, so the list shows what the price is about.
    /// </summary>
    private async Task<List<AdminReferenceRow>> PriceRowsAsync(CancellationToken cancellationToken)
    {
        List<ReferencePriceEntity> prices = await _dbContext.ReferencePrices.AsNoTracking()
            .OrderBy(price => price.SortOrder)
            .ThenBy(price => price.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        (IReadOnlyDictionary<int, ResourceTypeFact> resources, IReadOnlyDictionary<int, ProductionPartSummaryDto> parts) =
            await ItemsAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. prices.Select(price =>
            {
                (string code, string nameKey, _) = SubjectOf(price, resources, parts);

                return new AdminReferenceRow(
                    price.Id,
                    code,
                    nameKey,
                    null,
                    price.IsArchived,
                    price.Version,
                    new Dictionary<string, object?>
                    {
                        [ResourceField] = price.ResourceTypeId,
                        [PartField] = price.PartTypeId,
                        [PriceField] = price.Price,
                        [SortOrderField] = price.SortOrder,
                    });
            }),
        ];
    }

    /// <summary>
    /// Gets the items of the prices: the resources of the world and the parts of the production.
    /// They live in the tables of their modules, so they are read through their contracts.
    /// </summary>
    private async Task<(IReadOnlyDictionary<int, ResourceTypeFact> Resources, IReadOnlyDictionary<int, ProductionPartSummaryDto> Parts)> ItemsAsync(
        CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProductionPartSummaryDto> parts = await _production.GetPartsAsync(cancellationToken).ConfigureAwait(false);

        return (
            resources.Resources.ToDictionary(resource => resource.Id),
            parts.ToDictionary(part => part.Id));
    }

    /// <summary>
    /// Gets the code and the keys of the name and of the description of the item a price is of.
    /// </summary>
    private static (string Code, string NameKey, string DescriptionKey) SubjectOf(
        ReferencePriceEntity price,
        IReadOnlyDictionary<int, ResourceTypeFact> resources,
        IReadOnlyDictionary<int, ProductionPartSummaryDto> parts)
    {
        if (price.ResourceTypeId is { } resourceTypeId && resources.TryGetValue(resourceTypeId, out ResourceTypeFact? resource))
        {
            return (resource.Code, resource.NameKey, string.Empty);
        }

        if (price.PartTypeId is { } partTypeId && parts.TryGetValue(partTypeId, out ProductionPartSummaryDto? part))
        {
            return (part.Code, part.NameKey, part.DescriptionKey);
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    private async Task<MarketLevelEntity> LockLevelAsync(int id, CancellationToken cancellationToken)
    {
        // The row is locked first, so two changes of one row compare their versions one after
        // the other and the second one meets the new version (task 00023, decision 5).
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM market_level WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.MarketLevels.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    private async Task<ReferencePriceEntity> LockPriceAsync(int id, CancellationToken cancellationToken)
    {
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM market_reference_price WHERE id = {id} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);

        return await _dbContext.ReferencePrices.SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new AdminReferenceException(GameErrorCodes.NotFound);
    }

    /// <summary>
    /// Refuses a change from an older version and an archiving of an archived row (or a return of
    /// an active one).
    /// </summary>
    private static void CheckState(int storedVersion, bool storedArchived, int sentVersion, bool isArchived)
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

    /// <summary>
    /// Drops the copy of the texts after the change is committed, so a player and the form see it
    /// at once.
    /// </summary>
    private void Committed() => _texts.Invalidate();

    private static ArgumentException UnknownKind(string kind) => new($"The market module has no reference kind '{kind}'.", nameof(kind));

    /// <summary>
    /// The values of the form of a level.
    /// </summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="Visibility">The scope of the level.</param>
    /// <param name="CommissionPercent">The commission of the level in per cent.</param>
    /// <param name="IsActive">Whether the level takes lots.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    private sealed record LevelValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        string? Visibility,
        decimal? CommissionPercent,
        bool? IsActive,
        int? SortOrder,
        string? ImageKey)
    {
        public static LevelValues Of(MarketLevelEntity level, IReadOnlyList<LocalizationPack> packs) =>
            new(
                AdminReferenceRules.TextsOf(packs, level.NameKey),
                AdminReferenceRules.TextsOf(packs, level.DescriptionKey),
                level.Visibility,
                level.CommissionPercent,
                level.IsActive,
                level.SortOrder,
                level.ImageKey);

        public static LevelValues Of(MarketLevelEntity level, (Dictionary<string, string> Name, Dictionary<string, string> Description) texts) =>
            new(texts.Name, texts.Description, level.Visibility, level.CommissionPercent, level.IsActive, level.SortOrder, level.ImageKey);

        public (Dictionary<string, string> Name, Dictionary<string, string> Description) Trimmed(ILocalizationTexts texts) =>
            (Trim(texts, Name), Trim(texts, Description));
    }

    /// <summary>
    /// The values of the form of a price.
    /// </summary>
    /// <param name="ResourceTypeId">The resource of the price.</param>
    /// <param name="PartTypeId">The part of the price.</param>
    /// <param name="Price">The price of one unit of the item.</param>
    /// <param name="SortOrder">The order in the lists.</param>
    private sealed record PriceValues(
        int? ResourceTypeId,
        int? PartTypeId,
        long? Price,
        int? SortOrder)
    {
        public Dictionary<string, object?> ToDictionary() => new()
        {
            [ResourceField] = ResourceTypeId,
            [PartField] = PartTypeId,
            [PriceField] = Price,
            [SortOrderField] = SortOrder,
        };
    }

    private static Dictionary<string, string> Trim(ILocalizationTexts texts, Dictionary<string, string>? values) =>
        texts.Languages.ToDictionary(language => language, language => values?.GetValueOrDefault(language)?.Trim() ?? string.Empty, StringComparer.Ordinal);
}

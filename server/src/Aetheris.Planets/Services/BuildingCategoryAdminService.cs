using Aetheris.Core.Admin;
using Aetheris.Core.Data;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The building categories in the admin panel: the list, the form, the creation, the change and
/// the archive (task 00020, section 3.1; the rules of the kinds are those of task 00023).
/// </summary>
/// <remarks>
/// <para>
/// A category is data: the server gives the neutral code <c>group_N</c>, the administrator gives
/// the name and the description in every language, the order of the groups and the picture. The
/// client groups the tiles of the building page by it and never guesses the group from the class
/// of a building or from its effects.
/// </para>
/// <para>
/// A category that buildings have cannot be archived (422): the building page would lose the
/// group. An archived category is not offered to a new or a changed building (the check lives in
/// the building kind).
/// </para>
/// </remarks>
public sealed class BuildingCategoryAdminService : IAdminReferenceService
{
    /// <summary>The kind of the building categories.</summary>
    public const string CategoryKind = "building_category";

    private const string LabelPrefix = "admin.reference.";

    private const string SortOrderField = "sort_order";

    private const string ArchivedField = "is_archived";

    /// <summary>The place of the categories in the menu of the reference kinds, right after the buildings.</summary>
    private const int CategorySortOrder = 31;

    private readonly PlanetsDbContext _dbContext;
    private readonly PlanetsUnitOfWork _unitOfWork;
    private readonly ICodeSequence _codes;
    private readonly ILocalizationTexts _texts;
    private readonly IAdminReferenceImages _images;
    private readonly BuildingCatalogCache _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildingCategoryAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="catalogCache">The building catalog in memory.</param>
    public BuildingCategoryAdminService(
        PlanetsDbContext dbContext,
        PlanetsUnitOfWork unitOfWork,
        ICodeSequence codes,
        ILocalizationTexts texts,
        IAdminReferenceImages images,
        BuildingCatalogCache catalogCache)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(catalogCache);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _codes = codes;
        _texts = texts;
        _images = images;
        _catalogCache = catalogCache;
    }

    /// <summary>
    /// Gets the kind of the building categories.
    /// </summary>
    public static AdminReferenceDescriptor CategoryDescriptor { get; } = new(
        CategoryKind,
        PlanetsTextKeys.CategoryCodeGroup,
        PlanetsTextKeys.Category,
        LabelPrefix + CategoryKind + ".title",
        CategorySortOrder,
        "/planets/building-categories",
        new AdminReferencePermissions(
            "admin.planets_building_categories_read",
            "admin.planets_building_category_read",
            "admin.planets_building_category_added",
            "admin.planets_building_category_updated",
            "admin.planets_building_category_archived",
            "admin.planets_building_category_restored"),
        [
            new(AdminReferenceRules.ImageField, AdminReferenceFieldTypes.Image, LabelPrefix + "field.image"),
            new(AdminReferenceRules.NameField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.name", IsRequired: true, HasLanguages: true, InList: true),
            new(AdminReferenceRules.DescriptionField, AdminReferenceFieldTypes.Text, LabelPrefix + "field.description", IsRequired: true, HasLanguages: true),
            new(SortOrderField, AdminReferenceFieldTypes.Number, LabelPrefix + "field.sort_order", IsRequired: true, InList: true, Minimum: 0),
        ]);

    /// <inheritdoc />
    public IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; } = [CategoryDescriptor];

    /// <inheritdoc />
    public IReadOnlyList<LocalizationTextEntry> LabelTexts { get; } = [];

    /// <inheritdoc />
    public async Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        CheckKind(kind);

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
        List<BuildingCategoryEntity> categories = await _dbContext.BuildingCategories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return AdminReferenceRules.Page(
            categories.Select(category => new AdminReferenceRow(
                category.Id,
                category.Code,
                category.NameKey,
                category.ImageKey,
                category.IsArchived,
                category.Version,
                new Dictionary<string, object?> { [SortOrderField] = category.SortOrder })),
            query,
            packs);
    }

    /// <inheritdoc />
    public async Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken)
    {
        CheckKind(kind);

        BuildingCategoryEntity? category = await _dbContext.BuildingCategories.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken).ConfigureAwait(false);

        if (category is null)
        {
            return null;
        }

        IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);

        return new AdminReferenceRecord(
            category.Id,
            category.Code,
            category.NameKey,
            category.DescriptionKey,
            category.ImageKey,
            category.IsArchived,
            category.Version,
            CategoryValues.Of(category, packs).ToDictionary());
    }

    /// <inheritdoc />
    public async Task<AdminReferenceChange> CreateAsync(string kind, AdminReferenceInput input, string adminId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrEmpty(adminId);
        CheckKind(kind);

        CategoryValues values = AdminReferenceJson.Read<CategoryValues>(input.Values);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                await CheckAsync(values, null, cancellationToken).ConfigureAwait(false);

                string code = await _codes.NextAsync(PlanetsTextKeys.CategoryCodeGroup, cancellationToken).ConfigureAwait(false);
                BuildingCategoryEntity category = new()
                {
                    Code = code,
                    NameKey = PlanetsTextKeys.CategoryName(code),
                    DescriptionKey = PlanetsTextKeys.CategoryDescription(code),
                    SortOrder = values.SortOrder ?? 0,
                    ImageKey = AdminReferenceRules.ImageKey(values.ImageKey),
                };

                _dbContext.BuildingCategories.Add(category);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.AddMissingAsync(
                    AdminReferenceRules.Entries(_texts, category.NameKey, category.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(CategoryKind, category.Id),
                    null,
                    new { code, values = values.Trimmed(_texts) },
                    category.Id,
                    category.Version);
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

        CategoryValues values = AdminReferenceJson.Read<CategoryValues>(input.Values);

        AdminReferenceChange change = await _unitOfWork.RunAsync(
            async () =>
            {
                BuildingCategoryEntity category = await LockAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(category.Version, input.Version);
                await CheckAsync(values, category, cancellationToken).ConfigureAwait(false);

                IReadOnlyList<LocalizationPack> packs = await AdminReferenceRules.PacksAsync(_texts, cancellationToken).ConfigureAwait(false);
                CategoryValues before = CategoryValues.Of(category, packs);

                category.SortOrder = values.SortOrder ?? 0;
                category.ImageKey = AdminReferenceRules.ImageKey(values.ImageKey);
                category.Version++;
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await _texts.SetAsync(
                    AdminReferenceRules.Entries(_texts, category.NameKey, category.DescriptionKey, values.Name!, values.Description!),
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(CategoryKind, category.Id),
                    before,
                    values.Trimmed(_texts),
                    category.Id,
                    category.Version);
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
                BuildingCategoryEntity category = await LockAsync(id, cancellationToken).ConfigureAwait(false);
                AdminReferenceRules.CheckVersion(category.Version, input.Version);

                if (category.IsArchived == isArchived)
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { [ArchivedField] = [AdminReferenceErrors.StateUnchanged] });
                }

                // A building has the category, so the group of its tile on the building page
                // would be lost: the category stays until no building has it. The buildings
                // lock the category row (shared) while they are written, so a building that
                // takes the category at this moment is either counted or refused.
                if (isArchived && await _dbContext.BuildingTypes.AnyAsync(type => type.CategoryId == category.Id, cancellationToken).ConfigureAwait(false))
                {
                    throw new AdminReferenceException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { [ArchivedField] = [AdminReferenceErrors.InUse] });
                }

                category.IsArchived = isArchived;
                category.Version++;

                return AdminReferenceRules.Change(
                    AdminReferenceEndpoints.Target(CategoryKind, category.Id),
                    new Dictionary<string, object?> { [ArchivedField] = !isArchived },
                    new Dictionary<string, object?> { [ArchivedField] = isArchived },
                    category.Id,
                    category.Version);
            },
            cancellationToken).ConfigureAwait(false);

        Committed();

        return change;
    }

    private static void CheckKind(string kind)
    {
        if (!string.Equals(kind, CategoryKind, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The building categories have no reference kind '{kind}'.", nameof(kind));
        }
    }

    private async Task CheckAsync(CategoryValues values, BuildingCategoryEntity? stored, CancellationToken cancellationToken)
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

    private void Committed()
    {
        // The row and its texts are committed: the copies in memory are dropped, so a player
        // sees the change without waiting for the lifetime of the cache.
        _catalogCache.Invalidate();
        _texts.Invalidate();
    }

    private async Task<BuildingCategoryEntity> LockAsync(int id, CancellationToken cancellationToken) =>
        (await _dbContext.BuildingCategories
            .FromSql($"SELECT * FROM building_category WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
        ?? throw new AdminReferenceException(GameErrorCodes.NotFound);

    /// <summary>The values of the form of a building category.</summary>
    /// <param name="Name">The name by language.</param>
    /// <param name="Description">The description by language.</param>
    /// <param name="SortOrder">The order of the groups on the building page.</param>
    /// <param name="ImageKey">The key of the picture.</param>
    private sealed record CategoryValues(
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Description,
        int? SortOrder,
        string? ImageKey)
    {
        public static CategoryValues Of(BuildingCategoryEntity category, IReadOnlyList<LocalizationPack> packs) =>
            new(AdminReferenceRules.TextsOf(packs, category.NameKey), AdminReferenceRules.TextsOf(packs, category.DescriptionKey), category.SortOrder, category.ImageKey);

        public CategoryValues Trimmed(ILocalizationTexts texts) => this with
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
}

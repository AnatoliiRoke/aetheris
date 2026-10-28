using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;

namespace Aetheris.Core.Admin;

/// <summary>
/// The JSON settings of the reference kinds: snake_case names like the rest of the API
/// (documentation/tech/08, rule 3), the keys of the dictionaries as they are.
/// </summary>
public static class AdminReferenceJson
{
    /// <summary>
    /// Gets the serializer settings.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Reads the values of a row into the typed values of a kind.
    /// </summary>
    /// <typeparam name="T">The typed values.</typeparam>
    /// <param name="values">The values as the client sent them.</param>
    /// <returns>The typed values.</returns>
    /// <exception cref="AdminReferenceException">The values cannot be read.</exception>
    public static T Read<T>(JsonElement values)
        where T : class
    {
        try
        {
            return values.ValueKind == JsonValueKind.Object
                ? values.Deserialize<T>(Options) ?? throw Invalid()
                : throw Invalid();
        }
        catch (JsonException)
        {
            throw Invalid();
        }
    }

    private static AdminReferenceException Invalid() =>
        new(GameErrorCodes.ValidationFailed, new Dictionary<string, string[]> { ["values"] = [AdminReferenceErrors.Invalid] });
}

/// <summary>
/// The checks and the reads shared by the reference kinds of every module (task 00023,
/// section 4.1).
/// </summary>
public static class AdminReferenceRules
{
    /// <summary>The name of the field of the name.</summary>
    public const string NameField = "name";

    /// <summary>The name of the field of the description.</summary>
    public const string DescriptionField = "description";

    /// <summary>The name of the field of the picture.</summary>
    public const string ImageField = "image_key";

    /// <summary>
    /// Adds a failure of a field.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="field">The path of the field.</param>
    /// <param name="code">The code of the failure.</param>
    public static void Fail(Dictionary<string, List<string>> failures, string field, string code)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (!failures.TryGetValue(field, out List<string>? codes))
        {
            codes = [];
            failures[field] = codes;
        }

        if (!codes.Contains(code, StringComparer.Ordinal))
        {
            codes.Add(code);
        }
    }

    /// <summary>
    /// Refuses the change when a field failed.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <exception cref="AdminReferenceException">A field failed.</exception>
    public static void ThrowIfInvalid(Dictionary<string, List<string>> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (failures.Count > 0)
        {
            throw new AdminReferenceException(
                GameErrorCodes.ValidationFailed,
                failures.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal));
        }
    }

    /// <summary>
    /// Refuses a change made from an older version of the row.
    /// </summary>
    /// <param name="stored">The version of the row.</param>
    /// <param name="sent">The version the administrator read.</param>
    /// <exception cref="AdminReferenceException">The versions differ.</exception>
    public static void CheckVersion(int stored, int sent)
    {
        if (stored != sent)
        {
            throw new AdminReferenceException(AdminReferenceErrors.VersionConflict);
        }
    }

    /// <summary>
    /// Checks the name and the description: a text in every language of the texts, not longer
    /// than the largest length.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="name">The name by language.</param>
    /// <param name="description">The description by language.</param>
    public static void CheckTexts(
        Dictionary<string, List<string>> failures,
        ILocalizationTexts texts,
        IReadOnlyDictionary<string, string>? name,
        IReadOnlyDictionary<string, string>? description)
    {
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(texts);

        foreach (string language in texts.Languages)
        {
            CheckText(failures, texts, NameField + "." + language, name?.GetValueOrDefault(language));
            CheckText(failures, texts, DescriptionField + "." + language, description?.GetValueOrDefault(language));
        }
    }

    /// <summary>
    /// Builds the texts of the name and the description of a row, trimmed, in every language.
    /// </summary>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="nameKey">The key of the name.</param>
    /// <param name="descriptionKey">The key of the description.</param>
    /// <param name="name">The checked name by language.</param>
    /// <param name="description">The checked description by language.</param>
    /// <returns>The texts.</returns>
    public static List<LocalizationTextEntry> Entries(
        ILocalizationTexts texts,
        string nameKey,
        string descriptionKey,
        IReadOnlyDictionary<string, string> name,
        IReadOnlyDictionary<string, string> description)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        return
        [
            .. texts.Languages.Select(language => new LocalizationTextEntry(nameKey, language, name[language].Trim())),
            .. texts.Languages.Select(language => new LocalizationTextEntry(descriptionKey, language, description[language].Trim())),
        ];
    }

    /// <summary>
    /// Reads the texts of a key in every language of the texts.
    /// </summary>
    /// <param name="packs">The packs of the languages.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text by language; a missing text is empty.</returns>
    public static Dictionary<string, string> TextsOf(IReadOnlyList<LocalizationPack> packs, string key)
    {
        ArgumentNullException.ThrowIfNull(packs);

        return packs.ToDictionary(pack => pack.Language, pack => pack.Texts.GetValueOrDefault(key, string.Empty), StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads the packs of every language of the texts.
    /// </summary>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The packs in the order of the languages.</returns>
    public static async Task<IReadOnlyList<LocalizationPack>> PacksAsync(ILocalizationTexts texts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);

        List<LocalizationPack> packs = [];

        foreach (string language in texts.Languages)
        {
            packs.Add(await texts.GetPackAsync(language, cancellationToken).ConfigureAwait(false));
        }

        return packs;
    }

    /// <summary>
    /// Filters and pages the rows of a reference kind in memory. A reference table holds tens of
    /// rows, and the names live in the texts of the game data, so the search runs here.
    /// </summary>
    /// <param name="rows">Every row of the kind.</param>
    /// <param name="query">The page, the search text and the archive filter.</param>
    /// <param name="packs">The packs of the languages, for the search by name.</param>
    /// <returns>The page.</returns>
    public static PageResult<AdminReferenceRow> Page(
        IEnumerable<AdminReferenceRow> rows,
        AdminReferenceQuery query,
        IReadOnlyList<LocalizationPack> packs)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(packs);

        IEnumerable<AdminReferenceRow> filtered = query.Archived is { } archived
            ? rows.Where(row => row.IsArchived == archived)
            : rows;

        if (query.Page.Search is { } search)
        {
            filtered = filtered.Where(row => row.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                || packs.Any(pack => pack.Texts.TryGetValue(row.NameKey, out string? name) && name.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        List<AdminReferenceRow> matching = [.. filtered];

        return new PageResult<AdminReferenceRow>(
            [.. matching.Skip(query.Page.Skip).Take(query.Page.PageSize)],
            matching.Count,
            query.Page.Page,
            query.Page.PageSize);
    }

    /// <summary>
    /// Builds the journal record of a change: the values before and after, as JSON.
    /// </summary>
    /// <param name="target">The changed row.</param>
    /// <param name="before">The values before, or <see langword="null"/> for a new row.</param>
    /// <param name="after">The values after.</param>
    /// <param name="id">The identifier of the row.</param>
    /// <param name="version">The new version of the row.</param>
    /// <returns>The change.</returns>
    public static AdminReferenceChange Change(string target, object? before, object after, int id, int version) =>
        new(
            target,
            before is null ? "{}" : JsonSerializer.Serialize(before, AdminReferenceJson.Options),
            JsonSerializer.Serialize(after, AdminReferenceJson.Options),
            id,
            version);

    /// <summary>
    /// Reads the texts of the labels of a module from its embedded file: the key and the text by
    /// language.
    /// </summary>
    /// <param name="assembly">The assembly of the module.</param>
    /// <param name="fileName">The name of the embedded file.</param>
    /// <returns>The texts.</returns>
    public static IReadOnlyList<LocalizationTextEntry> LoadLabelTexts(Assembly assembly, string fileName)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        using Stream stream = assembly.GetManifestResourceStream(fileName)
            ?? throw new InvalidOperationException($"The embedded texts {fileName} are missing.");

        Dictionary<string, Dictionary<string, string>> texts = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
            ?? throw new InvalidOperationException($"The embedded texts {fileName} are empty.");

        return [.. texts.SelectMany(entry => entry.Value.Select(text => new LocalizationTextEntry(entry.Key, text.Key, text.Value)))];
    }

    /// <summary>
    /// Checks the picture of a row: a new key must name a stored file, the stored key is kept
    /// as it is.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="images">The pictures of the server.</param>
    /// <param name="imageKey">The sent key, or <see langword="null"/> for no picture.</param>
    /// <param name="storedKey">The key the row has now.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the key is checked.</returns>
    public static async Task CheckImageAsync(
        Dictionary<string, List<string>> failures,
        IAdminReferenceImages images,
        string? imageKey,
        string? storedKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);

        if (imageKey is null || string.Equals(imageKey, storedKey, StringComparison.Ordinal))
        {
            return;
        }

        if (!await images.ExistsAsync(imageKey, cancellationToken).ConfigureAwait(false))
        {
            Fail(failures, ImageField, AdminReferenceErrors.Unknown);
        }
    }

    /// <summary>
    /// Checks the numbers of the levels of a row: at least one level, from one without a gap and
    /// not above the largest level.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="field">The path of the field of the levels.</param>
    /// <param name="levels">The numbers of the levels in the sent order.</param>
    /// <param name="maxLevel">The largest level, or <see langword="null"/> without a limit.</param>
    /// <returns><see langword="true"/> when the numbers are right.</returns>
    public static bool CheckLevelNumbers(Dictionary<string, List<string>> failures, string field, IReadOnlyList<int> levels, int? maxLevel)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (levels.Count == 0)
        {
            Fail(failures, field, AdminReferenceErrors.LevelsMissing);

            return false;
        }

        if (!levels.Order().SequenceEqual(Enumerable.Range(1, levels.Count)))
        {
            Fail(failures, field, AdminReferenceErrors.LevelsGap);

            return false;
        }

        if (maxLevel is { } limit && levels.Count > limit)
        {
            Fail(failures, field, AdminReferenceErrors.OutOfBounds);

            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the path of a field of a row of a list field, for example <c>levels.0.costs</c>.
    /// </summary>
    /// <param name="list">The list field.</param>
    /// <param name="index">The row of the list, from zero.</param>
    /// <param name="field">The field of the row.</param>
    /// <returns>The path.</returns>
    public static string PathOf(string list, int index, string field) =>
        list + "." + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "." + field;

    /// <summary>
    /// Normalizes a sent key of a picture: an empty text means no picture.
    /// </summary>
    /// <param name="imageKey">The sent key.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public static string? ImageKey(string? imageKey) => string.IsNullOrWhiteSpace(imageKey) ? null : imageKey.Trim();

    private static void CheckText(Dictionary<string, List<string>> failures, ILocalizationTexts texts, string field, string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            Fail(failures, field, AdminReferenceErrors.Required);
        }
        else if (trimmed.Length > texts.MaxLength)
        {
            Fail(failures, field, AdminReferenceErrors.TooLong);
        }
    }
}

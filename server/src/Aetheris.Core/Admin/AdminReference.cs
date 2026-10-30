using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;

namespace Aetheris.Core.Admin;

/// <summary>
/// The types of the fields of a reference kind: the client draws an input of the form by the type
/// (task 00023, section 4.1).
/// </summary>
/// <remarks>
/// The types are logical codes of the program. A field of the types <see cref="Levels"/>,
/// <see cref="Costs"/>, <see cref="Effects"/> and <see cref="Requirements"/> is a list of rows
/// whose columns are its own fields, so the client draws every list with one editor.
/// </remarks>
public static class AdminReferenceFieldTypes
{
    /// <summary>A text; with languages it holds one text per language of the texts.</summary>
    public const string Text = "text";

    /// <summary>A number.</summary>
    public const string Number = "number";

    /// <summary>One of the options of the field.</summary>
    public const string Select = "select";

    /// <summary>The identifier of a row of another reference kind.</summary>
    public const string Reference = "reference";

    /// <summary>The key of a picture stored on the server.</summary>
    public const string Image = "image";

    /// <summary>A yes or a no.</summary>
    public const string Flag = "flag";

    /// <summary>The levels of a row.</summary>
    public const string Levels = "levels";

    /// <summary>The costs of a level.</summary>
    public const string Costs = "costs";

    /// <summary>The effects of a level.</summary>
    public const string Effects = "effects";

    /// <summary>The requirements of a level.</summary>
    public const string Requirements = "requirements";
}

/// <summary>
/// The error codes of the reference changes of the admin panel, sent to the client as
/// localisation keys.
/// </summary>
public static class AdminReferenceErrors
{
    /// <summary>The row changed since the administrator opened it; nothing was written.</summary>
    public const string VersionConflict = "reference_version_conflict";

    /// <summary>The values of the row cannot be read.</summary>
    public const string Invalid = "invalid";

    /// <summary>The value is missing.</summary>
    public const string Required = "required";

    /// <summary>The text is longer than its column.</summary>
    public const string TooLong = "too_long";

    /// <summary>The referenced row does not exist.</summary>
    public const string Unknown = "unknown";

    /// <summary>The referenced row is archived, so it is not offered for new rows.</summary>
    public const string Archived = "archived";

    /// <summary>The value lies outside its bounds.</summary>
    public const string OutOfBounds = "out_of_bounds";

    /// <summary>The value appears twice.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>The value does not apply to the row.</summary>
    public const string NotApplicable = "not_applicable";

    /// <summary>The row has no level.</summary>
    public const string LevelsMissing = "levels_missing";

    /// <summary>The levels do not go from one without a gap.</summary>
    public const string LevelsGap = "levels_gap";

    /// <summary>The level has no cost.</summary>
    public const string CostMissing = "cost_missing";

    /// <summary>The level has no requirement.</summary>
    public const string RequirementsMissing = "requirements_missing";

    /// <summary>The row is already in the asked state.</summary>
    public const string StateUnchanged = "state_unchanged";

    /// <summary>A player has a level the change would remove, or has it in a queue.</summary>
    public const string LevelsInUse = "levels_in_use";

    /// <summary>A module holds data of the row, so its scope cannot change.</summary>
    public const string ScopeInUse = "scope_in_use";

    /// <summary>A currency of a civilization does not lie on a body, so it has no group.</summary>
    public const string CurrencyHasNoGroup = "currency_has_no_group";

    /// <summary>
    /// Gets the codes of the rules of the levels and the archive (task 00023, decisions 5 and 6):
    /// a refusal by one of them answers 422, a refusal of the form of a value answers 400 like the
    /// other changes of the admin panel.
    /// </summary>
    public static IReadOnlySet<string> RuleCodes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Archived,
        LevelsMissing,
        LevelsGap,
        CostMissing,
        RequirementsMissing,
        StateUnchanged,
        LevelsInUse,
        ScopeInUse,
    };
}

/// <summary>
/// An option of a field of the type <see cref="AdminReferenceFieldTypes.Select"/>.
/// </summary>
/// <param name="Value">The logical code the field stores.</param>
/// <param name="LabelKey">The localisation key of the label.</param>
/// <param name="ReferenceKind">The reference kind a dependent field picks from when this option is chosen.</param>
public sealed record AdminReferenceOption(string Value, string LabelKey, string? ReferenceKind = null);

/// <summary>
/// A field of a reference kind: what the list and the form show and the client sends back.
/// </summary>
/// <param name="Name">The name of the value in the record, snake_case.</param>
/// <param name="Type">The type, one of <see cref="AdminReferenceFieldTypes"/>.</param>
/// <param name="LabelKey">The localisation key of the label.</param>
/// <param name="IsRequired">Whether the value is mandatory.</param>
/// <param name="HasLanguages">Whether the text has one value per language of the texts.</param>
/// <param name="InList">Whether the list shows the field as a column.</param>
/// <param name="Minimum">The smallest number, or <see langword="null"/>.</param>
/// <param name="Maximum">The largest number, or <see langword="null"/>.</param>
/// <param name="IsDecimal">Whether the number may have a fraction.</param>
/// <param name="Options">The options of a select field.</param>
/// <param name="ReferenceKind">The reference kind a reference field picks from.</param>
/// <param name="ReferenceKindFrom">The select field of the same row whose option names the reference kind.</param>
/// <param name="Fields">The columns of a list field.</param>
public sealed record AdminReferenceField(
    string Name,
    string Type,
    string LabelKey,
    bool IsRequired = false,
    bool HasLanguages = false,
    bool InList = false,
    decimal? Minimum = null,
    decimal? Maximum = null,
    bool IsDecimal = false,
    IReadOnlyList<AdminReferenceOption>? Options = null,
    string? ReferenceKind = null,
    string? ReferenceKindFrom = null,
    IReadOnlyList<AdminReferenceField>? Fields = null);

/// <summary>
/// The action codes of the endpoints of a reference kind; every code is a permission
/// (task 00023, section 4.1).
/// </summary>
/// <param name="ListRead">The read of the list (a screen).</param>
/// <param name="RecordRead">The read of one record (a screen).</param>
/// <param name="Added">The creation of a row (an action).</param>
/// <param name="Updated">The change of a row (an action).</param>
/// <param name="Archived">The archiving of a row (an action).</param>
/// <param name="Restored">The return of a row from the archive (an action).</param>
public sealed record AdminReferencePermissions(
    string ListRead,
    string RecordRead,
    string Added,
    string Updated,
    string Archived,
    string Restored)
{
    /// <summary>
    /// Gets the codes of the reads.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Screens => [ListRead, RecordRead];

    /// <summary>
    /// Gets the codes of the changes.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Actions => [Added, Updated, Archived, Restored];
}

/// <summary>
/// Describes one reference kind the admin panel shows and changes (task 00023, section 4.1).
/// </summary>
/// <remarks>
/// The module that owns the tables describes the kind; the client draws the menu, the list and
/// the form from the description, so a new kind needs no code of the client.
/// </remarks>
/// <param name="Kind">The logical code of the kind.</param>
/// <param name="CodeGroup">The group of the neutral codes of the rows.</param>
/// <param name="NameKeyPrefix">The prefix of the localisation keys of the rows.</param>
/// <param name="TitleKey">The localisation key of the menu item and of the title of the screen.</param>
/// <param name="SortOrder">The place in the menu.</param>
/// <param name="Route">The route of the endpoints of the kind under the admin prefix.</param>
/// <param name="Permissions">The action codes of the endpoints.</param>
/// <param name="Fields">The fields of the list and the form.</param>
public sealed record AdminReferenceDescriptor(
    string Kind,
    string CodeGroup,
    string NameKeyPrefix,
    string TitleKey,
    int SortOrder,
    string Route,
    AdminReferencePermissions Permissions,
    IReadOnlyList<AdminReferenceField> Fields);

/// <summary>
/// The read of a list of a reference kind.
/// </summary>
/// <param name="Page">The page and the search text, matched against the code and the name.</param>
/// <param name="Archived">Only the archived rows, only the active ones, or every row when <see langword="null"/>.</param>
public sealed record AdminReferenceQuery(PageRequest Page, bool? Archived);

/// <summary>
/// A row of the list of a reference kind.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The neutral code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="IsArchived">Whether the row is archived.</param>
/// <param name="Version">The version of the row.</param>
/// <param name="Values">The values of the fields shown in the list.</param>
public sealed record AdminReferenceRow(
    int Id,
    string Code,
    string NameKey,
    string? ImageKey,
    bool IsArchived,
    int Version,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// A record of a reference kind for the form.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The neutral code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="IsArchived">Whether the row is archived.</param>
/// <param name="Version">The version of the row.</param>
/// <param name="Values">The values of every field, by the field name.</param>
public sealed record AdminReferenceRecord(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string? ImageKey,
    bool IsArchived,
    int Version,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
/// A creation, a change, an archiving or a return of a row.
/// </summary>
/// <param name="Version">The version the administrator read; ignored by a creation.</param>
/// <param name="Reason">Why the row is changed; written to the journal.</param>
/// <param name="Values">The values of the fields, by the field name; not used by the archiving.</param>
public sealed record AdminReferenceInput(int Version, string? Reason, JsonElement Values)
{
    /// <summary>The name of the property of the version.</summary>
    public const string VersionProperty = "version";

    /// <summary>The name of the property of the reason.</summary>
    public const string ReasonProperty = "reason";

    /// <summary>The name of the property of the values.</summary>
    public const string ValuesProperty = "values";

    /// <summary>
    /// Reads the body of a request. The values are the property <c>values</c>; a body without it
    /// is the values itself, the way the first reference creation of task 00022 was sent.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The input.</returns>
    public static AdminReferenceInput Parse(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            return new AdminReferenceInput(0, null, default);
        }

        int version = body.TryGetProperty(VersionProperty, out JsonElement versionValue) && versionValue.ValueKind == JsonValueKind.Number
            && versionValue.TryGetInt32(out int number)
            ? number
            : 0;
        string? reason = body.TryGetProperty(ReasonProperty, out JsonElement reasonValue) && reasonValue.ValueKind == JsonValueKind.String
            ? reasonValue.GetString()
            : null;
        JsonElement values = body.TryGetProperty(ValuesProperty, out JsonElement valuesValue) && valuesValue.ValueKind == JsonValueKind.Object
            ? valuesValue
            : body;

        return new AdminReferenceInput(version, reason, values);
    }
}

/// <summary>
/// What a stored reference change did, for the journal.
/// </summary>
/// <param name="Target">The changed row, for example <c>race:12</c>.</param>
/// <param name="OldValue">The values before, as JSON.</param>
/// <param name="NewValue">The values after, as JSON.</param>
/// <param name="Id">The identifier of the row.</param>
/// <param name="Version">The new version of the row.</param>
public sealed record AdminReferenceChange(string Target, string OldValue, string NewValue, int Id, int Version);

/// <summary>
/// A reference change that was refused; nothing was written.
/// </summary>
public sealed class AdminReferenceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AdminReferenceException"/> class.
    /// </summary>
    /// <param name="code">The error code: <see cref="GameErrorCodes.NotFound"/>, <see cref="AdminReferenceErrors.VersionConflict"/> or <see cref="GameErrorCodes.ValidationFailed"/>.</param>
    /// <param name="fields">The codes of the refused fields, by the path of the field.</param>
    public AdminReferenceException(string code, IReadOnlyDictionary<string, string[]>? fields = null)
        : base("The reference change was refused: " + code)
    {
        Code = code;
        Fields = fields ?? new Dictionary<string, string[]>();
    }

    /// <summary>Gets the error code.</summary>
    public string Code { get; }

    /// <summary>Gets the codes of the refused fields.</summary>
    public IReadOnlyDictionary<string, string[]> Fields { get; }
}

/// <summary>
/// The contract a module implements for every reference kind it owns (task 00023, section 4.1).
/// </summary>
/// <remarks>
/// <para>
/// This is a protocol, not a shared table: every module keeps its own tables and answers in the
/// same way, so the client has one list and one form for every kind.
/// </para>
/// <para>
/// A row is never deleted: it is archived and can come back from the archive. A change compares
/// the version the administrator read and drops the copy of the catalog the module keeps in
/// memory once it is committed.
/// </para>
/// </remarks>
public interface IAdminReferenceService
{
    /// <summary>
    /// Gets the kinds of this module.
    /// </summary>
    IReadOnlyList<AdminReferenceDescriptor> Descriptors { get; }

    /// <summary>
    /// Gets the texts of the labels of the kinds, added to the texts of the game data when they are
    /// missing.
    /// </summary>
    IReadOnlyList<LocalizationTextEntry> LabelTexts { get; }

    /// <summary>
    /// Reads the list of a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="query">The page, the search text and the archive filter.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    Task<PageResult<AdminReferenceRow>> ReadAsync(string kind, AdminReferenceQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one record for the form.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The row.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The record, or <see langword="null"/> when the row does not exist.</returns>
    Task<AdminReferenceRecord?> ReadOneAsync(string kind, int id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a row with its texts; the server gives the neutral code.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="input">The values.</param>
    /// <param name="adminId">The administrator, the author of the texts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was created.</returns>
    /// <exception cref="AdminReferenceException">The values break a rule.</exception>
    Task<AdminReferenceChange> CreateAsync(string kind, AdminReferenceInput input, string adminId, CancellationToken cancellationToken);

    /// <summary>
    /// Changes a row and its texts.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The row.</param>
    /// <param name="input">The version the administrator read and the new values.</param>
    /// <param name="adminId">The administrator, the author of the texts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="AdminReferenceException">The row does not exist, changed in between or the values break a rule.</exception>
    Task<AdminReferenceChange> UpdateAsync(string kind, int id, AdminReferenceInput input, string adminId, CancellationToken cancellationToken);

    /// <summary>
    /// Archives a row or returns it from the archive; the row is never deleted.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="id">The row.</param>
    /// <param name="isArchived">Whether the row is archived.</param>
    /// <param name="input">The version the administrator read.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="AdminReferenceException">The row does not exist or changed in between.</exception>
    Task<AdminReferenceChange> SetArchivedAsync(string kind, int id, bool isArchived, AdminReferenceInput input, CancellationToken cancellationToken);
}

/// <summary>
/// The pictures of the reference rows, stored on the server (task 00023, decision 2).
/// </summary>
/// <remarks>
/// The host keeps the files; a row only holds the key of its picture, and a row without one shows
/// the neutral placeholder.
/// </remarks>
public interface IAdminReferenceImages
{
    /// <summary>
    /// Checks whether a picture is stored.
    /// </summary>
    /// <param name="imageKey">The key of the picture.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    Task<bool> ExistsAsync(string imageKey, CancellationToken cancellationToken);
}

using System.Globalization;
using System.Text;

namespace Aetheris.World.Data.Migrations;

/// <summary>
/// A text that a migration moves into the table of texts.
/// </summary>
/// <param name="Key">The localisation key.</param>
/// <param name="Language">The language.</param>
/// <param name="Text">The text.</param>
public sealed record MigrationText(string Key, string Language, string Text);

/// <summary>
/// Builds the statements of the migrations that move the reference rows to neutral codes and
/// their texts into the database (task 00022, section 3.1).
/// </summary>
/// <remarks>
/// The statements only change codes and keys: the identifiers of the rows and every link
/// between them stay as they were. The values come from the table of the old and new codes and
/// from the texts of the migration, never from a request.
/// </remarks>
public static class NeutralCodeSql
{
    /// <summary>
    /// The author of the moved texts.
    /// </summary>
    public const string Author = "seed";

    /// <summary>
    /// Renames the codes of a table by the table of old and new codes. A row whose code the table
    /// does not know, for example one added by hand, gets the next free number of the group.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="group">The code group.</param>
    /// <param name="map">The old code and the new one.</param>
    /// <returns>The statements.</returns>
    public static string Rename(string table, string group, IReadOnlyDictionary<string, string> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        StringBuilder sql = new();

        if (map.Count > 0)
        {
            sql.Append(CultureInfo.InvariantCulture, $"UPDATE {table} AS t SET code = m.new_code FROM (VALUES ")
                .AppendJoin(", ", map.Select(entry => $"({Quote(entry.Key)}, {Quote(entry.Value)})"))
                .Append(") AS m(old_code, new_code) WHERE t.code = m.old_code;")
                .AppendLine();
        }

        sql.Append(CultureInfo.InvariantCulture, $"""
            WITH numbered AS (
                SELECT id, row_number() OVER (ORDER BY id) AS position
                FROM {table}
                WHERE code !~ {Quote(Pattern(group))})
            UPDATE {table} AS t
            SET code = {Quote(group + "_")} || ({LastNumber(table, group)} + numbered.position)
            FROM numbered
            WHERE t.id = numbered.id;
            """).AppendLine();

        return sql.ToString();
    }

    /// <summary>
    /// Gives every row of a table the next number of the group in the order of the identifiers,
    /// for a table whose old codes repeated.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="group">The code group.</param>
    /// <returns>The statement.</returns>
    public static string Renumber(string table, string group) =>
        $"""
        WITH numbered AS (SELECT id, row_number() OVER (ORDER BY id) AS position FROM {table})
        UPDATE {table} AS t SET code = {Quote(group + "_")} || numbered.position
        FROM numbered
        WHERE t.id = numbered.id;
        """;

    /// <summary>
    /// Rebuilds the name and description keys of a table from the neutral codes.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="prefix">The prefix of the keys.</param>
    /// <returns>The statement.</returns>
    public static string Keys(string table, string prefix) =>
        $"""
        UPDATE {table}
        SET name_key = {Quote(prefix + ".")} || code || {Quote("." + WorldTextKeys.NamePart)},
            description_key = {Quote(prefix + ".")} || code || {Quote("." + WorldTextKeys.DescriptionPart)};
        """;

    /// <summary>
    /// Sets a column of the rows with the given new codes.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="column">The column.</param>
    /// <param name="values">The value by the new code.</param>
    /// <returns>The statement, or an empty text without values.</returns>
    public static string SetByCode(string table, string column, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return values.Count == 0
            ? string.Empty
            : $"UPDATE {table} AS t SET {column} = v.value FROM (VALUES "
                + string.Join(", ", values.Select(entry => $"({Quote(entry.Key)}, {Quote(entry.Value)})"))
                + ") AS v(code, value) WHERE t.code = v.code;";
    }

    /// <summary>
    /// Adds the texts whose keys a table already uses; an existing text is kept.
    /// </summary>
    /// <param name="texts">The texts.</param>
    /// <param name="usedKeys">A query that returns the keys in use, one column named <c>key</c>.</param>
    /// <returns>The statement, or an empty text without texts.</returns>
    public static string Texts(IEnumerable<MigrationText> texts, string usedKeys)
    {
        ArgumentNullException.ThrowIfNull(texts);

        List<MigrationText> rows = [.. texts];

        return rows.Count == 0
            ? string.Empty
            : "INSERT INTO localization_text (key, language, text, version, updated_at, updated_by) "
                + $"SELECT t.key, t.language, t.text, 1, now(), {Quote(Author)} FROM (VALUES "
                + string.Join(", ", rows.Select(text => $"({Quote(text.Key)}, {Quote(text.Language)}, {Quote(text.Text)})"))
                + $") AS t(key, language, text) WHERE t.key IN ({usedKeys}) "
                + "ON CONFLICT (key, language) DO NOTHING;";
    }

    /// <summary>
    /// Raises the counter of a group above the largest number used in a table.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="group">The code group.</param>
    /// <returns>The statement.</returns>
    public static string Raise(string table, string group) =>
        $"""
        INSERT INTO code_sequence (code_group, last_number)
        SELECT {Quote(group)}, {LastNumber(table, group)}
        FROM {table}
        HAVING count(*) > 0
        ON CONFLICT (code_group) DO UPDATE SET last_number = GREATEST(code_sequence.last_number, EXCLUDED.last_number);
        """;

    /// <summary>
    /// Builds the expression of the largest number of a group used in a table.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="group">The code group.</param>
    /// <returns>The expression.</returns>
    public static string LastNumber(string table, string group) =>
        $"(SELECT coalesce(max(substring(code from {Quote("^" + group + "_([0-9]+)$")})::int), 0) FROM {table})";

    /// <summary>
    /// Quotes a text for a statement.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The quoted text.</returns>
    public static string Quote(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static string Pattern(string group) => "^" + group + "_[0-9]+$";
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Core.Data;

/// <summary>
/// The rules every seeder follows for the codes of the starting data (task 00052).
/// </summary>
/// <remarks>
/// The starting data is the source of the codes: a row of the file has the same code in the
/// database and in the keys of its texts. The counter of the server (<see cref="ICodeSequence"/>)
/// gives codes only to the rows an administrator creates later, so after the seed it is raised
/// above the largest number of the codes of the file and never gives a code the file already uses.
/// </remarks>
public static partial class SeedCodeRules
{
    /// <summary>
    /// Checks the codes of one kind of the rows of the starting data: every row has a code, the
    /// code holds only letters, digits and underscores, and no code is given twice.
    /// </summary>
    /// <param name="codes">The codes of the file, in the order of the file.</param>
    /// <param name="kind">The kind of the rows, for the message.</param>
    /// <exception cref="InvalidOperationException">A code is missing, malformed or repeated.</exception>
    public static void Validate(IEnumerable<string?> codes, string kind)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentException.ThrowIfNullOrEmpty(kind);

        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string? code in codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException($"The starting data gives a {kind} no code.");
            }

            if (!CodePattern().IsMatch(code))
            {
                throw new InvalidOperationException($"The code '{code}' of a {kind} of the starting data may hold only letters, digits and underscores.");
            }

            if (!seen.Add(code))
            {
                throw new InvalidOperationException($"The starting data gives the {kind} code '{code}' twice.");
            }
        }
    }

    /// <summary>
    /// Finds the largest number a set of codes ends with, so <c>recipe_r_113</c> gives 113 and a
    /// code that ends with no digits gives nothing.
    /// </summary>
    /// <param name="codes">The codes.</param>
    /// <returns>The largest trailing number, or 0 when no code ends with one.</returns>
    public static int LargestNumber(IEnumerable<string> codes)
    {
        ArgumentNullException.ThrowIfNull(codes);

        return codes.Select(TrailingNumber).DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Finds the number a code ends with.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The trailing number, or 0 when the code ends with no digits that fit a number.</returns>
    public static int TrailingNumber(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        int start = code.Length;

        while (start > 0 && char.IsAsciiDigit(code[start - 1]))
        {
            start--;
        }

        return start < code.Length && int.TryParse(code.AsSpan(start), NumberStyles.None, CultureInfo.InvariantCulture, out int number)
            ? number
            : 0;
    }

    /// <summary>
    /// Raises the counter of a group of codes above the largest number of the codes of the file,
    /// so the next code of the counter never meets a code of the file.
    /// </summary>
    /// <param name="codes">The counters.</param>
    /// <param name="group">The group of the counter.</param>
    /// <param name="seedCodes">The codes of the file of this group.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the counter is raised.</returns>
    public static async Task RaiseAsync(ICodeSequence codes, string group, IEnumerable<string> seedCodes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(seedCodes);

        int taken = LargestNumber(seedCodes);

        if (taken > 0)
        {
            await codes.RaiseAsync(group, taken, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops a step that adds the rows of the file that are missing, when the table holds rows
    /// but none of them has a code of the file: the codes of the database are of another form
    /// (the form of the counter of the server), and the step would add every row a second time.
    /// </summary>
    /// <param name="table">The table, for the message.</param>
    /// <param name="matchedRows">How many rows of the table have a code of the file.</param>
    /// <param name="tableHasRows">Whether the table holds any row.</param>
    /// <exception cref="InvalidOperationException">The codes of the table do not meet the file.</exception>
    public static void EnsureAligned(string table, int matchedRows, bool tableHasRows)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);

        if (matchedRows == 0 && tableHasRows)
        {
            throw new InvalidOperationException(
                $"The table '{table}' holds no code of the starting data. Run the command 'reconcile-seed-codes' before the server starts.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex CodePattern();
}

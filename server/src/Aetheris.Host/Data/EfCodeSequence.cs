using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// Gives the neutral codes from the <c>code_sequence</c> table (task 00022, decision 1).
/// </summary>
/// <remarks>
/// One statement raises the counter of the group and returns the new number. The row lock of
/// the statement orders parallel requests, so no two of them get the same number, and the
/// counter never goes down, so a number is not reused after its row is deleted.
/// </remarks>
public sealed partial class EfCodeSequence : ICodeSequence
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCodeSequence"/> class.
    /// </summary>
    /// <param name="dbContext">The main database context of the request.</param>
    public EfCodeSequence(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<string> NextAsync(string group, CancellationToken cancellationToken)
    {
        CheckGroup(group);

        // The statement is not composed into a subquery: PostgreSQL does not allow an INSERT
        // inside one, so the rows are read as they are.
        List<int> numbers = await _dbContext.Database.SqlQuery<int>(
            $"""
            INSERT INTO code_sequence (code_group, last_number) VALUES ({group}, 1)
            ON CONFLICT (code_group) DO UPDATE SET last_number = code_sequence.last_number + 1
            RETURNING last_number AS "Value"
            """)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Format(group, numbers.Single());
    }

    /// <inheritdoc />
    public async Task RaiseAsync(string group, int takenNumber, CancellationToken cancellationToken)
    {
        CheckGroup(group);
        ArgumentOutOfRangeException.ThrowIfNegative(takenNumber);

        await _dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO code_sequence (code_group, last_number) VALUES ({group}, {takenNumber})
            ON CONFLICT (code_group) DO UPDATE SET last_number = GREATEST(code_sequence.last_number, EXCLUDED.last_number)
            """,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a code from its group and number.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="number">The number.</param>
    /// <returns>The code.</returns>
    public static string Format(string group, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{group}_{number}");

    private static void CheckGroup(string group)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);

        if (!GroupPattern().IsMatch(group))
        {
            throw new ArgumentException($"The code group '{group}' may hold only lower-case letters and underscores.", nameof(group));
        }
    }

    [GeneratedRegex("^[a-z][a-z_]*$")]
    private static partial Regex GroupPattern();
}

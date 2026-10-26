using Aetheris.Core.Text;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// Checks a visible name against the restricted words of the database
/// (task 00006, decision 7, documentation/ui-art/04).
/// </summary>
/// <remarks>
/// <para>
/// The name is compared in lower case and without its separators, so that
/// <c>B-a_d W o r d</c> is found like <c>badword</c>.
/// </para>
/// <para>
/// An offensive word is refused anywhere in the name. A reserved word, such as the name of the
/// administration, is refused as a part of the name between separators and at its start, so
/// that <c>Admin_Bob</c> and <c>AdminBob</c> are refused while an ordinary word that merely
/// contains it in the middle is not.
/// </para>
/// </remarks>
public sealed class TextModeration : ITextModeration
{
    private static readonly char[] Separators = [' ', '_', '-'];

    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextModeration"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public TextModeration(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        string lower = text.ToLowerInvariant();
        string compact = Compact(lower);
        string[] parts = lower.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        List<RestrictedWordEntity> words = await _dbContext.RestrictedWords
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (RestrictedWordEntity entry in words)
        {
            string word = Compact(entry.Word.ToLowerInvariant());

            if (word.Length == 0)
            {
                continue;
            }

            bool found = string.Equals(entry.Kind, RestrictedWordKinds.Offensive, StringComparison.Ordinal)
                ? compact.Contains(word, StringComparison.Ordinal)
                : compact.StartsWith(word, StringComparison.Ordinal) || parts.Contains(word, StringComparer.Ordinal);

            if (found)
            {
                return false;
            }
        }

        return true;
    }

    private static string Compact(string value) => string.Concat(value.Where(character => !Separators.Contains(character)));
}

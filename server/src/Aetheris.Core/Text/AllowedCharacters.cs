using System.Text;

namespace Aetheris.Core.Text;

/// <summary>
/// A set of allowed characters, read from a definition stored in the database.
/// </summary>
/// <remarks>
/// <para>
/// The definition lists the characters as they are, for example <c>A-Za-z0-9 _'-</c>: two
/// characters joined by <c>-</c> are a range, and a <c>-</c> at the start or at the end of the
/// definition is the dash itself. Nothing else has a special meaning, so the definition is
/// never a regular expression and a typo in it cannot change how a text is matched.
/// </para>
/// <para>
/// A range written backwards matches nothing, and an empty definition allows nothing: a broken
/// definition refuses names instead of letting any character through.
/// </para>
/// </remarks>
public sealed class AllowedCharacters
{
    private const char RangeMark = '-';

    // A range is written as three characters: the first one, the mark and the last one.
    private const int RangeLength = 3;

    private readonly List<(int First, int Last)> _ranges;

    private AllowedCharacters(List<(int First, int Last)> ranges)
    {
        _ranges = ranges;
    }

    /// <summary>
    /// Reads a definition.
    /// </summary>
    /// <param name="definition">The definition, for example <c>A-Za-z0-9 _-</c>.</param>
    /// <returns>The set.</returns>
    public static AllowedCharacters Parse(string? definition)
    {
        Rune[] runes = [.. (definition ?? string.Empty).EnumerateRunes()];
        List<(int First, int Last)> ranges = [];

        for (int i = 0; i < runes.Length; i++)
        {
            int lastIndex = i + RangeLength - 1;
            bool isRange = lastIndex < runes.Length && runes[i + 1].Value == RangeMark;

            if (isRange)
            {
                ranges.Add((runes[i].Value, runes[lastIndex].Value));
                i = lastIndex;
            }
            else
            {
                ranges.Add((runes[i].Value, runes[i].Value));
            }
        }

        return new AllowedCharacters(ranges);
    }

    /// <summary>
    /// Checks that every character of a text belongs to the set.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when the text is not empty and has only allowed characters.</returns>
    public bool IsMatch(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length > 0 && text.EnumerateRunes().All(Contains);
    }

    private bool Contains(Rune character) =>
        _ranges.Exists(range => character.Value >= range.First && character.Value <= range.Last);
}

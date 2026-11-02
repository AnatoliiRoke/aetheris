using System.Text;

namespace Aetheris.Host.Observability;

/// <summary>
/// Prepares the text of an SQL command for the log.
/// </summary>
public static class SqlText
{
    /// <summary>
    /// Puts the text on one line, with single spaces, and cuts it at a length.
    /// </summary>
    /// <param name="commandText">The text of the command, with the names of its parameters.</param>
    /// <param name="maxLength">The largest number of characters.</param>
    /// <returns>The text for the log.</returns>
    public static string Shorten(string commandText, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(commandText);

        StringBuilder builder = new(Math.Min(commandText.Length, maxLength));
        bool previousWasSpace = false;

        foreach (char symbol in commandText)
        {
            if (builder.Length >= maxLength)
            {
                builder.Append("...");
                break;
            }

            bool isSpace = char.IsWhiteSpace(symbol);

            if (isSpace && previousWasSpace)
            {
                continue;
            }

            builder.Append(isSpace ? ' ' : symbol);
            previousWasSpace = isSpace;
        }

        return builder.ToString().Trim();
    }
}

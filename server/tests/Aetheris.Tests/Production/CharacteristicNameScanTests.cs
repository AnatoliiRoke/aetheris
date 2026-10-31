using System.Text.RegularExpressions;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests that the code names no characteristic, no group of the parts and no kind of the
/// damage of a part (task 00027, acceptance criterion 5).
/// </summary>
/// <remarks>
/// <para>
/// The characteristics of the parts are data: their names, their units of measure and the
/// groups that have them live in the tables of the database and in the texts, so a new
/// characteristic arrives as a row and never as a change of the code
/// (documentation/tech/09, section 2.2).
/// </para>
/// <para>
/// The scanner looks at the strings of the server sources and of the starting data and finds
/// the words that name a characteristic. The migrations, the table of the old codes and this
/// file itself are the only places that may hold them.
/// </para>
/// </remarks>
public sealed partial class CharacteristicNameScanTests
{
    /// <summary>
    /// The words that name one characteristic, in English and in Ukrainian. A word is matched as
    /// a whole token after the identifiers are split at case changes and separators, so
    /// <c>Characteristics</c> or a word that only contains one does not match.
    /// </summary>
    private static readonly HashSet<string> BannedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // The characteristics of the drives.
        "thrust", "traction",
        // The characteristics of the weapons.
        "rof",
        // The characteristics of the protection.
        "strength",
        // The characteristics of the ammunition.
        "ballistic",
    };

    /// <summary>
    /// The phrases that name a characteristic, in English. A phrase is matched inside a string
    /// literal with spaces, so a sentence of a description is not a match.
    /// </summary>
    private static readonly HashSet<string> BannedPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "output power", "power output",
        "damage per shot", "rate of fire", "rate-of-fire",
        "magazine size", "ammunition kinds", "accepted ammunition",
        "shield capacity", "shield recovery", "shield repair",
        "damage blocking", "energy blocking", "kinetic blocking",
        "flight speed",
        "output per unit of time", "scan radius", "scan speed", "excavation speed",
        "detection range",
        "crew reduction", "capacity in tonnes", "population capacity", "repair speed",
        "training speed", "trade slots", "preparation time", "transfer volume",
        "deployment time", "interceptions per unit of time",
        "damage kind", "damage type", "kinetic damage", "energy damage",
    };

    /// <summary>
    /// The file types that are scanned.
    /// </summary>
    private static readonly string[] ScannedExtensions = [".cs", ".json"];

    /// <summary>
    /// No string of the server sources, the starting data or the tests names a characteristic
    /// of the parts.
    /// </summary>
    [Fact]
    public void The_server_holds_no_name_of_a_characteristic()
    {
        string serverRoot = FindServerRoot();
        List<string> findings = [];

        foreach (string file in EnumerateScannedFiles(serverRoot))
        {
            string[] lines = File.ReadAllLines(file);

            for (int index = 0; index < lines.Length; index++)
            {
                foreach (string word in FindBannedWords(lines[index]))
                {
                    findings.Add($"{Path.GetRelativePath(serverRoot, file)}:{index + 1}: '{word}'");
                }
            }
        }

        Assert.True(findings.Count == 0, "Names of the characteristics found in the server:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    /// <summary>
    /// The scanner finds a name inside a key or a path and ignores a word that only contains one
    /// and a word of the comments that is not a name.
    /// </summary>
    [Fact]
    public void The_scanner_finds_a_name_inside_a_key()
    {
        Assert.Equal(["thrust"], FindBannedWords("string key = \"production.characteristic.thrust\";"));
        Assert.Equal(["strength"], FindBannedWords("\"name\": \"Strength\""));
        Assert.Empty(FindBannedWords("string kind = \"shipped\";"));
        Assert.Empty(FindBannedWords("\"description\": \"The drive of the ship.\""));
        Assert.Empty(FindBannedWords("// the scanner has no name of a characteristic here"));
    }

    private static List<string> FindBannedWords(string line)
    {
        List<string> found = [];

        foreach (Match literal in StringLiteral().Matches(line))
        {
            string text = InterpolationHole().Replace(literal.Groups["text"].Value, string.Empty);
            string lower = text.ToLowerInvariant();

            foreach (string phrase in BannedPhrases)
            {
                if (lower.Contains(phrase, StringComparison.Ordinal))
                {
                    found.Add(phrase);
                }
            }

            foreach (string word in PhraseSeparator().Split(lower))
            {
                if (word.Length > 0 && BannedWords.Contains(word))
                {
                    found.Add(word);
                }
            }
        }

        return [.. found.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static IEnumerable<string> EnumerateScannedFiles(string serverRoot)
    {
        string self = nameof(CharacteristicNameScanTests) + ".cs";
        string separator = Path.DirectorySeparatorChar.ToString();

        return new[] { Path.Combine(serverRoot, "src"), Path.Combine(serverRoot, "tests") }
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(file => ScannedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                && !file.Contains(separator + "obj" + separator, StringComparison.Ordinal)
                && !file.Contains(separator + "Migrations" + separator, StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), self, StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), "appsettings.Local.json", StringComparison.Ordinal))
            // The starting data holds the names of the characteristics: that is where they belong,
            // and the texts of the database are written from it (documentation/tech/09, section 4.1).
            .Where(file => !string.Equals(Path.GetFileName(file), "production-reference-seed.json", StringComparison.Ordinal))
            // The test of the reference rows writes the names of the characteristics on purpose.
            .Where(file => !string.Equals(Path.GetFileName(file), "CharacteristicReferenceTests.cs", StringComparison.Ordinal));
    }

    private static string FindServerRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    [GeneratedRegex("\"(?<text>(?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"\{[^}]*(\}|$)")]
    private static partial Regex InterpolationHole();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex PhraseSeparator();
}

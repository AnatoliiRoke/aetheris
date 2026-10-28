using System.Text.RegularExpressions;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests that the server names no game entity by an old code (task 00022, acceptance criterion 5).
/// </summary>
/// <remarks>
/// The old codes come from the table of old and new codes kept with the migrations. Every string
/// of the code, the starting data and the tests is split into words, and none of them may be an
/// old code. A string with a space is a text for people, such as the description of a resource,
/// and the holes of an interpolated string are code, not text; both are skipped. The migrations,
/// the table itself and the list of the English game words of the core scanner are the only
/// places that still hold the old words.
/// </remarks>
public sealed partial class NeutralCodeScanTests
{
    /// <summary>
    /// The old codes that are also logical codes and stay: the class of the gas giants is
    /// implemented by the code, its old type had the same word.
    /// </summary>
    private static readonly HashSet<string> LogicalCodes = new(StringComparer.Ordinal) { WorldObjectClasses.GasGiant };

    /// <summary>
    /// The file types that are scanned.
    /// </summary>
    private static readonly string[] ScannedExtensions = [".cs", ".json"];

    /// <summary>
    /// No string of the server sources, the starting data or the tests is an old code.
    /// </summary>
    [Fact]
    public void The_server_holds_no_old_code_of_a_game_entity()
    {
        HashSet<string> oldCodes = OldCodes();
        string serverRoot = FindServerRoot();
        List<string> findings = [];

        foreach (string file in EnumerateScannedFiles(serverRoot))
        {
            string[] lines = File.ReadAllLines(file);

            for (int index = 0; index < lines.Length; index++)
            {
                foreach (string word in FindOldCodes(lines[index], oldCodes))
                {
                    findings.Add($"{Path.GetRelativePath(serverRoot, file)}:{index + 1}: '{word}'");
                }
            }
        }

        Assert.True(findings.Count == 0, "Old codes found in the server:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    /// <summary>
    /// The scanner finds an old code inside a key or a path, and ignores words that only contain one.
    /// </summary>
    [Fact]
    public void The_scanner_finds_old_codes_inside_keys()
    {
        HashSet<string> oldCodes = OldCodes();

        Assert.Equal(["iron"], FindOldCodes("string key = \"world.resource.iron\";", oldCodes));
        Assert.Equal(["admin_center"], FindOldCodes("Code(\"admin_center\")", oldCodes));
        Assert.Empty(FindOldCodes("string service = \"resource_1\"; // iron in a comment", oldCodes));
        Assert.Empty(FindOldCodes("string kind = \"gas_giant\";", oldCodes));
        Assert.Empty(FindOldCodes("\"description\": \"Mines iron from the deposit.\"", oldCodes));
        Assert.Empty(FindOldCodes("$\"/rules/{iron.GetProperty(\"id\")}\"", oldCodes));
    }

    private static HashSet<string> OldCodes() =>
        [.. NeutralCodeMap.Load().Values.SelectMany(group => group.Keys).Where(code => !LogicalCodes.Contains(code))];

    private static List<string> FindOldCodes(string line, HashSet<string> oldCodes) =>
        [.. StringLiteral().Matches(line)
            .Select(literal => InterpolationHole().Replace(literal.Groups["text"].Value, string.Empty))
            .Where(text => !text.Contains(' ', StringComparison.Ordinal))
            .SelectMany(text => WordSeparator().Split(text))
            .Where(oldCodes.Contains)];

    private static IEnumerable<string> EnumerateScannedFiles(string serverRoot)
    {
        string self = nameof(NeutralCodeScanTests) + ".cs";
        string separator = Path.DirectorySeparatorChar.ToString();

        return new[] { Path.Combine(serverRoot, "src"), Path.Combine(serverRoot, "tests") }
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(file => ScannedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                && !file.Contains(separator + "obj" + separator, StringComparison.Ordinal)
                && !file.Contains(separator + "Migrations" + separator, StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), "neutral-code-map.json", StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), self, StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), nameof(Architecture.NoGameTermsTests) + ".cs", StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), "appsettings.Local.json", StringComparison.Ordinal));
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

    [GeneratedRegex("[^a-z0-9_]+")]
    private static partial Regex WordSeparator();
}

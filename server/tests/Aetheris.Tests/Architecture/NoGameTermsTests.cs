using System.Text.RegularExpressions;
using Aetheris.Host.Data;

namespace Aetheris.Tests.Architecture;

/// <summary>
/// Tests that the core carries no game terms: no names, rules or seed values of features.
/// </summary>
/// <remarks>
/// Task 00001, section 4: the core is built without game logic. Features arrive with their
/// own tasks and bring their own names. Until then the code, the migrations and the seed
/// data only hold neutral names, and the only seeded parameters are <c>test.*</c> and the
/// <c>security.*</c> parameters of the core mechanisms.
/// The module stubs required by task 00001, section 2.1 keep their project names; only
/// those names are allowed.
/// </remarks>
public sealed partial class NoGameTermsTests
{
    /// <summary>
    /// The words that name game concepts. A word is matched as a whole token after the
    /// identifiers are split at case changes and separators, so <c>Shipped</c> or
    /// <c>credentials</c> do not match.
    /// </summary>
    private static readonly HashSet<string> BannedWords = new(StringComparer.Ordinal)
    {
        "attack", "attacks", "battle", "battles", "building", "buildings", "clan", "clans",
        "colonies", "colonise", "colonised", "colonize", "colonized", "colony", "credit",
        "credits", "crystal", "diplomacy", "fleet", "fleets", "mined", "mining", "ore",
        "planet", "planets", "population", "resource", "resources", "ship", "ships",
        "shipyard", "technologies", "technology", "upkeep",
    };

    /// <summary>
    /// The prefixes that a seeded parameter key may have.
    /// </summary>
    private static readonly string[] AllowedSeedPrefixes = ["test.", "security."];

    /// <summary>
    /// The file types that are scanned.
    /// </summary>
    private static readonly string[] ScannedExtensions = [".cs", ".json", ".csproj", ".props", ".txt", ".sql"];

    /// <summary>
    /// No source file, project file, migration or configuration file of the server
    /// contains a game term.
    /// </summary>
    [Fact]
    public void The_server_sources_contain_no_game_terms()
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

        Assert.True(
            findings.Count == 0,
            "Game terms found in the core:" + Environment.NewLine + string.Join(Environment.NewLine, findings));
    }

    /// <summary>
    /// The seed only holds neutral test parameters and the parameters of the core mechanisms.
    /// </summary>
    [Fact]
    public void The_seed_holds_only_test_and_security_parameters()
    {
        List<string> foreignKeys = ConfigSeed.GetParameters()
            .Select(parameter => parameter.Key)
            .Where(key => !AllowedSeedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(foreignKeys.Count == 0, $"Seed parameters outside the core: {string.Join(", ", foreignKeys)}.");
    }

    /// <summary>
    /// The scanner itself recognises a game term inside an identifier, and ignores the
    /// allowed module names.
    /// </summary>
    [Fact]
    public void The_scanner_finds_terms_inside_identifiers_and_ignores_module_names()
    {
        Assert.Equal(["building"], FindBannedWords("public sealed class BuildBuildingRequest"));
        Assert.Equal(["credits"], FindBannedWords("\"economy.credits.starting_capital\""));
        Assert.Empty(FindBannedWords("using Aetheris.Planets; IFleetModule module; AnalyzerReleases.Shipped.md"));
    }

    private static IEnumerable<string> EnumerateScannedFiles(string serverRoot)
    {
        string[] roots =
        [
            Path.Combine(serverRoot, "src"),
            Path.Combine(serverRoot, "analyzers"),
            Path.Combine(serverRoot, "tests"),
        ];

        string self = nameof(NoGameTermsTests) + ".cs";

        return roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Where(file => ScannedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !IsBuildOutput(file))
            .Where(file => !string.Equals(Path.GetFileName(file), self, StringComparison.Ordinal))
            .Where(file => !string.Equals(Path.GetFileName(file), "appsettings.Local.json", StringComparison.Ordinal));
    }

    private static bool IsBuildOutput(string file)
    {
        string separator = Path.DirectorySeparatorChar.ToString();

        return file.Contains(separator + "bin" + separator, StringComparison.Ordinal)
            || file.Contains(separator + "obj" + separator, StringComparison.Ordinal);
    }

    private static List<string> FindBannedWords(string line)
    {
        string cleaned = AllowedNames().Replace(line, " ");

        return WordSeparators().Split(cleaned)
            .SelectMany(part => CaseBoundaries().Split(part))
            .Select(word => word.ToLowerInvariant())
            .Where(BannedWords.Contains)
            .ToList();
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

    /// <summary>
    /// The names that are allowed although they contain a game word: the module stubs of
    /// task 00001, section 2.1, and framework names that are not ours: the OpenTelemetry
    /// API that describes the service, MSBuild embedded files, a standard HTTP header and the
    /// reflection API that reads embedded files.
    /// </summary>
    [GeneratedRegex(@"\bI?(Economy|Planets|Fleet|Market|Rating|Chat|Admin)Module\b|Aetheris\.(Economy|Planets|Fleet|Market|Rating|Chat|Admin)\b|""(Economy|Planets|Fleet|Market|Rating|Chat|Admin)""|\b(Economy|Planets|Fleet|Market|Rating|Chat|Admin) module\b|OpenTelemetry\.Resources|ConfigureResource|EmbeddedResource|Cross-Origin-Resource-Policy|GetManifestResource(Names|Stream)")]
    private static partial Regex AllowedNames();

    [GeneratedRegex("[^A-Za-z]+")]
    private static partial Regex WordSeparators();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex CaseBoundaries();
}

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
/// The World module of task 00004 is the first module with game rules: the words of its
/// domain (bodies, resources, crystals) are allowed in its own project and in its own tests
/// only. Every other file, the core and the host included, stays under the ban.
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
    /// The prefixes that a seeded parameter key may have: the neutral test parameters, the
    /// parameters of the core mechanisms and those of the modules with their own definitions.
    /// </summary>
    private static readonly string[] AllowedSeedPrefixes = ["test.", "security.", "scheduler.", "world.", "admin.", "planets.", "research.", "production."];

    /// <summary>
    /// The folders, relative to the server root, where the words of the game domain are
    /// allowed: the World module, the Planets module of task 00011, the common requirements
    /// module of task 00012 and their tests.
    /// </summary>
    private static readonly string[] WorldDomainFolders =
    [
        Path.Combine("src", "Aetheris.World") + Path.DirectorySeparatorChar,
        Path.Combine("tests", "Aetheris.Tests", "World") + Path.DirectorySeparatorChar,
        Path.Combine("src", "Aetheris.Planets") + Path.DirectorySeparatorChar,
        Path.Combine("tests", "Aetheris.Tests", "Planets") + Path.DirectorySeparatorChar,
        Path.Combine("src", "Aetheris.Requirements") + Path.DirectorySeparatorChar,
        Path.Combine("src", "Aetheris.Research") + Path.DirectorySeparatorChar,
        Path.Combine("tests", "Aetheris.Tests", "Research") + Path.DirectorySeparatorChar,
        Path.Combine("src", "Aetheris.Production") + Path.DirectorySeparatorChar,
        Path.Combine("tests", "Aetheris.Tests", "Production") + Path.DirectorySeparatorChar,
    ];

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
    /// The seed only holds neutral test parameters, the parameters of the core mechanisms and
    /// the parameters that the World module defines itself.
    /// </summary>
    [Fact]
    public void The_seed_holds_only_core_and_module_parameters()
    {
        List<string> foreignKeys = ConfigSeed.GetParameters()
            .Select(parameter => parameter.Key)
            .Where(key => !AllowedSeedPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(foreignKeys.Count == 0, $"Seed parameters outside the core: {string.Join(", ", foreignKeys)}.");

        List<string> worldKeysOutsideModule = ConfigSeed.GetParameters()
            .Select(parameter => parameter.Key)
            .Where(key => key.StartsWith("world.", StringComparison.Ordinal))
            .Except(Aetheris.World.Configuration.WorldConfigKeys.Definitions.Select(definition => definition.Key), StringComparer.Ordinal)
            .ToList();

        Assert.True(
            worldKeysOutsideModule.Count == 0,
            $"World parameters not defined by the World module: {string.Join(", ", worldKeysOutsideModule)}.");
    }

    /// <summary>
    /// The words of the world domain are allowed in the World module and its tests only; the
    /// core, the host and the other modules are still scanned.
    /// </summary>
    [Fact]
    public void The_world_domain_is_allowed_only_in_the_world_module()
    {
        string serverRoot = FindServerRoot();

        Assert.True(IsWorldDomainFile(serverRoot, Path.Combine(serverRoot, "src", "Aetheris.World", "Generation", "SectorGenerator.cs")));
        Assert.True(IsWorldDomainFile(serverRoot, Path.Combine(serverRoot, "tests", "Aetheris.Tests", "World", "SectorGeneratorTests.cs")));
        Assert.False(IsWorldDomainFile(serverRoot, Path.Combine(serverRoot, "src", "Aetheris.Host", "Program.cs")));
        Assert.False(IsWorldDomainFile(serverRoot, Path.Combine(serverRoot, "src", "Aetheris.Core", "Commands", "Command.cs")));
        Assert.True(IsWorldDomainFile(serverRoot, Path.Combine(serverRoot, "src", "Aetheris.Planets", "PlanetsModule.cs")));
        Assert.False(IsWorldDomainFile(serverRoot, Path.Combine(serverRoot, "src", "Aetheris.Economy", "EconomyModule.cs")));
        Assert.Contains(
            Path.Combine(serverRoot, "src", "Aetheris.Host", "Program.cs"),
            EnumerateScannedFiles(serverRoot));
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
            .Where(file => !string.Equals(Path.GetFileName(file), "appsettings.Local.json", StringComparison.Ordinal))
            .Where(file => !IsWorldDomainFile(serverRoot, file));
    }

    private static bool IsWorldDomainFile(string serverRoot, string file)
    {
        string relative = Path.GetRelativePath(serverRoot, file);

        return WorldDomainFolders.Any(folder => relative.StartsWith(folder, StringComparison.Ordinal));
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
    [GeneratedRegex(@"\bI?(Economy|Planets|Fleet|Market|Rating|Chat|Admin|Requirements|Research)Module\b|Aetheris\.(Economy|Planets|Fleet|Market|Rating|Chat|Admin|Requirements|Research)\b|""(Economy|Planets|Fleet|Market|Rating|Chat|Admin|Requirements|Research)""|\b(Economy|Planets|Fleet|Market|Rating|Chat|Admin|Requirements|Research) module\b|OpenTelemetry\.Resources|ConfigureResource|EmbeddedResource|Cross-Origin-Resource-Policy|GetManifestResource(Names|Stream)")]
    private static partial Regex AllowedNames();

    [GeneratedRegex("[^A-Za-z]+")]
    private static partial Regex WordSeparators();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex CaseBoundaries();
}

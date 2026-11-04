using System.Text.RegularExpressions;

namespace Aetheris.Tests.Architecture;

/// <summary>
/// Tests that a module uses only the public contract of another module: its root namespace and
/// its <c>Api</c> namespace, never its services, data or other internals (documentation/tech/08,
/// rule 5; code audit of 04.11.2026).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ModuleIsolationTests"/> checks which modules reference which; this test checks
/// through which door. It reads the sources, because a reference to a public type in an internal
/// namespace looks the same as a reference to the contract in the compiled assembly.
/// </para>
/// <para>
/// The uses that existed before the check are listed in <see cref="KnownDebt"/>; the list may
/// only shrink. A new use of an internal namespace fails the test, and the Planets module is clean.
/// </para>
/// </remarks>
public sealed partial class ModuleInternalsTests
{
    private static readonly string[] ModuleNames =
    [
        "Economy",
        "Planets",
        "Fleet",
        "Market",
        "Rating",
        "Chat",
        "Admin",
        "World",
        "Requirements",
        "Research",
        "Production",
        "Processes",
    ];

    /// <summary>
    /// The module whose internals no other module may use at all.
    /// </summary>
    private const string ContractOnlyModule = "Aetheris.Planets";

    /// <summary>
    /// The namespaces of a module another module may use besides its root namespace.
    /// </summary>
    private static readonly string[] PublicNamespaces = ["Api"];

    /// <summary>
    /// The uses of internal namespaces that existed when the check was added: module, the other
    /// module, the namespace inside it. They are a debt to pay off, not a permission.
    /// </summary>
    private static readonly HashSet<string> KnownDebt = new(StringComparer.Ordinal)
    {
        "Aetheris.Economy -> Aetheris.World.Services",
        "Aetheris.Market -> Aetheris.Production.Data",
        "Aetheris.Market -> Aetheris.Production.Services",
        "Aetheris.Planets -> Aetheris.Economy.Services",
        "Aetheris.Planets -> Aetheris.Requirements.Services",
        "Aetheris.Planets -> Aetheris.World.Data",
        "Aetheris.Planets -> Aetheris.World.Services",
        "Aetheris.Planets -> Aetheris.World.Visibility",
        "Aetheris.Processes -> Aetheris.Economy.Services",
        "Aetheris.Processes -> Aetheris.Production.Data",
        "Aetheris.Processes -> Aetheris.Production.Services",
        "Aetheris.Processes -> Aetheris.Requirements.Services",
        "Aetheris.Processes -> Aetheris.World.Services",
        "Aetheris.Production -> Aetheris.Economy.Services",
        "Aetheris.Production -> Aetheris.Requirements.Services",
        "Aetheris.Production -> Aetheris.Research.Services",
        "Aetheris.Production -> Aetheris.World.Services",
        "Aetheris.Research -> Aetheris.Economy.Services",
        "Aetheris.Research -> Aetheris.Requirements.Services",
        "Aetheris.Research -> Aetheris.World.Data",
        "Aetheris.Research -> Aetheris.World.Services",
    };

    /// <summary>
    /// No module reaches into the internals of another module beyond the known debt, and no
    /// module reaches into the internals of the Planets module at all.
    /// </summary>
    [Fact]
    public void A_module_uses_only_the_contract_of_another_module()
    {
        string sourceRoot = Path.Combine(FindServerRoot(), "src");
        SortedSet<string> found = new(StringComparer.Ordinal);

        // Only a name that is a declared namespace of the other module counts, so a type of the
        // root namespace of a module (its contract) is never taken for an internal namespace.
        HashSet<string> namespaces = new(
            ModuleNames
                .Select(module => Path.Combine(sourceRoot, "Aetheris." + module))
                .Where(Directory.Exists)
                .SelectMany(SourceFiles)
                .SelectMany(file => NamespaceDeclaration().Matches(File.ReadAllText(file)))
                .Select(match => match.Groups["name"].Value),
            StringComparer.Ordinal);

        foreach (string module in ModuleNames)
        {
            string moduleRoot = Path.Combine(sourceRoot, "Aetheris." + module);

            if (!Directory.Exists(moduleRoot))
            {
                continue;
            }

            foreach (string file in SourceFiles(moduleRoot))
            {
                foreach (Match match in QualifiedName().Matches(File.ReadAllText(file)))
                {
                    string other = match.Groups["module"].Value;
                    string inner = match.Groups["inner"].Value;

                    if (string.Equals(other, module, StringComparison.Ordinal)
                        || !ModuleNames.Contains(other, StringComparer.Ordinal)
                        || PublicNamespaces.Contains(inner, StringComparer.Ordinal)
                        || !namespaces.Contains("Aetheris." + other + "." + inner))
                    {
                        continue;
                    }

                    found.Add("Aetheris." + module + " -> Aetheris." + other + "." + inner);
                }
            }
        }

        List<string> unexpected = [.. found.Where(use => !KnownDebt.Contains(use) || use.Contains("-> " + ContractOnlyModule + ".", StringComparison.Ordinal))];

        Assert.True(
            unexpected.Count == 0,
            "A module uses the internals of another module: " + string.Join(", ", unexpected) + ".");
    }

    private static IEnumerable<string> SourceFiles(string moduleRoot)
    {
        string separator = Path.DirectorySeparatorChar.ToString();

        return Directory.EnumerateFiles(moduleRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(separator + "bin" + separator, StringComparison.Ordinal)
                && !file.Contains(separator + "obj" + separator, StringComparison.Ordinal)
                && !file.Contains(separator + "Migrations" + separator, StringComparison.Ordinal));
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

    [GeneratedRegex(@"\bAetheris\.(?<module>[A-Z][A-Za-z]+)\.(?<inner>[A-Z][A-Za-z]+)")]
    private static partial Regex QualifiedName();

    [GeneratedRegex(@"^namespace (?<name>Aetheris\.[A-Z][A-Za-z]+\.[A-Z][A-Za-z]+)", RegexOptions.Multiline)]
    private static partial Regex NamespaceDeclaration();
}

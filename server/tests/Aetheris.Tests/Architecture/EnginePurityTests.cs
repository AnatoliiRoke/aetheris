namespace Aetheris.Tests.Architecture;

/// <summary>
/// The pure library stays free of clocks, draws, and project references (task 00063).
/// </summary>
public sealed class EnginePurityTests
{
    private static readonly string[] Forbidden =
    [
        "DateTime",
        "Random",
        "Guid",
        "Parallel",
        "FusedMultiplyAdd",
        ".Where(",
        ".Select(",
        "Enumerable",
    ];

    /// <summary>Sources of the library do not name a clock, a draw, or a query operator.</summary>
    [Fact]
    public void The_library_sources_stay_free_of_clocks_and_draws()
    {
        string root = FindServerRoot();
        string folder = Path.Combine(root, "src", "Aetheris.Combat.Engine");

        Assert.True(Directory.Exists(folder));

        foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file))
            {
                continue;
            }

            string text = File.ReadAllText(file);

            for (int i = 0; i < Forbidden.Length; i++)
            {
                Assert.DoesNotContain(Forbidden[i], text, StringComparison.Ordinal);
            }
        }

        string project = File.ReadAllText(Path.Combine(folder, "Aetheris.Combat.Engine.csproj"));

        Assert.DoesNotContain("ProjectReference", project, StringComparison.Ordinal);
    }

    private static bool IsBuildOutput(string file)
    {
        string separator = Path.DirectorySeparatorChar.ToString();

        return file.Contains(separator + "bin" + separator, StringComparison.Ordinal)
            || file.Contains(separator + "obj" + separator, StringComparison.Ordinal);
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
}

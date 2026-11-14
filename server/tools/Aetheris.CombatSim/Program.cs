using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aetheris.CombatSim;

/// <summary>The balance console. It only calls the combat engine.</summary>
internal static class Program
{
    private const int DefaultRuns = 200;

    private const ulong DefaultSeed = 1;

    private const int FlagPrefixLength = 2;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception error) when (error is InvalidOperationException
            or JsonException
            or FormatException
            or OverflowException
            or ArgumentOutOfRangeException
            or IOException)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Потрібні --fleet-a, --fleet-b, --behavior-a, --behavior-b і --out.");
            return 1;
        }

        Dictionary<string, string> options = ReadOptions(args);
        string behaviorA = Require(options, "behavior-a");
        string behaviorB = Require(options, "behavior-b");
        KnownBehavior(behaviorA);
        KnownBehavior(behaviorB);
        int runs = ReadRuns(options);
        ulong seed = ReadSeed(options);
        DesignCatalog catalog = CatalogReader.Read(FindDesigns(options));
        FleetFile fleetA = FleetReader.Read(Require(options, "fleet-a"));
        FleetFile fleetB = FleetReader.Read(Require(options, "fleet-b"));
        BuiltEngagement engagement = EngagementBuilder.Build(catalog, fleetA, fleetB, behaviorA, behaviorB);

        if (options.ContainsKey("benchmark"))
        {
            SegmentSample sample = SegmentProbe.Measure(engagement.State with { Seed = seed, Cycle = 0 }, catalog.Settings);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"відрізок {sample.Milliseconds:0.###} мс, виділено {sample.AllocatedBytes} байт"));
            return 0;
        }

        SegmentSample measured = SegmentProbe.Measure(engagement.State with { Seed = seed, Cycle = 0 }, catalog.Settings);
        RunSet headline = Campaign.Run(
            engagement.State,
            catalog.Settings,
            runs,
            seed,
            catalog.Layout.SideA,
            catalog.Layout.SideB,
            engagement.Weapons);
        IReadOnlyList<MatrixCell> matrix = Matrix(catalog, behaviorA, behaviorB, runs, seed);
        string reportPath = Require(options, "out");
        string csvPath = Path.ChangeExtension(reportPath, ".csv");
        ReportWriter.Write(
            reportPath,
            csvPath,
            new ReportRequest
            {
                Arguments = ArgumentsOf(args),
                DesignsPath = FindDesigns(options),
                Catalog = catalog,
                FleetA = fleetA,
                FleetB = fleetB,
                BehaviorA = behaviorA,
                BehaviorB = behaviorB,
                Seed = seed,
                Runs = runs,
                Headline = headline,
                Opening = engagement.Opening,
                Matrix = matrix,
                Sample = measured,
            });
        Console.WriteLine($"Звіт: {reportPath}");
        return 0;
    }

    private static IReadOnlyList<MatrixCell> Matrix(
        DesignCatalog catalog,
        string behaviorA,
        string behaviorB,
        int runs,
        ulong seed)
    {
        ShipDesign[] designs = new ShipDesign[catalog.Designs.Count];

        for (int i = 0; i < designs.Length; i++)
        {
            designs[i] = catalog.Designs[i];
        }

        Array.Sort(designs, static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        List<MatrixCell> cells = [];

        for (int row = 0; row < designs.Length; row++)
        {
            for (int column = 0; column < designs.Length; column++)
            {
                BuiltEngagement duel = EngagementBuilder.Build(
                    catalog,
                    FleetReader.Single(designs[row].Id, catalog.Duel.OriginAX, catalog.Duel.OriginAY),
                    FleetReader.Single(designs[column].Id, catalog.Duel.OriginBX, catalog.Duel.OriginBY),
                    behaviorA,
                    behaviorB);
                RunSet duelRuns = Campaign.Run(
                    duel.State,
                    catalog.Settings,
                    runs,
                    seed,
                    catalog.Layout.SideA,
                    catalog.Layout.SideB,
                    Campaign.NoWeapons);
                cells.Add(new MatrixCell
                {
                    RowId = designs[row].Id,
                    ColumnId = designs[column].Id,
                    RowName = designs[row].Name,
                    ColumnName = designs[column].Name,
                    Runs = duelRuns,
                });
            }
        }

        return cells;
    }

    private static Dictionary<string, string> ReadOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.Ordinal);

        for (int i = 0; i < args.Length; i++)
        {
            string token = args[i];

            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length <= FlagPrefixLength)
            {
                throw new InvalidOperationException($"Unknown argument {token}.");
            }

            string name = token[FlagPrefixLength..];

            if (name == "benchmark")
            {
                if (!options.TryAdd(name, ""))
                {
                    throw new InvalidOperationException($"Repeated --{name}.");
                }

                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new InvalidOperationException($"Missing value for --{name}.");
            }

            i++;

            if (!options.TryAdd(name, args[i]))
            {
                throw new InvalidOperationException($"Repeated --{name}.");
            }
        }

        return options;
    }

    private static string Require(Dictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out string? text) || text.Length == 0)
        {
            throw new InvalidOperationException($"Missing --{name}.");
        }

        return text;
    }

    private static int ReadRuns(Dictionary<string, string> options)
    {
        int runs = DefaultRuns;

        if (options.TryGetValue("runs", out string? runsText))
        {
            runs = int.Parse(runsText, CultureInfo.InvariantCulture);
        }

        if (runs <= 0)
        {
            throw new InvalidOperationException("The run count has to be positive.");
        }

        return runs;
    }

    private static ulong ReadSeed(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("seed", out string? seedText))
        {
            return DefaultSeed;
        }

        return ulong.Parse(seedText, CultureInfo.InvariantCulture);
    }

    private static void KnownBehavior(string logic)
    {
        if (logic != Aetheris.Combat.Engine.BehaviorLogics.Approach
            && logic != Aetheris.Combat.Engine.BehaviorLogics.KeepDistance
            && logic != Aetheris.Combat.Engine.BehaviorLogics.Stand
            && logic != Aetheris.Combat.Engine.BehaviorLogics.Escort)
        {
            throw new InvalidOperationException($"Unknown behavior {logic}.");
        }
    }

    private static string FindDesigns(Dictionary<string, string> options)
    {
        if (options.TryGetValue("designs", out string? given))
        {
            return given;
        }

        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            string underServer = Path.Combine(directory.FullName, "server", "tools", "balance", "combat", "designs.json");

            if (File.Exists(underServer))
            {
                return PreferRelative(underServer);
            }

            string beside = Path.Combine(directory.FullName, "tools", "balance", "combat", "designs.json");

            if (File.Exists(beside))
            {
                return PreferRelative(beside);
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("designs.json was not found. Pass --designs.");
    }

    private static string PreferRelative(string path)
    {
        string full = Path.GetFullPath(path);
        string current = Directory.GetCurrentDirectory();
        string prefix = current.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (full.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Path.GetRelativePath(current, full);
        }

        return full;
    }

    private static string ArgumentsOf(string[] args)
    {
        StringBuilder builder = new();

        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            string token = args[i];

            if (token.Contains(' ') || token.Contains('"'))
            {
                builder.Append('"');
                builder.Append(token.Replace("\"", "\\\"", StringComparison.Ordinal));
                builder.Append('"');
            }
            else
            {
                builder.Append(token);
            }
        }

        return builder.ToString();
    }
}

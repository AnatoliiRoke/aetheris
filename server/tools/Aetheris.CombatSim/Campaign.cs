using System.Diagnostics;

using Aetheris.Combat.Engine;

namespace Aetheris.CombatSim;

/// <summary>One measured segment, after a warmup of the same segment.</summary>
internal readonly record struct SegmentSample(double Milliseconds, long AllocatedBytes);

/// <summary>One cell of the design matrix.</summary>
internal sealed class MatrixCell
{
    public required string RowId { get; init; }

    public required string ColumnId { get; init; }

    public required string RowName { get; init; }

    public required string ColumnName { get; init; }

    public required RunSet Runs { get; init; }
}

/// <summary>Damage attributed to one weapon part across the runs.</summary>
internal sealed class ModuleScore
{
    public required string Part { get; init; }

    public required string Name { get; init; }

    public int Copies { get; init; }

    public double Cost { get; init; }

    public double MeanDamage { get; init; }

    public double DamagePerCost { get; init; }
}

/// <summary>The totals of one series of runs.</summary>
internal sealed class RunSet
{
    public int Runs { get; init; }

    public int WinsA { get; init; }

    public int WinsB { get; init; }

    public int Draws { get; init; }

    public int ShipsLostA { get; init; }

    public double CostLostA { get; init; }

    public int ShipsLostB { get; init; }

    public double CostLostB { get; init; }

    public int CycleSum { get; init; }

    public long ShotsA { get; init; }

    public long HitsA { get; init; }

    public long ShotsB { get; init; }

    public long HitsB { get; init; }

    public double FirstRunClosest { get; init; }

    public required IReadOnlyList<int> ByCycles { get; init; }

    public required IReadOnlyList<ModuleScore> Modules { get; init; }
}

/// <summary>Repeats an engagement. The engine does not advance the cycle, so this loop does.</summary>
internal static class Campaign
{
    public static IReadOnlyDictionary<int, IReadOnlyList<WeaponShare>> NoWeapons { get; } =
        new Dictionary<int, IReadOnlyList<WeaponShare>>();

    public static RunSet Run(
        BattleState template,
        CombatSettings settings,
        int runs,
        ulong seed,
        int sideA,
        int sideB,
        IReadOnlyDictionary<int, IReadOnlyList<WeaponShare>> weapons)
    {
        if (runs > 1 && (ulong)(runs - 1) > ulong.MaxValue - seed)
        {
            throw new InvalidOperationException("The seed range does not fit.");
        }

        SegmentInput input = new(settings.SegmentSeconds);
        int[] byCycles = new int[settings.MaxCycles + 1];
        Dictionary<int, int> sideOf = SideOf(template);
        Dictionary<string, ModuleTally> tallies = Register(weapons);
        int winsA = 0;
        int winsB = 0;
        int draws = 0;
        int shipsLostA = 0;
        int shipsLostB = 0;
        double costLostA = 0;
        double costLostB = 0;
        int cycleSum = 0;
        long shotsA = 0;
        long hitsA = 0;
        long shotsB = 0;
        long hitsB = 0;
        double firstRunClosest = double.PositiveInfinity;

        for (int index = 0; index < runs; index++)
        {
            ulong battleSeed = seed + (ulong)index;
            BattleState current = template with { Seed = battleSeed, Cycle = 0 };
            string kind = OutcomeKinds.Draw;
            int winner = 0;
            int cycles = 0;

            for (int cycle = 0; cycle < settings.MaxCycles; cycle++)
            {
                current = current with { Cycle = cycle };
                SegmentResult finished = SegmentSimulator.Run(current, input, settings);
                cycles++;
                Attribute(
                    finished,
                    sideOf,
                    sideA,
                    weapons,
                    tallies,
                    ref shotsA,
                    ref hitsA,
                    ref shotsB,
                    ref hitsB);
                current = finished.NewState;

                if (finished.Outcome.Kind != OutcomeKinds.Continue)
                {
                    kind = finished.Outcome.Kind;
                    winner = finished.Outcome.SideId;
                    break;
                }

                current = CycleEnd.Apply(current, settings);
            }

            if (cycles < byCycles.Length)
            {
                byCycles[cycles]++;
            }

            cycleSum += cycles;

            if (kind == OutcomeKinds.SideWon && winner == sideA)
            {
                winsA++;
            }
            else if (kind == OutcomeKinds.SideWon && winner == sideB)
            {
                winsB++;
            }
            else
            {
                draws++;
            }

            CountLosses(current, sideA, sideB, ref shipsLostA, ref costLostA, ref shipsLostB, ref costLostB);

            if (index == 0)
            {
                firstRunClosest = ClosestOpposing(current, sideA, sideB);
            }
        }

        return new RunSet
        {
            Runs = runs,
            WinsA = winsA,
            WinsB = winsB,
            Draws = draws,
            ShipsLostA = shipsLostA,
            CostLostA = costLostA,
            ShipsLostB = shipsLostB,
            CostLostB = costLostB,
            CycleSum = cycleSum,
            ShotsA = shotsA,
            HitsA = hitsA,
            ShotsB = shotsB,
            HitsB = hitsB,
            FirstRunClosest = firstRunClosest,
            ByCycles = byCycles,
            Modules = Scores(tallies, runs),
        };
    }

    private static double ClosestOpposing(BattleState state, int sideA, int sideB)
    {
        double closest = double.PositiveInfinity;

        for (int left = 0; left < state.Ships.Count; left++)
        {
            if (state.Ships[left].SideId != sideA)
            {
                continue;
            }

            for (int right = 0; right < state.Ships.Count; right++)
            {
                if (state.Ships[right].SideId != sideB)
                {
                    continue;
                }

                double distance = Math.Sqrt(Vec3.DistanceSquared(state.Ships[left].Position, state.Ships[right].Position));

                if (distance < closest)
                {
                    closest = distance;
                }
            }
        }

        return closest;
    }

    private static Dictionary<int, int> SideOf(BattleState template)
    {
        Dictionary<int, int> sideOf = new(template.Squadrons.Count);

        for (int i = 0; i < template.Squadrons.Count; i++)
        {
            sideOf[template.Squadrons[i].SquadronId] = template.Squadrons[i].SideId;
        }

        return sideOf;
    }

    private static Dictionary<string, ModuleTally> Register(
        IReadOnlyDictionary<int, IReadOnlyList<WeaponShare>> weapons)
    {
        Dictionary<string, ModuleTally> tallies = new(StringComparer.Ordinal);

        foreach (KeyValuePair<int, IReadOnlyList<WeaponShare>> pair in weapons)
        {
            IReadOnlyList<WeaponShare> shares = pair.Value;

            for (int i = 0; i < shares.Count; i++)
            {
                WeaponShare share = shares[i];

                if (!tallies.TryGetValue(share.Part, out ModuleTally? tally))
                {
                    tally = new ModuleTally
                    {
                        Part = share.Part,
                        Name = share.Name,
                        Cost = share.Cost,
                    };
                    tallies[share.Part] = tally;
                }

                tally.Copies += share.Copies;
            }
        }

        return tallies;
    }

    private static void Attribute(
        SegmentResult finished,
        Dictionary<int, int> sideOf,
        int sideA,
        IReadOnlyDictionary<int, IReadOnlyList<WeaponShare>> weapons,
        Dictionary<string, ModuleTally> tallies,
        ref long shotsA,
        ref long hitsA,
        ref long shotsB,
        ref long hitsB)
    {
        IReadOnlyList<BattleLogEntry> log = finished.Log;

        for (int i = 0; i < log.Count; i++)
        {
            BattleLogEntry line = log[i];

            if (line.Type != LogTypes.FireSummary)
            {
                continue;
            }

            if (sideOf.TryGetValue(line.FromSquadronId, out int side))
            {
                if (side == sideA)
                {
                    shotsA += line.Shots;
                    hitsA += line.Hits;
                }
                else
                {
                    shotsB += line.Shots;
                    hitsB += line.Hits;
                }
            }

            double dealt = line.Shield + line.Armor + line.Hull;

            if (dealt <= 0 || !weapons.TryGetValue(line.FromSquadronId, out IReadOnlyList<WeaponShare>? shares))
            {
                continue;
            }

            double weight = 0;

            for (int shareIndex = 0; shareIndex < shares.Count; shareIndex++)
            {
                weight += shares[shareIndex].SalvoWeight;
            }

            if (weight <= 0)
            {
                continue;
            }

            for (int shareIndex = 0; shareIndex < shares.Count; shareIndex++)
            {
                WeaponShare share = shares[shareIndex];

                if (tallies.TryGetValue(share.Part, out ModuleTally? tally))
                {
                    tally.Damage += dealt * share.SalvoWeight / weight;
                }
            }
        }
    }

    private static void CountLosses(
        BattleState state,
        int sideA,
        int sideB,
        ref int shipsLostA,
        ref double costLostA,
        ref int shipsLostB,
        ref double costLostB)
    {
        for (int i = 0; i < state.Ships.Count; i++)
        {
            ShipState ship = state.Ships[i];

            if (!ship.Destroyed && ship.Hull > 0)
            {
                continue;
            }

            if (ship.SideId == sideA)
            {
                shipsLostA++;
                costLostA += ship.Cost;
            }
            else if (ship.SideId == sideB)
            {
                shipsLostB++;
                costLostB += ship.Cost;
            }
        }
    }

    private static ModuleScore[] Scores(Dictionary<string, ModuleTally> tallies, int runs)
    {
        ModuleScore[] scores = new ModuleScore[tallies.Count];
        int cursor = 0;

        foreach (ModuleTally tally in tallies.Values)
        {
            double meanDamage = runs > 0 ? tally.Damage / runs : 0;
            double spent = tally.Cost * tally.Copies;
            scores[cursor] = new ModuleScore
            {
                Part = tally.Part,
                Name = tally.Name,
                Copies = tally.Copies,
                Cost = tally.Cost,
                MeanDamage = meanDamage,
                DamagePerCost = spent > 0 ? meanDamage / spent : 0,
            };
            cursor++;
        }

        Array.Sort(scores, static (left, right) =>
        {
            int byRatio = right.DamagePerCost.CompareTo(left.DamagePerCost);

            if (byRatio != 0)
            {
                return byRatio;
            }

            return string.CompareOrdinal(left.Part, right.Part);
        });

        return scores;
    }

    private sealed class ModuleTally
    {
        public required string Part { get; init; }

        public required string Name { get; init; }

        public double Cost { get; init; }

        public int Copies { get; set; }

        public double Damage { get; set; }
    }
}

/// <summary>Times one segment and reads the bytes allocated on this thread.</summary>
internal static class SegmentProbe
{
    public static SegmentSample Measure(BattleState state, CombatSettings settings)
    {
        SegmentInput input = new(settings.SegmentSeconds);
        SegmentSimulator.Run(state, input, settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        SegmentSimulator.Run(state, input, settings);
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        return new SegmentSample(elapsed.TotalMilliseconds, allocated);
    }
}

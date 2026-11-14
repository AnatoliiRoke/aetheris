using System.Diagnostics;
using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>A segment of two lines stays inside the time and allocation budget (task 00063).</summary>
public sealed class PerformanceTests
{
    private const int ShipsPerSide = 300;
    private const int DronesTotal = 60;

    [Fact]
    [Trait("Category", "Perf")]
    public void Two_lines_finish_a_segment_inside_the_budget()
    {
        CombatSettings settings = Engagement.Settings(
            tickMilliseconds: 250,
            segmentSeconds: 60,
            fieldWidth: 8000,
            fieldHeight: 8000);
        BattleState state = Lines();

        SegmentSimulator.Run(state, new SegmentInput(60), settings);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch watch = Stopwatch.StartNew();
        SegmentSimulator.Run(state, new SegmentInput(60), settings);
        watch.Stop();
        long used = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(watch.Elapsed.TotalMilliseconds < 200, $"elapsed {watch.Elapsed.TotalMilliseconds}");
        Assert.True(used < 5 * 1024 * 1024, $"allocated {used}");
    }

    private static BattleState Lines()
    {
        ShipState[] ships = new ShipState[ShipsPerSide * 2];
        List<long> first = [];
        List<long> second = [];
        List<long> third = [];
        List<long> fourth = [];
        List<DroneState> drones = [];
        long droneId = 1;

        for (int i = 0; i < ShipsPerSide; i++)
        {
            long shipId = i + 1;
            int squadronId = i < 150 ? 1 : 2;
            ships[i] = LineShip(shipId, 1, squadronId, Along(i), 3700);
            (squadronId == 1 ? first : second).Add(shipId);

            if (i % 10 == 0)
            {
                drones.Add(Drone(droneId, shipId, 1, Along(i), 3700));
                droneId++;
            }
        }

        for (int i = 0; i < ShipsPerSide; i++)
        {
            long shipId = ShipsPerSide + i + 1;
            int squadronId = i < 150 ? 3 : 4;
            ships[ShipsPerSide + i] = LineShip(shipId, 2, squadronId, Along(i), 4100);
            (squadronId == 3 ? third : fourth).Add(shipId);

            if (i % 10 == 0)
            {
                drones.Add(Drone(droneId, shipId, 2, Along(i), 4100));
                droneId++;
            }
        }

        Assert.Equal(DronesTotal, drones.Count);

        return new BattleState(
            1,
            0,
            1,
            new FieldState(8000, 8000, []),
            [new SideState(1, true), new SideState(2, false)],
            [
                new SquadronState(1, 1, "a", first, Engagement.Order()),
                new SquadronState(2, 1, "b", second, Engagement.Order()),
                new SquadronState(3, 2, "c", third, Engagement.Order()),
                new SquadronState(4, 2, "d", fourth, Engagement.Order()),
            ],
            ships,
            drones,
            []);
    }

    private static double Along(int index) => 700 + (index * 22);

    private static ShipState LineShip(long shipId, int sideId, int squadronId, double x, double y)
    {
        return Engagement.Ship(
            shipId,
            sideId,
            squadronId,
            x,
            y,
            [
                Engagement.Module(shipId * 10, ModuleKinds.Reactor, Engagement.Facts(power: 100), priority: 0, mass: 1),
                Engagement.Module(shipId * 10 + 1, ModuleKinds.Radar, Engagement.Facts(detection: 500, energyUse: 1), priority: 30, mass: 1),
                Engagement.Module(
                    shipId * 10 + 2,
                    ModuleKinds.Weapon,
                    Engagement.Facts(damage: 1, rate: 1, range: 500, energyUse: 1, damageKind: DamageKinds.Energy),
                    priority: 20,
                    mass: 1,
                    hullGroupId: 1,
                    accuracy: 1),
            ],
            hull: 1_000_000);
    }

    private static DroneState Drone(long droneId, long carrierId, int sideId, double x, double y)
    {
        return new DroneState(
            droneId,
            carrierId,
            sideId,
            9,
            1,
            new Vec3(x, y, 0),
            default,
            10,
            10,
            20,
            1,
            0,
            0,
            0,
            80,
            false,
            false,
            false,
            0,
            Engagement.Facts(),
            []);
    }
}

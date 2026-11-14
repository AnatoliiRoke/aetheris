using Aetheris.Combat.Engine;

namespace Aetheris.Tests.Combat;

/// <summary>Small engagements for the segment tests. The numbers here are the cases, not the catalog.</summary>
internal static class Engagement
{
    public static CombatSettings Settings(
        int tickMilliseconds = 1000,
        int segmentSeconds = 60,
        double moduleDamageShare = 0.25,
        double distanceFalloff = 0,
        double targetSpeedFalloff = 0,
        double shieldRestoreShare = 0.30,
        double asteroidHitMultiplier = 1,
        int maxCycles = 6,
        int fieldWidth = 20000,
        int fieldHeight = 12000,
        double debrisShare = 0.20)
    {
        return new CombatSettings(
            300,
            420,
            segmentSeconds,
            tickMilliseconds,
            10,
            maxCycles,
            shieldRestoreShare,
            moduleDamageShare,
            fieldWidth,
            fieldHeight,
            3000,
            1,
            asteroidHitMultiplier,
            1,
            0.5,
            distanceFalloff,
            targetSpeedFalloff,
            12,
            5,
            debrisShare,
            0,
            2,
            12,
            1,
            1);
    }

    public static ModuleFacts Facts(
        double damage = 0,
        double rate = 0,
        double range = 0,
        double flightSpeed = 0,
        string damageKind = "",
        double detection = 0,
        double power = 0,
        double energyUse = 0,
        double speed = 0,
        double shieldCapacity = 0,
        double energyBlock = 0,
        double kineticBlock = 0,
        double antiMissileChance = 0,
        double launcherSize = 0,
        int ammoPartTypeId = 0,
        double repair = 0,
        double missiles = 0)
    {
        return new ModuleFacts(
            energyUse,
            power,
            speed,
            detection,
            damage,
            rate,
            range,
            flightSpeed,
            damageKind,
            ammoPartTypeId,
            energyBlock,
            kineticBlock,
            shieldCapacity,
            0,
            repair,
            antiMissileChance,
            missiles,
            launcherSize,
            0);
    }

    public static ModuleState Module(
        long moduleId,
        string kind,
        ModuleFacts facts,
        double integrity = 100,
        double mass = 1,
        int priority = 20,
        bool on = true,
        int hullGroupId = 1,
        double accuracy = 1,
        double cooldownSeconds = 0)
    {
        AccuracyEntry[] rows = accuracy < 0
            ? []
            : [new AccuracyEntry(hullGroupId, accuracy)];

        return new ModuleState(
            moduleId,
            (int)moduleId,
            kind,
            integrity,
            integrity,
            priority,
            mass,
            on,
            cooldownSeconds,
            facts,
            rows);
    }

    public static ShipState Ship(
        long shipId,
        int sideId,
        int squadronId,
        double x,
        double y,
        IReadOnlyList<ModuleState> modules,
        double hull = 1000,
        double shieldNow = 0,
        double shieldCapacity = 0,
        double armor = 0,
        double armorMax = 0,
        double signature = 1,
        double energyBlock = 0,
        double kineticBlock = 0,
        int hullGroupId = 1,
        Vec3 velocity = default,
        double armorShare = 1,
        IReadOnlyDictionary<int, int>? hold = null,
        IReadOnlyList<DronePattern>? patterns = null,
        double cost = 0)
    {
        return new ShipState(
            shipId,
            sideId,
            squadronId,
            hullGroupId,
            new Vec3(x, y, 0),
            velocity,
            0,
            hull,
            hull,
            armor,
            armorMax > 0 ? armorMax : armor,
            shieldNow,
            shieldCapacity,
            signature,
            0,
            modules,
            new Dictionary<int, int>(),
            hold ?? new Dictionary<int, int>(),
            false,
            false,
            false,
            default,
            1,
            0,
            energyBlock,
            kineticBlock,
            armorShare,
            cost,
            false,
            patterns);
    }

    public static SquadronOrder Order(
        string behavior = BehaviorLogics.Stand,
        bool retreat = false,
        DroneCommand drones = default,
        string rule = TargetRuleLogics.Nearest)
    {
        return new SquadronOrder([], behavior, rule, null, null, drones, retreat, null);
    }

    public static BattleState Duel(
        ShipState left,
        ShipState right,
        string leftBehavior = BehaviorLogics.Stand,
        string rightBehavior = BehaviorLogics.Stand,
        bool leftRetreat = false,
        DroneCommand? leftDrones = null,
        IReadOnlyList<DroneState>? drones = null,
        IReadOnlyList<FieldObject>? circles = null,
        ulong seed = 1,
        int cycle = 0,
        double width = 20000,
        double height = 12000)
    {
        SquadronState[] squadrons =
        [
            new(left.SquadronId, left.SideId, "a", [left.ShipId], Order(leftBehavior, leftRetreat, leftDrones ?? default)),
            new(right.SquadronId, right.SideId, "b", [right.ShipId], Order(rightBehavior)),
        ];

        return new BattleState(
            1,
            cycle,
            seed,
            new FieldState(width, height, circles ?? []),
            [new SideState(left.SideId, true), new SideState(right.SideId, false)],
            squadrons,
            [left, right],
            drones ?? [],
            []);
    }

    public static BattleState Solo(
        ShipState ship,
        bool retreat = false,
        IReadOnlyList<DroneState>? drones = null,
        ulong seed = 1,
        int cycle = 0,
        double width = 20000,
        double height = 12000)
    {
        return new BattleState(
            1,
            cycle,
            seed,
            new FieldState(width, height, []),
            [new SideState(ship.SideId, true)],
            [new SquadronState(ship.SquadronId, ship.SideId, "a", [ship.ShipId], Order(retreat: retreat))],
            [ship],
            drones ?? [],
            []);
    }

    public static SegmentResult Run(BattleState state, CombatSettings? settings = null, int seconds = 1)
    {
        return SegmentSimulator.Run(state, new SegmentInput(seconds), settings ?? Settings());
    }

    public static ShipState Find(SegmentResult finished, long shipId)
    {
        for (int i = 0; i < finished.NewState.Ships.Count; i++)
        {
            if (finished.NewState.Ships[i].ShipId == shipId)
            {
                return finished.NewState.Ships[i];
            }
        }

        throw new InvalidOperationException($"The ship {shipId} is missing.");
    }

    public static int Shots(SegmentResult finished) => Sum(finished, static entry => entry.Shots);

    public static int Hits(SegmentResult finished) => Sum(finished, static entry => entry.Hits);

    public static bool Logged(SegmentResult finished, string type)
    {
        for (int i = 0; i < finished.Log.Count; i++)
        {
            if (string.Equals(finished.Log[i].Type, type, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int Sum(SegmentResult finished, Func<BattleLogEntry, int> pick)
    {
        int total = 0;

        for (int i = 0; i < finished.Log.Count; i++)
        {
            if (string.Equals(finished.Log[i].Type, LogTypes.FireSummary, StringComparison.Ordinal))
            {
                total += pick(finished.Log[i]);
            }
        }

        return total;
    }
}

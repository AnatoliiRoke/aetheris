using Aetheris.Combat.Engine;

namespace Aetheris.CombatSim;

/// <summary>How much of a squadron's fire belongs to one weapon part.</summary>
internal sealed class WeaponShare
{
    public required string Part { get; init; }

    public required string Name { get; init; }

    public double Cost { get; init; }

    public int Copies { get; init; }

    public double SalvoWeight { get; init; }
}

/// <summary>The state that enters the engine, plus the weapon shares used by the report.</summary>
internal sealed class BuiltEngagement
{
    public required BattleState State { get; init; }

    public required IReadOnlyDictionary<int, IReadOnlyList<WeaponShare>> Weapons { get; init; }

    public required OpeningPicture Opening { get; init; }
}

/// <summary>Distances and detection at the first tick, before anyone moves.</summary>
internal readonly record struct OpeningPicture(
    int Pairs,
    int Seen,
    double MaxDistance,
    double MinReach,
    double MaxRangeA,
    double MaxRangeB);

/// <summary>Places fleets on the field. The engine then owns the movement.</summary>
internal static class EngagementBuilder
{
    private const long FirstId = 1;

    private static readonly DroneCommand HoldDrones = new(DroneCommandLogics.Hold, null);

    public static BuiltEngagement Build(
        DesignCatalog catalog,
        FleetFile fleetA,
        FleetFile fleetB,
        string behaviorA,
        string behaviorB)
    {
        long nextShipId = FirstId;
        long nextModuleId = FirstId;
        List<ShipState> ships = [];
        Dictionary<int, Dictionary<string, MutableShare>> weapons = [];
        List<long> idsA = [];
        List<long> idsB = [];

        Place(
            catalog,
            fleetA,
            catalog.Layout.SideA,
            catalog.Layout.SquadronA,
            ships,
            idsA,
            weapons,
            ref nextShipId,
            ref nextModuleId);
        Place(
            catalog,
            fleetB,
            catalog.Layout.SideB,
            catalog.Layout.SquadronB,
            ships,
            idsB,
            weapons,
            ref nextShipId,
            ref nextModuleId);
        ApplyOffsets(ships, catalog.Layout.SideA);
        ApplyOffsets(ships, catalog.Layout.SideB);
        ships.Sort(static (left, right) => left.ShipId.CompareTo(right.ShipId));

        SquadronState[] squadrons =
        [
            Squadron(catalog.Layout.SquadronA, catalog.Layout.SideA, "A", idsA, behaviorA, catalog.TargetRule),
            Squadron(catalog.Layout.SquadronB, catalog.Layout.SideB, "B", idsB, behaviorB, catalog.TargetRule),
        ];
        Array.Sort(squadrons, static (left, right) => left.SquadronId.CompareTo(right.SquadronId));

        BattleState state = new(
            BattleId: FirstId,
            Cycle: 0,
            Seed: 0,
            Field: new FieldState(catalog.Settings.FieldWidth, catalog.Settings.FieldHeight, []),
            Sides: Sides(catalog.Layout),
            Squadrons: squadrons,
            Ships: ships,
            Drones: [],
            Debris: []);

        return new BuiltEngagement
        {
            State = state,
            Weapons = Freeze(weapons),
            Opening = MeasureOpening(ships, catalog.Layout.SideA, catalog.Layout.SideB),
        };
    }

    private static void Place(
        DesignCatalog catalog,
        FleetFile fleet,
        int sideId,
        int squadronId,
        List<ShipState> ships,
        List<long> shipIds,
        Dictionary<int, Dictionary<string, MutableShare>> weapons,
        ref long nextShipId,
        ref long nextModuleId)
    {
        int slot = 0;

        for (int groupIndex = 0; groupIndex < fleet.Groups.Count; groupIndex++)
        {
            FleetGroup group = fleet.Groups[groupIndex];
            ShipDesign design = catalog.Require(group.DesignId);

            for (int copy = 0; copy < group.Count; copy++)
            {
                long shipId = nextShipId;
                nextShipId++;
                shipIds.Add(shipId);
                double x = fleet.Formation.OriginX + (slot * fleet.Formation.SpacingX);
                double y = fleet.Formation.OriginY + (slot * fleet.Formation.SpacingY);
                slot++;
                ModuleState[] modules = new ModuleState[design.Modules.Count];

                for (int moduleIndex = 0; moduleIndex < design.Modules.Count; moduleIndex++)
                {
                    FittedModule module = design.Modules[moduleIndex];
                    long moduleId = nextModuleId;
                    nextModuleId++;
                    modules[moduleIndex] = new ModuleState(
                        moduleId,
                        module.PartTypeId,
                        module.Kind,
                        module.Integrity,
                        module.Integrity,
                        module.Priority,
                        module.Mass,
                        true,
                        0,
                        module.Facts,
                        module.Accuracy);
                    AddWeapon(weapons, squadronId, module);
                }

                ships.Add(new ShipState(
                    shipId,
                    sideId,
                    squadronId,
                    design.HullGroupId,
                    new Vec3(x, y, 0),
                    default,
                    0,
                    design.Hull,
                    design.HullMax,
                    design.Armor,
                    design.ArmorMax,
                    design.ShieldNow,
                    design.ShieldCapacity,
                    design.Signature,
                    design.SignatureCut,
                    modules,
                    new Dictionary<int, int>(),
                    new Dictionary<int, int>(),
                    false,
                    false,
                    LoadFactor: design.LoadFactor,
                    EnergyBlock: design.EnergyBlock,
                    KineticBlock: design.KineticBlock,
                    ArmorShare: design.ArmorShare,
                    Cost: design.Cost));
            }
        }
    }

    private static void AddWeapon(
        Dictionary<int, Dictionary<string, MutableShare>> weapons,
        int squadronId,
        FittedModule module)
    {
        if (module.Kind != ModuleKinds.Weapon)
        {
            return;
        }

        if (!weapons.TryGetValue(squadronId, out Dictionary<string, MutableShare>? shares))
        {
            shares = new Dictionary<string, MutableShare>(StringComparer.Ordinal);
            weapons[squadronId] = shares;
        }

        if (!shares.TryGetValue(module.Part, out MutableShare? share))
        {
            double perCopy = module.Facts.Rate > 0 ? module.Facts.Damage * module.Facts.Rate : module.Facts.Damage;
            share = new MutableShare
            {
                Part = module.Part,
                Name = module.Name,
                Cost = module.Cost,
                WeightPerCopy = perCopy,
            };
            shares[module.Part] = share;
        }

        share.Copies++;
    }

    private static Dictionary<int, IReadOnlyList<WeaponShare>> Freeze(
        Dictionary<int, Dictionary<string, MutableShare>> source)
    {
        Dictionary<int, IReadOnlyList<WeaponShare>> frozen = new(source.Count);

        foreach (KeyValuePair<int, Dictionary<string, MutableShare>> pair in source)
        {
            WeaponShare[] shares = new WeaponShare[pair.Value.Count];
            int index = 0;

            foreach (MutableShare share in pair.Value.Values)
            {
                shares[index] = new WeaponShare
                {
                    Part = share.Part,
                    Name = share.Name,
                    Cost = share.Cost,
                    Copies = share.Copies,
                    SalvoWeight = share.WeightPerCopy * share.Copies,
                };
                index++;
            }

            Array.Sort(shares, static (left, right) => string.CompareOrdinal(left.Part, right.Part));
            frozen[pair.Key] = shares;
        }

        return frozen;
    }

    private static void ApplyOffsets(List<ShipState> ships, int sideId)
    {
        double sumX = 0;
        double sumY = 0;
        int living = 0;

        for (int i = 0; i < ships.Count; i++)
        {
            if (ships[i].SideId != sideId)
            {
                continue;
            }

            sumX += ships[i].Position.X;
            sumY += ships[i].Position.Y;
            living++;
        }

        if (living == 0)
        {
            return;
        }

        double centerX = sumX / living;
        double centerY = sumY / living;

        for (int i = 0; i < ships.Count; i++)
        {
            ShipState ship = ships[i];

            if (ship.SideId != sideId)
            {
                continue;
            }

            ships[i] = ship with
            {
                FormationOffset = new Vec3(ship.Position.X - centerX, ship.Position.Y - centerY, 0),
            };
        }
    }

    private static SideState[] Sides(SideLayout layout)
    {
        SideState attacker = new(layout.SideA, true);
        SideState defender = new(layout.SideB, false);

        if (layout.SideA < layout.SideB)
        {
            return [attacker, defender];
        }

        return [defender, attacker];
    }

    private static SquadronState Squadron(
        int squadronId,
        int sideId,
        string name,
        List<long> shipIds,
        string behavior,
        string targetRule)
    {
        return new SquadronState(
            squadronId,
            sideId,
            name,
            shipIds,
            new SquadronOrder(
                [],
                behavior,
                targetRule,
                null,
                null,
                HoldDrones,
                false,
                null));
    }

    private static OpeningPicture MeasureOpening(IReadOnlyList<ShipState> ships, int sideA, int sideB)
    {
        int pairs = 0;
        int seen = 0;
        double maxDistance = 0;
        double minReach = double.PositiveInfinity;
        double maxRangeA = 0;
        double maxRangeB = 0;

        for (int i = 0; i < ships.Count; i++)
        {
            ShipState ship = ships[i];
            double range = MaxWeaponRange(ship);

            if (ship.SideId == sideA && range > maxRangeA)
            {
                maxRangeA = range;
            }

            if (ship.SideId == sideB && range > maxRangeB)
            {
                maxRangeB = range;
            }
        }

        for (int left = 0; left < ships.Count; left++)
        {
            if (ships[left].SideId != sideA)
            {
                continue;
            }

            for (int right = 0; right < ships.Count; right++)
            {
                if (ships[right].SideId != sideB)
                {
                    continue;
                }

                pairs++;
                double distance = Math.Sqrt(Vec3.DistanceSquared(ships[left].Position, ships[right].Position));

                if (distance > maxDistance)
                {
                    maxDistance = distance;
                }

                double reach = Reach(ships[left], ships[right]);
                double other = Reach(ships[right], ships[left]);

                if (reach < minReach)
                {
                    minReach = reach;
                }

                if (other < minReach)
                {
                    minReach = other;
                }

                if (Sees(ships[left], ships[right]) && Sees(ships[right], ships[left]))
                {
                    seen++;
                }
            }
        }

        return new OpeningPicture(pairs, seen, maxDistance, minReach, maxRangeA, maxRangeB);
    }

    private static bool Sees(ShipState observer, ShipState target)
    {
        double reach = Reach(observer, target);

        return Vec3.DistanceSquared(observer.Position, target.Position) <= reach * reach;
    }

    private static double Reach(ShipState observer, ShipState target)
    {
        double factor = target.Signature * (1 - target.SignatureCut);

        if (factor < 0)
        {
            factor = 0;
        }

        return DetectionOf(observer) * factor;
    }

    private static double DetectionOf(ShipState ship)
    {
        double detect = 0;

        for (int i = 0; i < ship.Modules.Count; i++)
        {
            if (ship.Modules[i].Facts.Detection > detect)
            {
                detect = ship.Modules[i].Facts.Detection;
            }
        }

        return detect;
    }

    private static double MaxWeaponRange(ShipState ship)
    {
        double range = 0;

        for (int i = 0; i < ship.Modules.Count; i++)
        {
            ModuleState module = ship.Modules[i];

            if (module.Kind == ModuleKinds.Weapon && module.Facts.Range > range)
            {
                range = module.Facts.Range;
            }
        }

        return range;
    }

    private sealed class MutableShare
    {
        public required string Part { get; init; }

        public required string Name { get; init; }

        public double Cost { get; init; }

        public double WeightPerCopy { get; init; }

        public int Copies { get; set; }
    }
}

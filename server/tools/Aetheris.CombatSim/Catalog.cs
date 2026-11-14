using System.Text.Json;

using Aetheris.Combat.Engine;

namespace Aetheris.CombatSim;

/// <summary>The designs, the field and the side identifiers the simulator reads.</summary>
internal sealed class DesignCatalog
{
    public required string Source { get; init; }

    public required CombatSettings Settings { get; init; }

    public required SideLayout Layout { get; init; }

    public required DuelSpots Duel { get; init; }

    public required string TargetRule { get; init; }

    public required IReadOnlyList<ShipDesign> Designs { get; init; }

    public ShipDesign Require(string designId)
    {
        for (int i = 0; i < Designs.Count; i++)
        {
            if (string.Equals(Designs[i].Id, designId, StringComparison.Ordinal))
            {
                return Designs[i];
            }
        }

        throw new InvalidOperationException($"Unknown design {designId}.");
    }
}

/// <summary>The two sides and the two squadrons. The identifiers come from the catalog.</summary>
internal sealed class SideLayout
{
    public int SideA { get; init; }

    public int SideB { get; init; }

    public int SquadronA { get; init; }

    public int SquadronB { get; init; }
}

/// <summary>Where a one-against-one pair is placed.</summary>
internal sealed class DuelSpots
{
    public double OriginAX { get; init; }

    public double OriginAY { get; init; }

    public double OriginBX { get; init; }

    public double OriginBY { get; init; }
}

/// <summary>One exported design. The numbers are the seed, already resolved for the engine.</summary>
internal sealed class ShipDesign
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public double Cost { get; init; }

    public int HullGroupId { get; init; }

    public required string HullGroup { get; init; }

    public required string HullGroupName { get; init; }

    public required string HullPart { get; init; }

    public required string HullName { get; init; }

    public double Signature { get; init; }

    public double Hull { get; init; }

    public double HullMax { get; init; }

    public double Armor { get; init; }

    public double ArmorMax { get; init; }

    public double ShieldNow { get; init; }

    public double ShieldCapacity { get; init; }

    public double EnergyBlock { get; init; }

    public double KineticBlock { get; init; }

    public double ArmorShare { get; init; }

    public double LoadFactor { get; init; }

    public double SignatureCut { get; init; }

    public required IReadOnlyList<FittedModule> Modules { get; init; }
}

/// <summary>One module of a design.</summary>
internal sealed class FittedModule
{
    public required string Part { get; init; }

    public required string Name { get; init; }

    public int PartTypeId { get; init; }

    public required string Kind { get; init; }

    public double Cost { get; init; }

    public int Priority { get; init; }

    public double Mass { get; init; }

    public double Integrity { get; init; }

    public ModuleFacts Facts { get; init; }

    public required IReadOnlyList<AccuracyEntry> Accuracy { get; init; }
}

/// <summary>A fleet file: groups of one design and a flat formation.</summary>
internal sealed class FleetFile
{
    public required IReadOnlyList<FleetGroup> Groups { get; init; }

    public required FleetFormation Formation { get; init; }
}

/// <summary>How many ships of one design the fleet brings.</summary>
internal sealed class FleetGroup
{
    public required string DesignId { get; init; }

    public int Count { get; init; }
}

/// <summary>The first ship starts at the origin. The next ships step by the spacing.</summary>
internal sealed class FleetFormation
{
    public double OriginX { get; init; }

    public double OriginY { get; init; }

    public double SpacingX { get; init; }

    public double SpacingY { get; init; }
}

/// <summary>Reads the numbers of the catalog without substituting a balance value.</summary>
internal static class JsonReads
{
    public static JsonElement RequireObject(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Field {name} is missing.");
        }

        return property;
    }

    public static JsonElement RequireArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Field {name} is missing.");
        }

        return property;
    }

    public static string RequireString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Field {name} is missing.");
        }

        string text = property.GetString() ?? "";

        if (text.Length == 0)
        {
            throw new InvalidOperationException($"Field {name} is empty.");
        }

        return text;
    }

    public static string OptionalString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return "";
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Field {name} is not text.");
        }

        return property.GetString() ?? "";
    }

    public static double RequireDouble(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property))
        {
            throw new InvalidOperationException($"Field {name} is missing.");
        }

        return AsDouble(property, name);
    }

    public static double OptionalDouble(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property))
        {
            return 0;
        }

        return AsDouble(property, name);
    }

    public static int RequireInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property))
        {
            throw new InvalidOperationException($"Field {name} is missing.");
        }

        return AsInt(property, name);
    }

    public static int OptionalInt(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out JsonElement property))
        {
            return 0;
        }

        return AsInt(property, name);
    }

    private static double AsDouble(JsonElement property, string name)
    {
        if (property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Field {name} is not a number.");
        }

        return property.GetDouble();
    }

    private static int AsInt(JsonElement property, string name)
    {
        double number = AsDouble(property, name);

        if (!double.IsFinite(number) || number != Math.Truncate(number) || number > int.MaxValue || number < int.MinValue)
        {
            throw new InvalidOperationException($"Field {name} is not an integer.");
        }

        return (int)number;
    }
}

/// <summary>Loads <c>designs.json</c>.</summary>
internal static class CatalogReader
{
    public static DesignCatalog Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        SideLayout layout = ReadLayout(JsonReads.RequireObject(root, "layout"));
        string targetRule = JsonReads.RequireString(root, "targetRule");
        KnownTarget(targetRule);
        ShipDesign[] designs = ReadDesigns(JsonReads.RequireArray(root, "designs"));

        if (designs.Length == 0)
        {
            throw new InvalidOperationException("The catalog has no designs.");
        }

        return new DesignCatalog
        {
            Source = JsonReads.OptionalString(root, "source"),
            Settings = ReadSettings(JsonReads.RequireObject(root, "settings")),
            Layout = layout,
            Duel = ReadDuel(JsonReads.RequireObject(root, "duel")),
            TargetRule = targetRule,
            Designs = designs,
        };
    }

    private static CombatSettings ReadSettings(JsonElement settings)
    {
        return new CombatSettings(
            deploySeconds: JsonReads.RequireInt(settings, "deploySeconds"),
            pauseSeconds: JsonReads.RequireInt(settings, "pauseSeconds"),
            segmentSeconds: JsonReads.RequireInt(settings, "segmentSeconds"),
            tickMilliseconds: JsonReads.RequireInt(settings, "tickMilliseconds"),
            trackStepSeconds: JsonReads.RequireInt(settings, "trackStepSeconds"),
            maxCycles: JsonReads.RequireInt(settings, "maxCycles"),
            shieldRestoreShare: JsonReads.RequireDouble(settings, "shieldRestoreShare"),
            moduleDamageShare: JsonReads.RequireDouble(settings, "moduleDamageShare"),
            fieldWidth: JsonReads.RequireInt(settings, "fieldWidth"),
            fieldHeight: JsonReads.RequireInt(settings, "fieldHeight"),
            deployZoneDepth: JsonReads.RequireInt(settings, "deployZoneDepth"),
            asteroidDetectionMultiplier: JsonReads.RequireDouble(settings, "asteroidDetectionMultiplier"),
            asteroidHitMultiplier: JsonReads.RequireDouble(settings, "asteroidHitMultiplier"),
            asteroidSpeedMultiplier: JsonReads.RequireDouble(settings, "asteroidSpeedMultiplier"),
            accuracyDefault: JsonReads.RequireDouble(settings, "accuracyDefault"),
            distanceFalloff: JsonReads.RequireDouble(settings, "distanceFalloff"),
            targetSpeedFalloff: JsonReads.RequireDouble(settings, "targetSpeedFalloff"),
            squadronMax: JsonReads.RequireInt(settings, "squadronMax"),
            formationMax: JsonReads.RequireInt(settings, "formationMax"),
            debrisShare: JsonReads.RequireDouble(settings, "debrisShare"),
            commanderExperience: JsonReads.RequireInt(settings, "commanderExperience"),
            orderIntervalSeconds: JsonReads.RequireInt(settings, "orderIntervalSeconds"),
            scenarioRuleMax: JsonReads.RequireInt(settings, "scenarioRuleMax"),
            forceDamageWeight: JsonReads.RequireDouble(settings, "forceDamageWeight"),
            forceDefenseWeight: JsonReads.RequireDouble(settings, "forceDefenseWeight"));
    }

    private static SideLayout ReadLayout(JsonElement layout)
    {
        SideLayout sides = new()
        {
            SideA = JsonReads.RequireInt(layout, "sideA"),
            SideB = JsonReads.RequireInt(layout, "sideB"),
            SquadronA = JsonReads.RequireInt(layout, "squadronA"),
            SquadronB = JsonReads.RequireInt(layout, "squadronB"),
        };

        if (sides.SideA == sides.SideB)
        {
            throw new InvalidOperationException("The sides share an identifier.");
        }

        if (sides.SquadronA == sides.SquadronB)
        {
            throw new InvalidOperationException("The squadrons share an identifier.");
        }

        return sides;
    }

    private static DuelSpots ReadDuel(JsonElement duel)
    {
        return new DuelSpots
        {
            OriginAX = JsonReads.RequireDouble(duel, "originAX"),
            OriginAY = JsonReads.RequireDouble(duel, "originAY"),
            OriginBX = JsonReads.RequireDouble(duel, "originBX"),
            OriginBY = JsonReads.RequireDouble(duel, "originBY"),
        };
    }

    private static ShipDesign[] ReadDesigns(JsonElement designs)
    {
        List<ShipDesign> loaded = [];
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (JsonElement design in designs.EnumerateArray())
        {
            string designId = JsonReads.RequireString(design, "id");

            if (!seen.Add(designId))
            {
                throw new InvalidOperationException($"Repeated design {designId}.");
            }

            loaded.Add(new ShipDesign
            {
                Id = designId,
                Name = JsonReads.RequireString(design, "name"),
                Cost = JsonReads.RequireDouble(design, "cost"),
                HullGroupId = JsonReads.RequireInt(design, "hullGroupId"),
                HullGroup = JsonReads.RequireString(design, "hullGroup"),
                HullGroupName = JsonReads.RequireString(design, "hullGroupName"),
                HullPart = JsonReads.RequireString(design, "hullPart"),
                HullName = JsonReads.RequireString(design, "hullName"),
                Signature = JsonReads.RequireDouble(design, "signature"),
                Hull = JsonReads.RequireDouble(design, "hull"),
                HullMax = JsonReads.RequireDouble(design, "hullMax"),
                Armor = JsonReads.RequireDouble(design, "armor"),
                ArmorMax = JsonReads.RequireDouble(design, "armorMax"),
                ShieldNow = JsonReads.RequireDouble(design, "shieldNow"),
                ShieldCapacity = JsonReads.RequireDouble(design, "shieldCapacity"),
                EnergyBlock = JsonReads.RequireDouble(design, "energyBlock"),
                KineticBlock = JsonReads.RequireDouble(design, "kineticBlock"),
                ArmorShare = JsonReads.RequireDouble(design, "armorShare"),
                LoadFactor = JsonReads.RequireDouble(design, "loadFactor"),
                SignatureCut = JsonReads.RequireDouble(design, "signatureCut"),
                Modules = ReadModules(JsonReads.RequireArray(design, "modules")),
            });
        }

        return loaded.ToArray();
    }

    private static FittedModule[] ReadModules(JsonElement modules)
    {
        List<FittedModule> loaded = [];

        foreach (JsonElement module in modules.EnumerateArray())
        {
            string kind = JsonReads.RequireString(module, "kind");
            KnownKind(kind);
            loaded.Add(new FittedModule
            {
                Part = JsonReads.RequireString(module, "part"),
                Name = JsonReads.RequireString(module, "name"),
                PartTypeId = JsonReads.RequireInt(module, "partTypeId"),
                Kind = kind,
                Cost = JsonReads.RequireDouble(module, "cost"),
                Priority = JsonReads.RequireInt(module, "priority"),
                Mass = JsonReads.RequireDouble(module, "mass"),
                Integrity = JsonReads.RequireDouble(module, "integrity"),
                Facts = ReadFacts(module),
                Accuracy = ReadAccuracy(module),
            });
        }

        return loaded.ToArray();
    }

    private static ModuleFacts ReadFacts(JsonElement module)
    {
        JsonElement facts = JsonReads.RequireObject(module, "facts");

        return new ModuleFacts(
            EnergyUse: JsonReads.OptionalDouble(facts, "energyUse"),
            Power: JsonReads.OptionalDouble(facts, "power"),
            Speed: JsonReads.OptionalDouble(facts, "speed"),
            Detection: JsonReads.OptionalDouble(facts, "detection"),
            Damage: JsonReads.OptionalDouble(facts, "damage"),
            Rate: JsonReads.OptionalDouble(facts, "rate"),
            Range: JsonReads.OptionalDouble(facts, "range"),
            FlightSpeed: JsonReads.OptionalDouble(facts, "flightSpeed"),
            DamageKind: JsonReads.OptionalString(facts, "damageKind"),
            AmmoPartTypeId: JsonReads.OptionalInt(facts, "ammoPartTypeId"),
            EnergyBlock: JsonReads.OptionalDouble(facts, "energyBlock"),
            KineticBlock: JsonReads.OptionalDouble(facts, "kineticBlock"),
            ShieldCapacity: JsonReads.OptionalDouble(facts, "shieldCapacity"),
            ShieldRestore: JsonReads.OptionalDouble(facts, "shieldRestore"),
            Repair: JsonReads.OptionalDouble(facts, "repair"),
            AntiMissileChance: JsonReads.OptionalDouble(facts, "antiMissileChance"),
            Missiles: JsonReads.OptionalDouble(facts, "missiles"),
            LauncherSize: JsonReads.OptionalDouble(facts, "launcherSize"),
            Magazine: JsonReads.OptionalDouble(facts, "magazine"));
    }

    private static AccuracyEntry[] ReadAccuracy(JsonElement module)
    {
        if (!module.TryGetProperty("accuracy", out JsonElement accuracy))
        {
            return [];
        }

        if (accuracy.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Field accuracy is not a list.");
        }

        List<AccuracyEntry> rows = [];

        foreach (JsonElement row in accuracy.EnumerateArray())
        {
            rows.Add(new AccuracyEntry(
                JsonReads.RequireInt(row, "hullGroupId"),
                JsonReads.RequireDouble(row, "chance")));
        }

        return rows.ToArray();
    }

    private static void KnownTarget(string logic)
    {
        if (logic != TargetRuleLogics.Nearest
            && logic != TargetRuleLogics.Weakest
            && logic != TargetRuleLogics.Strongest
            && logic != TargetRuleLogics.Carriers
            && logic != TargetRuleLogics.Drones)
        {
            throw new InvalidOperationException($"Unknown target rule {logic}.");
        }
    }

    private static void KnownKind(string kind)
    {
        if (kind != ModuleKinds.Engine
            && kind != ModuleKinds.Reactor
            && kind != ModuleKinds.Weapon
            && kind != ModuleKinds.Shield
            && kind != ModuleKinds.Armor
            && kind != ModuleKinds.Radar
            && kind != ModuleKinds.AntiMissile
            && kind != ModuleKinds.ShieldRestorer
            && kind != ModuleKinds.Cargo
            && kind != ModuleKinds.Other)
        {
            throw new InvalidOperationException($"Unknown module kind {kind}.");
        }
    }
}

/// <summary>Loads a fleet file.</summary>
internal static class FleetReader
{
    public static FleetFile Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        FleetFile fleet = new()
        {
            Groups = ReadGroups(JsonReads.RequireArray(root, "ships")),
            Formation = ReadFormation(JsonReads.RequireObject(root, "formation")),
        };

        if (fleet.Groups.Count == 0)
        {
            throw new InvalidOperationException($"The fleet {path} has no ships.");
        }

        return fleet;
    }

    public static FleetFile Single(string designId, double originX, double originY)
    {
        return new FleetFile
        {
            Groups = [new FleetGroup { DesignId = designId, Count = 1 }],
            Formation = new FleetFormation
            {
                OriginX = originX,
                OriginY = originY,
                SpacingX = 0,
                SpacingY = 0,
            },
        };
    }

    private static FleetGroup[] ReadGroups(JsonElement ships)
    {
        List<FleetGroup> groups = [];

        foreach (JsonElement ship in ships.EnumerateArray())
        {
            int count = JsonReads.RequireInt(ship, "count");

            if (count <= 0)
            {
                throw new InvalidOperationException("A fleet group needs a positive count.");
            }

            groups.Add(new FleetGroup
            {
                DesignId = JsonReads.RequireString(ship, "designId"),
                Count = count,
            });
        }

        return groups.ToArray();
    }

    private static FleetFormation ReadFormation(JsonElement formation)
    {
        return new FleetFormation
        {
            OriginX = JsonReads.RequireDouble(formation, "originX"),
            OriginY = JsonReads.RequireDouble(formation, "originY"),
            SpacingX = JsonReads.RequireDouble(formation, "spacingX"),
            SpacingY = JsonReads.RequireDouble(formation, "spacingY"),
        };
    }
}

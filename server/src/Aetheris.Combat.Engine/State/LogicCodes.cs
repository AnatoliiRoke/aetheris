namespace Aetheris.Combat.Engine;

/// <summary>How a squadron moves. The values are the logical codes of the directory.</summary>
public static class BehaviorLogics
{
    /// <summary>Move toward the nearest visible enemy until the weapons reach.</summary>
    public const string Approach = "approach";

    /// <summary>Hold a distance of four fifths of the longest weapon, then fall back.</summary>
    public const string KeepDistance = "keep_distance";

    /// <summary>Stay in place.</summary>
    public const string Stand = "stand";

    /// <summary>Follow another squadron.</summary>
    public const string Escort = "escort";
}

/// <summary>How a weapon picks a target. The values are the logical codes of the directory.</summary>
public static class TargetRuleLogics
{
    /// <summary>The nearest target.</summary>
    public const string Nearest = "nearest";

    /// <summary>The target with the least hull, armor and shield.</summary>
    public const string Weakest = "weakest";

    /// <summary>The target with the most hull, armor and shield.</summary>
    public const string Strongest = "strongest";

    /// <summary>A ship that carries drones, then the nearest.</summary>
    public const string Carriers = "carriers";

    /// <summary>A drone, then the nearest ship.</summary>
    public const string Drones = "drones";
}

/// <summary>What a module is, for the formulas that read it.</summary>
public static class ModuleKinds
{
    /// <summary>A drive. Its speed fact is summed with the others.</summary>
    public const string Engine = "engine";

    /// <summary>A reactor. It supplies power while it is on.</summary>
    public const string Reactor = "reactor";

    /// <summary>A weapon.</summary>
    public const string Weapon = "weapon";

    /// <summary>A shield generator. It holds the saturation and the blocks.</summary>
    public const string Shield = "shield";

    /// <summary>An armor plate. Its integrity is the armor pool.</summary>
    public const string Armor = "armor";

    /// <summary>A detector.</summary>
    public const string Radar = "radar";

    /// <summary>A system that can stop a rocket.</summary>
    public const string AntiMissile = "anti_missile";

    /// <summary>A module that restores shield saturation at the end of a cycle.</summary>
    public const string ShieldRestorer = "shield_restorer";

    /// <summary>A hold. Destroying it clears what the hold stores.</summary>
    public const string Cargo = "cargo";

    /// <summary>A module the formulas do not single out.</summary>
    public const string Other = "other";
}

/// <summary>The kind of a hit. Thermal and ion are read as energy. An unknown kind is kinetic.</summary>
public static class DamageKinds
{
    /// <summary>A beam. It uses the energy block and is not stopped by an anti-rocket system.</summary>
    public const string Energy = "energy";

    /// <summary>A shell. It uses the kinetic block and is not stopped unless it is a rocket.</summary>
    public const string Kinetic = "kinetic";

    /// <summary>A rocket. It uses the kinetic block and can be stopped.</summary>
    public const string Missile = "missile";

    /// <summary>A torpedo. It uses the kinetic block and can be stopped.</summary>
    public const string Torpedo = "torpedo";

    /// <summary>An explosive rocket. It uses the kinetic block and can be stopped.</summary>
    public const string Explosive = "explosive";

    /// <summary>Thermal damage. It is treated as energy.</summary>
    public const string Thermal = "thermal";

    /// <summary>Ion damage. It is treated as energy.</summary>
    public const string Ion = "ion";
}

/// <summary>What stands on the field.</summary>
public static class FieldObjectKinds
{
    /// <summary>A circle that blocks movement and the line of sight.</summary>
    public const string Planet = "planet";

    /// <summary>A circle that slows ships and cuts detection and hits.</summary>
    public const string AsteroidField = "asteroid_field";
}

/// <summary>An order for the drones of a squadron.</summary>
public static class DroneCommandLogics
{
    /// <summary>Launch the combat drones that are still in the hold.</summary>
    public const string Launch = "launch";

    /// <summary>Put the launched combat drones back into the hold.</summary>
    public const string Recall = "recall";

    /// <summary>Leave the drones as they are.</summary>
    public const string Hold = "hold";
}

/// <summary>A line of the segment log.</summary>
public static class LogTypes
{
    /// <summary>Shots, hits and damage from one squadron into another, for the whole segment.</summary>
    public const string FireSummary = "fire_summary";

    /// <summary>A ship was destroyed.</summary>
    public const string ShipDestroyed = "ship_destroyed";

    /// <summary>A module was destroyed.</summary>
    public const string ModuleDestroyed = "module_destroyed";

    /// <summary>A drone was destroyed.</summary>
    public const string DroneDestroyed = "drone_destroyed";

    /// <summary>Every living ship of a squadron has left the field.</summary>
    public const string SquadronLeft = "squadron_left";

    /// <summary>A shield fell from above zero to zero.</summary>
    public const string ShieldDown = "shield_down";

    /// <summary>Debris was picked up. The collection itself belongs to a later task, so the engine does not emit this line.</summary>
    public const string DebrisCollected = "debris_collected";
}

/// <summary>How a segment ended.</summary>
public static class OutcomeKinds
{
    /// <summary>Both sides still have a ship on the field and the cycle limit is not reached.</summary>
    public const string Continue = "continue";

    /// <summary>Only one side still has a ship on the field.</summary>
    public const string SideWon = "side_won";

    /// <summary>Nobody is left, or the cycle limit was reached with both sides still present.</summary>
    public const string Draw = "draw";
}

/// <summary>What a stack in a hold is.</summary>
public static class HoldKinds
{
    /// <summary>Rounds for a launcher. A stack with no mass does not fill the hold.</summary>
    public const string Ammo = "ammo";

    /// <summary>Drones. They fill the hold by their mass.</summary>
    public const string Drone = "drone";

    /// <summary>Other cargo. It fills the hold by its mass.</summary>
    public const string Cargo = "cargo";
}

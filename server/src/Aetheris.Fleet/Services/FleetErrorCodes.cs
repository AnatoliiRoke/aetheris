namespace Aetheris.Fleet.Services;

/// <summary>
/// The error and reason codes of the fleet module, sent to the client as localisation keys
/// (task 00038).
/// </summary>
public static class FleetErrorCodes
{
    /// <summary>The project does not exist or does not belong to the player.</summary>
    public const string DesignUnknown = "fleet_design_unknown";

    /// <summary>The project is archived, so it is no longer changed.</summary>
    public const string DesignArchived = "fleet_design_archived";

    /// <summary>The body is not a colony of the player.</summary>
    public const string PlanetUnknown = "fleet_planet_unknown";

    /// <summary>The name of the project is empty or too long.</summary>
    public const string NameInvalid = "fleet_name_invalid";

    /// <summary>The player already has as many projects as the design bureau allows.</summary>
    public const string DesignLimitReached = "fleet_design_limit_reached";

    /// <summary>The hull does not exist in the catalog.</summary>
    public const string HullUnknown = "fleet_hull_unknown";

    /// <summary>The hull is a drone, so no ship is built of it.</summary>
    public const string HullIsDrone = "fleet_hull_is_drone";

    /// <summary>The hull is archived, so no new project is made of it.</summary>
    public const string HullArchived = "fleet_hull_archived";

    /// <summary>The part does not exist in the catalog.</summary>
    public const string ModuleUnknown = "fleet_module_unknown";

    /// <summary>The slots of the ship are over.</summary>
    public const string SlotsFull = "fleet_slots_full";

    /// <summary>The project has no drive.</summary>
    public const string DriveMissing = "fleet_drive_missing";

    /// <summary>The project has no reactor.</summary>
    public const string ReactorMissing = "fleet_reactor_missing";

    /// <summary>The modules of the project take more energy than the reactors give.</summary>
    public const string EnergyShort = "fleet_energy_short";

    /// <summary>The part does not go on this hull: it is of another role.</summary>
    public const string RoleMismatch = "fleet_role_mismatch";

    /// <summary>The part does not fit this hull.</summary>
    public const string NotCompatible = "fleet_not_compatible";

    /// <summary>The hold of the hull does not take the least set of ammunition.</summary>
    public const string HoldTooSmall = "fleet_hold_too_small";

    /// <summary>The number of the slot is not a slot of the hull.</summary>
    public const string SlotUnknown = "fleet_slot_unknown";

    /// <summary>The hull gives no extra slot to the race of the player.</summary>
    public const string RaceSlotUnavailable = "fleet_race_slot_unavailable";
}

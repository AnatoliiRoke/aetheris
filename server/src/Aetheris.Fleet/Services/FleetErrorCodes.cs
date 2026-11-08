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

    /// <summary>The body has no shipyard.</summary>
    public const string ShipyardMissing = "fleet_shipyard_missing";

    /// <summary>The building is not the shipyard of this body.</summary>
    public const string ShipyardMismatch = "fleet_shipyard_mismatch";

    /// <summary>The queue of the shipyard has no free place.</summary>
    public const string ShipyardQueueFull = "fleet_shipyard_queue_full";

    /// <summary>The record of the queue does not exist.</summary>
    public const string ShipyardOrderUnknown = "fleet_shipyard_order_unknown";

    /// <summary>The ship does not exist.</summary>
    public const string ShipUnknown = "fleet_ship_unknown";

    /// <summary>The body lacks the parts of the exact stacks the project names.</summary>
    public const string PartMissing = "fleet_part_missing";

    /// <summary>The body has fewer inhabitants than the crew of the ship.</summary>
    public const string CrewShort = "fleet_crew_short";

    /// <summary>The body or the civilization lacks the resources of the assembly.</summary>
    public const string ResourcesShort = "fleet_resources_short";

    /// <summary>The hull has no assembly time in the directory.</summary>
    public const string AssemblyTimeMissing = "fleet_assembly_time_missing";

    /// <summary>A finished ship of the project exists, so the project is not changed or archived.</summary>
    public const string DesignBuilt = "fleet_design_built";

    /// <summary>The batch does not hold a positive number of ships, or that number does not fit.</summary>
    public const string QuantityInvalid = "fleet_quantity_invalid";

    /// <summary>The fleet does not exist or does not belong to the player (task 00040).</summary>
    public const string FleetUnknown = "fleet_fleet_unknown";

    /// <summary>The player already has as many fleets as the spaceports allow (task 00040).</summary>
    public const string FleetLimitReached = "fleet_fleet_limit_reached";

    /// <summary>The request names no ship, or a fleet would be left without one (task 00040).</summary>
    public const string FleetNeedsShip = "fleet_fleet_needs_ship";

    /// <summary>The last ship of a fleet cannot be taken out: the fleet is disbanded instead (task 00040).</summary>
    public const string FleetLastShip = "fleet_fleet_last_ship";

    /// <summary>The ship is in a fleet already, and a ship is in one fleet at most (task 00040).</summary>
    public const string ShipInFleet = "fleet_ship_in_fleet";

    /// <summary>The ship does not stand on the body of the fleet (task 00040).</summary>
    public const string ShipElsewhere = "fleet_ship_elsewhere";

    /// <summary>The ship is not in the fleet the request names (task 00040).</summary>
    public const string ShipNotInFleet = "fleet_ship_not_in_fleet";

    /// <summary>The two fleets of a transfer do not stand on the same body, or are the same fleet (task 00040).</summary>
    public const string FleetsApart = "fleet_fleets_apart";
}

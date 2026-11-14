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

    /// <summary>The fleet has no flight plan (task 00050).</summary>
    public const string FlightPlanUnknown = "fleet_flight_plan_unknown";

    /// <summary>The fleet already has a flight plan: it has one at most (task 00050).</summary>
    public const string FlightPlanExists = "fleet_flight_plan_exists";

    /// <summary>The plan of the fleet is under way, so it is not changed nor deleted (task 00050).</summary>
    public const string FlightPlanUnderWay = "fleet_flight_plan_under_way";

    /// <summary>The plan of the fleet is not under way, so it cannot be stopped (task 00050).</summary>
    public const string FlightPlanNotUnderWay = "fleet_flight_plan_not_under_way";

    /// <summary>The plan has more orders than a plan may hold (task 00050).</summary>
    public const string FlightPlanTooManyOrders = "fleet_flight_plan_too_many_orders";

    /// <summary>The order of the plan names no type, or a type that does not exist (task 00050).</summary>
    public const string FlightOrderTypeUnknown = "fleet_flight_order_type_unknown";

    /// <summary>The order of the plan names a target that does not exist (task 00050).</summary>
    public const string FlightOrderTargetUnknown = "fleet_flight_order_target_unknown";

    /// <summary>The fleet does not stand at a body of the player, or its plan is under way, so its ships are not changed (task 00053).</summary>
    public const string FleetAway = "fleet_fleet_away";

    /// <summary>The plan has no orders, so there is nothing to carry out (task 00053).</summary>
    public const string FlightPlanEmpty = "fleet_flight_plan_empty";

    /// <summary>The plan has an order that the fleet cannot carry out yet (task 00053).</summary>
    public const string FlightOrderNotSupported = "fleet_flight_order_not_supported";

    /// <summary>The fleet has no working scanner of systems.</summary>
    public const string ScanNoModule = "fleet_scan_no_module";

    /// <summary>The target of a flight is not a place of the map any more, or lies outside its sector (task 00053).</summary>
    public const string FlightTargetUnknown = "fleet_flight_target_unknown";

    /// <summary>No ship of the fleet has a drive, so the fleet does not fly (task 00053).</summary>
    public const string FlightNoDrive = "fleet_flight_no_drive";

    /// <summary>An order of a load or an unload names nothing to carry, or names an amount that is not a number above zero (task 00057).</summary>
    public const string CargoItemsInvalid = "fleet_cargo_items_invalid";

    /// <summary>The fleet is not on a body of its owner, so it cannot load or unload (task 00057).</summary>
    public const string CargoNoPlanet = "fleet_cargo_no_planet";

    /// <summary>What the order loads weighs more than the room that is left in the holds of the fleet (task 00057).</summary>
    public const string CargoOverflow = "fleet_cargo_overflow";

    /// <summary>The body has less of a resource or of a part than the order of the load asks for (task 00057).</summary>
    public const string CargoShort = "fleet_cargo_short";

    /// <summary>
    /// The weight of a resource or a module in the data is not above zero, so the room it takes in a hold
    /// cannot be counted. It is a fault of the data, not of the order.
    /// </summary>
    public const string CargoWeightInvalid = "fleet_cargo_weight_invalid";

    /// <summary>
    /// The project has a hyperdrive or a jump drive but no ordinary drive (task 00072): such a ship
    /// may not fly inside a system, so a fleet with it may not fly at all.
    /// </summary>
    public const string FtlNeedsNormalDrive = "fleet_ftl_needs_normal_drive";

    /// <summary>
    /// The command of a hyperflight or of a jump names a target in the system the fleet stands in
    /// (task 00072): inside a system only the ordinary drives fly.
    /// </summary>
    public const string FtlSameSystem = "fleet_ftl_same_system";

    /// <summary>
    /// A ship of the fleet has no drive of the class the command needs (task 00072): a ship without
    /// a hyperdrive grounds a hyperflight and a ship without a jump drive grounds a jump.
    /// </summary>
    public const string FtlShipMissing = "fleet_ftl_ship_missing";

    /// <summary>
    /// The hold of the fleet holds less fuel than the route needs (task 00072).
    /// </summary>
    public const string FuelShort = "fleet_fuel_short";

    /// <summary>
    /// The module in the slot of the project is only a cargo and is not fitted to a hull
    /// (task 00073).
    /// </summary>
    public const string DesignPartCargoOnly = "fleet_design_part_cargo_only";

    /// <summary>
    /// The hold of the fleet holds no cargo that opens the order (task 00073): a fleet without a
    /// colony module may not colonize a planet.
    /// </summary>
    public const string ColonizeNoModule = "fleet_colonize_no_module";

    /// <summary>
    /// The fleet does not stand on a planet when the colonization begins (task 00073): the
    /// colonization names no target and works on the planet the fleet stands on.
    /// </summary>
    public const string ColonizeNotOnPlanet = "fleet_colonize_not_on_planet";

    /// <summary>
    /// The planet was taken by another player while the fleet flew to it (task 00073).
    /// </summary>
    public const string ColonizeTaken = "fleet_colonize_taken";

    /// <summary>
    /// The planet does not suit the race of the player (task 00073).
    /// </summary>
    public const string ColonizeUnsuitable = "fleet_colonize_unsuitable";

    /// <summary>
    /// The player has as many planets as the limit allows (task 00073).
    /// </summary>
    public const string ColonizeLimit = "fleet_colonize_limit";

    /// <summary>
    /// The fleet cannot fly to the planet it is to colonize (task 00073): a ship of it has no drive
    /// for the way the plan would take.
    /// </summary>
    public const string ColonizeNoDrive = "fleet_colonize_no_drive";
}

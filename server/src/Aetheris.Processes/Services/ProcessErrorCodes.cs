namespace Aetheris.Processes.Services;

/// <summary>
/// The error and reason codes of the processes module, sent to the client as localisation
/// keys (task 00014).
/// </summary>
public static class ProcessErrorCodes
{
    /// <summary>The recipe does not exist.</summary>
    public const string RecipeUnknown = "processes_recipe_unknown";

    /// <summary>The kind of the process does not exist.</summary>
    public const string TypeUnknown = "processes_type_unknown";

    /// <summary>The record of the queue does not exist.</summary>
    public const string OrderUnknown = "processes_order_unknown";

    /// <summary>The queue of the building has no free place.</summary>
    public const string QueueFull = "processes_queue_full";

    /// <summary>The body lacks the resources or the parts for the cost of the order.</summary>
    public const string ResourcesShort = "processes_resources_short";

    /// <summary>The body does not have the building the recipe needs.</summary>
    public const string BuildingMissing = "processes_building_missing";

    /// <summary>The building does not run the process of the recipe.</summary>
    public const string WrongBuilding = "processes_wrong_building";

    /// <summary>The recipe is archived, so no new order of it starts.</summary>
    public const string RecipeArchived = "processes_recipe_archived";

    /// <summary>The part is not on the body.</summary>
    public const string PartMissing = "processes_part_missing";

    /// <summary>The order is not a positive number of units.</summary>
    public const string QuantityInvalid = "processes_quantity_invalid";

    /// <summary>The body is not a colony of the player.</summary>
    public const string PlanetUnknown = "processes_planet_unknown";

    /// <summary>The body has no building of the level the recipe needs.</summary>
    public const string RequirementBuildingLevel = "processes_requirement_building_level";

    /// <summary>The player has not learned the technology the recipe needs.</summary>
    public const string RequirementTechnology = "processes_requirement_technology";

    /// <summary>The requirement of the recipe is of a kind the game does not know.</summary>
    public const string RequirementUnknown = "processes_requirement_unknown";

    /// <summary>The source of the conversion is not in the group the recipe takes.</summary>
    public const string SourceGroupMismatch = "processes_source_group_mismatch";

    /// <summary>The target of the conversion is not in the group the recipe gives.</summary>
    public const string TargetGroupMismatch = "processes_target_group_mismatch";

    /// <summary>The source and the target of the conversion are the same.</summary>
    public const string SameSourceAndTarget = "processes_same_source_and_target";

    /// <summary>There is no ship on the orbit of the body.</summary>
    public const string NoShipOnOrbit = "processes_no_ship_on_orbit";

    /// <summary>The level of the technology or of the building is too low for the process.</summary>
    public const string LevelTooLow = "processes_level_too_low";
}

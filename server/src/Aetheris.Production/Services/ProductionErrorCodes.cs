namespace Aetheris.Production.Services;

/// <summary>
/// The error and reason codes of the production module, sent to the client as localisation
/// keys (task 00013).
/// </summary>
public static class ProductionErrorCodes
{
    /// <summary>The recipe does not exist.</summary>
    public const string RecipeUnknown = "production_recipe_unknown";

    /// <summary>The record of the queue does not exist.</summary>
    public const string OrderUnknown = "production_order_unknown";

    /// <summary>The queue of the factory has no free place.</summary>
    public const string QueueFull = "production_queue_full";

    /// <summary>The body lacks the resources or the parts for the cost of the order.</summary>
    public const string ResourcesShort = "production_resources_short";

    /// <summary>The body does not have the factory the recipe needs.</summary>
    public const string FactoryMissing = "production_factory_missing";

    /// <summary>The factory does not make the recipe.</summary>
    public const string WrongFactory = "production_wrong_factory";

    /// <summary>The storage of the parts of the body is full.</summary>
    public const string StorageFull = "production_storage_full";

    /// <summary>The recipe is archived, so no new order of it starts.</summary>
    public const string RecipeArchived = "production_recipe_archived";

    /// <summary>The part is not on the body.</summary>
    public const string PartMissing = "production_part_missing";

    /// <summary>The batch is not a positive number of units.</summary>
    public const string QuantityInvalid = "production_quantity_invalid";

    /// <summary>The body is not a colony of the player.</summary>
    public const string PlanetUnknown = "production_planet_unknown";

    /// <summary>The body has no building of the level the recipe needs.</summary>
    public const string RequirementBuildingLevel = "production_requirement_building_level";

    /// <summary>The player has not learned the technology the recipe needs.</summary>
    public const string RequirementTechnology = "production_requirement_technology";

    /// <summary>Only the race of the recipe may make it.</summary>
    public const string RequirementRace = "production_requirement_race";

    /// <summary>The requirement of the recipe is of a kind the game does not know.</summary>
    public const string RequirementUnknown = "production_requirement_unknown";
}

/// <summary>
/// The kinds of the requirements of the production module (task 00013, decision 2.7).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program, so they keep their meaning when the neutral
/// codes of the rows change (documentation/tech/09, section 3.4).
/// </remarks>
public static class ProductionRequirementTypes
{
    /// <summary>The race of the player may make the recipe.</summary>
    public const string Race = "race";
}

namespace Aetheris.Planets.Services;

/// <summary>
/// The kinds of effects a building level has (task 00011, decision 10).
/// </summary>
/// <remarks>
/// These are the shapes of the formulas, not data: which building has which effect with which
/// value is a row of <c>building_level_effect</c>.
/// </remarks>
public static class BuildingEffectTypes
{
    /// <summary>Yields a metal per hour; the value is multiplied by the richness of the deposit and the metal bonus.</summary>
    public const string MetalProduction = "metal_production";

    /// <summary>Yields a crystal per hour; the value is multiplied by the richness of the deposit and the crystal bonus.</summary>
    public const string CrystalProduction = "crystal_production";

    /// <summary>Adds capacity to the storage of a resource group.</summary>
    public const string StorageCapacity = "storage_capacity";

    /// <summary>Produces energy.</summary>
    public const string EnergyOutput = "energy_output";

    /// <summary>Consumes energy.</summary>
    public const string EnergyConsumption = "energy_consumption";

    /// <summary>Raises the largest population of the body; kept for the population task.</summary>
    public const string PopulationMax = "population_max";

    /// <summary>The level of every other building of the body cannot exceed the value.</summary>
    public const string LevelCap = "level_cap";

    /// <summary>Adds room for the parts in the storage of the parts of the body (task 00013, decision 9).</summary>
    public const string PartStorageCapacity = "part_storage_capacity";

    /// <summary>
    /// Gets every effect type, in the order of the form of the admin panel (task 00023).
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        MetalProduction,
        CrystalProduction,
        StorageCapacity,
        EnergyOutput,
        EnergyConsumption,
        PopulationMax,
        LevelCap,
        PartStorageCapacity,
    ];

    /// <summary>
    /// Gets the effect types that name the resource they produce.
    /// </summary>
    public static IReadOnlySet<string> OfResource { get; } = new HashSet<string>(StringComparer.Ordinal) { MetalProduction, CrystalProduction };

    /// <summary>
    /// Gets the effect types that name the resource group they store.
    /// </summary>
    public static IReadOnlySet<string> OfResourceGroup { get; } = new HashSet<string>(StringComparer.Ordinal) { StorageCapacity };
}

/// <summary>
/// The error and reason codes of the bodies module, sent to the client as localisation keys.
/// </summary>
public static class PlanetsErrorCodes
{
    /// <summary>The body has no state in the module: it is not a colony.</summary>
    public const string PlanetUnknown = "planets_planet_unknown";

    /// <summary>The building type does not exist.</summary>
    public const string BuildingUnknown = "planets_building_unknown";

    /// <summary>The building type is archived: no new building or level of it (task 00023).</summary>
    public const string BuildingArchived = "planets_building_archived";

    /// <summary>A requirement of the building or of its level is not met.</summary>
    public const string RequirementUnmet = "planets_requirement_unmet";

    /// <summary>The building has the largest level.</summary>
    public const string LevelMax = "planets_level_max";

    /// <summary>The building table has no row for the next level.</summary>
    public const string LevelMissing = "planets_level_missing";

    /// <summary>The level would exceed the limit of the main building of the body.</summary>
    public const string LevelCapped = "planets_level_capped";

    /// <summary>Every place of the construction queue is taken.</summary>
    public const string QueueFull = "planets_queue_full";

    /// <summary>After the building is done the energy balance of the body would go below zero.</summary>
    public const string EnergyShort = "planets_energy_short";

    /// <summary>The body lacks resources for the cost.</summary>
    public const string ResourcesShort = "planets_resources_short";

    /// <summary>The queue record does not exist on the body.</summary>
    public const string QueueItemUnknown = "planets_queue_item_unknown";

    /// <summary>A building of the target type and level is required.</summary>
    public const string RequirementBuildingLevel = "planets_requirement_building_level";

    /// <summary>A technology of the target level is required.</summary>
    public const string RequirementTechnology = "planets_requirement_technology";

    /// <summary>A deposit of the target resource is required on the body.</summary>
    public const string RequirementDeposit = "planets_requirement_deposit";

    /// <summary>The requirement type is not known to the server.</summary>
    public const string RequirementUnknown = "planets_requirement_unknown";
}

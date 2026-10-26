using Aetheris.Core.Configuration;

namespace Aetheris.Planets.Configuration;

/// <summary>
/// The configuration keys of the bodies, their buildings and construction queues, with their
/// starting values (task 00011).
/// </summary>
/// <remarks>
/// The costs, times and effects of the buildings live in the building tables; these are the
/// limits around them. The host adds these definitions to its seed, so the admin panel checks
/// the bounds like for every other parameter.
/// </remarks>
public static class PlanetsConfigKeys
{
    /// <summary>The largest level of any building.</summary>
    public const string BuildingMaxLevel = "planets.building.max_level";

    /// <summary>The places of the construction queue of a body, the building under way included.</summary>
    public const string QueueBaseSlots = "planets.queue.base_slots";

    /// <summary>The largest number of paid places added to the construction queue of a body.</summary>
    public const string QueueMaxExtraSlots = "planets.queue.max_extra_slots";

    /// <summary>How long the building tables stay in the memory of the server before they are read again, in seconds.</summary>
    public const string CatalogCacheSeconds = "planets.catalog.cache_seconds";

    /// <summary>The code of the building every civilization starts with.</summary>
    public const string StartBuildingCode = "planets.start.building_code";

    /// <summary>The level of the start building.</summary>
    public const string StartBuildingLevel = "planets.start.building_level";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(BuildingMaxLevel, "20", "1", "1000", "Largest level of any building on a body."),
        new(QueueBaseSlots, "2", "1", "100", "Places of the construction queue of a body, the building under way included."),
        new(QueueMaxExtraSlots, "2", "0", "100", "Largest number of paid places added to the construction queue of a body."),
        new(CatalogCacheSeconds, "30", "0", "86400", "How long the building tables stay in memory before they are read again, in seconds."),
        new(StartBuildingCode, "admin_center", null, null, "Code of the building every new civilization starts with."),
        new(StartBuildingLevel, "1", "1", "1000", "Level of the start building."),
    ];
}

namespace Aetheris.Requirements.Services;

/// <summary>
/// The kinds of requirements of the common requirement mechanism (task 00011, decision 3;
/// task 00012, decision 3).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program, so they keep their meaning when the neutral
/// codes of the rows change (documentation/tech/09, section 3.4).
/// </remarks>
public static class RequirementTypes
{
    /// <summary>A building of the target type of at least the target level on the same body.</summary>
    public const string BuildingLevel = "building_level";

    /// <summary>A technology of the player of at least the target level.</summary>
    public const string TechnologyLevel = "technology_level";

    /// <summary>A deposit of the target resource on the body.</summary>
    public const string Deposit = "deposit";
}

/// <summary>
/// The kinds of subjects that can be closed by requirements (task 00011, decision 3).
/// </summary>
/// <remarks>
/// Building types and technologies use the mechanism now; modules, recipes and ship classes
/// bring their rows with their tasks.
/// </remarks>
public static class RequirementSubjectTypes
{
    /// <summary>A building type and its levels.</summary>
    public const string BuildingType = "building_type";

    /// <summary>A technology.</summary>
    public const string Technology = "technology";

    /// <summary>A ship module.</summary>
    public const string Module = "module";

    /// <summary>A production recipe.</summary>
    public const string Recipe = "recipe";

    /// <summary>A ship class.</summary>
    public const string ShipClass = "ship_class";
}

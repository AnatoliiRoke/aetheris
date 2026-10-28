namespace Aetheris.Planets.Services;

/// <summary>
/// The classes of the building types: what a building is for (task 00012, decision 1).
/// </summary>
/// <remarks>
/// <para>
/// The class is a logical code that the program implements, so it is readable and stable
/// (documentation/tech/09, section 3.4). It says what a building does without naming the
/// building itself: the science center is the building of the class
/// <see cref="ResearchCenter"/>, and the task that needs it asks for the class, not for a
/// code of a row.
/// </para>
/// <para>
/// The values are written to the column <c>building_type.building_class</c>. A building
/// without a special role has no class, so the column is empty.
/// </para>
/// </remarks>
public static class BuildingClasses
{
    /// <summary>A building that has no special role: it is only built and improved.</summary>
    public const string None = "";

    /// <summary>A building where the player researches technologies.</summary>
    public const string ResearchCenter = "research_center";

    /// <summary>A building where the player builds ships.</summary>
    public const string Shipyard = "shipyard";

    /// <summary>A building where the player trades with other players.</summary>
    public const string TradeCenter = "trade_center";
}

/// <summary>The classes a building type may have.</summary>
public static class BuildingClassValues
{
    /// <summary>
    /// Gets the classes a building type may have, the empty one first.
    /// </summary>
    /// <returns>The classes.</returns>
    public static IReadOnlyList<string> All { get; } =
    [
        BuildingClasses.None,
        BuildingClasses.ResearchCenter,
        BuildingClasses.Shipyard,
        BuildingClasses.TradeCenter,
    ];

    /// <summary>
    /// Checks whether a class is one of the known classes.
    /// </summary>
    /// <param name="buildingClass">The class.</param>
    /// <returns><see langword="true"/> when the class is known.</returns>
    public static bool IsKnown(string? buildingClass) =>
        buildingClass is not null && All.Contains(buildingClass, StringComparer.Ordinal);
}

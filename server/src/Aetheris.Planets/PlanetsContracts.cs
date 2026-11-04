namespace Aetheris.Planets;

/// <summary>
/// What the buildings of every body of a player give the whole civilization (task 00028).
/// </summary>
/// <param name="ResearchQueueSlots">The places the science centres add to the queue of the research.</param>
/// <param name="ResearchSpeedBonus">The share the science centres take off the time of the research.</param>
/// <param name="DesignQueueSlots">The places the design bureaus add to the queue of the designs.</param>
/// <param name="HullClassLimit">The classes of the hulls the owner may build.</param>
/// <param name="FleetLimit">The ships the spaceports of the owner allow.</param>
public sealed record OwnerBuildingBonus(
    decimal ResearchQueueSlots = 0m,
    decimal ResearchSpeedBonus = 0m,
    decimal DesignQueueSlots = 0m,
    decimal HullClassLimit = 0m,
    decimal FleetLimit = 0m)
{
    /// <summary>Gets the bonus of a player without such buildings: nothing changes.</summary>
    public static OwnerBuildingBonus Empty { get; } = new();
}

/// <summary>
/// A body of a player with the level of its building of a class (task 00012, decision 5).
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Name">The name of the body.</param>
/// <param name="Level">The level of the building, or zero when the body has none.</param>
public sealed record PlanetBuildingLevel(long PlanetId, string? Name, int Level);

/// <summary>A requirement of a subject.</summary>
/// <param name="Id">The requirement.</param>
/// <param name="SubjectType">The subject type.</param>
/// <param name="SubjectId">The subject.</param>
/// <param name="SubjectLevel">The first level of the subject it applies to.</param>
/// <param name="RequirementType">The requirement type.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The required level of the target.</param>
public sealed record RequirementInfo(
    int Id,
    string SubjectType,
    int SubjectId,
    int SubjectLevel,
    string RequirementType,
    int TargetId,
    int TargetLevel);

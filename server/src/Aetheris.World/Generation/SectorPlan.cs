using Aetheris.World.Geometry;

namespace Aetheris.World.Generation;

/// <summary>A resource with its coefficient: a deposit or a share of a composition.</summary>
/// <param name="ResourceId">The resource.</param>
/// <param name="Coefficient">The coefficient.</param>
public sealed record ResourceShare(int ResourceId, decimal Coefficient);

/// <summary>A body on an orbit.</summary>
/// <param name="Orbit">The orbit.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="Name">The name.</param>
/// <param name="SizeClassId">The size class.</param>
/// <param name="Size">The size.</param>
/// <param name="DepositIds">The deposits.</param>
public sealed record BodyPlan(
    int Orbit,
    int ObjectTypeId,
    string Name,
    int SizeClassId,
    int Size,
    IReadOnlyList<ResourceShare> DepositIds);

/// <summary>An asteroid field.</summary>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="Name">The name.</param>
/// <param name="Orbit">The outermost orbit of its system, or <see langword="null"/> for a free field.</param>
/// <param name="Position">The position within the sector.</param>
/// <param name="Mass">The total mass.</param>
/// <param name="CompositionIds">The composition.</param>
public sealed record FieldPlan(
    int ObjectTypeId,
    string Name,
    int? Orbit,
    Point3 Position,
    long Mass,
    IReadOnlyList<ResourceShare> CompositionIds);

/// <summary>A nebula.</summary>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="Name">The name.</param>
/// <param name="Position">The centre within the sector.</param>
/// <param name="Radius">The radius.</param>
/// <param name="BlocksJumpDrive">Whether the jump drive is forbidden inside.</param>
/// <param name="CompositionIds">The composition.</param>
public sealed record NebulaPlan(
    int ObjectTypeId,
    string Name,
    Point3 Position,
    double Radius,
    bool BlocksJumpDrive,
    IReadOnlyList<ResourceShare> CompositionIds);

/// <summary>A star system.</summary>
/// <param name="Number">The number within the sector.</param>
/// <param name="Name">The name.</param>
/// <param name="Position">The position within the sector.</param>
/// <param name="InNebula">Whether the system lies inside a nebula.</param>
/// <param name="StarTypeId">The type of the star.</param>
/// <param name="BodyIds">The bodies on the orbits.</param>
/// <param name="Field">The field on the outermost orbit, if any.</param>
public sealed record SystemPlan(
    int Number,
    string Name,
    Point3 Position,
    bool InNebula,
    int StarTypeId,
    IReadOnlyList<BodyPlan> BodyIds,
    FieldPlan? Field);

/// <summary>
/// The description of a generated sector, the output of the pure generator.
/// </summary>
/// <param name="Seed">The seed.</param>
/// <param name="Name">The name.</param>
/// <param name="SystemIds">The star systems.</param>
/// <param name="FreeFieldIds">The fields free in the sector.</param>
/// <param name="NebulaIds">The nebulae.</param>
public sealed record SectorPlan(
    long Seed,
    string Name,
    IReadOnlyList<SystemPlan> SystemIds,
    IReadOnlyList<FieldPlan> FreeFieldIds,
    IReadOnlyList<NebulaPlan> NebulaIds)
{
    /// <summary>
    /// Gets every field of the sector, on orbits and free.
    /// </summary>
    public IEnumerable<FieldPlan> AllFields =>
        SystemIds.Where(system => system.Field is not null).Select(system => system.Field!).Concat(FreeFieldIds);
}

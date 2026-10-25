namespace Aetheris.World.Data.Entities;

/// <summary>
/// The galaxy.
/// </summary>
public sealed class GalaxyEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the seed the sector seeds are derived from.</summary>
    public long Seed { get; set; }

    /// <summary>Gets or sets the moment of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A sector of the galaxy.
/// </summary>
public sealed class SectorEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the galaxy.</summary>
    public int GalaxyId { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the seed of the generation.</summary>
    public long Seed { get; set; }

    /// <summary>Gets or sets the position on the layout spiral; a higher index lies further out.</summary>
    public int LayoutIndex { get; set; }

    /// <summary>Gets or sets the x coordinate of the origin in the galaxy.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the y coordinate of the origin in the galaxy.</summary>
    public double Y { get; set; }

    /// <summary>Gets or sets the z coordinate of the origin in the galaxy.</summary>
    public double Z { get; set; }

    /// <summary>Gets or sets a value indicating whether newcomers start here.</summary>
    public bool IsNewbie { get; set; }

    /// <summary>Gets or sets the moment of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A star system. Its coordinates are local to the sector.
/// </summary>
public sealed class StarSystemEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the number of the system within the sector.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the x coordinate within the sector.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the y coordinate within the sector.</summary>
    public double Y { get; set; }

    /// <summary>Gets or sets the z coordinate within the sector.</summary>
    public double Z { get; set; }

    /// <summary>Gets or sets a value indicating whether the system lies inside a nebula.</summary>
    public bool InNebula { get; set; }

    /// <summary>Gets or sets the number of newcomers who started in the system.</summary>
    public int NewbieCount { get; set; }

    /// <summary>Gets or sets the objects of the system.</summary>
    public List<CelestialObjectEntity> ObjectIds { get; set; } = [];
}

/// <summary>
/// An object at a place of the map: a star, a body on an orbit, a field or a nebula.
/// </summary>
public sealed class CelestialObjectEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the star system, or <see langword="null"/> for an object free in the sector.</summary>
    public long? SystemId { get; set; }

    /// <summary>Gets or sets the type; it may change later (terraforming).</summary>
    public int ObjectTypeId { get; set; }

    /// <summary>Gets or sets the orbit, or <see langword="null"/> for a star or a free object.</summary>
    public int? Orbit { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the x coordinate within the sector.</summary>
    public double X { get; set; }

    /// <summary>Gets or sets the y coordinate within the sector.</summary>
    public double Y { get; set; }

    /// <summary>Gets or sets the z coordinate within the sector.</summary>
    public double Z { get; set; }

    /// <summary>Gets or sets the moment of creation.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the system of the object.</summary>
    public StarSystemEntity? System { get; set; }
}

/// <summary>
/// A body on an orbit: a planet or a gas giant.
/// </summary>
public sealed class PlanetEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector, kept here for the queries of one sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the star system.</summary>
    public long SystemId { get; set; }

    /// <summary>Gets or sets the celestial object.</summary>
    public long CelestialObjectId { get; set; }

    /// <summary>Gets or sets the size class.</summary>
    public int SizeClassId { get; set; }

    /// <summary>Gets or sets the size; it never changes after creation.</summary>
    public int Size { get; set; }

    /// <summary>Gets or sets the owning player, or <see langword="null"/> for a free body.</summary>
    public string? OwnerPlayerId { get; set; }

    /// <summary>Gets or sets the celestial object.</summary>
    public CelestialObjectEntity? CelestialObject { get; set; }

    /// <summary>Gets or sets the deposits of the body.</summary>
    public List<DepositEntity> DepositIds { get; set; } = [];
}

/// <summary>
/// A deposit of a body: one resource with its coefficient (task 00004, decision 23).
/// </summary>
public sealed class DepositEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets the coefficient of the deposit.</summary>
    public decimal Coefficient { get; set; }
}

/// <summary>
/// An asteroid field: a total mass and a constant composition (task 00004, decision 18).
/// </summary>
public sealed class AsteroidFieldEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the star system of a field on the outermost orbit, or <see langword="null"/> for a free field.</summary>
    public long? SystemId { get; set; }

    /// <summary>Gets or sets the celestial object.</summary>
    public long CelestialObjectId { get; set; }

    /// <summary>Gets or sets the mass at creation.</summary>
    public long InitialMass { get; set; }

    /// <summary>Gets or sets the mass that is left.</summary>
    public long Mass { get; set; }

    /// <summary>Gets or sets the moment of game time when the field was depleted and disappeared.</summary>
    public DateTimeOffset? DepletedAt { get; set; }

    /// <summary>Gets or sets the celestial object.</summary>
    public CelestialObjectEntity? CelestialObject { get; set; }

    /// <summary>Gets or sets the composition of the field.</summary>
    public List<FieldCompositionEntity> CompositionIds { get; set; } = [];
}

/// <summary>
/// One share of the composition of an asteroid field.
/// </summary>
public sealed class FieldCompositionEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the field.</summary>
    public long AsteroidFieldId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets the share of the resource in the processed mass, from 0 to 1.</summary>
    public decimal Coefficient { get; set; }
}

/// <summary>
/// A nebula: a zone of the sector with its own sources and effects.
/// </summary>
public sealed class NebulaEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the celestial object at the centre of the nebula.</summary>
    public long CelestialObjectId { get; set; }

    /// <summary>Gets or sets the radius.</summary>
    public double Radius { get; set; }

    /// <summary>Gets or sets a value indicating whether the jump drive is forbidden inside.</summary>
    public bool BlocksJumpDrive { get; set; }

    /// <summary>Gets or sets the celestial object.</summary>
    public CelestialObjectEntity? CelestialObject { get; set; }

    /// <summary>Gets or sets the composition of the nebula.</summary>
    public List<NebulaCompositionEntity> CompositionIds { get; set; } = [];
}

/// <summary>
/// One share of the composition of a nebula.
/// </summary>
public sealed class NebulaCompositionEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the nebula.</summary>
    public long NebulaId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets the coefficient of the resource.</summary>
    public decimal Coefficient { get; set; }
}

/// <summary>
/// The start body of a player. The key on the player makes the allocation happen once.
/// </summary>
public sealed class StartPlanetAllocationEntity
{
    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the moment of the allocation.</summary>
    public DateTimeOffset AllocatedAt { get; set; }
}

/// <summary>
/// A record of a generation action: what was generated, with which seed and by whom.
/// </summary>
public sealed class WorldGenerationLogEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the galaxy.</summary>
    public int? GalaxyId { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int? SectorId { get; set; }

    /// <summary>Gets or sets the action, one of <see cref="WorldGenerationActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Gets or sets the seed.</summary>
    public long? Seed { get; set; }

    /// <summary>Gets or sets who started the action: an administrator or the system.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the details as JSON.</summary>
    public string Detail { get; set; } = "{}";

    /// <summary>Gets or sets the moment of the action.</summary>
    public DateTimeOffset At { get; set; }
}

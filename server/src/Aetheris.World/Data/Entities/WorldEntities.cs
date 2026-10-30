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

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

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

    /// <summary>Gets or sets the name the owner gave the body, or <see langword="null"/>.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets whether a device of a ship has made the deposits of the body rich: a body
    /// is terraformed only once, whoever colonizes it later (task 00014, section 9).
    /// </summary>
    public bool IsTerraformed { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

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
/// A change of the coefficient of a deposit of a body, in the journal of the world (task
/// 00014, decision 2).
/// </summary>
public sealed class DepositChangeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the deposit belongs to.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the resource of the deposit.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets the coefficient before the change.</summary>
    public decimal OldCoefficient { get; set; }

    /// <summary>Gets or sets the coefficient after the change.</summary>
    public decimal NewCoefficient { get; set; }

    /// <summary>Gets or sets why the deposit changed.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets who changed the deposit.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the moment of the change.</summary>
    public DateTimeOffset At { get; set; }
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

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

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

/// <summary>
/// The civilization of a player: the race and the home body chosen at the first step of
/// the game (task 00006, decision 9). A player has one, and the race does not change.
/// </summary>
public sealed class PlayerCivilizationEntity
{
    /// <summary>Gets or sets the player, the primary key.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the race.</summary>
    public int RaceId { get; set; }

    /// <summary>Gets or sets the home body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the home body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the moment of creation.</summary>
    public DateTimeOffset FoundedAt { get; set; }
}

/// <summary>
/// The amount of one resource in the storage of a body.
/// </summary>
/// <remarks>
/// The start package is written here (task 00006, decision 11). The production and the
/// lazy amounts of documentation/tech/01, section 3.1, arrive with the economy.
/// </remarks>
public sealed class PlanetStorageEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount; never negative.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// The start building of a new civilization, waiting for the buildings module to place it.
/// </summary>
/// <remarks>
/// This is the default of the extension point <see cref="Services.IStartBuildingPlacer"/>:
/// until the buildings module exists, the grant is kept here, so the building is not lost.
/// </remarks>
public sealed class StartBuildingGrantEntity
{
    /// <summary>Gets or sets the body, the primary key.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the building type.</summary>
    public int BuildingTypeId { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the moment of the grant.</summary>
    public DateTimeOffset GrantedAt { get; set; }
}

/// <summary>
/// A generation run started from the admin panel: who, when, with what and with what result
/// (task 00007, decision 3).
/// </summary>
public sealed class GenerationRunEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the administrator who started the run.</summary>
    public string InitiatorId { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of sectors to add.</summary>
    public int SectorCount { get; set; }

    /// <summary>Gets or sets the seed, or <see langword="null"/> to derive the seeds from the galaxy seed.</summary>
    public long? Seed { get; set; }

    /// <summary>Gets or sets the code of the rule set, one of <see cref="GenerationRuleSets"/>.</summary>
    public string RuleSet { get; set; } = string.Empty;

    /// <summary>Gets or sets the expected number of star systems shown at the confirmation.</summary>
    public int EstimatedSystemCount { get; set; }

    /// <summary>Gets or sets the status, one of <see cref="GenerationRunStatuses"/>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets <see langword="true"/> while the run is queued or running and
    /// <see langword="null"/> afterwards. A unique index on it lets only one run be active.
    /// </summary>
    public bool? ActiveLock { get; set; }

    /// <summary>Gets or sets the UTC moment the run was requested.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC moment the work started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the UTC moment the work ended.</summary>
    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Gets or sets what was created, as JSON.</summary>
    public string Summary { get; set; } = "{}";

    /// <summary>Gets or sets the error code of a failed run.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// The statuses of a generation run.
/// </summary>
public static class GenerationRunStatuses
{
    /// <summary>Waits for the scheduler.</summary>
    public const string Queued = "queued";

    /// <summary>Creates the sectors.</summary>
    public const string Running = "running";

    /// <summary>Every sector was created.</summary>
    public const string Succeeded = "succeeded";

    /// <summary>The run stopped with an error; the sectors created before it stay.</summary>
    public const string Failed = "failed";
}

/// <summary>
/// The rule sets a generation run can use.
/// </summary>
/// <remarks>
/// The database holds one set of generation rules (<c>generation_rule_group</c> and
/// <c>generation_rule</c>), so the only choice is the current one.
/// </remarks>
public static class GenerationRuleSets
{
    /// <summary>The rules stored in the database now.</summary>
    public const string Current = "current";
}

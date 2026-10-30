namespace Aetheris.Planets.Data.Entities;

/// <summary>
/// A building type: a row of the reference table <c>building_type</c> (task 00011, decision 1).
/// </summary>
/// <remarks>
/// A type is data: a new building is a new row with its levels and requirements. A body has
/// at most one building of every type, and the building grows by levels.
/// </remarks>
public sealed class BuildingTypeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the order in the lists of the client.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a race without a start building gets this type
    /// at start (task 00022, decision 5: a flag of the data instead of a code in the
    /// configuration).
    /// </summary>
    public bool IsStartBuilding { get; set; }

    /// <summary>
    /// Gets or sets the class of the type: what the building is for, one of
    /// <see cref="Services.BuildingClasses"/> (task 00012, decision 1).
    /// </summary>
    /// <remarks>
    /// The class is a logical code, not the neutral code of the row: the research task asks
    /// for the class <c>research_center</c> and gets the building whatever its code is.
    /// </remarks>
    public string BuildingClass { get; set; } = Services.BuildingClasses.None;

    /// <summary>Gets or sets the key of the picture, or <see langword="null"/> for the neutral placeholder.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the type is archived: the buildings of the players
    /// keep working and the queue finishes, but no new building or level of it is offered
    /// (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the levels of the type.</summary>
    public List<BuildingLevelEntity> LevelIds { get; set; } = [];
}

/// <summary>
/// A level of a building type: the time to reach it, its costs and its effects (decision 10).
/// </summary>
public sealed class BuildingLevelEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the building type.</summary>
    public int BuildingTypeId { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the time to build the level, in game seconds.</summary>
    public long DurationSeconds { get; set; }

    /// <summary>Gets or sets the costs of the level.</summary>
    public List<BuildingLevelCostEntity> CostIds { get; set; } = [];

    /// <summary>Gets or sets the effects of a building of this level.</summary>
    public List<BuildingLevelEffectEntity> EffectIds { get; set; } = [];
}

/// <summary>
/// One resource of the cost of a building level.
/// </summary>
public sealed class BuildingLevelCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int BuildingLevelId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// One effect of a building of a level: the total value at that level, not the step from the
/// level below.
/// </summary>
public sealed class BuildingLevelEffectEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int BuildingLevelId { get; set; }

    /// <summary>Gets or sets the effect type, one of <see cref="Services.BuildingEffectTypes"/>.</summary>
    public string EffectType { get; set; } = string.Empty;

    /// <summary>Gets or sets the resource of a production effect.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the resource group of a storage effect.</summary>
    public int? ResourceGroupId { get; set; }

    /// <summary>
    /// Gets or sets the value. It has a fraction, so a multiplier such as the speed of the
    /// population growth lies in the data for every level without a loss (task 00015).
    /// </summary>
    public decimal Value { get; set; }
}

/// <summary>
/// The state of a body in the module: the owner, the paid places of the queue and the moment
/// the balance was last fixed.
/// </summary>
/// <remarks>
/// Every change of a body locks this row first, so two commands of one body never spend the
/// same resources (task 00011, criterion 7).
/// </remarks>
public sealed class PlanetStateEntity
{
    /// <summary>Gets or sets the body, the primary key.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the owner.</summary>
    public string? OwnerPlayerId { get; set; }

    /// <summary>Gets or sets the paid places added to the construction queue (decision 5).</summary>
    public int ExtraQueueSlots { get; set; }

    /// <summary>Gets or sets the moment of game time the balance of the body was last fixed.</summary>
    public DateTimeOffset ResourcesUpdatedAt { get; set; }
}

/// <summary>
/// A building of a body with its current level.
/// </summary>
public sealed class PlanetBuildingEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the building type.</summary>
    public int BuildingTypeId { get; set; }

    /// <summary>Gets or sets the level.</summary>
    public int Level { get; set; }
}

/// <summary>
/// A record of the construction queue of a body (decision 5).
/// </summary>
/// <remarks>
/// The first record by <see cref="Position"/> is under way and has its start and end; the
/// others wait. The cost paid at the queueing is kept, so a cancel returns exactly it.
/// </remarks>
public sealed class ConstructionQueueItemEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the building type.</summary>
    public int BuildingTypeId { get; set; }

    /// <summary>Gets or sets the level the building reaches.</summary>
    public int TargetLevel { get; set; }

    /// <summary>Gets or sets the place in the queue, from 1.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the time of the construction, in game seconds, fixed at the queueing.</summary>
    public long DurationSeconds { get; set; }

    /// <summary>Gets or sets the moment of game time of the queueing.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the moment of game time the construction started, or <see langword="null"/> while it waits.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the moment of game time the construction ends, or <see langword="null"/> while it waits.</summary>
    public DateTimeOffset? FinishesAt { get; set; }

    /// <summary>Gets or sets the cost paid at the queueing.</summary>
    public List<ConstructionQueueCostEntity> CostIds { get; set; } = [];
}

/// <summary>
/// One resource paid for a record of the construction queue.
/// </summary>
public sealed class ConstructionQueueCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the queue record.</summary>
    public long ConstructionQueueItemId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// The amount of one resource on a body at the moment of the last fix
/// (<see cref="PlanetStateEntity.ResourcesUpdatedAt"/>); never negative.
/// </summary>
public sealed class PlanetResourceEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// The state of the storage of one resource group of a body: the moment the group is full
/// (decision 8).
/// </summary>
/// <remarks>
/// It is written at every fix of the balance from the rates of that moment, so the alarm
/// "storage full" is data: the group is full while the current game time is not before
/// <see cref="FullAt"/>.
/// </remarks>
public sealed class PlanetStorageGroupEntity
{
    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the resource group.</summary>
    public int ResourceGroupId { get; set; }

    /// <summary>Gets or sets the sector.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the moment of game time the group is or will be full, or <see langword="null"/> when it does not fill.</summary>
    public DateTimeOffset? FullAt { get; set; }
}

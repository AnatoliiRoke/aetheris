namespace Aetheris.Fleet.Data.Entities;

/// <summary>
/// A project of a ship: the hull the player chose and the modules he put into its slots
/// (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// <para>
/// A project belongs to the player, not to a body: it is available on every body of the
/// civilization (decision 6). The body in <see cref="PlanetId"/> is the one the project was
/// made on, so the builder shows it in the place it belongs to.
/// </para>
/// <para>
/// The parts live in the tables of the production and the bodies in the tables of the world,
/// so this table keeps only their identifiers and carries no foreign keys into the tables of
/// the other modules (documentation/tech/08, rule 5).
/// </para>
/// </remarks>
public sealed class ShipDesignEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owner of the project.</summary>
    public string OwnerPlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body the project was made on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the name of the project.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the hull of the project.</summary>
    public int HullPartTypeId { get; set; }

    /// <summary>
    /// Gets or sets whether the project has the extra slot of the race: it is allowed for the
    /// heavy hulls and the super hulls on the higher levels of the technology of the hull
    /// (decision 11).
    /// </summary>
    public bool HasRaceBonusSlot { get; set; }

    /// <summary>Gets or sets the moment of game time the project was created at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the moment of game time the project was changed at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets whether the project is archived: it is kept, so a ship already built of it does not lose its link (task 00023).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; a change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A slot of a project of a ship: the part the player put into it (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// The slots are numbered from zero and have no gaps: the builder shows them in a row, so a
/// gap would be a hole on the screen.
/// </remarks>
public sealed class ShipDesignSlotEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the project the slot belongs to.</summary>
    public long DesignId { get; set; }

    /// <summary>Gets or sets the number of the slot, from zero.</summary>
    public int SlotIndex { get; set; }

    /// <summary>Gets or sets the part in the slot.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the mark of the part in the slot.</summary>
    public int MarkLevel { get; set; }

    /// <summary>Gets or sets whether the part in the slot carries the bonus of the race that made it.</summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>
    /// Gets or sets the shutdown order of the slot (task 00062). A smaller number turns off later.
    /// A saved project sets the number. The database default covers a row written without the column.
    /// </summary>
    public short PowerPriority { get; set; }
}

/// <summary>
/// The lock row of the shipyard queue of one body (task 00039).
/// </summary>
/// <remarks>
/// The queue belongs to the body, not to the civilization, so two commands of one body wait
/// on this row and the commands of two bodies do not. The row carries no foreign key into the
/// tables of the bodies module.
/// </remarks>
public sealed class ShipyardQueueStateEntity
{
    /// <summary>Gets or sets the body the queue belongs to.</summary>
    public long PlanetId { get; set; }
}

/// <summary>
/// A record of the shipyard queue of a body (task 00039).
/// </summary>
/// <remarks>
/// The records follow one another: the one under way holds the moment its current ship finishes
/// and the ones that wait have none. One record is one batch: the whole batch is paid when it
/// enters and the ships are finished one by one. The duration is the time of one ship without
/// the technology, stored when the record enters, so a technology learned while it waits applies
/// when a ship starts and not before.
/// </remarks>
public sealed class ShipyardOrderEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body the ship is assembled on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the shipyard the record was put in.</summary>
    public long PlanetBuildingId { get; set; }

    /// <summary>Gets or sets the project the ship is assembled from.</summary>
    public long ShipDesignId { get; set; }

    /// <summary>Gets or sets the position of the record in the queue.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets how many ships the record assembles. The whole batch is one queue place.</summary>
    public long Quantity { get; set; } = 1;

    /// <summary>Gets or sets how many ships of the record are already finished.</summary>
    public long ProducedQuantity { get; set; }

    /// <summary>Gets or sets the duration of one ship without the technology, in game seconds.</summary>
    public long BaseDurationSeconds { get; set; }

    /// <summary>Gets or sets the crew the record paid for, in inhabitants.</summary>
    public long Crew { get; set; }

    /// <summary>Gets or sets the upkeep of the ship, in credits per hour. Zero until the modelling fills it.</summary>
    public decimal UpkeepPerHour { get; set; }

    /// <summary>Gets or sets the moment of game time the record was created at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the moment the assembly starts, or <see langword="null"/> when it waits.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>Gets or sets the moment the assembly finishes, or <see langword="null"/> when it waits.</summary>
    public DateTimeOffset? FinishesAt { get; set; }

    /// <summary>Gets or sets the resources the record paid for.</summary>
    public List<ShipyardOrderCostEntity> CostIds { get; set; } = [];

    /// <summary>Gets or sets the parts the record paid for.</summary>
    public List<ShipyardOrderPartEntity> PartIds { get; set; } = [];
}

/// <summary>
/// The resources a shipyard record paid for (task 00039).
/// </summary>
/// <remarks>
/// The amounts are kept, so a cancellation returns exactly what it paid. A dismantling of the
/// finished ship does not return them: the project gives back its components and the crew.
/// </remarks>
public sealed class ShipyardOrderCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the record of the queue.</summary>
    public long OrderId { get; set; }

    /// <summary>Gets or sets the resource of the cost.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount of the resource.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// A stack of parts a shipyard record paid for (task 00039).
/// </summary>
public sealed class ShipyardOrderPartEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the record of the queue.</summary>
    public long OrderId { get; set; }

    /// <summary>Gets or sets the kind of the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the mark of the part.</summary>
    public int MarkLevel { get; set; }

    /// <summary>Gets or sets whether the part carries the bonus of the race that made it.</summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>Gets or sets the condition of the part in per cent.</summary>
    public int ConditionPercent { get; set; }

    /// <summary>Gets or sets how many parts.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets whether the stack is the hull.</summary>
    public bool IsHull { get; set; }
}

/// <summary>
/// A finished ship (task 00039). The fleets of task 00040 take it from here.
/// </summary>
/// <remarks>
/// The ship keeps the project it was built from, the body it stands on and the crew assigned
/// to it. A dismantling reads that project and returns every component of it, and the crew.
/// The resources the assembly paid stay spent. The body is never destroyed, so the ship
/// always has one.
/// </remarks>
public sealed class ShipEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body the ship stands on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the project the ship was built from.</summary>
    public long ShipDesignId { get; set; }

    /// <summary>Gets or sets the crew assigned to the ship, in inhabitants.</summary>
    public long Crew { get; set; }

    /// <summary>Gets or sets the upkeep of the ship, in credits per hour.</summary>
    public decimal UpkeepPerHour { get; set; }

    /// <summary>Gets or sets the state of the ship, one of <see cref="ShipStates"/>.</summary>
    public string StateCode { get; set; } = ShipStates.Ready;

    /// <summary>Gets or sets the moment of game time the ship was finished at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the parts of the ship.</summary>
    public List<ShipModuleEntity> ModuleIds { get; set; } = [];

    /// <summary>Gets or sets the costs the assembly paid, copied from the queue.</summary>
    public List<ShipCostEntity> CostIds { get; set; } = [];
}

/// <summary>
/// A stack of parts on a finished ship (task 00039).
/// </summary>
/// <remarks>
/// The condition is kept for the battles of a later task. A new ship carries whole parts.
/// </remarks>
public sealed class ShipModuleEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the ship.</summary>
    public long ShipId { get; set; }

    /// <summary>Gets or sets the kind of the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the mark of the part.</summary>
    public int MarkLevel { get; set; }

    /// <summary>Gets or sets whether the part carries the bonus of the race that made it.</summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>Gets or sets the condition of the part in per cent.</summary>
    public int ConditionPercent { get; set; }

    /// <summary>Gets or sets the condition of the part, one of <see cref="ShipModuleConditions"/>.</summary>
    public string ConditionCode { get; set; } = ShipModuleConditions.Intact;

    /// <summary>Gets or sets how many parts.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets whether the stack is the hull.</summary>
    public bool IsHull { get; set; }
}

/// <summary>
/// The resources the assembly of a ship paid for, copied onto the ship (task 00039).
/// </summary>
public sealed class ShipCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the ship.</summary>
    public long ShipId { get; set; }

    /// <summary>Gets or sets the resource of the cost.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount of the resource.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// The states of a ship. They are logical codes, not numbers of the balance.
/// </summary>
public static class ShipStates
{
    /// <summary>The ship is finished and stands on its body.</summary>
    public const string Ready = "ready";
}

/// <summary>
/// The conditions of a part on a ship. They are logical codes, not numbers of the balance.
/// </summary>
public static class ShipModuleConditions
{
    /// <summary>The part is whole.</summary>
    public const string Intact = "intact";

    /// <summary>The part is damaged. The battles of a later task set this.</summary>
    public const string Damaged = "damaged";

    /// <summary>The part is destroyed. The battles of a later task set this.</summary>
    public const string Destroyed = "destroyed";
}

namespace Aetheris.Processes.Data.Entities;

/// <summary>
/// A parameter of a process: a value that depends on a level, a percentage of the output or
/// a range of the multipliers (task 00014, section 3.1).
/// </summary>
/// <remarks>
/// The kind of the parameter is a logical code, so a new process brings a new kind of the
/// parameter and not a new column.
/// </remarks>
public sealed class ProcessParamEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the parameter.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the name of the parameter in the texts of the game data.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the description of the parameter in the texts of the game data.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind of the process the parameter belongs to.</summary>
    public int ProcessTypeId { get; set; }

    /// <summary>
    /// Gets or sets the logical code of the kind of the parameter: a value by a level, a
    /// percentage of the output or a range.
    /// </summary>
    public string ParamKind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the level belongs to: a building, a technology or a part. The
    /// parameter follows the level of that thing.
    /// </summary>
    public string LevelSource { get; set; } = string.Empty;

    /// <summary>Gets or sets the thing whose level the parameter follows.</summary>
    public int? LevelTargetId { get; set; }

    /// <summary>Gets or sets the level the parameter starts at.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Gets or sets the value of the parameter.</summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Gets or sets the second value of the parameter, for the ranges: the lowest and the
    /// highest multiplier of the deposits.
    /// </summary>
    public decimal? ValueMax { get; set; }

    /// <summary>Gets or sets whether the parameter is archived.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row for the conflicts of the admin panel.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the place of the parameter in the lists.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// An order of a process in the queue of a building (task 00014, decision 1).
/// </summary>
public sealed class ProcessOrderEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the process runs on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the building the process runs in.</summary>
    public long PlanetBuildingId { get; set; }

    /// <summary>Gets or sets the recipe of the order.</summary>
    public int ProcessRecipeId { get; set; }

    /// <summary>Gets or sets how many units the order holds.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets how many units are already made.</summary>
    public long ProducedQuantity { get; set; }

    /// <summary>
    /// Gets or sets the resource the conversion takes, for the processes that convert one
    /// resource into another.
    /// </summary>
    public int? SourceResourceTypeId { get; set; }

    /// <summary>
    /// Gets or sets the resource the conversion gives, for the processes that convert one
    /// resource into another.
    /// </summary>
    public int? TargetResourceTypeId { get; set; }

    /// <summary>Gets or sets the part the process works on, for the recycling and the repair.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets the condition of the part the process works on, in per cent.</summary>
    public int? PartConditionPercent { get; set; }

    /// <summary>
    /// Gets or sets how long one unit of the order takes, in game seconds. It is fixed when the
    /// order enters the queue, so a later change of the recipe does not move the order.
    /// </summary>
    public long UnitSeconds { get; set; }

    /// <summary>Gets or sets the place of the order in the queue.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the moment the order started.</summary>
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>Gets or sets the moment the next unit is ready.</summary>
    public DateTimeOffset? NextUnitAt { get; set; }

    /// <summary>Gets or sets the moment the whole order is ready.</summary>
    public DateTimeOffset? FinishesAt { get; set; }

    /// <summary>Gets or sets whether the order waits for a free place in the storage.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>Gets or sets the moment the order was made.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the costs of the order, kept for the refund when it is cancelled.</summary>
    public List<ProcessOrderCostEntity> CostIds { get; set; } = [];
}

/// <summary>
/// A cost of an order of a process: a resource or a part the order took (task 00014,
/// decision 1).
/// </summary>
public sealed class ProcessOrderCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the order the cost belongs to.</summary>
    public long ProcessOrderId { get; set; }

    /// <summary>Gets or sets the resource the cost took.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part the cost took.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets the mark level of the part the cost took.</summary>
    public int? MarkLevel { get; set; }

    /// <summary>Gets or sets whether the part the cost took carries the bonus of the race.</summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>Gets or sets the condition of the part the cost took, in per cent.</summary>
    public int? ConditionPercent { get; set; }

    /// <summary>Gets or sets how many units the cost took.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// The extra places of the queue of a building (task 00014, decision 1).
/// </summary>
public sealed class ProcessQueueStateEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the building stands on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the building.</summary>
    public long PlanetBuildingId { get; set; }

    /// <summary>Gets or sets how many paid places are added to the queue.</summary>
    public int ExtraSlots { get; set; }
}

/// <summary>
/// The state of the population of a body that the processes changed: the crew that came back
/// and waits for the population task to settle it, and the flag that the crew is over the
/// limit (task 00014, decision 3).
/// </summary>
/// <remarks>
/// The population itself is the task of 00015, so this table holds only what the processes
/// produce and the flag the population task reads.
/// </remarks>
public sealed class ProcessPopulationStateEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the state belongs to.</summary>
    public long PlanetId { get; set; }

    /// <summary>
    /// Gets or sets the crew that came back and waits to be settled by the population task.
    /// </summary>
    public long CrewPending { get; set; }

    /// <summary>Gets or sets whether the crew is over the limit of the body.</summary>
    public bool IsOverLimit { get; set; }

    /// <summary>Gets or sets the moment the crew went over the limit.</summary>
    public DateTimeOffset? OverLimitAt { get; set; }
}

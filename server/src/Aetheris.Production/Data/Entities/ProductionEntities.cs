namespace Aetheris.Production.Data.Entities;

/// <summary>
/// A record of the queue of a factory: a recipe and how many units are ordered
/// (task 00013, decision 2.5).
/// </summary>
/// <remarks>
/// The queue is one for every factory, so the record names the body and the factory it
/// belongs to. The records follow one another: the first is under way and holds the moment
/// it finishes, the ones that wait have none.
/// </remarks>
public sealed class ProductionOrderEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the factory stands on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the factory the record belongs to.</summary>
    public long PlanetBuildingId { get; set; }

    /// <summary>Gets or sets the recipe of the record.</summary>
    public int RecipeId { get; set; }

    /// <summary>Gets or sets how many units are ordered.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets how many units are already made.</summary>
    public long ProducedQuantity { get; set; }

    /// <summary>Gets or sets the place of the record in the queue of the factory.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the moment the record entered the queue.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the moment the record under way started, or <see langword="null"/>.</summary>
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>Gets or sets the moment the next unit is ready, or <see langword="null"/>.</summary>
    public DateTimeOffset? NextUnitAt { get; set; }

    /// <summary>Gets or sets the moment the whole batch is ready, or <see langword="null"/>.</summary>
    public DateTimeOffset? FinishesAt { get; set; }

    /// <summary>
    /// Gets or sets whether the record waits because the storage of the body is full
    /// (task 00013, decision 2.4).
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>Gets or sets the ingredients the record paid for.</summary>
    public List<ProductionOrderCostEntity> CostIds { get; set; } = [];
}

/// <summary>
/// An ingredient a record of the queue paid for, with the part it took when the part is
/// not a resource of the world (task 00013, decision 2.3).
/// </summary>
public sealed class ProductionOrderCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the record of the queue.</summary>
    public long ProductionOrderId { get; set; }

    /// <summary>Gets or sets the resource the ingredient is, or <see langword="null"/>.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part the ingredient is, or <see langword="null"/>.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>
    /// Gets or sets whether the parts taken carried the bonus of a race, so cancelling the
    /// batch gives back the same stack (task 00013, decision 2.8).
    /// </summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>Gets or sets how many of the ingredient was paid.</summary>
    public long Amount { get; set; }
}

/// <summary>
/// The condition of a part in per cent (task 00013, decision 2.13).
/// </summary>
public static class PartCondition
{
    /// <summary>The condition of a new part, in per cent.</summary>
    public const int Full = 100;
}

/// <summary>
/// A stack of the parts on the body (task 00013, decision 2.8).
/// </summary>
/// <remarks>
/// The stack is keyed by the part, its mark level, the mark of the race that made it and
/// its condition, so a new part of the same kind with a bonus of the race is another stack.
/// A damaged part is a row of its own, because it has its own condition.
/// </remarks>
public sealed class PartStockEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the stack is on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the part of the stack.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the mark level of the part.</summary>
    public int MarkLevel { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether the parts of the stack carry the bonus of the race that made
    /// them (task 00013, decision 2.8).
    /// </summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>
    /// Gets or sets the condition of the parts of the stack in per cent, a hundred for a
    /// new part (task 00013, decision 2.13).
    /// </summary>
    public int ConditionPercent { get; set; } = PartCondition.Full;

    /// <summary>Gets or sets how many parts the stack holds.</summary>
    public long Quantity { get; set; }
}

/// <summary>
/// The state of the storage of the parts of a body (task 00013, decision 2.9).
/// </summary>
/// <remarks>
/// The parts are not a group of resources of the world, so their storage has its own row,
/// which also locks the storage of the body. Every kind of part has the same room of its own
/// (decision of the author, 31.10.2026): how many parts of a kind lie there is the sum of the
/// stacks of the kind, so the row keeps no total and no moment of its own.
/// </remarks>
public sealed class PartStorageEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the capacity of the storage for every kind of part, in parts of the kind.</summary>
    public long Capacity { get; set; }
}

/// <summary>
/// The extra places of the queue of a factory (task 00013, decision 2.5).
/// </summary>
public sealed class ProductionQueueStateEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the body the factory stands on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the factory.</summary>
    public long PlanetBuildingId { get; set; }

    /// <summary>Gets or sets the extra places of the queue of the factory.</summary>
    public int ExtraQueueSlots { get; set; }
}

/// <summary>
/// The bonus of a race in a group of the parts (task 00013, decision 2.8).
/// </summary>
/// <remarks>
/// The bonus is a rule of the production, so it lives with the parts: a race that has a
/// bonus in a group stamps its parts, and the stamp is taken off when a part is sold.
/// </remarks>
public sealed class RacePartBonusEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the race.</summary>
    public int RaceId { get; set; }

    /// <summary>Gets or sets the group of the parts the bonus touches.</summary>
    public int PartGroupId { get; set; }

    /// <summary>Gets or sets the multiplier of the bonus.</summary>
    public decimal Multiplier { get; set; } = 1;

    /// <summary>Gets or sets whether the race may make the top modules of the group alone.</summary>
    public bool IsExclusive { get; set; }
}

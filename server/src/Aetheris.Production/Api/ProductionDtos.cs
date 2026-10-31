namespace Aetheris.Production.Api;

/// <summary>
/// A factory of a body with its recipes, its queue and the storage of the parts
/// (task 00013, section 3.1).
/// </summary>
/// <param name="PlanetId">The body the factory stands on.</param>
/// <param name="PlanetBuildingId">The factory.</param>
/// <param name="FactoryLevel">The level of the factory.</param>
/// <param name="Recipes">The recipes of the factory with their availability.</param>
/// <param name="Queue">The batches of the queue.</param>
/// <param name="Storage">The storage of the parts of the body.</param>
/// <param name="GameNow">The moment of game time.</param>
public sealed record ProductionFactoryDto(
    long PlanetId,
    long PlanetBuildingId,
    int FactoryLevel,
    IReadOnlyList<ProductionRecipeDto> Recipes,
    IReadOnlyList<ProductionOrderDto> Queue,
    ProductionStorageDto Storage,
    DateTimeOffset GameNow);

/// <summary>
/// A factory of a body, without its recipes.
/// </summary>
/// <param name="PlanetId">The body the factory stands on.</param>
/// <param name="PlanetBuildingId">The row of the factory.</param>
/// <param name="BuildingTypeId">The type of the factory.</param>
/// <param name="Level">The level of the factory.</param>
/// <param name="NameKey">The localisation key of the name of the factory.</param>
public sealed record ProductionFactorySummaryDto(
    long PlanetId,
    long PlanetBuildingId,
    int BuildingTypeId,
    int Level,
    string NameKey);

/// <summary>
/// A recipe of a factory with the reasons why it is not available.
/// </summary>
/// <param name="RecipeId">The recipe.</param>
/// <param name="Code">The neutral code of the recipe.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="BuildingTypeId">The factory that makes the product, or <see langword="null"/>.</param>
/// <param name="PartTypeId">The part the recipe makes, or <see langword="null"/>.</param>
/// <param name="ResourceTypeId">The resource the recipe makes, or <see langword="null"/>.</param>
/// <param name="OutputQuantity">How many units one batch gives.</param>
/// <param name="DurationSeconds">The time of one unit, in game seconds.</param>
/// <param name="IsAvailable">Whether the recipe can be ordered.</param>
/// <param name="Reasons">The reasons why it is not available.</param>
/// <param name="ImageKey">The key of the picture, or <see langword="null"/>.</param>
/// <param name="UnitSeconds">The time of one unit for the player, with the multiplier of the technologies.</param>
/// <param name="Ingredients">The ingredients of one unit.</param>
public sealed record ProductionRecipeDto(
    int RecipeId,
    string Code,
    string NameKey,
    string DescriptionKey,
    int? BuildingTypeId,
    int? PartTypeId,
    int? ResourceTypeId,
    long OutputQuantity,
    long DurationSeconds,
    bool IsAvailable,
    IReadOnlyList<ProductionReasonDto> Reasons,
    string? ImageKey,
    long UnitSeconds,
    IReadOnlyList<ProductionIngredientDto> Ingredients);

/// <summary>
/// An ingredient of one unit of a recipe (task 00013, section 3.2).
/// </summary>
/// <param name="ResourceTypeId">The resource the ingredient is, or <see langword="null"/>.</param>
/// <param name="PartTypeId">The part the ingredient is, or <see langword="null"/>.</param>
/// <param name="Amount">How many of the ingredient one unit takes.</param>
/// <param name="NameKey">The localisation key of the name of the ingredient.</param>
public sealed record ProductionIngredientDto(
    int? ResourceTypeId,
    int? PartTypeId,
    long Amount,
    string NameKey);

/// <summary>
/// A reason why a recipe is not available.
/// </summary>
/// <param name="Code">The localisation key of the reason.</param>
/// <param name="TargetId">What is required, when the reason names it.</param>
/// <param name="TargetLevel">The required level of the target.</param>
/// <param name="TargetNameKey">The localisation key of the name of the target, or <see langword="null"/>.</param>
public sealed record ProductionReasonDto(
    string Code,
    int TargetId,
    int TargetLevel,
    string? TargetNameKey = null);

/// <summary>
/// A batch of a queue.
/// </summary>
/// <param name="OrderId">The batch.</param>
/// <param name="RecipeId">The recipe of the batch.</param>
/// <param name="Quantity">How many units the batch holds.</param>
/// <param name="ProducedQuantity">How many units are already made.</param>
/// <param name="Position">The place of the batch in the queue.</param>
/// <param name="StartsAt">The moment the batch started, or <see langword="null"/>.</param>
/// <param name="NextUnitAt">The moment the next unit is ready, or <see langword="null"/>.</param>
/// <param name="FinishesAt">The moment the whole batch is ready, or <see langword="null"/>.</param>
/// <param name="IsBlocked">Whether the batch waits for a free place in the storage.</param>
public sealed record ProductionOrderDto(
    long OrderId,
    int RecipeId,
    long Quantity,
    long ProducedQuantity,
    int Position,
    DateTimeOffset? StartsAt,
    DateTimeOffset? NextUnitAt,
    DateTimeOffset? FinishesAt,
    bool IsBlocked);

/// <summary>
/// Every batch that is in the queues of the factories of a body, with the places of those
/// queues, so one screen shows them all together (task 00020, section 7.5).
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="QueueSlots">How many places the queues of all its factories hold together.</param>
/// <param name="QueueUsedSlots">How many of those places are taken.</param>
/// <param name="Orders">The batches of all the factories, in the order of the queues.</param>
/// <param name="GameNow">The moment of game time.</param>
public sealed record ProductionActiveOrdersDto(
    long PlanetId,
    int QueueSlots,
    int QueueUsedSlots,
    IReadOnlyList<ProductionActiveOrderDto> Orders,
    DateTimeOffset GameNow);

/// <summary>
/// A batch of the queue of one factory of a body, with the names the screen needs.
/// </summary>
/// <param name="OrderId">The batch.</param>
/// <param name="PlanetBuildingId">The factory the batch runs in.</param>
/// <param name="BuildingNameKey">The localisation key of the name of the factory.</param>
/// <param name="RecipeId">The recipe of the batch.</param>
/// <param name="NameKey">The localisation key of the name of what the batch makes.</param>
/// <param name="Quantity">How many units the batch holds.</param>
/// <param name="ProducedQuantity">How many units are already made.</param>
/// <param name="Position">The place of the batch in the queue of its factory.</param>
/// <param name="UnitSeconds">The time of one unit for the player, in game seconds.</param>
/// <param name="StartsAt">The moment the batch started, or <see langword="null"/>.</param>
/// <param name="NextUnitAt">The moment the next unit is ready, or <see langword="null"/>.</param>
/// <param name="FinishesAt">The moment the whole batch is ready, or <see langword="null"/>.</param>
/// <param name="IsBlocked">Whether the batch waits for a free place in the storage.</param>
public sealed record ProductionActiveOrderDto(
    long OrderId,
    long PlanetBuildingId,
    string BuildingNameKey,
    int RecipeId,
    string NameKey,
    long Quantity,
    long ProducedQuantity,
    int Position,
    long UnitSeconds,
    DateTimeOffset? StartsAt,
    DateTimeOffset? NextUnitAt,
    DateTimeOffset? FinishesAt,
    bool IsBlocked);

/// <summary>
/// The storage of the parts of a body: every kind of part has the same room of its own.
/// </summary>
/// <param name="Capacity">How many parts of every kind the storage holds.</param>
/// <param name="Kinds">How many parts every kind on the body holds against that room.</param>
public sealed record ProductionStorageDto(
    long Capacity,
    IReadOnlyList<ProductionPartKindDto> Kinds);

/// <summary>
/// The parts of one kind on a body against the room of the kind.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="Quantity">How many parts of the kind lie on the body, in every stack of the kind.</param>
/// <param name="Capacity">How many parts of the kind the storage holds.</param>
/// <param name="IsFull">Whether the kind has no room left, so the production of it waits.</param>
public sealed record ProductionPartKindDto(
    int PartTypeId,
    long Quantity,
    long Capacity,
    bool IsFull);

/// <summary>
/// The storage of the parts of a body with the stacks it holds.
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Stacks">The stacks of the parts.</param>
/// <param name="Storage">The storage of the parts.</param>
/// <param name="GameNow">The moment of game time.</param>
public sealed record ProductionStockDto(
    long PlanetId,
    IReadOnlyList<ProductionStackDto> Stacks,
    ProductionStorageDto Storage,
    DateTimeOffset GameNow);

/// <summary>
/// A stack of the parts on a body.
/// </summary>
/// <param name="PartTypeId">The part of the stack.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the parts carry the bonus of their race.</param>
/// <param name="ConditionPercent">The condition of the parts in per cent.</param>
/// <param name="Quantity">How many parts the stack holds.</param>
/// <param name="NameKey">The localisation key of the name of the part.</param>
/// <param name="ImageKey">The key of the picture of the part, or <see langword="null"/>.</param>
/// <param name="PartGroupId">The group of the part, so the client groups the parts by it (task 00020).</param>
/// <param name="PartGroupNameKey">The localisation key of the name of the group.</param>
public sealed record ProductionStackDto(
    int PartTypeId,
    int MarkLevel,
    bool HasRaceBonus,
    int ConditionPercent,
    long Quantity,
    string NameKey,
    string? ImageKey,
    int PartGroupId = 0,
    string? PartGroupNameKey = null);

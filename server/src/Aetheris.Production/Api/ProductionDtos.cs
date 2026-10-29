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
    IReadOnlyList<ProductionReasonDto> Reasons);

/// <summary>
/// A reason why a recipe is not available.
/// </summary>
/// <param name="Code">The localisation key of the reason.</param>
/// <param name="TargetId">What is required, when the reason names it.</param>
/// <param name="TargetLevel">The required level of the target.</param>
public sealed record ProductionReasonDto(
    string Code,
    int TargetId,
    int TargetLevel);

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
/// The storage of the parts of a body.
/// </summary>
/// <param name="Capacity">How many parts the storage holds.</param>
/// <param name="Stored">How many parts it holds now.</param>
/// <param name="FullAt">The moment the storage is or will be full.</param>
public sealed record ProductionStorageDto(
    long Capacity,
    long Stored,
    DateTimeOffset? FullAt);

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
public sealed record ProductionStackDto(
    int PartTypeId,
    int MarkLevel,
    bool HasRaceBonus,
    int ConditionPercent,
    long Quantity);

namespace Aetheris.Processes.Api;

/// <summary>
/// A building of a body that runs a process, with its recipes and its queue (task 00014).
/// </summary>
/// <param name="PlanetId">The body the building stands on.</param>
/// <param name="PlanetBuildingId">The building.</param>
/// <param name="BuildingTypeId">The type of the building.</param>
/// <param name="BuildingLevel">The level of the building.</param>
/// <param name="Recipes">The recipes of the processes the building runs.</param>
/// <param name="Queue">The orders of the queue.</param>
/// <param name="GameNow">The moment of game time.</param>
public sealed record ProcessBuildingDto(
    long PlanetId,
    long PlanetBuildingId,
    int BuildingTypeId,
    int BuildingLevel,
    IReadOnlyList<ProcessRecipeDto> Recipes,
    IReadOnlyList<ProcessOrderDto> Queue,
    DateTimeOffset GameNow);

/// <summary>
/// A building of a body that runs a process, without its recipes.
/// </summary>
/// <param name="PlanetId">The body the building stands on.</param>
/// <param name="PlanetBuildingId">The building.</param>
/// <param name="BuildingTypeId">The type of the building.</param>
/// <param name="Level">The level of the building.</param>
/// <param name="NameKey">The localisation key of the name of the building.</param>
public sealed record ProcessBuildingSummaryDto(
    long PlanetId,
    long PlanetBuildingId,
    int BuildingTypeId,
    int Level,
    string NameKey);

/// <summary>
/// A recipe of a process with the reasons why it is not available.
/// </summary>
/// <param name="RecipeId">The recipe.</param>
/// <param name="Code">The neutral code of the recipe.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="ProcessTypeNameKey">The localisation key of the name of the kind of the process.</param>
/// <param name="BuildingTypeId">The building the process runs in.</param>
/// <param name="SourceGroupId">The group of the resources the process takes.</param>
/// <param name="TargetGroupId">The group of the resources the process gives.</param>
/// <param name="DurationSeconds">How long one unit takes, in game seconds.</param>
/// <param name="ShipStub">The stub of the ship on the orbit of the body.</param>
/// <param name="IsAvailable">Whether the recipe can be ordered.</param>
/// <param name="Reasons">The reasons why it is not available.</param>
/// <param name="ImageKey">The key of the image.</param>
/// <param name="Ingredients">The ingredients of the recipe.</param>
public sealed record ProcessRecipeDto(
    int RecipeId,
    string Code,
    string NameKey,
    string DescriptionKey,
    string ProcessTypeNameKey,
    int? BuildingTypeId,
    int? SourceGroupId,
    int? TargetGroupId,
    long DurationSeconds,
    string? ShipStub,
    bool IsAvailable,
    IReadOnlyList<ProcessReasonDto> Reasons,
    string? ImageKey,
    IReadOnlyList<ProcessIngredientDto> Ingredients);

/// <summary>
/// An ingredient of a recipe of a process.
/// </summary>
/// <param name="ResourceTypeId">The resource the ingredient takes.</param>
/// <param name="PartTypeId">The part the ingredient takes.</param>
/// <param name="Amount">How many units one unit of the process takes.</param>
public sealed record ProcessIngredientDto(int? ResourceTypeId, int? PartTypeId, long Amount);

/// <summary>
/// A reason why a recipe of a process is not available.
/// </summary>
/// <param name="Code">The localisation key of the reason.</param>
/// <param name="TargetId">What is required, when the reason names it.</param>
/// <param name="TargetLevel">The level that is required.</param>
/// <param name="TargetNameKey">The localisation key of the name of what is required.</param>
public sealed record ProcessReasonDto(
    string Code,
    int TargetId,
    int TargetLevel,
    string? TargetNameKey);

/// <summary>
/// An order of a process in the queue of a building.
/// </summary>
/// <param name="OrderId">The order.</param>
/// <param name="RecipeId">The recipe of the order.</param>
/// <param name="Quantity">How many units the order holds.</param>
/// <param name="ProducedQuantity">How many units are already made.</param>
/// <param name="Position">The place of the order in the queue.</param>
/// <param name="StartsAt">The moment the order started.</param>
/// <param name="NextUnitAt">The moment the next unit is ready.</param>
/// <param name="FinishesAt">The moment the whole order is ready.</param>
/// <param name="IsBlocked">Whether the order waits for a free place in the storage.</param>
public sealed record ProcessOrderDto(
    long OrderId,
    int RecipeId,
    long Quantity,
    long ProducedQuantity,
    int Position,
    DateTimeOffset? StartsAt,
    DateTimeOffset? NextUnitAt,
    DateTimeOffset? FinishesAt,
    bool IsBlocked);

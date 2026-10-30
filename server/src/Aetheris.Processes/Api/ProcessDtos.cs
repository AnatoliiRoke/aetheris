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
/// <param name="FullConditionPercent">The condition of a new part, in per cent, so the client tells a damaged part from a new one.</param>
public sealed record ProcessBuildingDto(
    long PlanetId,
    long PlanetBuildingId,
    int BuildingTypeId,
    int BuildingLevel,
    IReadOnlyList<ProcessRecipeDto> Recipes,
    IReadOnlyList<ProcessOrderDto> Queue,
    DateTimeOffset GameNow,
    int FullConditionPercent);

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
/// <param name="NeedsShip">Whether the process needs a ship of the player on the orbit of the body.</param>
/// <param name="PartSubject">The part an order names, one of the logical codes of the part subjects.</param>
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
    bool NeedsShip,
    string PartSubject,
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

/// <summary>
/// The terraforming a player may start (task 00014, sections 9 and 11): the free bodies that
/// may be settled with the ships of the player by them and the devices in the holds of those
/// ships, and the range of the multiplier.
/// </summary>
/// <param name="Targets">The free bodies that may be settled and that a ship of the player stands by.</param>
/// <param name="MultiplierMin">The lowest multiplier a deposit can get.</param>
/// <param name="MultiplierMax">The highest multiplier a deposit can get.</param>
public sealed record TerraformingDto(
    IReadOnlyList<TerraformingTargetDto> Targets,
    decimal MultiplierMin,
    decimal MultiplierMax);

/// <summary>
/// A free body a device of the terraforming may be turned on at, with the ships of the player
/// that stand by it.
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="SystemId">The star system of the body.</param>
/// <param name="ObjectTypeId">The type of the body.</param>
/// <param name="Orbit">The orbit of the body.</param>
/// <param name="Name">The name of the body.</param>
/// <param name="IsTerraformed">Whether the body is terraformed already.</param>
/// <param name="ShipIds">The ships of the player by the body that carry a device in their holds.</param>
public sealed record TerraformingTargetDto(
    long PlanetId,
    long SystemId,
    int ObjectTypeId,
    int? Orbit,
    string Name,
    bool IsTerraformed,
    IReadOnlyList<TerraformingShipDto> ShipIds);

/// <summary>
/// A ship of the player by a body with the devices of the terraforming in its hold.
/// </summary>
/// <param name="ShipId">The ship.</param>
/// <param name="Devices">The devices of the terraforming in the hold of the ship.</param>
public sealed record TerraformingShipDto(
    long ShipId,
    IReadOnlyList<TerraformingDeviceDto> Devices);

/// <summary>
/// A device of the terraforming in the hold of a ship.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="NameKey">The localisation key of the name of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="Quantity">How many devices of the kind the hold carries.</param>
public sealed record TerraformingDeviceDto(
    int PartTypeId,
    string NameKey,
    int MarkLevel,
    long Quantity);

/// <summary>
/// The outcome of a device of the terraforming turned on at a free body.
/// </summary>
/// <param name="TargetPlanetId">The body that is terraformed.</param>
/// <param name="ShipId">The ship the device was taken from.</param>
/// <param name="PartTypeId">The device that is spent.</param>
/// <param name="ChangedDeposits">The number of the deposits that became richer.</param>
/// <param name="DepositIds">The deposits that became richer, each with the multiplier of its own roll.</param>
/// <param name="Terraforming">The terraforming of the colony after the change.</param>
public sealed record TerraformingResultDto(
    long TargetPlanetId,
    long ShipId,
    int PartTypeId,
    int ChangedDeposits,
    IReadOnlyList<TerraformedDepositDto> DepositIds,
    TerraformingDto Terraforming);

/// <summary>
/// A deposit that became richer by the terraforming.
/// </summary>
/// <param name="ResourceTypeId">The resource of the deposit.</param>
/// <param name="OldCoefficient">The coefficient before.</param>
/// <param name="NewCoefficient">The coefficient the roll gave the deposit.</param>
public sealed record TerraformedDepositDto(
    int ResourceTypeId,
    decimal OldCoefficient,
    decimal NewCoefficient);

using Aetheris.Production.Services;

namespace Aetheris.Processes.Services;

/// <summary>
/// What the check and the input of an order look at (task 00014, decision 6).
/// </summary>
/// <param name="PlayerId">The player who made the order.</param>
/// <param name="PlanetId">The body the process runs on.</param>
/// <param name="PlanetBuildingId">The building the process runs in.</param>
/// <param name="Recipe">The recipe of the order.</param>
/// <param name="ProcessType">The kind of the process.</param>
/// <param name="Quantity">How many units the order holds.</param>
/// <param name="BuildingLevel">The level of the building the process runs in.</param>
/// <param name="RequirementContext">The levels and the names the requirements are checked against.</param>
/// <param name="Request">What the player asked for.</param>
/// <param name="ResourceGroups">The group of every resource the player named, so a handler that works on a resource can check its group.</param>
/// <param name="Catalog">The catalog of the processes.</param>
public sealed record ProcessCheckContext(
    string PlayerId,
    long PlanetId,
    long PlanetBuildingId,
    ProcessRecipeInfo Recipe,
    ProcessTypeInfo ProcessType,
    long Quantity,
    int BuildingLevel,
    ProcessRequirementContext RequirementContext,
    ProcessOrderRequest Request,
    IReadOnlyDictionary<int, int> ResourceGroups,
    ProcessCatalog Catalog);

/// <summary>
/// What the result of a unit of an order looks at (task 00014, decision 6).
/// </summary>
/// <param name="PlayerId">The player who made the order.</param>
/// <param name="PlanetId">The body the process runs on.</param>
/// <param name="PlanetBuildingId">The building the process runs in.</param>
/// <param name="Recipe">The recipe of the order.</param>
/// <param name="ProcessType">The kind of the process.</param>
/// <param name="Quantity">How many units the whole order holds.</param>
/// <param name="UnitIndex">The unit that is made, counted from one.</param>
/// <param name="BuildingLevel">The level of the building the process runs in.</param>
/// <param name="TechnologyLevels">The levels of the technologies of the player.</param>
/// <param name="Catalog">The catalog of the processes.</param>
/// <param name="Request">What the player asked for.</param>
/// <param name="TakenParts">The parts the whole order took from the storage, as they were.</param>
public sealed record ProcessApplyContext(
    string PlayerId,
    long PlanetId,
    long PlanetBuildingId,
    ProcessRecipeInfo Recipe,
    ProcessTypeInfo ProcessType,
    long Quantity,
    long UnitIndex,
    int BuildingLevel,
    IReadOnlyDictionary<int, int> TechnologyLevels,
    ProcessCatalog Catalog,
    ProcessOrderRequest Request,
    IReadOnlyList<PartStackTaken> TakenParts);

/// <summary>
/// What the player asked for: the numbers of the order that are not in the recipe (task
/// 00014).
/// </summary>
/// <param name="Quantity">How many units the order holds.</param>
/// <param name="SourceResourceTypeId">The resource the conversion takes, for the processes that convert one resource into another.</param>
/// <param name="TargetResourceTypeId">The resource the conversion gives, for the processes that convert one resource into another.</param>
/// <param name="PartTypeId">The part the process works on, for the recycling, the repair and the replacement.</param>
/// <param name="PartConditionPercent">The condition of the part the process works on, in per cent; a new part when it is not named.</param>
public sealed record ProcessOrderRequest(
    long Quantity,
    int? SourceResourceTypeId = null,
    int? TargetResourceTypeId = null,
    int? PartTypeId = null,
    int? PartConditionPercent = null);

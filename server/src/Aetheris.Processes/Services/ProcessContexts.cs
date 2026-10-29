using Aetheris.Processes.Data.Entities;

namespace Aetheris.Processes.Services;

/// <summary>
/// What the check of an order looks at (task 00014, decision 6).
/// </summary>
/// <param name="PlayerId">The player who made the order.</param>
/// <param name="PlanetId">The body the process runs on.</param>
/// <param name="PlanetBuildingId">The building the process runs in.</param>
/// <param name="Recipe">The recipe of the order.</param>
/// <param name="ProcessType">The kind of the process.</param>
/// <param name="Quantity">How many units the order holds.</param>
/// <param name="Requirements">The requirements of the recipe.</param>
/// <param name="RequirementContext">The levels and the names the requirements are checked against.</param>
/// <param name="Request">What the player asked for.</param>
/// <param name="ResourceGroups">The group of every resource the player named, so a handler that works on a resource can check its group.</param>
public sealed record ProcessCheckContext(
    string PlayerId,
    long PlanetId,
    long PlanetBuildingId,
    ProcessRecipeInfo Recipe,
    ProcessTypeInfo ProcessType,
    long Quantity,
    IReadOnlyList<ProcessRequirementInfo> Requirements,
    ProcessRequirementContext RequirementContext,
    ProcessOrderRequest Request,
    IReadOnlyDictionary<int, int> ResourceGroups);

/// <summary>
/// What the result of an order looks at (task 00014, decision 6).
/// </summary>
/// <param name="PlayerId">The player who made the order.</param>
/// <param name="PlanetId">The body the process runs on.</param>
/// <param name="PlanetBuildingId">The building the process runs in.</param>
/// <param name="Recipe">The recipe of the order.</param>
/// <param name="ProcessType">The kind of the process.</param>
/// <param name="Quantity">How many units of the order are made at once.</param>
/// <param name="BuildingLevel">The level of the building the process runs in.</param>
/// <param name="TechnologyLevels">The levels of the technologies of the player.</param>
/// <param name="Catalog">The catalog of the processes.</param>
/// <param name="Request">What the player asked for.</param>
public sealed record ProcessApplyContext(
    string PlayerId,
    long PlanetId,
    long PlanetBuildingId,
    ProcessRecipeInfo Recipe,
    ProcessTypeInfo ProcessType,
    long Quantity,
    int BuildingLevel,
    IReadOnlyDictionary<int, int> TechnologyLevels,
    ProcessCatalog Catalog,
    ProcessOrderRequest Request);

/// <summary>
/// What the player asked for: the numbers of the order that are not in the recipe (task
/// 00014).
/// </summary>
/// <param name="Quantity">How many units the order holds.</param>
/// <param name="SourceResourceTypeId">The resource the conversion takes, for the processes that convert one resource into another.</param>
/// <param name="TargetResourceTypeId">The resource the conversion gives, for the processes that convert one resource into another.</param>
/// <param name="PartTypeId">The part the process works on, for the recycling and the repair.</param>
/// <param name="ShipStub">The stub of the ship on the orbit of the body.</param>
public sealed record ProcessOrderRequest(
    long Quantity,
    int? SourceResourceTypeId = null,
    int? TargetResourceTypeId = null,
    int? PartTypeId = null,
    string? ShipStub = null);

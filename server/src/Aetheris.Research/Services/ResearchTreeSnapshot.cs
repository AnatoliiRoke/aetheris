using Aetheris.Planets;
using Aetheris.Requirements.Services;
using Aetheris.Research.Data.Entities;

namespace Aetheris.Research.Services;

/// <summary>
/// The tree of the technologies and the queue of a player, as they are in memory (task 00012,
/// section 3.2).
/// </summary>
/// <param name="Catalog">The catalog of the technologies.</param>
/// <param name="Queue">The records of the queue, in their order.</param>
/// <param name="LearnedLevels">The levels the player has learned, by technology.</param>
/// <param name="QueueSlots">The places of the queue, the research under way included.</param>
/// <param name="SelectedPlanetId">The body the research starts from, or <see langword="null"/> when the player has no body.</param>
/// <param name="AvailableCenterLevel">The level of the science centre of the selected body.</param>
/// <param name="Planets">The bodies of the player with the level of their science centre.</param>
/// <param name="Requirements">The requirements of the next level of every technology.</param>
/// <param name="BuildingNameKeys">The localisation keys of the names of the building types.</param>
/// <param name="GameNow">The moment of game time of the answer.</param>
/// <param name="TimeMultiplier">The game seconds per real second.</param>
/// <param name="UnlockNameKeys">The localisation keys of the names of the subjects of the other modules a technology opens, by kind.</param>
/// <param name="SelectedBalance">The balance of the selected body by resource, as of the moment of the answer; the tree shows what a level is short of.</param>
/// <param name="CurrencyBalances">The balances of the currencies of the civilization, as of the moment of the answer; a cost in a currency is checked against them and not against the body (task 00015, decision 5).</param>
/// <param name="CurrencyResourceIds">The resources that belong to the whole civilization, by resource; they are paid from the balance of the civilization and not from the balance of the body.</param>
/// <param name="BuildingLevels">The built levels of the selected body by building type. A requirement on a building is checked against them.</param>
/// <param name="DepositResourceIds">The resources that have a deposit on the selected body.</param>
/// <param name="ResourceNameKeys">The localisation keys of the names of the resources, for a requirement on a deposit.</param>
public sealed record ResearchTreeSnapshot(
    TechnologyCatalog Catalog,
    IReadOnlyList<ResearchQueueItemEntity> Queue,
    IReadOnlyDictionary<int, int> LearnedLevels,
    int QueueSlots,
    long? SelectedPlanetId,
    int AvailableCenterLevel,
    IReadOnlyList<PlanetBuildingLevel> Planets,
    IReadOnlyList<SubjectRequirement> Requirements,
    IReadOnlyDictionary<int, string> BuildingNameKeys,
    DateTimeOffset GameNow,
    double TimeMultiplier,
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>>? UnlockNameKeys = null,
    IReadOnlyDictionary<int, long>? SelectedBalance = null,
    IReadOnlyDictionary<int, long>? CurrencyBalances = null,
    IReadOnlySet<int>? CurrencyResourceIds = null,
    IReadOnlyDictionary<int, int>? BuildingLevels = null,
    IReadOnlySet<int>? DepositResourceIds = null,
    IReadOnlyDictionary<int, string>? ResourceNameKeys = null,
    decimal ResearchTimeMultiplier = 1m);

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
/// <param name="SelectedPlanetId">The body the research starts from.</param>
/// <param name="AvailableCenterLevel">The level of the science centre of the selected body.</param>
public sealed record ResearchTreeSnapshot(
    TechnologyCatalog Catalog,
    IReadOnlyList<ResearchQueueItemEntity> Queue,
    IReadOnlyDictionary<int, int> LearnedLevels,
    int QueueSlots,
    long? SelectedPlanetId,
    int AvailableCenterLevel);

namespace Aetheris.Research.Api;

/// <summary>
/// A technology of the tree with its state for a player (task 00012, section 3.2).
/// </summary>
/// <param name="Id">The technology.</param>
/// <param name="Code">The neutral code of the technology.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="BranchId">The branch of the tree.</param>
/// <param name="Branch">The neutral code of the branch, for the grouping of the client.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="MaxLevel">The largest level of the technology.</param>
/// <param name="LearnedLevel">The level the player has learned; zero when the player knows nothing.</param>
/// <param name="Status">The state of the technology, one of <see cref="ResearchTechnologyStatuses"/>.</param>
/// <param name="ResearchCenterLevel">The level of the science centre the research of the next level needs.</param>
/// <param name="AvailableCenterLevel">The level of the science centre of the chosen body.</param>
/// <param name="ReasonCode">The localisation key of the reason the technology is not available, or <see langword="null"/>.</param>
/// <param name="Levels">The levels of the technology.</param>
/// <param name="CurrentEffects">What the technology gives at the level the player has learned, as the server counts it; empty when nothing is learned yet.</param>
public sealed record TechnologyDto(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    int BranchId,
    string Branch,
    int SortOrder,
    int MaxLevel,
    int LearnedLevel,
    string Status,
    int ResearchCenterLevel,
    int AvailableCenterLevel,
    string? ReasonCode,
    IReadOnlyList<TechnologyLevelDto> Levels,
    IReadOnlyList<TechnologyEffectDto>? CurrentEffects = null);

/// <summary>
/// A level of a technology with its time, its costs and its effects (task 00012, section 3.2).
/// </summary>
/// <param name="Level">The level.</param>
/// <param name="DurationSeconds">The time of the research in game seconds.</param>
/// <param name="Costs">The costs by resource.</param>
/// <param name="Effects">The effects of the level.</param>
/// <param name="UnmetRequirementIds">The identifiers of the requirements that are not met.</param>
/// <param name="MissingResources">For the next level of a technology the player can still research: the resources the selected body is short of, by how much; empty for every other level.</param>
public sealed record TechnologyLevelDto(
    int Level,
    long DurationSeconds,
    IReadOnlyList<ResourceAmountDto> Costs,
    IReadOnlyList<TechnologyEffectDto> Effects,
    IReadOnlyList<int> UnmetRequirementIds,
    IReadOnlyList<ResourceAmountDto>? MissingResources = null);

/// <summary>
/// An effect of a level of a technology (task 00012, section 3.2).
/// </summary>
/// <param name="EffectType">The kind of the effect, one of <see cref="Services.TechnologyEffectTypes"/>.</param>
/// <param name="UnlockKind">The kind of the subject the effect opens, one of <see cref="Services.TechnologyUnlockKinds"/>.</param>
/// <param name="UnlockTargetId">The subject the effect opens.</param>
/// <param name="UnlockNameKey">The localisation key of the name of the subject the effect opens.</param>
/// <param name="Value">The value of the effect.</param>
public sealed record TechnologyEffectDto(
    string EffectType,
    string? UnlockKind,
    int? UnlockTargetId,
    string? UnlockNameKey,
    decimal Value);

/// <summary>
/// An amount of a resource (task 00012, section 3.2).
/// </summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Amount">The amount.</param>
public sealed record ResourceAmountDto(int ResourceTypeId, long Amount);

/// <summary>
/// A record of the queue of the researches of a player (task 00012, section 3.2).
/// </summary>
/// <param name="Id">The record.</param>
/// <param name="TechnologyId">The technology.</param>
/// <param name="TechnologyNameKey">The localisation key of the name of the technology.</param>
/// <param name="TargetLevel">The level the record researches.</param>
/// <param name="PlanetId">The body the research starts from.</param>
/// <param name="Position">The position of the record in the queue.</param>
/// <param name="StartsAt">The moment the research starts.</param>
/// <param name="FinishesAt">The moment the research finishes, or <see langword="null"/> when it waits.</param>
/// <param name="Costs">The resources the record paid for.</param>
public sealed record ResearchQueueItemDto(
    int Id,
    int TechnologyId,
    string TechnologyNameKey,
    int TargetLevel,
    long PlanetId,
    int Position,
    DateTimeOffset StartsAt,
    DateTimeOffset? FinishesAt,
    IReadOnlyList<ResourceAmountDto> Costs);

/// <summary>
/// The tree of the technologies of a player with the queue of the researches (task 00012,
/// section 3.2).
/// </summary>
/// <param name="Branches">The branches of the tree.</param>
/// <param name="Technologies">The technologies of every branch.</param>
/// <param name="Queue">The records of the queue.</param>
/// <param name="QueueSlots">The places of the queue, the research under way included.</param>
/// <param name="QueueUsedSlots">The places of the queue that are taken.</param>
/// <param name="SelectedPlanetId">The body the research starts from.</param>
/// <param name="AvailableCenterLevel">The level of the science centre of the selected body.</param>
/// <param name="Planets">The bodies of the player the research can start from.</param>
/// <param name="GameNow">The moment of game time of the answer, for the timers of the client.</param>
/// <param name="TimeMultiplier">The game seconds per real second.</param>
public sealed record ResearchTreeDto(
    IReadOnlyList<ResearchBranchDto> Branches,
    IReadOnlyList<TechnologyDto> Technologies,
    IReadOnlyList<ResearchQueueItemDto> Queue,
    int QueueSlots,
    int QueueUsedSlots,
    long? SelectedPlanetId,
    int AvailableCenterLevel,
    IReadOnlyList<ResearchPlanetDto> Planets,
    DateTimeOffset GameNow,
    double TimeMultiplier);

/// <summary>
/// A body of the player the research can start from (task 00012, decisions 5 and 5a).
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Name">The name of the body.</param>
/// <param name="ResearchCenterLevel">The level of the science centre of the body.</param>
/// <param name="IsAvailable">Whether a research can start from the body.</param>
/// <param name="ReasonCode">Why a research cannot start from the body.</param>
public sealed record ResearchPlanetDto(long PlanetId, string? Name, int ResearchCenterLevel, bool IsAvailable, string? ReasonCode);

/// <summary>
/// A branch of the tree of the technologies (task 00012, section 3.2; task 00023, decision 8).
/// </summary>
/// <param name="Id">The branch.</param>
/// <param name="Branch">The neutral code of the branch.</param>
/// <param name="NameKey">The localisation key of the name of the branch.</param>
public sealed record ResearchBranchDto(int Id, string Branch, string NameKey);

/// <summary>
/// The states of a technology for a player (task 00012, section 3.2).
/// </summary>
public static class ResearchTechnologyStatuses
{
    /// <summary>The technology lies beyond what the player can reach: a technology it needs is itself out of reach, so the tree shows the card dimmed and without details.</summary>
    public const string Unknown = "unknown";

    /// <summary>The player knows the technology and may research its next level.</summary>
    public const string Available = "available";

    /// <summary>The technology waits in the queue.</summary>
    public const string Queued = "queued";

    /// <summary>The technology is under way.</summary>
    public const string Researching = "researching";

    /// <summary>The player has learned every level of the technology.</summary>
    public const string Learned = "learned";

    /// <summary>The player cannot research the technology yet, and the reason says why.</summary>
    public const string Unavailable = "unavailable";
}

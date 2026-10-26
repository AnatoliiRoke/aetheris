using System.Text.Json;

namespace Aetheris.Planets.Api;

/// <summary>
/// The JSON settings of the bodies API: snake_case names (documentation/tech/08, rule 3).
/// </summary>
public static class PlanetsJson
{
    /// <summary>
    /// Gets the serializer settings.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

/// <summary>Everything the client shows about a body: buildings, queue, balance, storage and energy.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="GameNow">The game time of the answer; the timers of the queue count from it.</param>
/// <param name="TimeMultiplier">The game seconds per real second.</param>
/// <param name="Resources">The balance of the body.</param>
/// <param name="StorageGroups">The storage by resource group.</param>
/// <param name="Energy">The energy of the body.</param>
/// <param name="Queue">The construction queue.</param>
/// <param name="Buildings">Every building type with its level and the next level.</param>
/// <param name="PopulationMax">The largest population the buildings allow.</param>
/// <param name="ResourceTypes">The names of the resources.</param>
/// <param name="ResourceGroups">The names of the resource groups.</param>
public sealed record PlanetOverviewDto(
    long PlanetId,
    DateTimeOffset GameNow,
    double TimeMultiplier,
    IReadOnlyList<PlanetResourceDto> Resources,
    IReadOnlyList<StorageGroupDto> StorageGroups,
    EnergyDto Energy,
    ConstructionQueueDto Queue,
    IReadOnlyList<BuildingDto> Buildings,
    long PopulationMax,
    IReadOnlyList<NamedItemDto> ResourceTypes,
    IReadOnlyList<NamedItemDto> ResourceGroups);

/// <summary>The amount of a resource on a body now.</summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="ResourceGroupId">The group of the resource.</param>
/// <param name="Amount">The amount now.</param>
/// <param name="RatePerHour">The production per game hour, zero while the group is full.</param>
public sealed record PlanetResourceDto(int ResourceTypeId, int ResourceGroupId, long Amount, decimal RatePerHour);

/// <summary>The storage of a resource group.</summary>
/// <param name="ResourceGroupId">The group.</param>
/// <param name="Capacity">The capacity.</param>
/// <param name="Stored">The sum of the amounts of the group.</param>
/// <param name="IsFull">Whether the group is full and its producers stand still.</param>
/// <param name="FullAt">The moment of game time the group is or will be full.</param>
public sealed record StorageGroupDto(int ResourceGroupId, long Capacity, long Stored, bool IsFull, DateTimeOffset? FullAt);

/// <summary>The energy of a body.</summary>
/// <param name="Output">The energy produced.</param>
/// <param name="Consumption">The energy consumed.</param>
/// <param name="Balance">The output minus the consumption.</param>
/// <param name="QueuedBalance">The balance once every queued building is done.</param>
public sealed record EnergyDto(long Output, long Consumption, long Balance, long QueuedBalance);

/// <summary>The construction queue of a body.</summary>
/// <param name="SlotCount">The places of the queue, the building under way included.</param>
/// <param name="Items">The records in the order of the queue.</param>
public sealed record ConstructionQueueDto(int SlotCount, IReadOnlyList<QueueItemDto> Items);

/// <summary>A record of the construction queue.</summary>
/// <param name="Id">The record.</param>
/// <param name="BuildingTypeId">The building type.</param>
/// <param name="TargetLevel">The level the building reaches.</param>
/// <param name="Position">The place in the queue, from 1.</param>
/// <param name="DurationSeconds">The construction time in game seconds.</param>
/// <param name="StartedAt">The start, or <see langword="null"/> while the record waits.</param>
/// <param name="FinishesAt">The end, or <see langword="null"/> while the record waits.</param>
/// <param name="Costs">The cost paid, returned in full on a cancel.</param>
public sealed record QueueItemDto(
    long Id,
    int BuildingTypeId,
    int TargetLevel,
    int Position,
    long DurationSeconds,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishesAt,
    IReadOnlyList<ResourceAmountDto> Costs);

/// <summary>A building type on a body.</summary>
/// <param name="BuildingTypeId">The building type.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="Level">The level built, zero when there is no building.</param>
/// <param name="QueuedLevel">The level once the queue is done.</param>
/// <param name="MaxLevel">The largest level.</param>
/// <param name="Effects">The effects of the level built.</param>
/// <param name="Next">The next level that can be queued, with the reasons it cannot.</param>
public sealed record BuildingDto(
    int BuildingTypeId,
    string Code,
    string NameKey,
    string DescriptionKey,
    int Level,
    int QueuedLevel,
    int MaxLevel,
    IReadOnlyList<EffectDto> Effects,
    NextLevelDto Next);

/// <summary>The next level of a building and whether it can be queued now.</summary>
/// <param name="Level">The level.</param>
/// <param name="DurationSeconds">The construction time in game seconds, if the level exists.</param>
/// <param name="Costs">The cost.</param>
/// <param name="Effects">The effects at that level.</param>
/// <param name="EnergyBalanceAfter">The energy balance once the queue and this level are done.</param>
/// <param name="CanEnqueue">Whether the server would accept it now.</param>
/// <param name="ReasonCodes">The reasons it cannot, as localisation keys.</param>
/// <param name="UnmetRequirements">The requirements that are not met.</param>
/// <param name="MissingResources">The resources that are short, by how much.</param>
public sealed record NextLevelDto(
    int Level,
    long? DurationSeconds,
    IReadOnlyList<ResourceAmountDto> Costs,
    IReadOnlyList<EffectDto> Effects,
    long EnergyBalanceAfter,
    bool CanEnqueue,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<RequirementDto> UnmetRequirements,
    IReadOnlyList<ResourceAmountDto> MissingResources);

/// <summary>An amount of a resource.</summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Amount">The amount.</param>
public sealed record ResourceAmountDto(int ResourceTypeId, long Amount);

/// <summary>An effect of a building level.</summary>
/// <param name="EffectType">The effect type.</param>
/// <param name="ResourceTypeId">The resource of a production effect.</param>
/// <param name="ResourceGroupId">The group of a storage effect.</param>
/// <param name="Value">The value at the level.</param>
public sealed record EffectDto(string EffectType, int? ResourceTypeId, int? ResourceGroupId, long Value);

/// <summary>A requirement that is not met.</summary>
/// <param name="RequirementId">The requirement.</param>
/// <param name="RequirementType">The requirement type.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The required level.</param>
/// <param name="ReasonCode">The localisation key of the reason.</param>
/// <param name="TargetNameKey">The localisation key of the name of the target, when the server knows it.</param>
public sealed record RequirementDto(int RequirementId, string RequirementType, int TargetId, int TargetLevel, string ReasonCode, string? TargetNameKey);

/// <summary>A row of a reference table with its localisation key.</summary>
/// <param name="Id">The row.</param>
/// <param name="Code">The stable code.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="ResourceGroupId">The group, for a resource.</param>
public sealed record NamedItemDto(int Id, string Code, string NameKey, int? ResourceGroupId);

/// <summary>The answer of the requirement check of a subject (task 00011, decision 3).</summary>
/// <param name="IsAvailable">Whether every requirement is met.</param>
/// <param name="UnmetRequirements">The requirements that are not met.</param>
public sealed record AvailabilityDto(bool IsAvailable, IReadOnlyList<RequirementDto> UnmetRequirements);

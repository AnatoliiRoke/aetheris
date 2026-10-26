using System.Text.Json.Serialization;

namespace Aetheris.World.Api;

/// <summary>A generation run requested from the admin panel (task 00007, decision 3).</summary>
/// <param name="SectorCount">The number of sectors to add.</param>
/// <param name="Seed">The seed, or <see langword="null"/> to derive the seeds from the galaxy seed.</param>
/// <param name="RuleSet">The code of the rule set.</param>
public sealed record StartGenerationRequest(
    [property: JsonPropertyName("sector_count")] int SectorCount,
    [property: JsonPropertyName("seed")] long? Seed,
    [property: JsonPropertyName("rule_set")] string? RuleSet);

/// <summary>A rule set a generation run can use.</summary>
/// <param name="Code">The code.</param>
/// <param name="RuleGroupCount">The rule groups of the set.</param>
/// <param name="RuleCount">The rules of the set.</param>
public sealed record GenerationRuleSetDto(string Code, int RuleGroupCount, int RuleCount);

/// <summary>What a generation run would create, shown before the confirmation.</summary>
/// <param name="SectorCount">The number of sectors.</param>
/// <param name="EstimatedSystemCount">The expected number of star systems.</param>
/// <param name="SystemsMin">The smallest number of systems of a sector.</param>
/// <param name="SystemsMax">The largest number of systems of a sector.</param>
/// <param name="MaxSectorsPerRun">The largest number of sectors of one run.</param>
/// <param name="MaxSystemsPerRun">The largest expected number of systems of one run.</param>
/// <param name="Allowed">Whether the run fits the limits.</param>
/// <param name="ActiveRunId">The run that is queued or running now, if any.</param>
/// <param name="StatusPollSeconds">How often the client asks for the status of a run.</param>
/// <param name="RuleSets">The rule sets.</param>
public sealed record GenerationEstimateDto(
    int SectorCount,
    int EstimatedSystemCount,
    int SystemsMin,
    int SystemsMax,
    int MaxSectorsPerRun,
    int MaxSystemsPerRun,
    bool Allowed,
    long? ActiveRunId,
    int StatusPollSeconds,
    IReadOnlyList<GenerationRuleSetDto> RuleSets);

/// <summary>A sector created by a generation run.</summary>
/// <param name="SectorId">The sector.</param>
/// <param name="Seed">The seed it was generated from.</param>
public sealed record GeneratedSectorDto(int SectorId, long Seed);

/// <summary>What a generation run created.</summary>
/// <param name="SectorIds">The new sectors.</param>
/// <param name="Sectors">The new sectors with their seeds, in the same order.</param>
/// <param name="SystemCount">The new star systems.</param>
/// <param name="ObjectCountsByClass">The new celestial objects by class: planets, gas giants, fields and the rest.</param>
public sealed record GenerationSummaryDto(
    IReadOnlyList<int> SectorIds,
    IReadOnlyList<GeneratedSectorDto> Sectors,
    int SystemCount,
    IReadOnlyDictionary<string, int> ObjectCountsByClass);

/// <summary>A generation run.</summary>
/// <param name="Id">The run.</param>
/// <param name="InitiatorId">The administrator who started it.</param>
/// <param name="SectorCount">The number of sectors asked for.</param>
/// <param name="Seed">The seed, if one was given.</param>
/// <param name="RuleSet">The rule set.</param>
/// <param name="EstimatedSystemCount">The expected number of systems shown at the confirmation.</param>
/// <param name="Status">The status.</param>
/// <param name="CreatedAt">When the run was requested.</param>
/// <param name="StartedAt">When the work started.</param>
/// <param name="FinishedAt">When the work ended.</param>
/// <param name="Summary">What was created.</param>
/// <param name="Error">The error code of a failed run.</param>
public sealed record GenerationRunDto(
    long Id,
    string InitiatorId,
    int SectorCount,
    long? Seed,
    string RuleSet,
    int EstimatedSystemCount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    GenerationSummaryDto Summary,
    string? Error);

/// <summary>A sector row of the admin panel.</summary>
/// <param name="Id">The sector.</param>
/// <param name="Name">The name.</param>
/// <param name="Seed">The seed.</param>
/// <param name="LayoutIndex">The index on the layout spiral.</param>
/// <param name="IsNewbie">Whether newcomers start there.</param>
/// <param name="X">The x of the origin.</param>
/// <param name="Y">The y of the origin.</param>
/// <param name="Z">The z of the origin.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="SystemCount">The star systems.</param>
/// <param name="BodyCount">The bodies on orbits.</param>
/// <param name="OwnedCount">The bodies with an owner.</param>
/// <param name="FieldCount">The fields that still exist.</param>
public sealed record AdminSectorRow(
    int Id,
    string Name,
    long Seed,
    int LayoutIndex,
    bool IsNewbie,
    double X,
    double Y,
    double Z,
    DateTimeOffset CreatedAt,
    int SystemCount,
    int BodyCount,
    int OwnedCount,
    int FieldCount);

/// <summary>A star system row of the admin panel.</summary>
/// <param name="Id">The system.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SectorName">The name of the sector.</param>
/// <param name="Number">The number within the sector.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x within the sector.</param>
/// <param name="Y">The y within the sector.</param>
/// <param name="Z">The z within the sector.</param>
/// <param name="InNebula">Whether it lies inside a nebula.</param>
/// <param name="NewbieCount">The newcomers who started there.</param>
/// <param name="BodyCount">The bodies on its orbits.</param>
/// <param name="FieldCount">The fields that still exist in it.</param>
public sealed record AdminSystemRow(
    long Id,
    int SectorId,
    string SectorName,
    int Number,
    string Name,
    double X,
    double Y,
    double Z,
    bool InNebula,
    int NewbieCount,
    int BodyCount,
    int FieldCount);

/// <summary>A share of a resource in a body or a field.</summary>
/// <param name="ResourceId">The resource type.</param>
/// <param name="ResourceCode">The code of the resource type.</param>
/// <param name="Coefficient">The share.</param>
public sealed record AdminShareDto(int ResourceId, string ResourceCode, decimal Coefficient);

/// <summary>A body row of the admin panel: a planet or a gas giant.</summary>
/// <param name="Id">The body.</param>
/// <param name="CelestialObjectId">The celestial object.</param>
/// <param name="Name">The name.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="ObjectTypeCode">The code of the type.</param>
/// <param name="ObjectClass">The class of the type.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SystemId">The star system.</param>
/// <param name="SystemName">The name of the star system.</param>
/// <param name="Orbit">The orbit.</param>
/// <param name="SizeClassCode">The code of the size class.</param>
/// <param name="Size">The size.</param>
/// <param name="OwnerPlayerId">The owner, if any.</param>
/// <param name="DepositIds">The deposits.</param>
public sealed record AdminBodyRow(
    long Id,
    long CelestialObjectId,
    string Name,
    int ObjectTypeId,
    string ObjectTypeCode,
    string ObjectClass,
    int SectorId,
    long SystemId,
    string SystemName,
    int? Orbit,
    string SizeClassCode,
    int Size,
    string? OwnerPlayerId,
    IReadOnlyList<AdminShareDto> DepositIds);

/// <summary>An asteroid field row of the admin panel.</summary>
/// <param name="Id">The field.</param>
/// <param name="CelestialObjectId">The celestial object.</param>
/// <param name="Name">The name.</param>
/// <param name="ObjectTypeCode">The code of the type.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SystemId">The star system, or <see langword="null"/> for a free field.</param>
/// <param name="SystemName">The name of the star system.</param>
/// <param name="InitialMass">The mass at creation.</param>
/// <param name="Mass">The mass left.</param>
/// <param name="DepletedAt">When the field was used up.</param>
/// <param name="CompositionIds">The composition.</param>
public sealed record AdminFieldRow(
    long Id,
    long CelestialObjectId,
    string Name,
    string ObjectTypeCode,
    int SectorId,
    long? SystemId,
    string? SystemName,
    long InitialMass,
    long Mass,
    DateTimeOffset? DepletedAt,
    IReadOnlyList<AdminShareDto> CompositionIds);

/// <summary>The filters of the body table.</summary>
/// <param name="SectorId">Only this sector.</param>
/// <param name="SystemId">Only this star system.</param>
/// <param name="ObjectTypeId">Only this type.</param>
/// <param name="ObjectClass">Only this class.</param>
/// <param name="Owned">Only the owned or only the free bodies.</param>
public sealed record AdminBodyFilter(int? SectorId, long? SystemId, int? ObjectTypeId, string? ObjectClass, bool? Owned);

/// <summary>The filters of the field table.</summary>
/// <param name="SectorId">Only this sector.</param>
/// <param name="SystemId">Only this star system.</param>
/// <param name="Free">Only the free fields or only the fields in systems.</param>
/// <param name="Depleted">Only the used up or only the remaining fields.</param>
public sealed record AdminFieldFilter(int? SectorId, long? SystemId, bool? Free, bool? Depleted);

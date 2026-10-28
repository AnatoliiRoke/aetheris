using System.Text.Json.Serialization;

namespace Aetheris.World.Api;

/// <summary>A resource with its coefficient sent by the admin panel: a share of a field or a deposit of a body.</summary>
/// <param name="ResourceId">The resource type.</param>
/// <param name="Coefficient">The share of a field or the richness multiplier of a deposit.</param>
public sealed record AdminShareInput(
    [property: JsonPropertyName("resource_id")] int ResourceId,
    [property: JsonPropertyName("coefficient")] decimal Coefficient);

/// <summary>
/// The new state of an asteroid field (task 00009, decision 2). The whole composition is sent
/// and replaces the old one; the rest of the mass is empty rock.
/// </summary>
/// <param name="Version">The version the administrator saw.</param>
/// <param name="Reason">Why the field is changed; written to the journal.</param>
/// <param name="Name">The name.</param>
/// <param name="ObjectTypeId">The field type.</param>
/// <param name="X">The x within the sector; a field on an orbit keeps the one of its system.</param>
/// <param name="Y">The y within the sector.</param>
/// <param name="Z">The z within the sector.</param>
/// <param name="Mass">The mass that is left.</param>
/// <param name="InitialMass">The mass at creation.</param>
/// <param name="Depleted">Whether the field is used up.</param>
/// <param name="CompositionIds">The composition.</param>
public sealed record AdminFieldUpdate(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("object_type_id")] int ObjectTypeId,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z,
    [property: JsonPropertyName("mass")] long Mass,
    [property: JsonPropertyName("initial_mass")] long InitialMass,
    [property: JsonPropertyName("depleted")] bool Depleted,
    [property: JsonPropertyName("composition_ids")] IReadOnlyList<AdminShareInput>? CompositionIds);

/// <summary>
/// The new state of a body: a planet or a gas giant (task 00009, decision 2). The owner and the
/// name the owner gave are data of the player and are not part of the request (decision 5).
/// </summary>
/// <param name="Version">The version the administrator saw.</param>
/// <param name="Reason">Why the body is changed; written to the journal.</param>
/// <param name="Name">The generated name of the object.</param>
/// <param name="ObjectTypeId">The type, of the same class.</param>
/// <param name="SizeClassId">The size class.</param>
/// <param name="Size">The size, within the size class.</param>
/// <param name="Orbit">The orbit, free in the system.</param>
/// <param name="DepositIds">The deposits.</param>
public sealed record AdminBodyUpdate(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("object_type_id")] int ObjectTypeId,
    [property: JsonPropertyName("size_class_id")] int SizeClassId,
    [property: JsonPropertyName("size")] int Size,
    [property: JsonPropertyName("orbit")] int Orbit,
    [property: JsonPropertyName("deposit_ids")] IReadOnlyList<AdminShareInput>? DepositIds);

/// <summary>The new state of a star system (task 00009, decision 2).</summary>
/// <param name="Version">The version the administrator saw.</param>
/// <param name="Reason">Why the system is changed; written to the journal.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x within the sector; the objects of the system move with it.</param>
/// <param name="Y">The y within the sector.</param>
/// <param name="Z">The z within the sector.</param>
/// <param name="StarTypeId">The type of the star.</param>
public sealed record AdminSystemUpdate(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z,
    [property: JsonPropertyName("star_type_id")] int StarTypeId);

/// <summary>The new values of an object type (task 00009, decision 7).</summary>
/// <param name="Version">The version the administrator saw.</param>
/// <param name="Reason">Why the type is changed; written to the journal.</param>
/// <param name="GenerationWeight">The weight among the types of the class; zero switches the type off for new objects.</param>
/// <param name="MassMin">The smallest mass of a field type.</param>
/// <param name="MassMax">The largest mass of a field type.</param>
/// <param name="IsColonizable">Whether a player may own an object of the type.</param>
public sealed record AdminObjectTypeUpdate(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("generation_weight")] int GenerationWeight,
    [property: JsonPropertyName("mass_min")] long? MassMin,
    [property: JsonPropertyName("mass_max")] long? MassMax,
    [property: JsonPropertyName("is_colonizable")] bool IsColonizable);

/// <summary>The new values of a generation rule group: how many resources it picks.</summary>
/// <param name="Version">The version the administrator saw.</param>
/// <param name="Reason">Why the group is changed; written to the journal.</param>
/// <param name="MinPicks">The picks that always happen.</param>
/// <param name="MaxPicks">The largest number of picks.</param>
/// <param name="ExtraPickProbability">The probability of every further pick.</param>
public sealed record AdminRuleGroupUpdate(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("min_picks")] int MinPicks,
    [property: JsonPropertyName("max_picks")] int MaxPicks,
    [property: JsonPropertyName("extra_pick_probability")] double ExtraPickProbability);

/// <summary>The values of a generation rule, new or changed.</summary>
/// <param name="Version">The version the administrator saw; ignored when a rule is added.</param>
/// <param name="Reason">Why the rule is changed; written to the journal.</param>
/// <param name="RuleGroupId">The group of a new rule; ignored when a rule is changed.</param>
/// <param name="ResourceId">The resource.</param>
/// <param name="Weight">The weight within the group.</param>
/// <param name="CoefficientMin">The smallest coefficient.</param>
/// <param name="CoefficientMax">The largest coefficient.</param>
/// <param name="RichCoefficientMin">The smallest coefficient on a rich planet, or <see langword="null"/>.</param>
/// <param name="RichCoefficientMax">The largest coefficient on a rich planet, or <see langword="null"/>.</param>
/// <param name="IsActive">Whether the generation uses the rule.</param>
public sealed record AdminRuleInput(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("rule_group_id")] int RuleGroupId,
    [property: JsonPropertyName("resource_id")] int ResourceId,
    [property: JsonPropertyName("weight")] int Weight,
    [property: JsonPropertyName("coefficient_min")] decimal CoefficientMin,
    [property: JsonPropertyName("coefficient_max")] decimal CoefficientMax,
    [property: JsonPropertyName("rich_coefficient_min")] decimal? RichCoefficientMin,
    [property: JsonPropertyName("rich_coefficient_max")] decimal? RichCoefficientMax,
    [property: JsonPropertyName("is_active")] bool IsActive);

/// <summary>The limits a card of a field is checked against, shown next to the inputs.</summary>
/// <param name="MaxCompositionShare">The largest total share of the composition.</param>
/// <param name="SectorSize">The width and the height of the sector.</param>
/// <param name="SectorDepth">The depth of the sector.</param>
public sealed record AdminFieldBounds(decimal MaxCompositionShare, double SectorSize, double SectorDepth);

/// <summary>The card of an asteroid field.</summary>
/// <param name="Id">The field.</param>
/// <param name="Version">The version of the field.</param>
/// <param name="CelestialObjectId">The celestial object.</param>
/// <param name="Name">The name.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="ObjectTypeCode">The code of the type.</param>
/// <param name="ObjectTypeNameKey">The localisation key of the type.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SystemId">The star system, or <see langword="null"/> for a free field.</param>
/// <param name="SystemName">The name of the star system.</param>
/// <param name="X">The x within the sector.</param>
/// <param name="Y">The y within the sector.</param>
/// <param name="Z">The z within the sector.</param>
/// <param name="InitialMass">The mass at creation.</param>
/// <param name="Mass">The mass left.</param>
/// <param name="DepletedAt">When the field was used up.</param>
/// <param name="CompositionIds">The composition.</param>
/// <param name="Bounds">The limits of the inputs.</param>
public sealed record AdminFieldCard(
    long Id,
    int Version,
    long CelestialObjectId,
    string Name,
    int ObjectTypeId,
    string ObjectTypeCode,
    string ObjectTypeNameKey,
    int SectorId,
    long? SystemId,
    string? SystemName,
    double X,
    double Y,
    double Z,
    long InitialMass,
    long Mass,
    DateTimeOffset? DepletedAt,
    IReadOnlyList<AdminShareDto> CompositionIds,
    AdminFieldBounds Bounds);

/// <summary>A size class with its range, for the card of a body.</summary>
/// <param name="Id">The size class.</param>
/// <param name="Code">The code.</param>
/// <param name="NameKey">The localisation key.</param>
/// <param name="SizeMin">The smallest size.</param>
/// <param name="SizeMax">The largest size.</param>
public sealed record AdminSizeClassDto(int Id, string Code, string NameKey, int SizeMin, int SizeMax);

/// <summary>An orbit of a star system held by another object.</summary>
/// <param name="Orbit">The orbit.</param>
/// <param name="Name">The name of the object on it.</param>
public sealed record AdminTakenOrbitDto(int Orbit, string Name);

/// <summary>The limits a card of a body is checked against.</summary>
/// <param name="OrbitMin">The innermost orbit of a body.</param>
/// <param name="OrbitMax">The outermost orbit of a body; the last one is kept for a field.</param>
/// <param name="TakenOrbitIds">The orbits of the system held by other objects.</param>
/// <param name="DepositCoefficientMin">The smallest richness multiplier of a deposit.</param>
/// <param name="DepositCoefficientMax">The largest richness multiplier of a deposit.</param>
/// <param name="AllowedResourceIds">The resources that may lie on a body.</param>
/// <param name="SizeClassIds">The size classes with their ranges.</param>
public sealed record AdminBodyBounds(
    int OrbitMin,
    int OrbitMax,
    IReadOnlyList<AdminTakenOrbitDto> TakenOrbitIds,
    decimal DepositCoefficientMin,
    decimal DepositCoefficientMax,
    IReadOnlyList<int> AllowedResourceIds,
    IReadOnlyList<AdminSizeClassDto> SizeClassIds);

/// <summary>The card of a body: a planet or a gas giant.</summary>
/// <param name="Id">The body.</param>
/// <param name="Version">The version of the body.</param>
/// <param name="CelestialObjectId">The celestial object.</param>
/// <param name="Name">The generated name of the object.</param>
/// <param name="OwnerName">The name the owner gave, shown only.</param>
/// <param name="OwnerPlayerId">The owner, shown only.</param>
/// <param name="ObjectTypeId">The type.</param>
/// <param name="ObjectTypeCode">The code of the type.</param>
/// <param name="ObjectTypeNameKey">The localisation key of the type.</param>
/// <param name="ObjectClass">The class of the type.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SystemId">The star system.</param>
/// <param name="SystemName">The name of the star system.</param>
/// <param name="Orbit">The orbit.</param>
/// <param name="X">The x within the sector, the one of its system.</param>
/// <param name="Y">The y within the sector.</param>
/// <param name="Z">The z within the sector.</param>
/// <param name="SizeClassId">The size class.</param>
/// <param name="SizeClassCode">The code of the size class.</param>
/// <param name="SizeClassNameKey">The localisation key of the size class.</param>
/// <param name="Size">The size.</param>
/// <param name="DepositIds">The deposits.</param>
/// <param name="Bounds">The limits of the inputs.</param>
public sealed record AdminBodyCard(
    long Id,
    int Version,
    long CelestialObjectId,
    string Name,
    string? OwnerName,
    string? OwnerPlayerId,
    int ObjectTypeId,
    string ObjectTypeCode,
    string ObjectTypeNameKey,
    string ObjectClass,
    int SectorId,
    long SystemId,
    string SystemName,
    int? Orbit,
    double X,
    double Y,
    double Z,
    int SizeClassId,
    string SizeClassCode,
    string SizeClassNameKey,
    int Size,
    IReadOnlyList<AdminShareDto> DepositIds,
    AdminBodyBounds Bounds);

/// <summary>The limits a card of a star system is checked against.</summary>
/// <param name="SectorSize">The width and the height of the sector.</param>
/// <param name="SectorDepth">The depth of the sector.</param>
public sealed record AdminSystemBounds(double SectorSize, double SectorDepth);

/// <summary>The card of a star system.</summary>
/// <param name="Id">The system.</param>
/// <param name="Version">The version of the system.</param>
/// <param name="SectorId">The sector.</param>
/// <param name="SectorName">The name of the sector.</param>
/// <param name="Number">The number within the sector.</param>
/// <param name="Name">The name.</param>
/// <param name="X">The x within the sector.</param>
/// <param name="Y">The y within the sector.</param>
/// <param name="Z">The z within the sector.</param>
/// <param name="InNebula">Whether it lies inside a nebula.</param>
/// <param name="StarTypeId">The type of the star, if the system has one.</param>
/// <param name="StarTypeCode">The code of the type of the star.</param>
/// <param name="StarTypeNameKey">The localisation key of the type of the star.</param>
/// <param name="Bounds">The limits of the inputs.</param>
public sealed record AdminSystemCard(
    long Id,
    int Version,
    int SectorId,
    string SectorName,
    int Number,
    string Name,
    double X,
    double Y,
    double Z,
    bool InNebula,
    int? StarTypeId,
    string? StarTypeCode,
    string? StarTypeNameKey,
    AdminSystemBounds Bounds);

/// <summary>A row of the screen of the object types (task 00009, decision 7).</summary>
/// <param name="Id">The type.</param>
/// <param name="Version">The version of the type.</param>
/// <param name="Code">The code.</param>
/// <param name="NameKey">The localisation key.</param>
/// <param name="ObjectClass">The class.</param>
/// <param name="GenerationWeight">The weight among the types of the class.</param>
/// <param name="MassMin">The smallest mass of a field type.</param>
/// <param name="MassMax">The largest mass of a field type.</param>
/// <param name="IsColonizable">Whether a player may own an object of the type.</param>
/// <param name="ObjectCount">The objects of the type that exist.</param>
public sealed record AdminObjectTypeRow(
    int Id,
    int Version,
    string Code,
    string NameKey,
    string ObjectClass,
    int GenerationWeight,
    long? MassMin,
    long? MassMax,
    bool IsColonizable,
    int ObjectCount);

/// <summary>A row of the screen of the composition rules: one rule with its group.</summary>
/// <param name="Id">The rule.</param>
/// <param name="Version">The version of the rule.</param>
/// <param name="RuleGroupId">The group.</param>
/// <param name="RuleGroupVersion">The version of the group.</param>
/// <param name="RuleGroupCode">The code of the group.</param>
/// <param name="ObjectTypeId">The object type of the group.</param>
/// <param name="ObjectTypeCode">The code of the object type.</param>
/// <param name="ObjectTypeNameKey">The localisation key of the object type.</param>
/// <param name="ObjectClass">The class of the object type.</param>
/// <param name="MinPicks">The picks of the group that always happen.</param>
/// <param name="MaxPicks">The largest number of picks of the group.</param>
/// <param name="ExtraPickProbability">The probability of every further pick of the group.</param>
/// <param name="ResourceId">The resource.</param>
/// <param name="ResourceCode">The code of the resource.</param>
/// <param name="ResourceNameKey">The localisation key of the resource.</param>
/// <param name="Weight">The weight within the group.</param>
/// <param name="CoefficientMin">The smallest coefficient.</param>
/// <param name="CoefficientMax">The largest coefficient.</param>
/// <param name="RichCoefficientMin">The smallest coefficient on a rich planet.</param>
/// <param name="RichCoefficientMax">The largest coefficient on a rich planet.</param>
/// <param name="IsActive">Whether the generation uses the rule.</param>
public sealed record AdminRuleRow(
    int Id,
    int Version,
    int RuleGroupId,
    int RuleGroupVersion,
    string RuleGroupCode,
    int ObjectTypeId,
    string ObjectTypeCode,
    string ObjectTypeNameKey,
    string ObjectClass,
    int MinPicks,
    int MaxPicks,
    double ExtraPickProbability,
    int ResourceId,
    string ResourceCode,
    string ResourceNameKey,
    int Weight,
    decimal CoefficientMin,
    decimal CoefficientMax,
    decimal? RichCoefficientMin,
    decimal? RichCoefficientMax,
    bool IsActive);

/// <summary>The filters of the screen of the composition rules.</summary>
/// <param name="ObjectTypeId">Only the rules of this object type.</param>
/// <param name="IsActive">Only the active or only the switched off rules.</param>
public sealed record AdminRuleFilter(int? ObjectTypeId, bool? IsActive);

/// <summary>
/// A new resource type from the admin panel (task 00022, decision 6). The server gives the code;
/// the name and the description are required in every language of the texts.
/// </summary>
/// <param name="Reason">Why the resource is added; written to the journal.</param>
/// <param name="ResourceGroupId">The group of the resource.</param>
/// <param name="Name">The name by language.</param>
/// <param name="Description">The description by language.</param>
public sealed record AdminResourceCreate(
    [property: JsonPropertyName("reason")] string? Reason,
    [property: JsonPropertyName("resource_group_id")] int ResourceGroupId,
    [property: JsonPropertyName("name")] IReadOnlyDictionary<string, string>? Name,
    [property: JsonPropertyName("description")] IReadOnlyDictionary<string, string>? Description);

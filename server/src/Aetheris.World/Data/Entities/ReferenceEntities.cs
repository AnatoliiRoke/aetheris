namespace Aetheris.World.Data.Entities;

/// <summary>
/// A group of resource types. The groups are data: a new group is a new row with a neutral code
/// and its texts (task 00022).
/// </summary>
public sealed class ResourceGroupEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the group, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a resource of the group may lie on a planet or a gas giant.</summary>
    public bool AllowedOnPlanet { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the group is archived: it stays with its resources,
    /// but no new resource gets it (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A resource type of the reference table <c>resource_type</c> (task 00004, decision 23).
/// </summary>
public sealed class ResourceTypeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the resource, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the group of the resource. A currency of a civilization has no group: it
    /// does not lie on a body and has no share of a body (task 00015).
    /// </summary>
    public int? ResourceGroupId { get; set; }

    /// <summary>
    /// Gets or sets the scope of the resource: where it lies. A resource of a planet is stored
    /// on the body, a resource of a civilization belongs to the whole civilization (task 00015).
    /// </summary>
    public string Scope { get; set; } = ResourceScopes.Planet;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture of the resource, or <see langword="null"/> for the neutral placeholder.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the resource is archived: the stocks of the players and
    /// the costs already written stay, but no new level, recipe or generation rule gets it
    /// (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A type of celestial object: a star type, a planet type, a gas giant, a field type, a nebula type.
/// </summary>
/// <remarks>
/// The class (<see cref="WorldObjectClasses"/>) says how the object behaves; the type is data:
/// a new type is a new row.
/// </remarks>
public sealed class CelestialObjectTypeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the type, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the class of the type, one of <see cref="WorldObjectClasses"/>.</summary>
    public string ObjectClass { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture of the type, or <see langword="null"/> for the neutral placeholder.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a newcomer whose race names no body type starts on
    /// a body of this type (task 00022, decision 5: a flag of the data instead of a code in the
    /// configuration).
    /// </summary>
    public bool IsStartDefault { get; set; }

    /// <summary>
    /// Gets or sets the resource a field of this type hides, or <see langword="null"/>. The hidden
    /// share is not part of the composition the player sees (task 00022, decision 7); the
    /// extraction from fields uses it with its own task.
    /// </summary>
    public int? HiddenResourceTypeId { get; set; }

    /// <summary>Gets or sets the smallest hidden share of a cycle of extraction, or <see langword="null"/>.</summary>
    public decimal? HiddenShareMin { get; set; }

    /// <summary>Gets or sets the largest hidden share of a cycle of extraction, or <see langword="null"/>.</summary>
    public decimal? HiddenShareMax { get; set; }

    /// <summary>Gets or sets a value indicating whether a player may own an object of this type.</summary>
    public bool IsColonizable { get; set; }

    /// <summary>Gets or sets the weight of the type among the types of its class.</summary>
    public int GenerationWeight { get; set; }

    /// <summary>Gets or sets the smallest total mass of a field of this type.</summary>
    public long? MassMin { get; set; }

    /// <summary>Gets or sets the largest total mass of a field of this type.</summary>
    public long? MassMax { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A size class of a planet: small, medium, large and any class added later.
/// </summary>
public sealed class PlanetSizeClassEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the class, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the weight of the class.</summary>
    public int GenerationWeight { get; set; }

    /// <summary>Gets or sets the smallest size of the class.</summary>
    public int SizeMin { get; set; }

    /// <summary>Gets or sets the largest size of the class.</summary>
    public int SizeMax { get; set; }
}

/// <summary>
/// A group of generation rules of an object type: how many resources of the group are picked.
/// </summary>
/// <remarks>
/// <see cref="MinPicks"/> resources are always picked; every further one up to
/// <see cref="MaxPicks"/> is picked with <see cref="ExtraPickProbability"/>. A planet type
/// with a crystal group of one pick, rarely two, gives one kind of crystals, rarely two.
/// </remarks>
public sealed class GenerationRuleGroupEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the object type the group belongs to.</summary>
    public int ObjectTypeId { get; set; }

    /// <summary>Gets or sets the neutral code of the group, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of resources that are always picked.</summary>
    public int MinPicks { get; set; }

    /// <summary>Gets or sets the largest number of picked resources.</summary>
    public int MaxPicks { get; set; }

    /// <summary>Gets or sets the probability of every further pick.</summary>
    public double ExtraPickProbability { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the rules of the group.</summary>
    public List<GenerationRuleEntity> RuleIds { get; set; } = [];
}

/// <summary>
/// A generation rule: a resource that a group may pick, its weight and the range of its coefficient.
/// </summary>
public sealed class GenerationRuleEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the group of the rule.</summary>
    public int RuleGroupId { get; set; }

    /// <summary>Gets or sets the resource the rule gives.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets the weight of the resource within the group.</summary>
    public int Weight { get; set; }

    /// <summary>Gets or sets the smallest coefficient.</summary>
    public decimal CoefficientMin { get; set; }

    /// <summary>Gets or sets the largest coefficient.</summary>
    public decimal CoefficientMax { get; set; }

    /// <summary>
    /// Gets or sets the smallest coefficient on a rich body, or <see langword="null"/> when the
    /// rule has no rich range and a rich body uses the ordinary one (task 00009, decision 12).
    /// </summary>
    public decimal? RichCoefficientMin { get; set; }

    /// <summary>Gets or sets the largest coefficient on a rich body, or <see langword="null"/>.</summary>
    public decimal? RichCoefficientMax { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the generation uses the rule. A rule is switched
    /// off instead of deleted, so the objects created by it keep their history.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A race of the reference table <c>race</c> (task 00006, decisions 9 and 11).
/// </summary>
/// <remarks>
/// A race is data: a new race is a new row. It names the body type its players prefer at
/// the start and the start building of a new civilization, and owns its start resources.
/// </remarks>
public sealed class RaceEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the race, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the mood shown on the card of the race.</summary>
    public string MoodKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation keys of the strengths shown on the card of the race.</summary>
    public List<string> TraitKeys { get; set; } = [];

    /// <summary>Gets or sets the key of the picture of the race, or <see langword="null"/> for the neutral placeholder.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets the key of the colour of the race in the style of the client, or <see langword="null"/> for the neutral one.</summary>
    public string? ColorKey { get; set; }

    /// <summary>Gets or sets the order of the race in the list of the client.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the preferred start body type, or <see langword="null"/> for the start type of the configuration.</summary>
    public int? PreferredObjectTypeId { get; set; }

    /// <summary>
    /// Gets or sets the building type a new civilization starts with, or <see langword="null"/>
    /// while none is set.
    /// </summary>
    /// <remarks>
    /// The building types belong to the Planets module, which fills the column at start; the
    /// tables of the two modules have no foreign keys between them.
    /// </remarks>
    public int? StartBuildingTypeId { get; set; }

    /// <summary>Gets or sets the level of the start building.</summary>
    public int StartBuildingLevel { get; set; }

    /// <summary>Gets or sets the start resources of the race.</summary>
    public List<RaceStartResourceEntity> StartResourceIds { get; set; } = [];
}

/// <summary>
/// A start resource of a race: the amount a new civilization of the race gets in its
/// storage (task 00006, decision 11).
/// </summary>
public sealed class RaceStartResourceEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the race.</summary>
    public int RaceId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public long Amount { get; set; }
}

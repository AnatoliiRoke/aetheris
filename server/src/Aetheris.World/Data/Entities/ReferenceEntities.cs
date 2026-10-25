namespace Aetheris.World.Data.Entities;

/// <summary>
/// A group of resource types: extractable, crystal, fuel, ether and any group added later.
/// </summary>
public sealed class ResourceGroupEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the stable code of the group.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a resource of the group may lie on a planet or a gas giant.</summary>
    public bool AllowedOnPlanet { get; set; }
}

/// <summary>
/// A resource type of the reference table <c>resource_type</c> (task 00004, decision 23).
/// </summary>
public sealed class ResourceTypeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the stable code of the resource.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the group of the resource.</summary>
    public int ResourceGroupId { get; set; }

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;
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

    /// <summary>Gets or sets the stable code of the type.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the class of the type, one of <see cref="WorldObjectClasses"/>.</summary>
    public string ObjectClass { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a player may own an object of this type.</summary>
    public bool IsColonizable { get; set; }

    /// <summary>Gets or sets the weight of the type among the types of its class.</summary>
    public int GenerationWeight { get; set; }

    /// <summary>Gets or sets the smallest total mass of a field of this type.</summary>
    public long? MassMin { get; set; }

    /// <summary>Gets or sets the largest total mass of a field of this type.</summary>
    public long? MassMax { get; set; }
}

/// <summary>
/// A size class of a planet: small, medium, large and any class added later.
/// </summary>
public sealed class PlanetSizeClassEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the stable code of the class.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

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

    /// <summary>Gets or sets the stable code of the group.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of resources that are always picked.</summary>
    public int MinPicks { get; set; }

    /// <summary>Gets or sets the largest number of picked resources.</summary>
    public int MaxPicks { get; set; }

    /// <summary>Gets or sets the probability of every further pick.</summary>
    public double ExtraPickProbability { get; set; }

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
}

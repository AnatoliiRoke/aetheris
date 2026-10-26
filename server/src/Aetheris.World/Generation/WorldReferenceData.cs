using Aetheris.World.Configuration;

namespace Aetheris.World.Generation;

/// <summary>A resource group of the reference snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The code.</param>
/// <param name="AllowedOnPlanet">Whether the group may lie on a body.</param>
public sealed record ReferenceResourceGroup(int Id, string Code, bool AllowedOnPlanet);

/// <summary>A resource type of the reference snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The code.</param>
/// <param name="ResourceGroupId">The group.</param>
public sealed record ReferenceResource(int Id, string Code, int ResourceGroupId);

/// <summary>An object type of the reference snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The code.</param>
/// <param name="ObjectClass">The class.</param>
/// <param name="IsColonizable">Whether a player may own the object.</param>
/// <param name="GenerationWeight">The weight among the types of the class.</param>
/// <param name="MassMin">The smallest mass of a field.</param>
/// <param name="MassMax">The largest mass of a field.</param>
public sealed record ReferenceObjectType(
    int Id,
    string Code,
    string ObjectClass,
    bool IsColonizable,
    int GenerationWeight,
    long? MassMin,
    long? MassMax);

/// <summary>A size class of the reference snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Code">The code.</param>
/// <param name="GenerationWeight">The weight.</param>
/// <param name="SizeMin">The smallest size.</param>
/// <param name="SizeMax">The largest size.</param>
public sealed record ReferenceSizeClass(int Id, string Code, int GenerationWeight, int SizeMin, int SizeMax);

/// <summary>A generation rule of the reference snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="ResourceId">The resource.</param>
/// <param name="Weight">The weight within the group.</param>
/// <param name="CoefficientMin">The smallest coefficient.</param>
/// <param name="CoefficientMax">The largest coefficient.</param>
/// <param name="RichCoefficientMin">The smallest coefficient on a rich body, or <see langword="null"/> to use the ordinary range.</param>
/// <param name="RichCoefficientMax">The largest coefficient on a rich body, or <see langword="null"/> to use the ordinary range.</param>
public sealed record ReferenceRule(
    int Id,
    int ResourceId,
    int Weight,
    decimal CoefficientMin,
    decimal CoefficientMax,
    decimal? RichCoefficientMin = null,
    decimal? RichCoefficientMax = null);

/// <summary>A generation rule group of the reference snapshot.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="ObjectTypeId">The object type.</param>
/// <param name="Code">The code.</param>
/// <param name="MinPicks">The picks that always happen.</param>
/// <param name="MaxPicks">The largest number of picks.</param>
/// <param name="ExtraPickProbability">The probability of every further pick.</param>
/// <param name="RuleIds">The rules, ordered by identifier.</param>
public sealed record ReferenceRuleGroup(
    int Id,
    int ObjectTypeId,
    string Code,
    int MinPicks,
    int MaxPicks,
    double ExtraPickProbability,
    IReadOnlyList<ReferenceRule> RuleIds);

/// <summary>
/// An immutable snapshot of the reference tables, the data half of the generator input.
/// </summary>
/// <remarks>
/// Every list is ordered by identifier, so the generator walks the data in the same order on
/// every run and the result depends only on the seed and the data.
/// </remarks>
public sealed class WorldReferenceData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldReferenceData"/> class.
    /// </summary>
    /// <param name="groups">The resource groups.</param>
    /// <param name="resources">The resource types.</param>
    /// <param name="objectTypes">The object types.</param>
    /// <param name="sizeClasses">The size classes.</param>
    /// <param name="ruleGroups">The generation rule groups with their rules.</param>
    public WorldReferenceData(
        IEnumerable<ReferenceResourceGroup> groups,
        IEnumerable<ReferenceResource> resources,
        IEnumerable<ReferenceObjectType> objectTypes,
        IEnumerable<ReferenceSizeClass> sizeClasses,
        IEnumerable<ReferenceRuleGroup> ruleGroups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(objectTypes);
        ArgumentNullException.ThrowIfNull(sizeClasses);
        ArgumentNullException.ThrowIfNull(ruleGroups);

        Groups = [.. groups.OrderBy(group => group.Id)];
        Resources = [.. resources.OrderBy(resource => resource.Id)];
        ObjectTypes = [.. objectTypes.OrderBy(type => type.Id)];
        SizeClasses = [.. sizeClasses.OrderBy(size => size.Id)];
        RuleGroups =
        [
            .. ruleGroups
                .OrderBy(group => group.Id)
                .Select(group => group with { RuleIds = [.. group.RuleIds.OrderBy(rule => rule.Id)] }),
        ];
    }

    /// <summary>Gets the resource groups.</summary>
    public IReadOnlyList<ReferenceResourceGroup> Groups { get; }

    /// <summary>Gets the resource types.</summary>
    public IReadOnlyList<ReferenceResource> Resources { get; }

    /// <summary>Gets the object types.</summary>
    public IReadOnlyList<ReferenceObjectType> ObjectTypes { get; }

    /// <summary>Gets the size classes.</summary>
    public IReadOnlyList<ReferenceSizeClass> SizeClasses { get; }

    /// <summary>Gets the generation rule groups.</summary>
    public IReadOnlyList<ReferenceRuleGroup> RuleGroups { get; }

    /// <summary>
    /// Gets the object types of a class.
    /// </summary>
    /// <param name="objectClass">The class.</param>
    /// <returns>The types, ordered by identifier.</returns>
    public IReadOnlyList<ReferenceObjectType> TypesOf(string objectClass) =>
        [.. ObjectTypes.Where(type => string.Equals(type.ObjectClass, objectClass, StringComparison.Ordinal))];

    /// <summary>
    /// Gets the generation rule groups of an object type.
    /// </summary>
    /// <param name="objectTypeId">The object type.</param>
    /// <returns>The groups, ordered by identifier.</returns>
    public IReadOnlyList<ReferenceRuleGroup> GroupsOf(int objectTypeId) =>
        [.. RuleGroups.Where(group => group.ObjectTypeId == objectTypeId)];

    /// <summary>
    /// Finds an object type.
    /// </summary>
    /// <param name="objectTypeId">The identifier.</param>
    /// <returns>The type, or <see langword="null"/>.</returns>
    public ReferenceObjectType? FindType(int objectTypeId) => ObjectTypes.FirstOrDefault(type => type.Id == objectTypeId);

    /// <summary>
    /// Tells whether a resource may lie on a body.
    /// </summary>
    /// <param name="resourceId">The resource.</param>
    /// <returns><see langword="true"/> when the group of the resource allows it.</returns>
    public bool IsAllowedOnPlanet(int resourceId)
    {
        ReferenceResource? resource = Resources.FirstOrDefault(candidate => candidate.Id == resourceId);

        return resource is not null
            && Groups.Any(group => group.Id == resource.ResourceGroupId && group.AllowedOnPlanet);
    }
}

/// <summary>
/// The whole input of the generator besides the seed: the parameters and the reference data.
/// </summary>
/// <param name="Settings">The parameters from the configuration.</param>
/// <param name="Reference">The reference data.</param>
public sealed record WorldGenerationConfig(WorldSettings Settings, WorldReferenceData Reference);

using Aetheris.Core.Admin;

namespace Aetheris.Requirements.Services;

/// <summary>
/// The reference kinds the target of a requirement is picked from in the forms of the admin
/// panel (task 00023): the logical codes of the kinds of the modules that own the targets.
/// </summary>
public static class RequirementReferenceKinds
{
    /// <summary>The building types of the bodies module.</summary>
    public const string Building = "building";

    /// <summary>The technologies of the research module.</summary>
    public const string Technology = "technology";

    /// <summary>The resources of the world.</summary>
    public const string Resource = "resource";
}

/// <summary>
/// The requirements of a level in the forms of the admin panel, shared by every module whose
/// subjects the common requirements close (task 00023): the description of the field and the
/// check of a sent requirement.
/// </summary>
public static class RequirementAdminFields
{
    private const string LabelPrefix = "admin.reference.";

    /// <summary>
    /// Gets the field of the requirements of a level: the type of the requirement names the kind
    /// its target is picked from.
    /// </summary>
    public static AdminReferenceField Field { get; } = new(
        "requirements",
        AdminReferenceFieldTypes.Requirements,
        LabelPrefix + "field.requirements",
        Fields:
        [
            new(
                "requirement_type",
                AdminReferenceFieldTypes.Select,
                LabelPrefix + "field.requirement_type",
                IsRequired: true,
                Options:
                [
                    new(RequirementTypes.BuildingLevel, LabelPrefix + "requirement." + RequirementTypes.BuildingLevel, RequirementReferenceKinds.Building),
                    new(RequirementTypes.TechnologyLevel, LabelPrefix + "requirement." + RequirementTypes.TechnologyLevel, RequirementReferenceKinds.Technology),
                    new(RequirementTypes.Deposit, LabelPrefix + "requirement." + RequirementTypes.Deposit, RequirementReferenceKinds.Resource),
                ]),
            new("target_id", AdminReferenceFieldTypes.Reference, LabelPrefix + "field.target", IsRequired: true, ReferenceKindFrom: "requirement_type"),
            new("target_level", AdminReferenceFieldTypes.Number, LabelPrefix + "field.target_level", Minimum: 0),
        ]);

    /// <summary>
    /// Checks a requirement: a known type, an existing target and a level that suits the type.
    /// </summary>
    /// <param name="failures">The failures.</param>
    /// <param name="path">The path of the field.</param>
    /// <param name="requirementType">The requirement type.</param>
    /// <param name="targetId">The target.</param>
    /// <param name="targetLevel">The required level of the target.</param>
    /// <param name="buildingIds">The building types that exist.</param>
    /// <param name="technologyIds">The technologies that exist.</param>
    /// <param name="resourceIds">The resources that exist.</param>
    /// <returns>The requirement, or <see langword="null"/> when it is refused.</returns>
    public static RequirementDefinition? Check(
        Dictionary<string, List<string>> failures,
        string path,
        string? requirementType,
        int? targetId,
        int? targetLevel,
        IReadOnlySet<int> buildingIds,
        IReadOnlySet<int> technologyIds,
        IEnumerable<int> resourceIds)
    {
        ArgumentNullException.ThrowIfNull(buildingIds);
        ArgumentNullException.ThrowIfNull(technologyIds);
        ArgumentNullException.ThrowIfNull(resourceIds);

        bool known = requirementType switch
        {
            RequirementTypes.BuildingLevel => targetId is { } building && buildingIds.Contains(building),
            RequirementTypes.TechnologyLevel => targetId is { } technology && technologyIds.Contains(technology),
            RequirementTypes.Deposit => targetId is { } resource && resourceIds.Contains(resource),
            _ => false,
        };

        if (!known)
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.Unknown);

            return null;
        }

        // A deposit has no level; a building or a technology is required of at least level one.
        int level = requirementType == RequirementTypes.Deposit ? 0 : targetLevel ?? 0;

        if (requirementType != RequirementTypes.Deposit && level < 1)
        {
            AdminReferenceRules.Fail(failures, path, AdminReferenceErrors.OutOfBounds);

            return null;
        }

        return new RequirementDefinition(requirementType!, targetId!.Value, level);
    }
}

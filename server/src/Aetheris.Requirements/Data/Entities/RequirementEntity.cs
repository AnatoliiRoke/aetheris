namespace Aetheris.Requirements.Data.Entities;

/// <summary>
/// A requirement of a closed subject: a building type and its levels now, technologies,
/// modules, recipes and ship classes later (task 00011, decision 3; task 00012, decision 3).
/// </summary>
/// <remarks>
/// <para>
/// The subject is named by its type and identifier, so one table and one service serve every
/// subject. A requirement applies to the levels of the subject from <see cref="SubjectLevel"/> up.
/// </para>
/// <para>
/// The table lives in the common requirements module, so the bodies module and the research
/// module add their requirements through the public contract of that module and neither
/// reads the table of the other.
/// </para>
/// </remarks>
public sealed class RequirementEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the subject type, one of <see cref="Services.RequirementSubjectTypes"/>.</summary>
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>Gets or sets the subject.</summary>
    public int SubjectId { get; set; }

    /// <summary>Gets or sets the first level of the subject the requirement applies to.</summary>
    public int SubjectLevel { get; set; } = 1;

    /// <summary>Gets or sets the requirement type, one of <see cref="Services.RequirementTypes"/>.</summary>
    public string RequirementType { get; set; } = string.Empty;

    /// <summary>Gets or sets what is required: a building type, a technology or a resource, by the requirement type.</summary>
    public int TargetId { get; set; }

    /// <summary>Gets or sets the level of the target that is required.</summary>
    public int TargetLevel { get; set; } = 1;
}

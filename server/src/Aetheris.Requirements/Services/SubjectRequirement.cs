namespace Aetheris.Requirements.Services;

/// <summary>
/// A requirement of a subject, with the subject it belongs to.
/// </summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="SubjectLevel">The first level of the subject the requirement applies to.</param>
/// <param name="Requirement">The requirement.</param>
public sealed record SubjectRequirement(int SubjectId, int SubjectLevel, RequirementDefinition Requirement);

namespace Aetheris.Requirements.Services;

/// <summary>
/// A requirement of a subject: what has to be true for the subject to be open (task 00012,
/// decision 3).
/// </summary>
/// <param name="RequirementType">The requirement type, one of <see cref="RequirementTypes"/>.</param>
/// <param name="TargetId">What is required: a building type, a technology or a resource, by the requirement type.</param>
/// <param name="TargetLevel">The required level of the target.</param>
/// <param name="Id">The identifier of the row; zero when the requirement has not been added yet.</param>
public sealed record RequirementDefinition(string RequirementType, int TargetId, int TargetLevel, int Id = 0);

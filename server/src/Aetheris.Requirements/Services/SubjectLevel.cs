namespace Aetheris.Requirements.Services;

/// <summary>
/// A subject and the level its requirements are read for.
/// </summary>
/// <param name="SubjectId">The subject.</param>
/// <param name="Level">The level of the subject.</param>
public sealed record SubjectLevel(int SubjectId, int Level);

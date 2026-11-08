using Aetheris.Requirements.Services;

namespace Aetheris.Requirements;

/// <summary>
/// The kinds of subjects a requirement can close, for the modules that add those rows
/// (task 00039).
/// </summary>
/// <remarks>
/// The codes stay in <see cref="RequirementSubjectTypes"/>. This type only names them from
/// the public contract, so another module does not reach into the services.
/// </remarks>
public static class RequirementSubjects
{
    /// <summary>A ship module, the hull included.</summary>
    public const string Module = RequirementSubjectTypes.Module;
}

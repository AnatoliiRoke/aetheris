using Aetheris.Requirements.Services;

namespace Aetheris.Requirements;

/// <summary>
/// The public contract of the common requirements module (task 00012, decision 3).
/// </summary>
/// <remarks>
/// <para>
/// Every module that closes a subject with a condition keeps its requirements in the table of
/// this module and adds them through <see cref="AddAsync"/>, and reads the requirements of its
/// subjects through <see cref="GetAsync"/>. The module owns the table, so no module reaches
/// into the tables of another (documentation/tech/01, section 8).
/// </para>
/// <para>
/// A requirement names its subject by the type and the identifier of the subject, so one
/// table serves the buildings of the bodies module and the technologies of the research
/// module. The kinds of subjects and of requirements are logical codes
/// (documentation/tech/09, section 3.4).
/// </para>
/// </remarks>
public interface IRequirementsModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Adds the requirements of a subject.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="Services.RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="subjectLevel">The first level of the subject the requirements apply to.</param>
    /// <param name="requirements">The requirements to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the requirements are added.</returns>
    Task AddAsync(
        string subjectType,
        int subjectId,
        int subjectLevel,
        IReadOnlyList<RequirementDefinition> requirements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the requirements of a subject that apply to a level.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="Services.RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requirements, ordered by their identifier.</returns>
    Task<IReadOnlyList<RequirementDefinition>> GetAsync(
        string subjectType,
        int subjectId,
        int level,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the requirements of many subjects that apply to their levels.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="Services.RequirementSubjectTypes"/>.</param>
    /// <param name="subjects">The subjects with the level the requirements are read for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requirements, ordered by the subject and by their identifier.</returns>
    Task<IReadOnlyList<SubjectRequirement>> GetManyAsync(
        string subjectType,
        IReadOnlyList<SubjectLevel> subjects,
        CancellationToken cancellationToken = default);
}

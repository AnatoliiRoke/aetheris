using Aetheris.Requirements.Data;
using Aetheris.Requirements.Data.Entities;
using Aetheris.Requirements.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Requirements;

/// <summary>
/// The implementation of <see cref="IRequirementsModule"/>: a thin front of the requirements
/// service.
/// </summary>
public sealed class RequirementsModule : IRequirementsModule
{
    private readonly RequirementsService _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementsModule"/> class.
    /// </summary>
    /// <param name="requirements">The requirements service.</param>
    public RequirementsModule(RequirementsService requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        _requirements = requirements;
    }

    /// <inheritdoc />
    public string ModuleName => "Requirements";

    /// <inheritdoc />
    public Task AddAsync(
        string subjectType,
        int subjectId,
        int subjectLevel,
        IReadOnlyList<RequirementDefinition> requirements,
        CancellationToken cancellationToken = default) =>
        _requirements.AddAsync(subjectType, subjectId, subjectLevel, requirements, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RequirementDefinition>> GetAsync(
        string subjectType,
        int subjectId,
        int level,
        CancellationToken cancellationToken = default) =>
        _requirements.GetAsync(subjectType, subjectId, level, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SubjectRequirement>> GetManyAsync(
        string subjectType,
        IReadOnlyList<SubjectLevel> subjects,
        CancellationToken cancellationToken = default) =>
        _requirements.GetManyAsync(subjectType, subjects, cancellationToken);
}

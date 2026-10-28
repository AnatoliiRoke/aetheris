using Aetheris.Requirements.Data;
using Aetheris.Requirements.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Requirements.Services;

/// <summary>
/// The requirements of every closed subject: the buildings of the bodies module and the
/// technologies of the research module keep their conditions in the table of this module
/// (task 00012, decision 3).
/// </summary>
public sealed class RequirementsService
{
    private readonly RequirementsDbContext _dbContext;
    private readonly RequirementsUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementsService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    public RequirementsService(RequirementsDbContext dbContext, RequirementsUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Adds the requirements of a subject.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="subjectLevel">The first level of the subject the requirements apply to.</param>
    /// <param name="requirements">The requirements to add.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the requirements are added.</returns>
    public Task AddAsync(
        string subjectType,
        int subjectId,
        int subjectLevel,
        IReadOnlyList<RequirementDefinition> requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentNullException.ThrowIfNull(requirements);

        if (requirements.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _unitOfWork.RunAsync(
            async () =>
            {
                _dbContext.Requirements.AddRange(requirements.Select(requirement => new RequirementEntity
                {
                    SubjectType = subjectType,
                    SubjectId = subjectId,
                    SubjectLevel = subjectLevel,
                    RequirementType = requirement.RequirementType,
                    TargetId = requirement.TargetId,
                    TargetLevel = requirement.TargetLevel,
                }));

                await Task.CompletedTask.ConfigureAwait(false);

                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Replaces every requirement of a subject. A kept requirement keeps its row, so its
    /// identifier does not change; the others are removed or added.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="requirements">The new requirements with the first level of the subject each applies to; the subject of each is ignored.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the requirements are replaced.</returns>
    public Task ReplaceAsync(
        string subjectType,
        int subjectId,
        IReadOnlyList<SubjectRequirement> requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentNullException.ThrowIfNull(requirements);

        return _unitOfWork.RunAsync(
            async () =>
            {
                List<RequirementEntity> stored = await _dbContext.Requirements
                    .Where(requirement => requirement.SubjectType == subjectType && requirement.SubjectId == subjectId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<SubjectRequirement> missing = [.. requirements];

                foreach (RequirementEntity row in stored)
                {
                    SubjectRequirement? kept = missing.FirstOrDefault(requirement => requirement.SubjectLevel == row.SubjectLevel
                        && requirement.Requirement.RequirementType == row.RequirementType
                        && requirement.Requirement.TargetId == row.TargetId
                        && requirement.Requirement.TargetLevel == row.TargetLevel);

                    if (kept is null)
                    {
                        _dbContext.Requirements.Remove(row);
                    }
                    else
                    {
                        missing.Remove(kept);
                    }
                }

                _dbContext.Requirements.AddRange(missing.Select(requirement => new RequirementEntity
                {
                    SubjectType = subjectType,
                    SubjectId = subjectId,
                    SubjectLevel = requirement.SubjectLevel,
                    RequirementType = requirement.Requirement.RequirementType,
                    TargetId = requirement.Requirement.TargetId,
                    TargetLevel = requirement.Requirement.TargetLevel,
                }));

                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets the requirements of a subject that apply to a level.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requirements, ordered by their identifier.</returns>
    public async Task<IReadOnlyList<RequirementDefinition>> GetAsync(
        string subjectType,
        int subjectId,
        int level,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);

        return await _dbContext.Requirements.AsNoTracking()
            .Where(requirement => requirement.SubjectType == subjectType
                && requirement.SubjectId == subjectId
                && requirement.SubjectLevel <= level)
            .OrderBy(requirement => requirement.Id)
            .Select(requirement => new RequirementDefinition(
                requirement.RequirementType,
                requirement.TargetId,
                requirement.TargetLevel,
                requirement.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the requirements of many subjects that apply to their levels.
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjects">The subjects with the level the requirements are read for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The requirements, ordered by the subject and by their identifier.</returns>
    public async Task<IReadOnlyList<SubjectRequirement>> GetManyAsync(
        string subjectType,
        IReadOnlyList<SubjectLevel> subjects,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectType);
        ArgumentNullException.ThrowIfNull(subjects);

        if (subjects.Count == 0)
        {
            return [];
        }

        // The rows of the subjects are read in one query, so the number of requests does not
        // grow with the number of subjects. The level of every subject is applied to the rows
        // in the memory, because the level is not a column of the table.
        int[] subjectIds = [.. subjects.Select(subject => subject.SubjectId).Distinct()];
        Dictionary<int, int> levels = subjects.ToDictionary(subject => subject.SubjectId, subject => subject.Level);

        List<RequirementEntity> rows = await _dbContext.Requirements.AsNoTracking()
            .Where(requirement => requirement.SubjectType == subjectType
                && subjectIds.Contains(requirement.SubjectId))
            .OrderBy(requirement => requirement.SubjectId)
            .ThenBy(requirement => requirement.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows
                .Where(row => row.SubjectLevel <= levels[row.SubjectId])
                .Select(row => new SubjectRequirement(
                    row.SubjectId,
                    row.SubjectLevel,
                    new RequirementDefinition(
                        row.RequirementType,
                        row.TargetId,
                        row.TargetLevel,
                        row.Id))),
        ];
    }
}

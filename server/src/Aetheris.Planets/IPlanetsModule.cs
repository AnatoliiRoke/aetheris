using Aetheris.Planets.Api;
using Aetheris.Planets.Services;
using Aetheris.Requirements.Services;

namespace Aetheris.Planets;

/// <summary>
/// The public contract of the Planets module (task 00011): the buildings of the bodies, the
/// common requirement mechanism, the construction queues and the balance of the bodies.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8).
/// The module reads the world through its contract and keeps its own tables.
/// </remarks>
public interface IPlanetsModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Prepares the module: fills the building tables of a fresh database, gives the races
    /// their start building and adopts the civilizations created before the module. A repeated
    /// call changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    Task<PlanetsBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a body of a player: buildings, queue, balance, storage and energy.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The body, or <see langword="null"/> when the player does not own it.</returns>
    Task<PlanetOverviewDto?> GetPlanetAsync(string playerId, long planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the requirements of a closed subject on a body (task 00011, decision 3).
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether the subject is available and why not.</returns>
    Task<AvailabilityDto> CheckRequirementsAsync(
        string subjectType,
        int subjectId,
        int level,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the level of the building of a class on a body: the level of the science centre
    /// of a body, for example (task 00012, decision 6).
    /// </summary>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, or <see langword="null"/> when the level of any body is wanted.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the building, or zero when the body has no building of the class.</returns>
    Task<int> GetBuildingLevelAsync(
        string buildingClass,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default);
}

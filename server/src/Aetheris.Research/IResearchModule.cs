using Aetheris.Research.Api;
using Aetheris.Research.Services;

namespace Aetheris.Research;

/// <summary>
/// The public contract of the research module (task 00012): the tree of the technologies,
/// the queue of the researches and the bonuses the technologies give.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8).
/// The module reads the bodies module through its contract, for the level of the science
/// centre of the body a research starts from, and keeps the requirements of its technologies
/// in the common requirements module.
/// </remarks>
public interface IResearchModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Prepares the module: fills the technology tables of a fresh database. A repeated call
    /// changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    Task<ResearchBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the tree of the technologies of a player with the queue of the researches.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the research starts from, or <see langword="null"/> to take the body with the highest science centre.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tree of the technologies and the queue.</returns>
    Task<ResearchTreeDto> GetTreeAsync(string playerId, long? planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a level of a technology in the queue of the researches of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the research starts from.</param>
    /// <param name="technologyId">The technology.</param>
    /// <param name="targetLevel">The level to research.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tree of the technologies and the queue after the change.</returns>
    Task<ResearchTreeDto> EnqueueAsync(
        string playerId,
        long planetId,
        int technologyId,
        int targetLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a record from the queue of the researches of a player and returns its whole
    /// cost.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="queueItemId">The record.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The tree of the technologies and the queue after the change.</returns>
    Task<ResearchTreeDto> CancelAsync(
        string playerId,
        int queueItemId,
        CancellationToken cancellationToken = default);
}

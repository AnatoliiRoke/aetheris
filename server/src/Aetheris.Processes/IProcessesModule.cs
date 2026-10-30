using Aetheris.Processes.Api;
using Aetheris.Processes.Services;

namespace Aetheris.Processes;

/// <summary>
/// The public contract of the processes module (task 00014): the kinds of the processes,
/// their recipes, the queues of the buildings and the state of the population.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8).
/// The module reads the bodies, the production, the research and the world through their
/// contracts and keeps its own tables.
/// </remarks>
public interface IProcessesModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Prepares the module: fills the tables of a fresh database with the starting data. A
    /// repeated call changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    Task<ProcessesBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the buildings of a body that run a process.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The buildings of the body that run a process.</returns>
    Task<IReadOnlyList<ProcessBuildingSummaryDto>> GetBuildingsAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a building with its recipes of the processes and its queue.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the building stands on.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue.</returns>
    Task<ProcessBuildingDto> GetBuildingAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts an order of a recipe in the queue of a building. The whole cost of the order is
    /// paid at once, and cancelling it returns the cost in full unless the kind of the
    /// process does not refund it.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the process runs on.</param>
    /// <param name="planetBuildingId">The building the process runs in.</param>
    /// <param name="recipeId">The recipe of the order.</param>
    /// <param name="request">What the player asked for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue after the change.</returns>
    Task<ProcessBuildingDto> EnqueueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        int recipeId,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an order from the queue of a building and returns its cost.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the process runs on.</param>
    /// <param name="planetBuildingId">The building the process runs in.</param>
    /// <param name="orderId">The order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building with its queue after the change.</returns>
    Task<ProcessBuildingDto> CancelAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        long orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the terraforming a player may start from a colony: the devices of its storage and
    /// the free bodies the player sees (task 00014, section 9).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The colony the ship takes the device from.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The terraforming of the colony.</returns>
    Task<TerraformingDto> GetTerraformingAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a one-shot device on at a free body: the device is spent and the deposits of the
    /// body become rich. A colony and a body terraformed before are refused (task 00014,
    /// section 9).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The colony the ship takes the device from.</param>
    /// <param name="targetPlanetId">The free body.</param>
    /// <param name="partTypeId">The device.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome with the terraforming of the colony after the change.</returns>
    Task<TerraformingResultDto> TerraformAsync(
        string playerId,
        long planetId,
        long targetPlanetId,
        int partTypeId,
        CancellationToken cancellationToken = default);
}

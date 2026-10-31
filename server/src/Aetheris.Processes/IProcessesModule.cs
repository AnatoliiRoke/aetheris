using Aetheris.Processes.Api;
using Aetheris.Processes.Services;

namespace Aetheris.Processes;

/// <summary>
/// The public contract of the processes module (task 00014): the kinds of the processes,
/// their recipes, the queues of the buildings and the terraforming a ship starts.
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
    /// Gets every order that is in the queues of the buildings of a body that run a process,
    /// with the places of those queues.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The orders of all the buildings of the body.</returns>
    Task<ProcessActiveOrdersDto> GetActiveOrdersAsync(
        string playerId,
        long planetId,
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
    /// Gets the terraforming a player may start: the free bodies that may be settled with a ship
    /// of the player by them, and the devices in the holds of those ships (task 00014, sections 9
    /// and 11). The terraforming comes only from a ship, so no colony is named.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The terraforming of the player.</returns>
    Task<TerraformingDto> GetTerraformingAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a one-shot device from the hold of a ship on at the free body the ship stands by:
    /// the device is spent and every deposit of the body gets a multiplier of its own. A colony,
    /// a body that may not be settled and a body terraformed before are refused (task 00014,
    /// sections 9 and 11). The device is never taken from the storage of a colony.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="targetPlanetId">The free body the ship stands by.</param>
    /// <param name="shipId">The ship whose hold carries the device.</param>
    /// <param name="partTypeId">The device.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome with the terraforming of the player after the change.</returns>
    Task<TerraformingResultDto> TerraformAsync(
        string playerId,
        long targetPlanetId,
        long shipId,
        int partTypeId,
        CancellationToken cancellationToken = default);
}

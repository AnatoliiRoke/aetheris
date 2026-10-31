using Aetheris.Production.Api;
using Aetheris.Production.Services;

namespace Aetheris.Production;

/// <summary>
/// The public contract of the production module (task 00013): the parts, the recipes, the
/// queues of the factories and the storage of the parts.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8).
/// The module reads the world and the bodies through their contracts and keeps its own
/// tables.
/// </remarks>
public interface IProductionModule
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
    Task<ProductionBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the factories of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factories of the body.</returns>
    Task<IReadOnlyList<ProductionFactorySummaryDto>> GetFactoriesAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a factory with its recipes, its queue and the storage of the parts.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factory.</returns>
    Task<ProductionFactoryDto> GetFactoryAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every batch that is in the queues of the factories of a body, with the places of
    /// those queues.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The batches of all the factories of the body.</returns>
    Task<ProductionActiveOrdersDto> GetActiveOrdersAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the storage of the parts of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks of the parts and the storage.</returns>
    Task<ProductionStockDto> GetStockAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a batch of a recipe in the queue of a factory.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="recipeId">The recipe.</param>
    /// <param name="quantity">How many units the batch holds.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factory after the change.</returns>
    Task<ProductionFactoryDto> EnqueueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        int recipeId,
        long quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a batch from the queue of a factory and returns its whole cost.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body the factory stands on.</param>
    /// <param name="planetBuildingId">The factory.</param>
    /// <param name="orderId">The batch.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The factory after the change.</returns>
    Task<ProductionFactoryDto> CancelAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        long orderId,
        CancellationToken cancellationToken = default);
}

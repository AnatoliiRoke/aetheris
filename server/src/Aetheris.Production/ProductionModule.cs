using Aetheris.Production.Api;
using Aetheris.Production.Services;

namespace Aetheris.Production;

/// <summary>
/// The implementation of <see cref="IProductionModule"/>: a thin front of the services of
/// the module.
/// </summary>
public sealed class ProductionModule : IProductionModule
{
    private readonly ProductionBootstrapService _bootstrap;
    private readonly ProductionQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the module.</param>
    /// <param name="queue">The queue of the factories.</param>
    public ProductionModule(
        ProductionBootstrapService bootstrap,
        ProductionQueueService queue)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(queue);

        _bootstrap = bootstrap;
        _queue = queue;
    }

    /// <inheritdoc />
    public string ModuleName => "Production";

    /// <inheritdoc />
    public Task<ProductionBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ProductionFactorySummaryDto>> GetFactoriesAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default) =>
        _queue.ReadFactoriesAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<ProductionFactoryDto> GetFactoryAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken = default) =>
        _queue.ReadFactoryAsync(playerId, planetId, planetBuildingId, cancellationToken);

    /// <inheritdoc />
    public Task<ProductionActiveOrdersDto> GetActiveOrdersAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default) =>
        _queue.ReadActiveOrdersAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<ProductionStockDto> GetStockAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default) =>
        _queue.ReadStockAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<ProductionFactoryDto> EnqueueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        int recipeId,
        long quantity,
        CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(playerId, planetId, planetBuildingId, recipeId, quantity, cancellationToken);

    /// <inheritdoc />
    public Task<ProductionFactoryDto> CancelAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        long orderId,
        CancellationToken cancellationToken = default) =>
        _queue.CancelAsync(playerId, planetId, planetBuildingId, orderId, cancellationToken);
}

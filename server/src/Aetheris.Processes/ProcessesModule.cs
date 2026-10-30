using Aetheris.Processes.Api;
using Aetheris.Processes.Handlers;
using Aetheris.Processes.Services;

namespace Aetheris.Processes;

/// <summary>
/// The implementation of <see cref="IProcessesModule"/>: a thin front of the services of the
/// module.
/// </summary>
public sealed class ProcessesModule : IProcessesModule
{
    private readonly ProcessesBootstrapService _bootstrap;
    private readonly ProcessQueueService _queue;
    private readonly TerraformingHandler _terraforming;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessesModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the module.</param>
    /// <param name="queue">The queue of the processes.</param>
    /// <param name="terraforming">The terraforming started by a ship.</param>
    public ProcessesModule(ProcessesBootstrapService bootstrap, ProcessQueueService queue, TerraformingHandler terraforming)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(terraforming);

        _bootstrap = bootstrap;
        _queue = queue;
        _terraforming = terraforming;
    }

    /// <inheritdoc />
    public string ModuleName => "Processes";

    /// <inheritdoc />
    public Task<ProcessesBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessBuildingSummaryDto>> GetBuildingsAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default) =>
        _queue.ReadBuildingsAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessBuildingDto> GetBuildingAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        CancellationToken cancellationToken = default) =>
        _queue.ReadBuildingAsync(playerId, planetId, planetBuildingId, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessBuildingDto> EnqueueAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        int recipeId,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        _queue.EnqueueAsync(playerId, planetId, planetBuildingId, recipeId, request, cancellationToken);

    /// <inheritdoc />
    public Task<ProcessBuildingDto> CancelAsync(
        string playerId,
        long planetId,
        long planetBuildingId,
        long orderId,
        CancellationToken cancellationToken = default) =>
        _queue.CancelAsync(playerId, planetId, planetBuildingId, orderId, cancellationToken);

    /// <inheritdoc />
    public Task<TerraformingDto> GetTerraformingAsync(
        string playerId,
        long planetId,
        CancellationToken cancellationToken = default) =>
        _terraforming.ReadAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<TerraformingResultDto> TerraformAsync(
        string playerId,
        long planetId,
        long targetPlanetId,
        int partTypeId,
        CancellationToken cancellationToken = default) =>
        _terraforming.LaunchAsync(playerId, planetId, targetPlanetId, partTypeId, cancellationToken);
}

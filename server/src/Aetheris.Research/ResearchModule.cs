using Aetheris.Research.Api;
using Aetheris.Research.Services;

namespace Aetheris.Research;

/// <summary>
/// The implementation of <see cref="IResearchModule"/>: a thin front of the services of the
/// module.
/// </summary>
public sealed class ResearchModule : IResearchModule
{
    private readonly ResearchBootstrapService _bootstrap;
    private readonly ResearchQueueService _queue;
    private readonly ResearchTreeBuilder _builder;
    private readonly IShipyardTime _shipyardTime;
    private readonly IPlanetLimit _planetLimit;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the module.</param>
    /// <param name="queue">The queue of the researches.</param>
    /// <param name="builder">The builder of the tree of the technologies.</param>
    /// <param name="shipyardTime">The multiplier of the assembly time.</param>
    /// <param name="planetLimit">The limit of the bodies of a player (task 00073).</param>
    public ResearchModule(
        ResearchBootstrapService bootstrap,
        ResearchQueueService queue,
        ResearchTreeBuilder builder,
        IShipyardTime shipyardTime,
        IPlanetLimit planetLimit)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(shipyardTime);
        ArgumentNullException.ThrowIfNull(planetLimit);

        _bootstrap = bootstrap;
        _queue = queue;
        _builder = builder;
        _shipyardTime = shipyardTime;
        _planetLimit = planetLimit;
    }

    /// <inheritdoc />
    public string ModuleName => "Research";

    /// <inheritdoc />
    public Task<ResearchBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<ResearchTreeDto> GetTreeAsync(string playerId, long? planetId, CancellationToken cancellationToken = default)
    {
        ResearchTreeSnapshot snapshot = await _queue.ReadAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        return _builder.Build(snapshot);
    }

    /// <inheritdoc />
    public async Task<ResearchTreeDto> EnqueueAsync(
        string playerId,
        long planetId,
        int technologyId,
        int targetLevel,
        CancellationToken cancellationToken = default)
    {
        ResearchTreeSnapshot snapshot = await _queue.EnqueueAsync(playerId, planetId, technologyId, targetLevel, cancellationToken).ConfigureAwait(false);

        return _builder.Build(snapshot);
    }

    /// <inheritdoc />
    public async Task<ResearchTreeDto> CancelAsync(
        string playerId,
        int queueItemId,
        CancellationToken cancellationToken = default)
    {
        ResearchTreeSnapshot snapshot = await _queue.CancelAsync(playerId, queueItemId, cancellationToken).ConfigureAwait(false);

        return _builder.Build(snapshot);
    }

    /// <inheritdoc />
    public Task<decimal> ShipyardTimeMultiplierAsync(string playerId, long planetId, CancellationToken cancellationToken = default) =>
        _shipyardTime.MultiplierAsync(playerId, planetId, cancellationToken);

    /// <inheritdoc />
    public Task<int> GetPlanetLimitAsync(string playerId, CancellationToken cancellationToken = default) =>
        _planetLimit.LimitAsync(playerId, cancellationToken);
}

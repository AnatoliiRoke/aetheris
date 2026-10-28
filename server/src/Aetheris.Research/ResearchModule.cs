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

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the module.</param>
    /// <param name="queue">The queue of the researches.</param>
    /// <param name="builder">The builder of the tree of the technologies.</param>
    public ResearchModule(
        ResearchBootstrapService bootstrap,
        ResearchQueueService queue,
        ResearchTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(builder);

        _bootstrap = bootstrap;
        _queue = queue;
        _builder = builder;
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
}

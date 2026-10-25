using System.Threading.Channels;
using Aetheris.Core.Configuration;

namespace Aetheris.Host.Realtime;

/// <summary>
/// Assigns sequence numbers to game events and pushes them to connected clients.
/// </summary>
/// <remarks>
/// Sequence numbers are per player and strictly increasing. A client that loses the
/// connection sends the last received number and receives only the missed events
/// (documentation/tech/04-api.md, section 5).
/// </remarks>
public sealed class GameEventPublisher
{
    /// <summary>
    /// The SignalR method name clients listen to.
    /// </summary>
    public const string ReceiveMethod = "gameEvent";

    private readonly IGameConfig _config;
    private readonly Channel<GameEventEnvelope> _channel =
        Channel.CreateUnbounded<GameEventEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    private long _globalSeq;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameEventPublisher"/> class.
    /// </summary>
    /// <param name="config">The game configuration, used for the event sequence step.</param>
    public GameEventPublisher(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
    }

    /// <summary>
    /// Gets the next event sequence number.
    /// </summary>
    /// <returns>The next sequence number.</returns>
    public long NextSeq() => Interlocked.Increment(ref _globalSeq);

    /// <summary>
    /// Queues an event for delivery.
    /// </summary>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The JSON payload.</param>
    /// <returns>The queued envelope, including its sequence number.</returns>
    public GameEventEnvelope Enqueue(string type, string payload)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        GameEventEnvelope envelope = new(NextSeq(), type, payload, DateTimeOffset.UtcNow);

        _channel.Writer.TryWrite(envelope);

        return envelope;
    }

    /// <summary>
    /// Reads queued events, waiting until at least one is available.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <returns>The next envelope.</returns>
    public async ValueTask<GameEventEnvelope> ReadAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the configured step between event sequence numbers.
    /// </summary>
    /// <returns>The sequence step, by default 1.</returns>
    public long SeqStep() => Math.Max(1, _config.GetInt64("test.event_seq_step", 1));
}

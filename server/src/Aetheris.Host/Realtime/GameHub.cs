using Aetheris.Host.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Aetheris.Host.Realtime;

/// <summary>
/// SignalR hub that pushes game events to connected clients.
/// </summary>
public sealed class GameHub : Hub
{
    /// <summary>
    /// The hub route.
    /// </summary>
    public const string Route = "/hubs/game";

    private readonly GameEventPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHub"/> class.
    /// </summary>
    /// <param name="publisher">The event publisher.</param>
    public GameHub(GameEventPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(publisher);

        _publisher = publisher;
    }

    /// <summary>
    /// Handles a client connection.
    /// </summary>
    /// <returns>A task that completes when the connection is established.</returns>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync().ConfigureAwait(false);

        await Clients.Caller.SendAsync("connected", new { hub = Route }).ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a test event to the caller. Used by the SignalR test and by manual checks.
    /// </summary>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The JSON payload.</param>
    /// <returns>The published envelope, including its sequence number.</returns>
    public async Task<GameEventEnvelope> PublishTestEvent(string type = "test", string payload = "{}")
    {
        GameEventEnvelope envelope = _publisher.Enqueue(type, payload);

        await Clients.Caller.SendAsync(GameEventPublisher.ReceiveMethod, envelope).ConfigureAwait(false);

        return envelope;
    }
}

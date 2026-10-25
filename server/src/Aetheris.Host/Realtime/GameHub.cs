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
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHub"/> class.
    /// </summary>
    /// <param name="publisher">The event publisher.</param>
    /// <param name="environment">The host environment, used to switch off the test hook outside development.</param>
    public GameHub(GameEventPublisher publisher, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(environment);

        _publisher = publisher;
        _environment = environment;
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
    /// <remarks>
    /// This is a test hook for acceptance criterion 5 of task 00001. It is switched off
    /// outside development, because otherwise any connected client could generate events
    /// with arbitrary sequence numbers and confuse the reconnect logic of other clients.
    /// </remarks>
    public async Task<GameEventEnvelope> PublishTestEvent(string type = "test", string payload = "{}")
    {
        if (!_environment.IsDevelopment())
        {
            throw new HubException("The test event hook is available in development only.");
        }

        GameEventEnvelope envelope = _publisher.Enqueue(type, payload);

        await Clients.Caller.SendAsync(GameEventPublisher.ReceiveMethod, envelope).ConfigureAwait(false);

        return envelope;
    }
}

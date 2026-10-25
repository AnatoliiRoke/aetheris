using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Commands;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Aetheris.Host.Realtime;

/// <summary>
/// SignalR hub that pushes game events to connected clients.
/// </summary>
/// <remarks>
/// Only a signed-in player can connect (task 00002, section 2.3). A subscription to the
/// events of an object passes the same ownership check as a command, so a player never
/// receives the events of another player's objects.
/// </remarks>
[Authorize(Policy = SecurityPolicies.Player)]
public sealed class GameHub : Hub
{
    /// <summary>
    /// The path prefix of every hub.
    /// </summary>
    public const string RoutePrefix = "/hubs";

    /// <summary>
    /// The hub route.
    /// </summary>
    public const string Route = RoutePrefix + "/game";

    private readonly GameEventPublisher _publisher;
    private readonly IHostEnvironment _environment;
    private readonly OwnershipChecker _ownership;
    private readonly SecurityEventLog _securityLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameHub"/> class.
    /// </summary>
    /// <param name="publisher">The event publisher.</param>
    /// <param name="environment">The host environment, used to switch off the test hook outside development.</param>
    /// <param name="ownership">The ownership check.</param>
    /// <param name="securityLog">The security journal.</param>
    public GameHub(
        GameEventPublisher publisher,
        IHostEnvironment environment,
        OwnershipChecker ownership,
        SecurityEventLog securityLog)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(ownership);
        ArgumentNullException.ThrowIfNull(securityLog);

        _publisher = publisher;
        _environment = environment;
        _ownership = ownership;
        _securityLog = securityLog;
    }

    /// <summary>
    /// Builds the name of the group that receives the events of an object.
    /// </summary>
    /// <param name="objectType">The object type.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <returns>The group name.</returns>
    public static string GroupName(string objectType, string objectId) => $"{objectType}:{objectId}";

    /// <summary>
    /// Handles a client connection.
    /// </summary>
    /// <returns>A task that completes when the connection is established.</returns>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync().ConfigureAwait(false);

        // Every player receives the events of their own player object without asking.
        string playerId = Context.UserIdentifier!;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(PlayerObjectResolver.Type, playerId)).ConfigureAwait(false);

        await Clients.Caller.SendAsync("connected", new { hub = Route }).ConfigureAwait(false);
    }

    /// <summary>
    /// Subscribes the caller to the events of an object.
    /// </summary>
    /// <param name="objectType">The object type.</param>
    /// <param name="objectId">The object identifier.</param>
    /// <returns>A task that completes when the subscription is active.</returns>
    /// <exception cref="HubException">The caller does not own the object.</exception>
    public async Task Subscribe(string objectType, string objectId)
    {
        string playerId = Context.UserIdentifier!;

        if (!await _ownership.IsOwnerAsync(playerId, objectType, objectId, Context.ConnectionAborted).ConfigureAwait(false))
        {
            await _securityLog
                .WriteAsync(SecurityEventTypes.OwnershipDenied, Context.GetHttpContext(), playerId, "subscription:" + objectType)
                .ConfigureAwait(false);

            throw new HubException(GameErrorCodes.NotYourObject);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(objectType, objectId)).ConfigureAwait(false);
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

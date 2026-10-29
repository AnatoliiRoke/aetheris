using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Production.Services;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Production.Events;

/// <summary>
/// Makes the unit of a factory ready when its moment comes (task 00013, section 3.1).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event: the unit is added to the storage of the
/// body and the next one of the batch starts at the end of it. An event of a cancelled
/// batch, or of a unit a change of the player already made, changes nothing.
/// </remarks>
public sealed class ProductionUnitReadyHandler : IScheduledEventHandler
{
    private readonly ProductionQueueService _queue;
    private readonly IWorldModule _world;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionUnitReadyHandler"/> class.
    /// </summary>
    /// <param name="queue">The queue of the factories.</param>
    /// <param name="world">The world, for the owner of the body.</param>
    /// <param name="clock">The game clock.</param>
    public ProductionUnitReadyHandler(
        ProductionQueueService queue,
        IWorldModule world,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);

        _queue = queue;
        _world = world;
        _clock = clock;
    }

    /// <inheritdoc />
    public string EventType => ProductionQueueService.CompleteEventType;

    /// <inheritdoc />
    public async Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        ProductionQueueService.CompletePayload payload =
            JsonSerializer.Deserialize<ProductionQueueService.CompletePayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(payload.PlanetId, cancellationToken).ConfigureAwait(false);

        if (facts?.OwnerPlayerId is not { } ownerPlayerId)
        {
            // A body without an owner has no factory and no queue, so the event changes
            // nothing.
            return;
        }

        // The scheduler may run late, and a test may run it ahead of the clock: what is due at
        // the later of the two moments is made, each unit at its own end.
        DateTimeOffset now = _clock.GameNow > scheduledEvent.RunAt ? _clock.GameNow : scheduledEvent.RunAt;

        await _queue
            .CompleteDueAsync(ownerPlayerId, payload.PlanetId, payload.PlanetBuildingId, now, cancellationToken)
            .ConfigureAwait(false);
    }
}

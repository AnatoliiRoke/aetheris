using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>
/// Completes the assembly of a ship when its end comes (task 00039).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event. An event of a cancelled record, or of an
/// assembly a read already completed, changes nothing. A late run finishes the ship at the end
/// that was stored, and a test may run the event ahead of the clock.
/// </remarks>
public sealed class ShipyardCompleteHandler : IScheduledEventHandler
{
    private readonly ShipyardQueueService _queue;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipyardCompleteHandler"/> class.
    /// </summary>
    /// <param name="queue">The queue of the shipyard.</param>
    /// <param name="clock">The game clock.</param>
    public ShipyardCompleteHandler(ShipyardQueueService queue, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(clock);

        _queue = queue;
        _clock = clock;
    }

    /// <inheritdoc />
    public string EventType => ShipyardQueueService.CompleteEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        ShipyardQueueService.CompletePayload payload =
            JsonSerializer.Deserialize<ShipyardQueueService.CompletePayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        DateTimeOffset now = _clock.GameNow > scheduledEvent.RunAt ? _clock.GameNow : scheduledEvent.RunAt;

        return _queue.CompleteDueAsync(payload.PlayerId, payload.PlanetId, now, cancellationToken);
    }
}

using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Research.Services;

namespace Aetheris.Research.Events;

/// <summary>
/// Completes the research of a player when its end comes (task 00012, section 3.1).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event: the level of the technology is written,
/// the record leaves the queue and the next one starts at the end of the previous one. An
/// event of a cancelled record, or of a research that a change of the player already
/// completed, changes nothing.
/// </remarks>
public sealed class ResearchCompleteHandler : IScheduledEventHandler
{
    private readonly ResearchQueueService _queue;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchCompleteHandler"/> class.
    /// </summary>
    /// <param name="queue">The queue of the researches.</param>
    /// <param name="clock">The game clock.</param>
    public ResearchCompleteHandler(ResearchQueueService queue, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(clock);

        _queue = queue;
        _clock = clock;
    }

    /// <inheritdoc />
    public string EventType => ResearchQueueService.CompleteEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        ResearchQueueService.CompletePayload payload =
            JsonSerializer.Deserialize<ResearchQueueService.CompletePayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        // The scheduler may run late, and a test may run it ahead of the clock: what is due at
        // the later of the two moments is completed, each research at its own end.
        DateTimeOffset now = _clock.GameNow > scheduledEvent.RunAt ? _clock.GameNow : scheduledEvent.RunAt;

        return _queue.CompleteDueAsync(payload.PlayerId, now, cancellationToken);
    }
}

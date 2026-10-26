using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Planets.Api;
using Aetheris.Planets.Services;

namespace Aetheris.Planets.Events;

/// <summary>
/// Completes the construction of a body when its end comes (task 00011, section 3.1).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event: the level rises, the balance is fixed
/// and the next record of the queue starts at the end of the previous one. An event of a
/// cancelled record, or of a construction that a change of the body already completed,
/// changes nothing.
/// </remarks>
public sealed class ConstructionCompleteHandler : IScheduledEventHandler
{
    private readonly ConstructionService _construction;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructionCompleteHandler"/> class.
    /// </summary>
    /// <param name="construction">The changes of a body.</param>
    /// <param name="clock">The game clock.</param>
    public ConstructionCompleteHandler(ConstructionService construction, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(clock);

        _construction = construction;
        _clock = clock;
    }

    /// <inheritdoc />
    public string EventType => ConstructionService.CompleteEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        ConstructionService.CompletePayload payload = JsonSerializer.Deserialize<ConstructionService.CompletePayload>(scheduledEvent.Payload, PlanetsJson.Options)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        // The scheduler may run late, and a test may run it ahead of the clock: what is due at
        // the later of the two moments is completed, each construction at its own end.
        DateTimeOffset now = _clock.GameNow > scheduledEvent.RunAt ? _clock.GameNow : scheduledEvent.RunAt;

        return _construction.CompleteDueAsync(payload.PlanetId, now, cancellationToken);
    }
}

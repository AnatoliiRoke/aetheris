using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>
/// Does one cycle of the extraction of a fleet when its end comes (task 00050).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event. An event of a plan that is not under way
/// anymore changes nothing: the plan was stopped, deleted or finished.
/// </remarks>
public sealed class FleetExtractionCycleHandler : IScheduledEventHandler
{
    private readonly FleetExtractionCycleService _cycles;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetExtractionCycleHandler"/> class.
    /// </summary>
    /// <param name="cycles">The cycles of the extraction.</param>
    public FleetExtractionCycleHandler(FleetExtractionCycleService cycles)
    {
        ArgumentNullException.ThrowIfNull(cycles);

        _cycles = cycles;
    }

    /// <inheritdoc />
    public string EventType => FleetExtractionCycleService.CycleEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        FleetExtractionCycleService.CyclePayload payload =
            JsonSerializer.Deserialize<FleetExtractionCycleService.CyclePayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        return _cycles.CycleAsync(payload.FleetId, payload.OrderId, cancellationToken);
    }
}

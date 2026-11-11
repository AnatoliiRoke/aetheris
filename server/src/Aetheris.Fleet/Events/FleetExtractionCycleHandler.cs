using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>
/// Does one cycle of the extraction of a fleet when its end comes (task 00050, task 00053).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event. An event of a plan that is not under way
/// anymore, or of an earlier run of it, changes nothing. The logical time of the step is the moment
/// the event was planned for, so a late run does not shift the cycles after it.
/// </remarks>
public sealed class FleetExtractionCycleHandler : IScheduledEventHandler
{
    private readonly FlightPlanRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetExtractionCycleHandler"/> class.
    /// </summary>
    /// <param name="runner">Carries the orders of the plans out.</param>
    public FleetExtractionCycleHandler(FlightPlanRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        _runner = runner;
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

        return _runner.HandleCycleAsync(payload, scheduledEvent.RunAt, cancellationToken);
    }
}

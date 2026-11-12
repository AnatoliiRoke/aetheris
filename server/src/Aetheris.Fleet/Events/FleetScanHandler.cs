using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>Completes a fleet scan at its scheduled time.</summary>
public sealed class FleetScanHandler : IScheduledEventHandler
{
    private readonly FlightPlanRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="FleetScanHandler"/> class.</summary>
    /// <param name="runner">The flight plan runner.</param>
    public FleetScanHandler(FlightPlanRunner runner) => _runner = runner;

    /// <inheritdoc />
    public string EventType => FlightPlanRunner.ScanEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);
        FlightPlanRunner.ScanPayload payload = JsonSerializer.Deserialize<FlightPlanRunner.ScanPayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");
        return _runner.HandleScanAsync(payload, scheduledEvent.RunAt, cancellationToken);
    }
}

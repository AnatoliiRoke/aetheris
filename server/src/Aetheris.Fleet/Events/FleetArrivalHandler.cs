using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>
/// Brings a fleet to its destination when the end of its flight comes (task 00053).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event. The fleet gets its new position, and the plan
/// goes on with the next order. An arrival of a flight that was stopped changes nothing.
/// </remarks>
public sealed class FleetArrivalHandler : IScheduledEventHandler
{
    private readonly FlightPlanRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetArrivalHandler"/> class.
    /// </summary>
    /// <param name="runner">Carries the orders of the plans out.</param>
    public FleetArrivalHandler(FlightPlanRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        _runner = runner;
    }

    /// <inheritdoc />
    public string EventType => FlightPlanRunner.ArrivalEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        FlightPlanRunner.ArrivalPayload payload =
            JsonSerializer.Deserialize<FlightPlanRunner.ArrivalPayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        return _runner.HandleArrivalAsync(payload, scheduledEvent.RunAt, cancellationToken);
    }
}

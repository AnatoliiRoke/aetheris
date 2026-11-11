using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>
/// Ends an order of waiting of a fleet when its time has passed (task 00053).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event. The plan goes on with the next order; the end
/// of a wait of a plan that was stopped or started anew changes nothing.
/// </remarks>
public sealed class FleetWaitHandler : IScheduledEventHandler
{
    private readonly FlightPlanRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetWaitHandler"/> class.
    /// </summary>
    /// <param name="runner">Carries the orders of the plans out.</param>
    public FleetWaitHandler(FlightPlanRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        _runner = runner;
    }

    /// <inheritdoc />
    public string EventType => FlightPlanRunner.WaitEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        FlightPlanRunner.WaitPayload payload =
            JsonSerializer.Deserialize<FlightPlanRunner.WaitPayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        return _runner.HandleWaitAsync(payload, scheduledEvent.RunAt, cancellationToken);
    }
}

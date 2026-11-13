using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet.Events;

/// <summary>
/// Completes the colonization of a planet when the deployment of the colony module is over
/// (task 00073).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event: the body belongs to the player, the colony is
/// founded on it and the module is taken out of the hold of the fleet, all together. The
/// colonization of a plan that was stopped or started anew changes nothing.
/// </remarks>
public sealed class ColonizeCompleteHandler : IScheduledEventHandler
{
    private readonly FlightPlanRunner _runner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColonizeCompleteHandler"/> class.
    /// </summary>
    /// <param name="runner">Carries the orders of the plans out.</param>
    public ColonizeCompleteHandler(FlightPlanRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);

        _runner = runner;
    }

    /// <inheritdoc />
    public string EventType => FlightPlanRunner.ColonizeEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        FlightPlanRunner.ColonizePayload payload =
            JsonSerializer.Deserialize<FlightPlanRunner.ColonizePayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        return _runner.HandleColonizeCompleteAsync(payload, scheduledEvent.RunAt, cancellationToken);
    }
}

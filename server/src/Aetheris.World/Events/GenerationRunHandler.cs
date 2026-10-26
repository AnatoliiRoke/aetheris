using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.World.Api;
using Aetheris.World.Services;

namespace Aetheris.World.Events;

/// <summary>
/// Runs a generation run of the admin panel when the scheduler picks up its event
/// (task 00007, decision 3).
/// </summary>
public sealed class GenerationRunHandler : IScheduledEventHandler
{
    private readonly GenerationRunService _runs;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerationRunHandler"/> class.
    /// </summary>
    /// <param name="runs">The generation runs.</param>
    public GenerationRunHandler(GenerationRunService runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        _runs = runs;
    }

    /// <inheritdoc />
    public string EventType => GenerationRunService.EventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        GenerationRunPayload payload = JsonSerializer.Deserialize<GenerationRunPayload>(scheduledEvent.Payload, WorldJson.Options)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no generation run.");

        return _runs.ExecuteAsync(payload.RunId, cancellationToken);
    }
}

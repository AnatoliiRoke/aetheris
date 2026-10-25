using Aetheris.Core.Scheduling;
using Aetheris.World.Services;

namespace Aetheris.World.Events;

/// <summary>
/// Brings new fields into a sector after one disappeared (task 00004, decision 10).
/// </summary>
public sealed class FieldRespawnHandler : IScheduledEventHandler
{
    private readonly FieldService _fields;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldRespawnHandler"/> class.
    /// </summary>
    /// <param name="fields">The field service.</param>
    public FieldRespawnHandler(FieldService fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        _fields = fields;
    }

    /// <inheritdoc />
    public string EventType => FieldService.RespawnEventType;

    /// <inheritdoc />
    public Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        return _fields.SpawnFieldsAsync(scheduledEvent.SectorId, scheduledEvent.Id, cancellationToken);
    }
}

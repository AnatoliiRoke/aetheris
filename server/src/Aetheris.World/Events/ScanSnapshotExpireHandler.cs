using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.World.Services;

namespace Aetheris.World.Events;

/// <summary>Removes a scan snapshot at its expiration time.</summary>
public sealed class ScanSnapshotExpireHandler : IScheduledEventHandler
{
    private readonly ScanSnapshotService _snapshots;

    /// <summary>Initializes a new instance of the <see cref="ScanSnapshotExpireHandler"/> class.</summary>
    /// <param name="snapshots">The snapshot service.</param>
    public ScanSnapshotExpireHandler(ScanSnapshotService snapshots) => _snapshots = snapshots;

    /// <inheritdoc />
    public string EventType => ScanSnapshotService.ExpireEventType;

    /// <inheritdoc />
    public async Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);
        long id = JsonSerializer.Deserialize<long>(scheduledEvent.Payload);
        await _snapshots.ExpireAsync(id, scheduledEvent.RunAt, cancellationToken).ConfigureAwait(false);
    }
}

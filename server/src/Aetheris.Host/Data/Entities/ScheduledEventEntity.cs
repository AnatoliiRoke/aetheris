namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>scheduled_event</c> table: every timed action in the game.
/// </summary>
/// <remarks>
/// See documentation/tech/07-planuvalnyk-podii.md for the scheduler design.
/// </remarks>
public sealed class ScheduledEventEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the monotonically increasing sequence number that resolves equal timestamps.
    /// </summary>
    public long Seq { get; set; }

    /// <summary>
    /// Gets or sets the execution timestamp in game time.
    /// </summary>
    public DateTimeOffset RunAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sector that owns the event.
    /// </summary>
    public int SectorId { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON payload of the event.
    /// </summary>
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the event status: <c>pending</c>, <c>done</c>, <c>cancelled</c> or <c>failed</c>.
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Gets or sets an optional key that prevents the same action from creating two events.
    /// </summary>
    public string? DedupeKey { get; set; }

    /// <summary>
    /// Gets or sets the version used to cancel or replace the event.
    /// </summary>
    public int Version { get; set; }
}

namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>player_event</c> table: a notification queued for one player.
/// </summary>
/// <remarks>
/// The <see cref="Seq"/> number lets a client that lost the connection ask for the
/// events it missed (documentation/tech/04-api.md, section 5).
/// </remarks>
public sealed class PlayerEventEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the owning player.
    /// </summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the per-player sequence number.
    /// </summary>
    public long Seq { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the JSON payload of the event.
    /// </summary>
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}

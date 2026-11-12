namespace Aetheris.Mail.Data.Entities;

/// <summary>
/// A complaint of a player about a letter (task 00060).
/// </summary>
/// <remarks>
/// The complaint is written for the moderators: the player names the letter and the reason, and
/// the row waits in the table until a moderator looks at it. The screen of the moderation comes
/// with a later task, so the complaint waits here for now.
/// </remarks>
public sealed class MailComplaintEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the player who complained.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the letter the complaint is about.</summary>
    public long MessageId { get; set; }

    /// <summary>
    /// Gets or sets the reason of the complaint, as the player wrote it.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the moment of game time the complaint was written at.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets whether a moderator has looked at the complaint.
    /// </summary>
    public bool IsReviewed { get; set; }
}

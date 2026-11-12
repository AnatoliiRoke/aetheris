namespace Aetheris.Mail.Data.Entities;

/// <summary>
/// A draft of a letter that was not sent yet (task 00060).
/// </summary>
/// <remarks>
/// A draft belongs to the player who wrote it and nobody else. It holds the receiver, the subject
/// and the text, so the player may go back to it later and send it.
/// </remarks>
public sealed class MailDraftEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the player the draft belongs to.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the receiver named by the draft, as the player wrote it, or
    /// <see langword="null"/> when the draft has no receiver yet.
    /// </summary>
    public string? ReceiverPlayerId { get; set; }

    /// <summary>Gets or sets the subject of the draft.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Gets or sets the text of the draft.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the moment of game time the draft was written at.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the moment of game time the draft was changed at last.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

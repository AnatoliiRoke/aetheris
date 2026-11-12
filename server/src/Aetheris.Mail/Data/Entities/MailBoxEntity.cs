namespace Aetheris.Mail.Data.Entities;

/// <summary>
/// The box of a player: one letter of that player in one folder (task 00060).
/// </summary>
/// <remarks>
/// <para>
/// A letter of one player to another lies in two boxes: the box of the sender in the folder
/// «sent», and the box of the receiver in the folder «incoming». The flags of the box say what
/// the player did with the letter: it is read, it is saved or it lies in the bin.
/// </para>
/// <para>
/// The box carries the flags of the letter for its own player only: the receiver sees the letter
/// as unread, and the sender sees it as read.
/// </para>
/// </remarks>
public sealed class MailBoxEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the player the box belongs to.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the letter of the box.</summary>
    public long MessageId { get; set; }

    /// <summary>
    /// Gets or sets the folder of the letter for this player, one of <see cref="MailFolders"/>.
    /// </summary>
    public string FolderCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the player has read the letter.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets whether the player has saved the letter: a saved letter lies in the folder
    /// «saved» and is not counted in the bin.
    /// </summary>
    public bool IsSaved { get; set; }

    /// <summary>
    /// Gets or sets whether the player has moved the letter to the bin.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the moment of game time the letter was moved to the bin, or
    /// <see langword="null"/> when it is not in the bin.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the moment of game time the letter was put into the folder, so the list is
    /// ordered by it.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// The folders of the mail (task 00060).
/// </summary>
/// <remarks>
/// The folders are codes of the game: they are read from the seeds of the data and they never
/// change, because the letters of the old games keep their folder.
/// </remarks>
public static class MailFolders
{
    /// <summary>The letters other players wrote to the player.</summary>
    public const string Incoming = "incoming";

    /// <summary>The letters the player wrote to other players.</summary>
    public const string Sent = "sent";

    /// <summary>The letters the player saved.</summary>
    public const string Saved = "saved";

    /// <summary>The letters the player moved to the bin.</summary>
    public const string Trash = "trash";

    /// <summary>
    /// Gets every folder of the mail, in the order the screen shows them.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Incoming, Sent, Saved, Trash];
}

namespace Aetheris.Mail.Data.Entities;

/// <summary>
/// A letter of the mail (task 00060): what one player wrote, or what the game wrote to a player.
/// </summary>
/// <remarks>
/// <para>
/// The letter is written once and is copied into the box of every party: the box of the sender
/// holds the letter in the folder «sent», and the box of the receiver holds it in the folder
/// «incoming». A letter of the game has no sender, so it lies only in the box of the player it
/// was written to.
/// </para>
/// <para>
/// The text of a letter of a player is written by the player and is kept as it is. The text of a
/// letter of the game is not kept here: the letter carries the key of the text and the parameters
/// of it, so the text is read from the texts of the game and may be translated (task 00060,
/// section 3.1, point 5).
/// </para>
/// </remarks>
public sealed class MailMessageEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the sender of the letter, or <see langword="null"/> when the game wrote it.
    /// </summary>
    public string? SenderPlayerId { get; set; }

    /// <summary>
    /// Gets or sets the receiver of the letter, or <see langword="null"/> when the letter is a
    /// draft that was never sent.
    /// </summary>
    public string? ReceiverPlayerId { get; set; }

    /// <summary>Gets or sets the category of the letter.</summary>
    public long CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the subject of the letter. The subject of a letter of the game is empty: the
    /// title of such a letter is part of its text.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text of the letter, when it was written by a player.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key of the text of a letter of the game, or <see langword="null"/> for a
    /// letter of a player.
    /// </summary>
    public string? TextKey { get; set; }

    /// <summary>
    /// Gets or sets the parameters of the text of a letter of the game as a document, or
    /// <see langword="null"/> for a letter of a player.
    /// </summary>
    public string? TextParameters { get; set; }

    /// <summary>
    /// Gets or sets the kind of the letter: a letter of a player, a letter of the game or a
    /// report. The kind is a code of the game, and it is read from the seeds of the data.
    /// </summary>
    public string KindCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the kind of the letter of the game, when it has one, for example
    /// <c>extraction_report</c>. It tells the client what the letter is about, so the letter may
    /// be drawn with a mark of its own. The code is named by the module that writes the letter, so
    /// this module knows no game term.
    /// </summary>
    public string? SystemKindCode { get; set; }

    /// <summary>
    /// Gets or sets the moment of game time the letter was written at.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// The kinds of a letter (task 00060).
/// </summary>
/// <remarks>
/// The kinds are codes of the game: they are read from the seeds of the data and they never
/// change, because the letters of the old games keep their kind.
/// </remarks>
public static class MailMessageKinds
{
    /// <summary>A letter a player wrote to another player.</summary>
    public const string Player = "player";

    /// <summary>A letter the game wrote to a player.</summary>
    public const string System = "system";

    /// <summary>A report of the game, for example a report of a mission.</summary>
    public const string Report = "report";
}

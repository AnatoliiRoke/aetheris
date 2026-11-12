namespace Aetheris.Mail.Data.Entities;

/// <summary>
/// A category of letters (task 00060): the kind a letter is shown under in the list of the mail.
/// </summary>
/// <remarks>
/// <para>
/// The categories are data, not code: a row of the table is one category, and the game adds one
/// without a change of the code (documentation/tech/09, section 2). The name and the description
/// of a category live in the texts of the game, under the key of the code of the row.
/// </para>
/// <para>
/// The folder of a letter and its category are two different things: the folder says where the
/// letter stands for the player (incoming, sent, saved, bin), and the category says what the
/// letter is about (a letter of a player, a letter of the system, a report, a letter of the market).
/// </para>
/// </remarks>
public sealed class MailCategoryEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the code of the category, for example <c>mail_category_1</c>. The code names
    /// the category in the texts of the game and in the seeds of the data.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key of the name of the category in the texts of the game.
    /// </summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the key of the description of the category in the texts of the game.
    /// </summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the icon of the category in the client, or <see langword="null"/>
    /// when the category has no icon of its own.
    /// </summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets the position of the category in the list of the filters: the smaller the
    /// number, the earlier the category.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets whether the category is shown in the filters of the list. A hidden category
    /// still holds its letters.
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Gets or sets whether the category is a part of the archive: an archived category keeps its
    /// letters and is not offered in new ones.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the moment of game time the row was created at.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

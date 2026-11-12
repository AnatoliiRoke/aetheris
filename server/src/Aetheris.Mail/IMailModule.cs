namespace Aetheris.Mail;

/// <summary>
/// The public contract of the Mail module (task 00060): the letters of the game.
/// </summary>
/// <remarks>
/// <para>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8). A
/// module that has something to tell a player writes a letter through
/// <see cref="SendSystemAsync"/> and knows nothing of the tables of the mail.
/// </para>
/// <para>
/// A letter of the game carries the key of its text and the parameters of it, not the text
/// itself, so the text is read from the texts of the game and may be translated.
/// </para>
/// </remarks>
public interface IMailModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Writes a letter of the game to a player (task 00060).
    /// </summary>
    /// <param name="playerId">The player the letter is written to.</param>
    /// <param name="categoryCode">The code of the category of the letter, for example
    /// <c>mail_category_2</c>. The category is data, so a module that writes letters names the
    /// category it wants and the game may add another one without a change of the code.</param>
    /// <param name="textKey">The key of the text of the letter in the texts of the game.</param>
    /// <param name="parameters">The parameters of the text as a document, for example
    /// <c>{"name":"Вега"}</c>, or <see langword="null"/> when the text has no parameters.</param>
    /// <param name="systemKindCode">The kind of the letter, for example <c>extraction_report</c>, so the
    /// client may draw the letter with a mark of its own, or <see langword="null"/> when the
    /// letter has none.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier of the letter, or <see langword="null"/> when the category is
    /// unknown and the letter was not written.</returns>
    Task<long?> SendSystemAsync(
        string playerId,
        string categoryCode,
        string textKey,
        string? parameters,
        string? systemKindCode = null,
        CancellationToken cancellationToken = default);
}

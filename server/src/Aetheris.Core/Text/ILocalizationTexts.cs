namespace Aetheris.Core.Text;

/// <summary>
/// The text of a localisation key in one language.
/// </summary>
/// <param name="Key">The localisation key, for example the name key of a reference row.</param>
/// <param name="Language">The language, a two-letter code such as <c>uk</c>.</param>
/// <param name="Text">The text.</param>
public sealed record LocalizationTextEntry(string Key, string Language, string Text);

/// <summary>
/// The texts of one language as the client gets them.
/// </summary>
/// <param name="Language">The language.</param>
/// <param name="Version">The version of the pack; it grows with every added or changed text of the language.</param>
/// <param name="Texts">The texts by localisation key.</param>
public sealed record LocalizationPack(string Language, long Version, IReadOnlyDictionary<string, string> Texts);

/// <summary>
/// The texts of the game data: the names and the descriptions of the reference rows (task 00022,
/// decision 2).
/// </summary>
/// <remarks>
/// The texts live in the database, not in the code or the client: a reference row only holds the
/// keys of its name and description. The host keeps the texts in memory and drops the copy when
/// they change, so a renamed row shows its new name without a restart. A module that creates a
/// reference row adds its texts through this interface in the transaction of the request.
/// </remarks>
public interface ILocalizationTexts
{
    /// <summary>
    /// Gets the languages of the texts; a new reference row needs a text in every one of them,
    /// and the first one is the fallback.
    /// </summary>
    IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Gets the largest length of one text.
    /// </summary>
    int MaxLength { get; }

    /// <summary>
    /// Gets the texts of a language.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The pack; empty for a language without texts.</returns>
    Task<LocalizationPack> GetPackAsync(string language, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the texts whose key has no text in the language yet; an existing text is never
    /// overwritten. The texts join the transaction of the request.
    /// </summary>
    /// <param name="entries">The texts.</param>
    /// <param name="changedBy">Who adds them: an administrator or <c>seed</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of added texts.</returns>
    Task<int> AddMissingAsync(IEnumerable<LocalizationTextEntry> entries, string changedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the copy in memory; call it once the change of the texts is committed.
    /// </summary>
    void Invalidate();
}

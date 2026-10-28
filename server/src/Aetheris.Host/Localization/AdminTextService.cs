using Aetheris.Core.Admin;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Localization;

/// <summary>
/// A text of the admin screen «Тексти»: a key in one language, or the gap where its text is missing.
/// </summary>
/// <param name="Key">The localisation key.</param>
/// <param name="Language">The language.</param>
/// <param name="Text">The text, or <see langword="null"/> when the language has none.</param>
/// <param name="Version">The version, 0 for a missing text.</param>
/// <param name="UpdatedBy">Who changed the text last.</param>
/// <param name="UpdatedAt">When the text was changed last.</param>
public sealed record AdminTextRow(
    string Key,
    string Language,
    string? Text,
    int Version,
    string? UpdatedBy,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// The filters of the admin screen «Тексти».
/// </summary>
/// <param name="Language">Only the texts of one language, or <see langword="null"/> for every language.</param>
/// <param name="Missing">Only the keys whose text is missing.</param>
public sealed record AdminTextFilter(string? Language, bool Missing);

/// <summary>
/// The outcome of a change of a text.
/// </summary>
public enum TextChangeOutcome
{
    /// <summary>The change is written.</summary>
    Changed,

    /// <summary>The key has no text in any language: the screen only edits existing keys.</summary>
    NotFound,

    /// <summary>The text was changed by someone else since it was read.</summary>
    Conflict,
}

/// <summary>
/// Reads and changes the texts of the game data for the admin panel (task 00022, decision 6).
/// </summary>
/// <remarks>
/// A change is written at once; the audit filter then writes the audit record with the old
/// and the new text and the reason.
/// </remarks>
public sealed class AdminTextService
{
    private readonly AetherisDbContext _dbContext;
    private readonly TextSettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminTextService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="settings">The settings of the texts.</param>
    /// <param name="timeProvider">The clock.</param>
    public AdminTextService(AetherisDbContext dbContext, TextSettings settings, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Lists the texts: every key in every language of the settings, with the gaps where a
    /// text is missing. The table holds the names of the reference rows only, so the filters
    /// run in memory.
    /// </summary>
    /// <param name="page">The page and the search text, matched against the key and the text.</param>
    /// <param name="filter">The filters.</param>
    /// <param name="adminNames">The names of the administrators by identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminTextRow>> ListAsync(
        PageRequest page,
        AdminTextFilter filter,
        Func<IEnumerable<string?>, Task<Dictionary<string, string>>> adminNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(adminNames);

        List<LocalizationTextEntity> texts = await _dbContext.LocalizationTexts.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> names = await adminNames(texts.Select(text => text.UpdatedBy)).ConfigureAwait(false);
        Dictionary<(string Key, string Language), LocalizationTextEntity> byKey = texts.ToDictionary(text => (text.Key, text.Language));

        List<string> languages = [.. _settings.Languages.Union(texts.Select(text => text.Language), StringComparer.Ordinal)];
        IEnumerable<string> shownLanguages = filter.Language is null ? languages : [filter.Language];
        List<string> keys = [.. texts.Select(text => text.Key).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

        IEnumerable<AdminTextRow> rows = keys.SelectMany(key => shownLanguages.Select(language =>
            byKey.TryGetValue((key, language), out LocalizationTextEntity? text)
                ? new AdminTextRow(key, language, text.Text, text.Version, names.GetValueOrDefault(text.UpdatedBy, text.UpdatedBy), text.UpdatedAt)
                : new AdminTextRow(key, language, null, 0, null, null)));

        if (filter.Missing)
        {
            rows = rows.Where(row => row.Text is null);
        }

        if (page.Search is { } search)
        {
            // A key matches when any of its texts matches, so a search by the Ukrainian name
            // also shows the English row of the same key.
            HashSet<string> matchingKeys = [.. texts
                .Where(text => text.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
                .Select(text => text.Key)];

            rows = rows.Where(row => row.Key.Contains(search, StringComparison.OrdinalIgnoreCase) || matchingKeys.Contains(row.Key));
        }

        List<AdminTextRow> filtered = rows.ToList();

        return new PageResult<AdminTextRow>([.. filtered.Skip(page.Skip).Take(page.PageSize)], filtered.Count, page.Page, page.PageSize);
    }

    /// <summary>
    /// Changes the text of a key in a language. A missing text of an existing key is added
    /// with the version 0 of the request.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="language">The language, one of the settings.</param>
    /// <param name="text">The new text, already checked.</param>
    /// <param name="version">The version the administrator read, 0 for a missing text.</param>
    /// <param name="adminId">The administrator.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome, the old text and the new version.</returns>
    /// <remarks>
    /// The change is one statement that only matches the version the administrator read, so of
    /// two parallel changes of one text exactly one passes and the other gets a conflict.
    /// </remarks>
    public async Task<(TextChangeOutcome Outcome, string? OldText, int Version)> ChangeAsync(
        string key,
        string language,
        string text,
        int version,
        string adminId,
        CancellationToken cancellationToken)
    {
        LocalizationTextEntity? current = await _dbContext.LocalizationTexts.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Key == key && entry.Language == language, cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (current is null)
        {
            if (!await _dbContext.LocalizationTexts.AnyAsync(entry => entry.Key == key, cancellationToken).ConfigureAwait(false))
            {
                return (TextChangeOutcome.NotFound, null, 0);
            }

            if (version != 0)
            {
                return (TextChangeOutcome.Conflict, null, 0);
            }

            // A parallel request may add the same missing text first; the unique key of the
            // table then refuses the second one, which is a conflict.
            int inserted = await _dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO localization_text (key, language, text, version, updated_at, updated_by)
                VALUES ({key}, {language}, {text}, 1, {now}, {adminId})
                ON CONFLICT (key, language) DO NOTHING
                """,
                cancellationToken).ConfigureAwait(false);

            return inserted == 1 ? (TextChangeOutcome.Changed, null, 1) : (TextChangeOutcome.Conflict, null, 0);
        }

        int updated = await _dbContext.LocalizationTexts
            .Where(entry => entry.Id == current.Id && entry.Version == version)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(entry => entry.Text, text)
                    .SetProperty(entry => entry.Version, entry => entry.Version + 1)
                    .SetProperty(entry => entry.UpdatedAt, now)
                    .SetProperty(entry => entry.UpdatedBy, adminId),
                cancellationToken).ConfigureAwait(false);

        return updated == 1
            ? (TextChangeOutcome.Changed, current.Text, version + 1)
            : (TextChangeOutcome.Conflict, current.Text, current.Version);
    }
}

using System.Collections.Concurrent;
using Aetheris.Core.Text;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Localization;

/// <summary>
/// The texts of every language kept in memory (task 00022, section 3.1).
/// </summary>
/// <remarks>
/// A pack is read from the database once and served from memory until a change of the texts
/// drops it. A pack read while a change was being committed is not kept, so a dropped copy never
/// comes back with the old texts.
/// </remarks>
public sealed class LocalizationTextCache
{
    private readonly ConcurrentDictionary<string, LocalizationPack> _packs = new(StringComparer.Ordinal);
    private long _generation;

    /// <summary>
    /// Gets the current generation; it grows with every drop.
    /// </summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>
    /// Finds the pack of a language in memory.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <param name="pack">The pack, when it is in memory.</param>
    /// <returns><see langword="true"/> when the pack is in memory.</returns>
    public bool TryGet(string language, out LocalizationPack pack) => _packs.TryGetValue(language, out pack!);

    /// <summary>
    /// Keeps a pack read in a generation, unless the texts changed since.
    /// </summary>
    /// <param name="pack">The pack.</param>
    /// <param name="generation">The generation at the start of the read.</param>
    public void Keep(LocalizationPack pack, long generation)
    {
        ArgumentNullException.ThrowIfNull(pack);

        if (generation == Generation)
        {
            _packs[pack.Language] = pack;
        }
    }

    /// <summary>
    /// Drops every pack.
    /// </summary>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _packs.Clear();
    }
}

/// <summary>
/// Reads and adds the texts of the game data in the database of the host.
/// </summary>
/// <remarks>
/// The texts are added through the main context, which owns the transaction of the request, so
/// a reference row of a module and its texts are committed together.
/// </remarks>
public sealed class LocalizationTextStore : ILocalizationTexts
{
    private readonly AetherisDbContext _dbContext;
    private readonly LocalizationTextCache _cache;
    private readonly TextSettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationTextStore"/> class.
    /// </summary>
    /// <param name="dbContext">The main database context of the request.</param>
    /// <param name="cache">The texts in memory.</param>
    /// <param name="settings">The settings of the texts.</param>
    /// <param name="timeProvider">The clock.</param>
    public LocalizationTextStore(AetherisDbContext dbContext, LocalizationTextCache cache, TextSettings settings, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _cache = cache;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Languages => _settings.Languages;

    /// <inheritdoc />
    public int MaxLength => _settings.MaxLength;

    /// <inheritdoc />
    public async Task<LocalizationPack> GetPackAsync(string language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(language);

        if (_cache.TryGet(language, out LocalizationPack cached))
        {
            return cached;
        }

        long generation = _cache.Generation;
        List<LocalizationTextEntity> texts = await _dbContext.LocalizationTexts.AsNoTracking()
            .Where(text => text.Language == language)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Texts are only added or raised to a new version, so the sum of the versions grows with
        // every change of the language and serves as the version of the pack.
        LocalizationPack pack = new(
            language,
            texts.Sum(text => (long)text.Version),
            texts.ToDictionary(text => text.Key, text => text.Text, StringComparer.Ordinal));

        _cache.Keep(pack, generation);

        return pack;
    }

    /// <inheritdoc />
    public async Task<int> AddMissingAsync(IEnumerable<LocalizationTextEntry> entries, string changedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrEmpty(changedBy);

        List<LocalizationTextEntry> candidates = [.. entries.DistinctBy(entry => (entry.Key, entry.Language))];

        if (candidates.Count == 0)
        {
            return 0;
        }

        List<string> keys = [.. candidates.Select(entry => entry.Key).Distinct(StringComparer.Ordinal)];
        HashSet<(string Key, string Language)> existing = [.. (await _dbContext.LocalizationTexts.AsNoTracking()
            .Where(text => keys.Contains(text.Key))
            .Select(text => new { text.Key, text.Language })
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(text => (text.Key, text.Language))];

        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<LocalizationTextEntity> added = [.. candidates
            .Where(entry => !existing.Contains((entry.Key, entry.Language)))
            .Select(entry => new LocalizationTextEntity
            {
                Key = entry.Key,
                Language = entry.Language,
                Text = entry.Text,
                UpdatedAt = now,
                UpdatedBy = changedBy,
            })];

        _dbContext.LocalizationTexts.AddRange(added);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return added.Count;
    }

    /// <inheritdoc />
    public void Invalidate() => _cache.Invalidate();
}

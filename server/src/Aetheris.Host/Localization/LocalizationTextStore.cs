using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Core.Text;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Localization;

/// <summary>
/// The texts of every language kept in memory (task 00022, section 3.1).
/// </summary>
/// <remarks>
/// A pack is read from the database and served from memory until a change of the texts drops it
/// or it is older than <c>texts.pack_cache_seconds</c>. A pack read while a change was being
/// committed is not kept, so a dropped copy never comes back with the old texts.
/// </remarks>
public sealed class LocalizationTextCache
{
    private readonly ConcurrentDictionary<string, CachedPack> _packs = new(StringComparer.Ordinal);
    private long _generation;

    /// <summary>
    /// Gets the current generation; it grows with every drop.
    /// </summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>
    /// Finds the pack of a language in memory, when it is younger than its lifetime.
    /// </summary>
    /// <param name="language">The language.</param>
    /// <param name="lifetime">How long a pack stays in memory; zero keeps none.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="pack">The pack, when it is in memory.</param>
    /// <returns><see langword="true"/> when the pack is in memory and still valid.</returns>
    /// <remarks>
    /// An old pack is not served: it is read from the database again, so a change made outside the
    /// application reaches the clients without a restart (task 00052).
    /// </remarks>
    public bool TryGet(string language, TimeSpan lifetime, TimeProvider timeProvider, out LocalizationPack pack)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (lifetime > TimeSpan.Zero
            && _packs.TryGetValue(language, out CachedPack? cached)
            && timeProvider.GetElapsedTime(cached.Timestamp) < lifetime)
        {
            pack = cached.Pack;

            return true;
        }

        pack = null!;

        return false;
    }

    /// <summary>
    /// Keeps a pack read in a generation, unless the texts changed since.
    /// </summary>
    /// <param name="pack">The pack.</param>
    /// <param name="generation">The generation at the start of the read.</param>
    /// <param name="timestamp">The moment the pack was read, from the clock of the store.</param>
    public void Keep(LocalizationPack pack, long generation, long timestamp)
    {
        ArgumentNullException.ThrowIfNull(pack);

        if (generation == Generation)
        {
            _packs[pack.Language] = new CachedPack(pack, timestamp);
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

    private sealed record CachedPack(LocalizationPack Pack, long Timestamp);
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

    /// <summary>The number of bytes of the hash kept in the tag of the pack.</summary>
    private const int HashByteCount = 6;

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

        if (_cache.TryGet(language, _settings.PackCacheDuration, _timeProvider, out LocalizationPack cached))
        {
            return cached;
        }

        long generation = _cache.Generation;
        long readAt = _timeProvider.GetTimestamp();
        List<LocalizationTextEntity> texts = await _dbContext.LocalizationTexts.AsNoTracking()
            .Where(text => text.Language == language)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Texts are only added or raised to a new version, so the sum of the versions grows with
        // every change of the language and serves as the version of the pack. A key renamed in the
        // database without a new version leaves the sum as it was, so the hash of the content
        // gives the pack its new tag (task 00052).
        LocalizationPack pack = new(
            language,
            texts.Sum(text => (long)text.Version),
            texts.ToDictionary(text => text.Key, text => text.Text, StringComparer.Ordinal),
            ContentHash(texts));

        _cache.Keep(pack, generation, readAt);

        return pack;
    }

    /// <summary>
    /// Builds the short hash of the content of a pack: its keys, texts and versions in the order of
    /// the keys.
    /// </summary>
    /// <param name="texts">The texts of one language.</param>
    /// <returns>The hash, in lower-case hexadecimal.</returns>
    public static string ContentHash(IEnumerable<LocalizationTextEntity> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (LocalizationTextEntity text in texts.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{text.Key}\0{text.Text}\0{text.Version}\0")));
        }

        return Convert.ToHexStringLower(hash.GetCurrentHash().AsSpan(0, HashByteCount));
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
    public async Task<IReadOnlyDictionary<(string Key, string Language), string>> SetAsync(
        IEnumerable<LocalizationTextEntry> entries,
        string changedBy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrEmpty(changedBy);

        List<LocalizationTextEntry> candidates = [.. entries.DistinctBy(entry => (entry.Key, entry.Language))];
        List<string> keys = [.. candidates.Select(entry => entry.Key).Distinct(StringComparer.Ordinal)];
        Dictionary<(string Key, string Language), LocalizationTextEntity> existing = (await _dbContext.LocalizationTexts
            .Where(text => keys.Contains(text.Key))
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(text => (text.Key, text.Language));

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Dictionary<(string Key, string Language), string> before = existing.ToDictionary(entry => entry.Key, entry => entry.Value.Text);

        foreach (LocalizationTextEntry entry in candidates)
        {
            if (!existing.TryGetValue((entry.Key, entry.Language), out LocalizationTextEntity? text))
            {
                _dbContext.LocalizationTexts.Add(new LocalizationTextEntity
                {
                    Key = entry.Key,
                    Language = entry.Language,
                    Text = entry.Text,
                    UpdatedAt = now,
                    UpdatedBy = changedBy,
                });
            }
            else if (!string.Equals(text.Text, entry.Text, StringComparison.Ordinal))
            {
                text.Text = entry.Text;
                text.Version++;
                text.UpdatedAt = now;
                text.UpdatedBy = changedBy;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return before;
    }

    /// <inheritdoc />
    public void Invalidate() => _cache.Invalidate();
}

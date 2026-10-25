using System.Collections.Concurrent;
using System.Globalization;

namespace Aetheris.Core.Configuration;

/// <summary>
/// In-memory implementation of <see cref="IGameConfig"/>.
/// </summary>
/// <remarks>
/// The current values live in an immutable dictionary that is swapped atomically on reload.
/// Readers therefore always observe a consistent set of values and never block on a write.
/// </remarks>
public sealed class GameConfig : IGameConfig
{
    private volatile IReadOnlyDictionary<string, ConfigParameter> _values =
        new Dictionary<string, ConfigParameter>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the version of the currently loaded configuration set.
    /// </summary>
    public int Version { get; private set; }

    /// <inheritdoc />
    public string? GetRaw(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return _values.TryGetValue(key, out ConfigParameter? parameter) ? parameter.Value : null;
    }

    /// <inheritdoc />
    public long GetInt64(string key, long defaultValue = 0)
    {
        string? raw = GetRaw(key);

        return raw is not null && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : defaultValue;
    }

    /// <inheritdoc />
    public double GetDouble(string key, double defaultValue = 0d)
    {
        string? raw = GetRaw(key);

        return raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : defaultValue;
    }

    /// <inheritdoc />
    public bool GetBoolean(string key, bool defaultValue = false)
    {
        string? raw = GetRaw(key);

        return raw is not null && bool.TryParse(raw, out bool value) ? value : defaultValue;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ConfigParameter> Snapshot() => _values;

    /// <summary>
    /// Atomically replaces the whole configuration set.
    /// </summary>
    /// <param name="values">The new parameters, keyed by their dotted key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public void Replace(IEnumerable<KeyValuePair<string, ConfigParameter>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var next = new Dictionary<string, ConfigParameter>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, ConfigParameter> entry in values)
        {
            next[entry.Key] = entry.Value;
        }

        // The volatile write publishes a fully built dictionary to all readers at once.
        _values = next;
        Version++;
    }
}

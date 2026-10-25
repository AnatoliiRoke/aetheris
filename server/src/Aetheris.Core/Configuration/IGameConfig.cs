namespace Aetheris.Core.Configuration;

/// <summary>
/// A single tunable game parameter, stored in the <c>config_parameter</c> table.
/// </summary>
/// <param name="Key">Stable dotted key, for example <c>test.hello_multiplier</c>.</param>
/// <param name="Value">Raw string value as stored in the database.</param>
/// <param name="Version">Monotonically increasing version, bumped on every change.</param>
public sealed record ConfigParameter(string Key, string Value, int Version);

/// <summary>
/// Read access to the game configuration.
/// </summary>
/// <remarks>
/// Game rules must read balance values through this interface only. Values are never
/// hardcoded in rule code, so that the balance can be changed without a new release
/// (see documentation/tech/14 and task 00001, section 2.4).
/// </remarks>
public interface IGameConfig
{
    /// <summary>
    /// Gets the raw value of a parameter.
    /// </summary>
    /// <param name="key">Parameter key.</param>
    /// <returns>The raw value, or <see langword="null"/> when the key is unknown.</returns>
    string? GetRaw(string key);

    /// <summary>
    /// Gets a parameter as a 64-bit integer.
    /// </summary>
    /// <param name="key">Parameter key.</param>
    /// <param name="defaultValue">Value returned when the key is missing or not parseable.</param>
    /// <returns>The parsed value or <paramref name="defaultValue"/>.</returns>
    long GetInt64(string key, long defaultValue = 0);

    /// <summary>
    /// Gets a parameter as a double.
    /// </summary>
    /// <param name="key">Parameter key.</param>
    /// <param name="defaultValue">Value returned when the key is missing or not parseable.</param>
    /// <returns>The parsed value or <paramref name="defaultValue"/>.</returns>
    double GetDouble(string key, double defaultValue = 0d);

    /// <summary>
    /// Gets a parameter as a boolean.
    /// </summary>
    /// <param name="key">Parameter key.</param>
    /// <param name="defaultValue">Value returned when the key is missing or not parseable.</param>
    /// <returns>The parsed value or <paramref name="defaultValue"/>.</returns>
    bool GetBoolean(string key, bool defaultValue = false);

    /// <summary>
    /// Gets all parameters currently loaded in memory.
    /// </summary>
    /// <returns>A snapshot of the loaded parameters.</returns>
    IReadOnlyDictionary<string, ConfigParameter> Snapshot();
}

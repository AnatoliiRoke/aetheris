using System.Globalization;

namespace Aetheris.Host.Data;

/// <summary>
/// A parameter that the server knows about: its key, its starting value and its bounds.
/// </summary>
/// <param name="Key">The dotted parameter key.</param>
/// <param name="DefaultValue">The value written into a fresh database.</param>
/// <param name="Minimum">The smallest numeric value an administrator may set, if the parameter is numeric.</param>
/// <param name="Maximum">The largest numeric value an administrator may set, if the parameter is numeric.</param>
/// <param name="Description">What the parameter controls.</param>
public sealed record ConfigParameterDefinition(
    string Key,
    string DefaultValue,
    double? Minimum,
    double? Maximum,
    string Description)
{
    /// <summary>
    /// Checks that a value fits the bounds of the parameter.
    /// </summary>
    /// <param name="value">The raw value to check.</param>
    /// <returns><see langword="true"/> when the value is acceptable.</returns>
    public bool Accepts(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (Minimum is null && Maximum is null)
        {
            return true;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return false;
        }

        return (Minimum is null || number >= Minimum) && (Maximum is null || number <= Maximum);
    }
}

/// <summary>
/// The initial parameters written into a fresh database.
/// </summary>
/// <remarks>
/// The core has no game rules, so it only seeds neutral <c>test.*</c> parameters and the
/// <c>security.*</c> parameters of its own protection mechanisms. A feature adds its own
/// parameters here when it arrives. Every parameter can be changed later from the admin
/// panel; the seed only provides the starting point for a new database, so that the rules
/// always find a value.
/// </remarks>
public static class ConfigSeed
{
    /// <summary>
    /// The neutral test parameter that shows how a value flows from the database into the code.
    /// </summary>
    public const string HelloMultiplier = "test.hello_multiplier";

    /// <summary>
    /// The largest value of the neutral test parameter.
    /// </summary>
    private const double HelloMultiplierMaximum = 1_000;

    private static readonly ConfigParameterDefinition[] Definitions =
    [
        new(HelloMultiplier, "1", 0, HelloMultiplierMaximum, "Neutral example parameter used by the configuration tests."),
    ];

    private static readonly Dictionary<string, ConfigParameterDefinition> DefinitionsByKey =
        Definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    /// <summary>
    /// Gets every known parameter definition.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> All => Definitions;

    /// <summary>
    /// Finds the definition of a parameter.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The definition, or <see langword="null"/> when the key is not known.</returns>
    public static ConfigParameterDefinition? Find(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return DefinitionsByKey.GetValueOrDefault(key);
    }

    /// <summary>
    /// The parameters and their initial values.
    /// </summary>
    /// <returns>The seed parameters, ready to be inserted.</returns>
    public static IReadOnlyList<(string Key, string Value)> GetParameters()
    {
        return Definitions.Select(definition => (definition.Key, definition.DefaultValue)).ToList();
    }
}

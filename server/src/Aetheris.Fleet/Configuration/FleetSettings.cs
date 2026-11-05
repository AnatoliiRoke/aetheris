using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Fleet.Configuration;

/// <summary>
/// The parameters of the fleet, read at once from the configuration.
/// </summary>
/// <remarks>
/// A missing or unreadable value falls back to the starting value of its definition in
/// <see cref="FleetConfigKeys.Definitions"/>, so the code never carries its own number.
/// </remarks>
public sealed record FleetSettings
{
    /// <summary>
    /// Gets the least set of ammunition the hold of a hull must take.
    /// </summary>
    public decimal MinimumAmmunition { get; init; }

    /// <summary>
    /// Gets the largest number of the characters of the name of a project.
    /// </summary>
    public int DesignNameMaxLength { get; init; }

    /// <summary>
    /// Reads the parameters.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <returns>The parameters.</returns>
    public static FleetSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new FleetSettings
        {
            MinimumAmmunition = ReadDecimal(config, FleetConfigKeys.MinimumAmmunition),
            DesignNameMaxLength = ReadInt32(config, FleetConfigKeys.DesignNameMaxLength),
        };
    }

    private static decimal ReadDecimal(IGameConfig config, string key) =>
        decimal.TryParse(config.GetRaw(key), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : decimal.Parse(Default(key), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) =>
        int.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : int.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static string Default(string key) =>
        FleetConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Economy.Configuration;

/// <summary>
/// The configuration of the economy module, read once per change (task 00015).
/// </summary>
public sealed record EconomySettings
{
    /// <summary>Gets the share of the largest population the population grows with in an hour.</summary>
    public decimal PopulationGrowthPercent { get; init; }

    /// <summary>Gets how many inhabitants die in an hour above the largest population.</summary>
    public decimal PopulationDeclinePerHour { get; init; }

    /// <summary>Gets how many inhabitants a body has the moment it becomes a colony.</summary>
    public long PopulationInitial { get; init; }

    /// <summary>Gets how many credits one inhabitant brings to the civilization in an hour.</summary>
    public decimal CreditsPerInhabitant { get; init; }

    /// <summary>
    /// Reads the settings of the module from the configuration of the game.
    /// </summary>
    /// <param name="config">The configuration of the game.</param>
    /// <returns>The settings.</returns>
    public static EconomySettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new EconomySettings
        {
            PopulationGrowthPercent = ReadDecimal(config, EconomyConfigKeys.PopulationGrowthPercent),
            PopulationDeclinePerHour = ReadDecimal(config, EconomyConfigKeys.PopulationDeclinePerHour),
            PopulationInitial = (long)ReadDecimal(config, EconomyConfigKeys.PopulationInitial),
            CreditsPerInhabitant = ReadDecimal(config, EconomyConfigKeys.CreditsPerInhabitant),
        };
    }

    private static decimal ReadDecimal(IGameConfig config, string key) =>
        decimal.TryParse(config.GetRaw(key), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : decimal.Parse(Default(key), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string Default(string key) =>
        EconomyConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

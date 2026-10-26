using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Planets.Configuration;

/// <summary>
/// The parameters of the bodies, read at once from the configuration.
/// </summary>
/// <remarks>
/// A missing or unreadable value falls back to the starting value of its definition in
/// <see cref="PlanetsConfigKeys.Definitions"/>, so the code never carries its own number.
/// </remarks>
public sealed record PlanetsSettings
{
    /// <summary>Gets the largest level of any building.</summary>
    public int BuildingMaxLevel { get; init; }

    /// <summary>Gets the places of the construction queue, the building under way included.</summary>
    public int QueueBaseSlots { get; init; }

    /// <summary>Gets the largest number of paid places added to the queue.</summary>
    public int QueueMaxExtraSlots { get; init; }

    /// <summary>Gets how long the building tables stay in memory.</summary>
    public TimeSpan CatalogCacheDuration { get; init; }

    /// <summary>Gets the code of the start building.</summary>
    public string StartBuildingCode { get; init; } = string.Empty;

    /// <summary>Gets the level of the start building.</summary>
    public int StartBuildingLevel { get; init; }

    /// <summary>
    /// Reads the parameters.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <returns>The parameters.</returns>
    public static PlanetsSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new PlanetsSettings
        {
            BuildingMaxLevel = ReadInt32(config, PlanetsConfigKeys.BuildingMaxLevel),
            QueueBaseSlots = ReadInt32(config, PlanetsConfigKeys.QueueBaseSlots),
            QueueMaxExtraSlots = ReadInt32(config, PlanetsConfigKeys.QueueMaxExtraSlots),
            CatalogCacheDuration = TimeSpan.FromSeconds(ReadInt64(config, PlanetsConfigKeys.CatalogCacheSeconds)),
            StartBuildingCode = config.GetRaw(PlanetsConfigKeys.StartBuildingCode) ?? Default(PlanetsConfigKeys.StartBuildingCode),
            StartBuildingLevel = ReadInt32(config, PlanetsConfigKeys.StartBuildingLevel),
        };
    }

    private static long ReadInt64(IGameConfig config, string key) =>
        long.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : long.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) => (int)ReadInt64(config, key);

    private static string Default(string key) =>
        PlanetsConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

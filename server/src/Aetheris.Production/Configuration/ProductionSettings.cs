using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Production.Configuration;

/// <summary>
/// The parameters of the production, read at once from the configuration.
/// </summary>
/// <remarks>
/// A missing or unreadable value falls back to the starting value of its definition in
/// <see cref="ProductionConfigKeys.Definitions"/>, so the code never carries its own number.
/// </remarks>
public sealed record ProductionSettings
{
    /// <summary>Gets the places of the queue of a factory, the batch under way included.</summary>
    public int QueueBaseSlots { get; init; }

    /// <summary>Gets the largest number of paid places added to the queue of a factory.</summary>
    public int QueueMaxExtraSlots { get; init; }

    /// <summary>Gets how long the production tables stay in memory.</summary>
    public TimeSpan CatalogCacheDuration { get; init; }

    /// <summary>
    /// Reads the parameters.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <returns>The parameters.</returns>
    public static ProductionSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new ProductionSettings
        {
            QueueBaseSlots = ReadInt32(config, ProductionConfigKeys.QueueBaseSlots),
            QueueMaxExtraSlots = ReadInt32(config, ProductionConfigKeys.QueueMaxExtraSlots),
            CatalogCacheDuration = TimeSpan.FromSeconds(ReadInt64(config, ProductionConfigKeys.CatalogCacheSeconds)),
        };
    }

    private static long ReadInt64(IGameConfig config, string key) =>
        long.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : long.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) => (int)ReadInt64(config, key);

    private static string Default(string key) =>
        ProductionConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

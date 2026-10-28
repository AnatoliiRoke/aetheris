using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Research.Configuration;

/// <summary>
/// The parameters of the research, read at once from the configuration.
/// </summary>
/// <remarks>
/// A missing or unreadable value falls back to the starting value of its definition in
/// <see cref="ResearchConfigKeys.Definitions"/>, so the code never carries its own number.
/// </remarks>
public sealed record ResearchSettings
{
    /// <summary>Gets the places of the queue of the researches, the research under way included.</summary>
    public int QueueBaseSlots { get; init; }

    /// <summary>Gets the largest number of paid places added to the queue.</summary>
    public int QueueMaxExtraSlots { get; init; }

    /// <summary>Gets how long the technology tables stay in memory.</summary>
    public TimeSpan CatalogCacheDuration { get; init; }

    /// <summary>
    /// Reads the parameters.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    /// <returns>The parameters.</returns>
    public static ResearchSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new ResearchSettings
        {
            QueueBaseSlots = ReadInt32(config, ResearchConfigKeys.QueueBaseSlots),
            QueueMaxExtraSlots = ReadInt32(config, ResearchConfigKeys.QueueMaxExtraSlots),
            CatalogCacheDuration = TimeSpan.FromSeconds(ReadInt64(config, ResearchConfigKeys.CatalogCacheSeconds)),
        };
    }

    private static long ReadInt64(IGameConfig config, string key) =>
        long.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : long.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) => (int)ReadInt64(config, key);

    private static string Default(string key) =>
        ResearchConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

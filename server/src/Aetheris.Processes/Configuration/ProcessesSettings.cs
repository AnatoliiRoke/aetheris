using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Processes.Configuration;

/// <summary>
/// The configuration of the processes module, read once per change (task 00014).
/// </summary>
public sealed record ProcessesSettings
{
    /// <summary>Gets the places of the queue of a process, the one under way included.</summary>
    public int QueueBaseSlots { get; init; }

    /// <summary>Gets the largest number of paid places added to the queue of a process.</summary>
    public int QueueMaxExtraSlots { get; init; }

    /// <summary>Gets how long the tables of the processes stay in memory.</summary>
    public TimeSpan CatalogCacheDuration { get; init; }

    /// <summary>
    /// Reads the settings of the module from the configuration of the game.
    /// </summary>
    /// <param name="config">The configuration of the game.</param>
    /// <returns>The settings.</returns>
    public static ProcessesSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new ProcessesSettings
        {
            QueueBaseSlots = ReadInt32(config, ProcessesConfigKeys.QueueBaseSlots),
            QueueMaxExtraSlots = ReadInt32(config, ProcessesConfigKeys.QueueMaxExtraSlots),
            CatalogCacheDuration = TimeSpan.FromSeconds(ReadInt64(config, ProcessesConfigKeys.CatalogCacheSeconds)),
        };
    }

    private static long ReadInt64(IGameConfig config, string key) =>
        long.TryParse(config.GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : long.Parse(Default(key), NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static int ReadInt32(IGameConfig config, string key) => (int)ReadInt64(config, key);

    private static string Default(string key) =>
        ProcessesConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

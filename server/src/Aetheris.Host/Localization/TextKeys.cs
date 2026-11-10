using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Host.Localization;

/// <summary>
/// The configuration keys of the texts of the game data (task 00022).
/// </summary>
public static class TextKeys
{
    /// <summary>
    /// The languages of the texts, separated by commas. The admin panel asks for a text in every
    /// one of them when a reference row is created; the first one is the fallback language.
    /// </summary>
    public const string Languages = "admin.texts.languages";

    /// <summary>The largest length of one text.</summary>
    public const string MaxLength = "admin.texts.max_length";

    /// <summary>
    /// How long the server keeps a built pack of the texts in memory, in seconds. After this time
    /// the pack is read from the database again, so a change made outside the application reaches
    /// the clients without a restart (task 00052).
    /// </summary>
    public const string PackCacheSeconds = "texts.pack_cache_seconds";

    /// <summary>
    /// Gets the definitions of the parameters.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(Languages, "uk,en", null, null, "Languages of the texts of the game data, separated by commas; the first one is the fallback."),
        new(MaxLength, "2000", "1", "10000", "Largest length of one text of the game data."),
        new(PackCacheSeconds, "30", "0", "3600", "Скільки секунд сервер тримає зібраний пакет текстів у пам'яті перед повторним читанням з бази; 0 вимикає кеш."),
    ];
}

/// <summary>
/// The settings of the texts, read from the configuration.
/// </summary>
public sealed class TextSettings
{
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextSettings"/> class.
    /// </summary>
    /// <param name="config">The game configuration.</param>
    public TextSettings(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
    }

    /// <summary>
    /// Gets the languages of the texts; the first one is the fallback.
    /// </summary>
    public IReadOnlyList<string> Languages =>
        [.. (_config.GetRaw(TextKeys.Languages) ?? Default(TextKeys.Languages))
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    /// <summary>
    /// Gets the largest length of one text.
    /// </summary>
    public int MaxLength => (int)_config.GetInt64(TextKeys.MaxLength, long.Parse(Default(TextKeys.MaxLength), CultureInfo.InvariantCulture));

    /// <summary>
    /// Gets how long a built pack of the texts stays in memory; zero keeps none.
    /// </summary>
    public TimeSpan PackCacheDuration =>
        TimeSpan.FromSeconds(_config.GetInt64(TextKeys.PackCacheSeconds, long.Parse(Default(TextKeys.PackCacheSeconds), CultureInfo.InvariantCulture)));

    private static string Default(string key) =>
        TextKeys.Definitions.Single(definition => definition.Key == key).DefaultValue;
}

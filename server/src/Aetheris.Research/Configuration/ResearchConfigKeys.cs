using Aetheris.Core.Configuration;

namespace Aetheris.Research.Configuration;

/// <summary>
/// The configuration keys of the research module with their starting values (task 00012).
/// </summary>
/// <remarks>
/// The levels, the times, the costs and the effects of the technologies live in the tables of
/// the module; these are the limits around them. The host adds these definitions to its seed,
/// so the admin panel checks the bounds like for every other parameter.
/// </remarks>
public static class ResearchConfigKeys
{
    /// <summary>The places of the queue of the researches of a player, the research under way included.</summary>
    public const string QueueBaseSlots = "research.queue.base_slots";

    /// <summary>The largest number of paid places added to the queue of the researches of a player.</summary>
    public const string QueueMaxExtraSlots = "research.queue.max_extra_slots";

    /// <summary>How long the technology tables stay in the memory of the server before they are read again, in seconds.</summary>
    public const string CatalogCacheSeconds = "research.catalog.cache_seconds";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(QueueBaseSlots, "2", "1", "100", "Місць у черзі досліджень гравця, разом із тим, що досліджується."),
        new(QueueMaxExtraSlots, "2", "0", "100", "Найбільша кількість платних місць, доданих до черги досліджень гравця."),
        new(CatalogCacheSeconds, "30", "0", "86400", "Скільки секунд таблиці технологій залишаються в пам'яті сервера перед повторним читанням."),
    ];
}

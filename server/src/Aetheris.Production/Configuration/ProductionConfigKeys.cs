using Aetheris.Core.Configuration;

namespace Aetheris.Production.Configuration;

/// <summary>
/// The configuration keys of the production module with their starting values (task 00013).
/// </summary>
/// <remarks>
/// The recipes, the times, the costs and the capacities live in the tables of the module;
/// these are the limits around them. The host adds these definitions to its seed, so the
/// admin panel checks the bounds like for every other parameter.
/// </remarks>
public static class ProductionConfigKeys
{
    /// <summary>The places of the queue of a factory, the batch under way included.</summary>
    public const string QueueBaseSlots = "production.queue.base_slots";

    /// <summary>The largest number of paid places added to the queue of a factory.</summary>
    public const string QueueMaxExtraSlots = "production.queue.max_extra_slots";

    /// <summary>How long the production tables stay in the memory of the server before they are read again, in seconds.</summary>
    public const string CatalogCacheSeconds = "production.catalog.cache_seconds";

    /// <summary>
    /// The weight of one part in tonnes, for the parts that have no weight of their own yet
    /// (task 00027).
    /// </summary>
    public const string DefaultPartWeight = "production.part.default_weight";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(QueueBaseSlots, "2", "1", "100", "Місць у черзі заводу, разом із тим, що виробляється."),
        new(QueueMaxExtraSlots, "2", "0", "100", "Найбільша кількість платних місць, доданих до черги заводу."),
        new(CatalogCacheSeconds, "30", "0", "86400", "Скільки секунд таблиці виробництва залишаються в пам'яті сервера перед повторним читанням."),
        new(DefaultPartWeight, "1", "0", "1000000000", "Вага однієї деталі в тоннах, для деталей, у яких ще немає власної ваги."),
    ];
}

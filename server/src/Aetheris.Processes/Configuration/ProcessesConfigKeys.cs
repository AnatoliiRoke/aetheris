using Aetheris.Core.Configuration;

namespace Aetheris.Processes.Configuration;

/// <summary>
/// The configuration keys of the processes module with their starting values (task 00014).
/// </summary>
/// <remarks>
/// The kinds of the processes, the recipes, the times, the shares and the ranges live in the
/// tables of the module; these are the limits around them. The host adds these definitions
/// to its seed, so the admin panel checks the bounds like for every other parameter.
/// </remarks>
public static class ProcessesConfigKeys
{
    /// <summary>The places of the queue of a process, the one under way included.</summary>
    public const string QueueBaseSlots = "processes.queue.base_slots";

    /// <summary>The largest number of paid places added to the queue of a process.</summary>
    public const string QueueMaxExtraSlots = "processes.queue.max_extra_slots";

    /// <summary>How long the tables of the processes stay in the memory of the server before they are read again, in seconds.</summary>
    public const string CatalogCacheSeconds = "processes.catalog.cache_seconds";

    /// <summary>
    /// The lowest multiplier a deposit can get from the terraforming: every deposit rolls its own
    /// multiplier between this and the highest one (task 00014, section 11).
    /// </summary>
    public const string TerraformingMultiplierMin = "processes.terraforming.multiplier_min";

    /// <summary>
    /// The highest multiplier a deposit can get from the terraforming (task 00014, section 11).
    /// </summary>
    public const string TerraformingMultiplierMax = "processes.terraforming.multiplier_max";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(QueueBaseSlots, "2", "1", "100", "Місць у черзі процесу, разом із тим, що виконується."),
        new(QueueMaxExtraSlots, "2", "0", "100", "Найбільша кількість платних місць, доданих до черги процесу."),
        new(CatalogCacheSeconds, "30", "0", "86400", "Скільки секунд таблиці процесів залишаються в пам'яті сервера перед повторним читанням."),
        new(TerraformingMultiplierMin, "1.4", "0", "1000", "Нижня межа множника родовища після тераформінгу: кожне родовище отримує власний випадковий множник між цією та верхньою межею."),
        new(TerraformingMultiplierMax, "1.9", "0", "1000", "Верхня межа множника родовища після тераформінгу: кожне родовище отримує власний випадковий множник між нижньою та цією межею."),
    ];
}

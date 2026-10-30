using Aetheris.Core.Configuration;

namespace Aetheris.Economy.Configuration;

/// <summary>
/// The configuration keys of the economy module with their starting values (task 00015).
/// </summary>
/// <remarks>
/// The numbers of the rules of the economy live here and in the tables of the module; the
/// host adds these definitions to its seed, so the admin panel checks the bounds like for
/// every other parameter.
/// </remarks>
public static class EconomyConfigKeys
{
    /// <summary>The share of the largest population the population grows with in an hour.</summary>
    public const string PopulationGrowthPercent = "economy.population.growth_percent";

    /// <summary>How many inhabitants die in an hour above the largest population.</summary>
    public const string PopulationDeclinePerHour = "economy.population.decline_per_hour";

    /// <summary>How many credits one inhabitant brings to the civilization in an hour.</summary>
    public const string CreditsPerInhabitant = "economy.credits.per_inhabitant";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(PopulationGrowthPercent, "5", "0", "100", "Відсоток від найбільшого населення, на який населення зростає за годину."),
        new(PopulationDeclinePerHour, "1", "0", "1000", "Скільки мешканців зменшується за годину, коли населення вище за найбільше."),
        new(CreditsPerInhabitant, "1", "0", "1000", "Скільки кредитів приносить один мешканець цивілізації за годину."),
    ];
}

namespace Aetheris.Economy.Services;

/// <summary>
/// The logical codes of the economy module (task 00015).
/// </summary>
/// <remarks>
/// The codes are read by the program, so they are stable and readable. The numbers of the
/// rules of the game are not here: they live in the configuration and in the data
/// (documentation/tech/09, section 3.4).
/// </remarks>
public static class EconomyCodes
{
    /// <summary>The code of the group of the codes of the balances of the civilizations.</summary>
    public const string BalanceCodeGroup = "civilization_balance";

    /// <summary>The prefix of the keys of the texts of the balances.</summary>
    public const string BalanceTextPrefix = "economy.balance";

    /// <summary>
    /// The effect of a building that raises the largest population of a body.
    /// </summary>
    public const string PopulationMaxEffect = "population_max";

    /// <summary>
    /// The effect of a building or a technology that raises the speed the population grows
    /// with.
    /// </summary>
    public const string PopulationGrowthBonusEffect = "population_growth_bonus";

    /// <summary>
    /// The effect of a technology that raises the income of the credits.
    /// </summary>
    public const string CreditIncomeBonusEffect = "credit_income_bonus";
}

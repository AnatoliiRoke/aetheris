namespace Aetheris.Economy.Api;

/// <summary>
/// The population of a body at a moment of game time (task 00015).
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Amount">The number of the inhabitants.</param>
/// <param name="Maximum">The largest population the body may have.</param>
/// <param name="GrowthPerHour">How many inhabitants the population grows with in an hour; it is negative when the population dies down.</param>
/// <param name="OverLimit">Whether the population is above the largest population it may have.</param>
/// <param name="TimeToMaximum">How long the population needs to reach the largest population, or <see langword="null"/> when it will never reach it.</param>
public sealed record PopulationFacts(
    long PlanetId,
    long Amount,
    long Maximum,
    decimal GrowthPerHour,
    bool OverLimit,
    TimeSpan? TimeToMaximum);

/// <summary>
/// The balance of a currency of a civilization at a moment of game time (task 00015).
/// </summary>
/// <param name="PlayerId">The player.</param>
/// <param name="ResourceTypeId">The currency.</param>
/// <param name="Amount">The balance.</param>
/// <param name="IncomePerHour">How much the balance grows with in an hour.</param>
public sealed record CurrencyBalanceFacts(
    string PlayerId,
    int ResourceTypeId,
    long Amount,
    decimal IncomePerHour);

/// <summary>
/// The economy of a player at a moment of game time (task 00015).
/// </summary>
/// <param name="Populations">The populations of the bodies of the player.</param>
/// <param name="Balances">The balances of the currencies of the civilization.</param>
/// <param name="GameNow">The moment of game time.</param>
public sealed record EconomyOverviewDto(
    IReadOnlyList<PopulationFacts> Populations,
    IReadOnlyList<CurrencyBalanceFacts> Balances,
    DateTimeOffset GameNow);

namespace Aetheris.Economy.Services;

/// <summary>
/// The rules of the income of the credits (task 00015, decision 4).
/// </summary>
/// <remarks>
/// The credits of a civilization are the integral of the population of its bodies over the
/// time, so the income of a body is not constant: the population grows and dies down, and
/// the largest population may change in the middle of an interval. The rules are a pure
/// function of the population and the time, so they can be checked without a database.
/// </remarks>
public static class CreditRules
{
    /// <summary>
    /// The number of the ticks in an hour, used to turn a speed into an amount.
    /// </summary>
    private const decimal TicksPerHour = (decimal)TimeSpan.TicksPerHour;

    /// <summary>
    /// The number the average of the two ends of an interval is divided by, so the average of
    /// the population of a body over the interval is the area of a trapezoid.
    /// </summary>
    private const decimal AverageDivisor = 2m;

    /// <summary>
    /// Brings the credits of a civilization from one moment to another.
    /// </summary>
    /// <param name="amount">The credits at <paramref name="from"/>.</param>
    /// <param name="population">The population of the body at <paramref name="from"/>.</param>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPerHour">How many inhabitants the population grows with in an hour; it is negative when the population dies down.</param>
    /// <param name="creditsPerInhabitant">How many credits one inhabitant brings in an hour.</param>
    /// <param name="from">The moment of the credits.</param>
    /// <param name="to">The moment to bring them to.</param>
    /// <returns>The credits at <paramref name="to"/>.</returns>
    /// <remarks>
    /// The population grows linearly, so the integral of the population over the time is the
    /// area of a trapezoid: the average of the population at the ends of the interval times
    /// its length. The fraction of a credit is left out, so the balance is a whole number.
    /// </remarks>
    public static long Accumulate(
        long amount,
        long population,
        long maximum,
        decimal growthPerHour,
        decimal creditsPerInhabitant,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        decimal elapsedTicks = to > from ? (to - from).Ticks : 0;
        decimal hours = elapsedTicks / TicksPerHour;

        if (hours <= 0m)
        {
            return amount;
        }

        long populationAtEnd = PopulationRules.Accumulate(population, maximum, growthPerHour, from, to);
        decimal average = (population + populationAtEnd) / AverageDivisor;
        decimal earned = average * creditsPerInhabitant * hours;

        return amount + (long)decimal.Floor(earned);
    }

    /// <summary>
    /// Gets the income of the credits of a body in an hour.
    /// </summary>
    /// <param name="population">The population of the body now.</param>
    /// <param name="creditsPerInhabitant">How many credits one inhabitant brings in an hour.</param>
    /// <param name="incomeMultiplier">The multiplier the technologies give the income.</param>
    /// <returns>The income in an hour.</returns>
    public static decimal IncomePerHour(long population, decimal creditsPerInhabitant, decimal incomeMultiplier) =>
        population * creditsPerInhabitant * incomeMultiplier;
}

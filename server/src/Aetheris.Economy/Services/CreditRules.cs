namespace Aetheris.Economy.Services;

/// <summary>
/// The rules of the income of the credits (task 00015, decision 4).
/// </summary>
/// <remarks>
/// The credits of a civilization are the integral of the population of its bodies over the
/// time, so the income of a body is not constant: the population grows and dies down and stops
/// at the largest population in the middle of an interval. The rules are a pure function of the
/// population and the time, so they can be checked without a database.
/// </remarks>
public static class CreditRules
{
    /// <summary>
    /// The number the square of a time is divided by in the area under a line.
    /// </summary>
    private const decimal Two = 2m;

    /// <summary>
    /// Brings the credits of a civilization from one moment to another, in whole credits.
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
    /// The fraction of a credit is left out. The service keeps the fraction between its counts
    /// (see <see cref="Earned"/>), so a balance that is counted often loses nothing.
    /// </remarks>
    public static long Accumulate(
        long amount,
        long population,
        long maximum,
        decimal growthPerHour,
        decimal creditsPerInhabitant,
        DateTimeOffset from,
        DateTimeOffset to) =>
        amount + (long)decimal.Floor(Earned(population, maximum, growthPerHour, creditsPerInhabitant, from, to));

    /// <summary>
    /// Gets the credits a body earns over an interval, with their fraction.
    /// </summary>
    /// <param name="population">The population of the body at <paramref name="from"/>, with its fraction.</param>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPerHour">The speed of the population at <paramref name="from"/>: positive below the largest population, negative above it.</param>
    /// <param name="creditsPerInhabitant">How many credits one inhabitant brings in an hour.</param>
    /// <param name="from">The start of the interval.</param>
    /// <param name="to">The end of the interval.</param>
    /// <returns>The credits.</returns>
    /// <remarks>
    /// <para>
    /// The population changes linearly until it reaches the largest population and then stays
    /// there, so the integral has two parts: the area under the line until the population meets
    /// the largest population, and the constant population after that. A population that grows
    /// to the limit and a population that dies down to it are the same case from two sides.
    /// </para>
    /// <para>
    /// The speed and the largest population are the ones of the whole interval: the service counts
    /// the balance up to every change of them.
    /// </para>
    /// </remarks>
    public static decimal Earned(
        decimal population,
        long maximum,
        decimal growthPerHour,
        decimal creditsPerInhabitant,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        decimal hours = PopulationRules.Hours(from, to);

        if (hours <= 0m)
        {
            return 0m;
        }

        if (population < 0m)
        {
            population = 0m;
        }

        // How far the population is from the largest population in the direction it moves.
        decimal distance = growthPerHour >= 0m ? maximum - population : population - maximum;

        decimal personHours;

        if (growthPerHour == 0m || distance <= 0m)
        {
            // The population does not move: it is at the limit or has no speed.
            personHours = population * hours;
        }
        else
        {
            decimal hoursToLimit = distance / Math.Abs(growthPerHour);

            if (hoursToLimit >= hours)
            {
                // The population is on its line the whole interval.
                personHours = (population * hours) + (growthPerHour * hours * hours / Two);
            }
            else
            {
                // The population is on its line until the limit and constant after it.
                personHours = (population * hoursToLimit)
                    + (growthPerHour * hoursToLimit * hoursToLimit / Two)
                    + (maximum * (hours - hoursToLimit));
            }
        }

        return personHours * creditsPerInhabitant;
    }

    /// <summary>
    /// Gets the income of the credits of a body in an hour.
    /// </summary>
    /// <param name="population">The population of the body now.</param>
    /// <param name="creditsPerInhabitant">How many credits one inhabitant brings in an hour, with the multiplier of the technologies already in it.</param>
    /// <returns>The income in an hour.</returns>
    public static decimal IncomePerHour(long population, decimal creditsPerInhabitant) =>
        population * creditsPerInhabitant;
}

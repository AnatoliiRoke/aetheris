namespace Aetheris.Economy.Services;

/// <summary>
/// The rules of the population of a body (task 00015, decision 2).
/// </summary>
/// <remarks>
/// The population grows to the largest population the body has and dies down above it, so
/// the rules are a pure function of the amount, the largest population, the speed and the
/// time. They are here and not in the service, so they can be checked without a database.
/// </remarks>
public static class PopulationRules
{
    /// <summary>
    /// The number of the ticks in an hour, used to turn a speed into an amount.
    /// </summary>
    private const decimal TicksPerHour = (decimal)TimeSpan.TicksPerHour;

    /// <summary>
    /// The number of the per cent in one, used to turn a share into a part.
    /// </summary>
    private const decimal Percent = 100m;

    /// <summary>
    /// Brings the population of a body from one moment to another, in whole inhabitants.
    /// </summary>
    /// <param name="amount">The population at <paramref name="from"/>.</param>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPerHour">How many inhabitants the population grows with in an hour; it is negative when the population dies down.</param>
    /// <param name="from">The moment of the population.</param>
    /// <param name="to">The moment to bring it to.</param>
    /// <returns>The population at <paramref name="to"/>.</returns>
    /// <remarks>
    /// The population grows linearly to the largest population and stops there; above it the
    /// population dies down with the speed it is given, also linearly, and never goes below
    /// the largest population. The growth counts whole inhabitants that have been born and the
    /// decline whole inhabitants that have died, so the population is a whole number.
    /// </remarks>
    public static long Accumulate(long amount, long maximum, decimal growthPerHour, DateTimeOffset from, DateTimeOffset to)
    {
        decimal exact = AccumulateExact(amount, maximum, growthPerHour, from, to);

        return Settle(exact, growthPerHour < 0m).Amount;
    }

    /// <summary>
    /// Brings the population of a body from one moment to another, with the fraction of an
    /// inhabitant the speed has not yet made a whole one.
    /// </summary>
    /// <param name="amount">The population at <paramref name="from"/>, with its fraction.</param>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPerHour">How many inhabitants the population grows with in an hour; it is negative when the population dies down.</param>
    /// <param name="from">The moment of the population.</param>
    /// <param name="to">The moment to bring it to.</param>
    /// <returns>The population at <paramref name="to"/>.</returns>
    public static decimal AccumulateExact(decimal amount, long maximum, decimal growthPerHour, DateTimeOffset from, DateTimeOffset to)
    {
        if (maximum < 0)
        {
            maximum = 0;
        }

        if (amount < 0m)
        {
            amount = 0m;
        }

        decimal hours = Hours(from, to);

        if (hours <= 0m)
        {
            return amount;
        }

        if (growthPerHour >= 0m)
        {
            decimal room = Math.Max(maximum - amount, 0m);

            return amount + Math.Min(growthPerHour * hours, room);
        }

        decimal above = Math.Max(amount - maximum, 0m);

        return amount - Math.Min(-growthPerHour * hours, above);
    }

    /// <summary>
    /// Splits a population with a fraction into the whole inhabitants and the fraction left.
    /// </summary>
    /// <param name="exact">The population with its fraction.</param>
    /// <param name="declining">Whether the population dies down: an inhabitant dies only when the whole of it has gone, so the fraction is counted from above.</param>
    /// <returns>The whole inhabitants and the fraction, so their sum is <paramref name="exact"/>.</returns>
    public static (long Amount, decimal Fraction) Settle(decimal exact, bool declining)
    {
        long amount = (long)(declining ? decimal.Ceiling(exact) : decimal.Floor(exact));

        return (amount, exact - amount);
    }

    /// <summary>
    /// Gets the speed a population has at its start: it dies down above the largest population
    /// and grows below it.
    /// </summary>
    /// <param name="amount">The population, with its fraction.</param>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPerHour">The speed of the growth.</param>
    /// <param name="declinePerHour">The speed of the decline, a negative number.</param>
    /// <returns>The speed in an hour.</returns>
    public static decimal SpeedAt(decimal amount, long maximum, decimal growthPerHour, decimal declinePerHour) =>
        amount > maximum ? declinePerHour : growthPerHour;

    /// <summary>
    /// Gets the speed the population of a body grows with, in an hour.
    /// </summary>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPercent">The share of the largest population the population grows with in an hour.</param>
    /// <param name="growthMultiplier">The multiplier of the speed.</param>
    /// <returns>The speed; it is never negative.</returns>
    /// <remarks>
    /// The speed is a share of the largest population, so a body that can hold nobody does not
    /// grow. Above the largest population the population dies down instead; see
    /// <see cref="DeclinePerHour"/>.
    /// </remarks>
    public static decimal GrowthPerHour(long maximum, decimal growthPercent, decimal growthMultiplier)
    {
        if (maximum <= 0)
        {
            return 0m;
        }

        decimal growth = maximum * (growthPercent / Percent) * growthMultiplier;

        return growth > 0m ? growth : 0m;
    }

    /// <summary>
    /// Gets the speed the population of a body dies down with, in an hour.
    /// </summary>
    /// <param name="declinePerHour">How many inhabitants die in an hour above the largest population.</param>
    /// <returns>The speed, as a negative number.</returns>
    public static decimal DeclinePerHour(decimal declinePerHour) =>
        declinePerHour > 0m ? -declinePerHour : 0m;

    /// <summary>
    /// Gets how long the population of a body needs to reach its largest population.
    /// </summary>
    /// <param name="amount">The population now.</param>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPerHour">How many inhabitants the population grows with in an hour.</param>
    /// <returns>The time, or <see langword="null"/> when the population will never reach it.</returns>
    public static TimeSpan? TimeToMaximum(long amount, long maximum, decimal growthPerHour)
    {
        if (amount >= maximum)
        {
            return TimeSpan.Zero;
        }

        if (growthPerHour <= 0m)
        {
            return null;
        }

        decimal hours = (maximum - amount) / growthPerHour;

        return TimeSpan.FromTicks((long)decimal.Ceiling(hours * TicksPerHour));
    }

    /// <summary>
    /// Gets the length of an interval in hours; an interval that goes back is empty.
    /// </summary>
    /// <param name="from">The start.</param>
    /// <param name="to">The end.</param>
    /// <returns>The hours.</returns>
    internal static decimal Hours(DateTimeOffset from, DateTimeOffset to) =>
        to > from ? (to - from).Ticks / TicksPerHour : 0m;
}

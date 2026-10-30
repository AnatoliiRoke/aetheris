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
    /// Brings the population of a body from one moment to another.
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
    /// the largest population. The fraction of an inhabitant is left out, so the population is
    /// a whole number of inhabitants.
    /// </remarks>
    public static long Accumulate(long amount, long maximum, decimal growthPerHour, DateTimeOffset from, DateTimeOffset to)
    {
        if (maximum < 0)
        {
            maximum = 0;
        }

        if (amount < 0)
        {
            amount = 0;
        }

        decimal elapsedTicks = to > from ? (to - from).Ticks : 0;
        decimal hours = elapsedTicks / TicksPerHour;

        if (hours <= 0m)
        {
            return amount;
        }

        if (growthPerHour >= 0m)
        {
            long room = Math.Max(maximum - amount, 0);
            long added = (long)decimal.Floor(growthPerHour * hours);

            return amount + Math.Min(added, room);
        }

        long above = Math.Max(amount - maximum, 0);
        long removed = (long)decimal.Floor(-growthPerHour * hours);

        return amount - Math.Min(removed, above);
    }

    /// <summary>
    /// Gets the speed the population of a body grows or dies down with, in an hour.
    /// </summary>
    /// <param name="maximum">The largest population the body may have.</param>
    /// <param name="growthPercent">The share of the largest population the population grows with in an hour.</param>
    /// <param name="growthMultiplier">The multiplier the technologies give the speed.</param>
    /// <param name="declinePerHour">How many inhabitants die in an hour above the largest population.</param>
    /// <returns>The speed; it is negative when the population is above the largest population.</returns>
    /// <remarks>
    /// Below the largest population the population grows with a share of it; above it the
    /// population dies down with the speed it is given. The growth is not applied above the
    /// largest population, because the population there only dies down.
    /// </remarks>
    public static decimal GrowthPerHour(
        long maximum,
        decimal growthPercent,
        decimal growthMultiplier,
        decimal declinePerHour)
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
}

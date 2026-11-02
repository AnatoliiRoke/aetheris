namespace Aetheris.Research.Services;

/// <summary>
/// The multiplier a learned technology gives the time of a research.
/// </summary>
/// <remarks>
/// It works like the multiplier of the construction time: the value of the learned level
/// multiplies the duration, and a value below one makes the research faster. A technology
/// that shortens construction or production does not shorten the research.
/// </remarks>
public static class ResearchTime
{
    /// <summary>
    /// Multiplies the multipliers of the research time the player has learned.
    /// </summary>
    /// <param name="catalog">The catalog of the technologies.</param>
    /// <param name="levels">The levels the player has learned, by technology.</param>
    /// <returns>The multiplier. One changes nothing.</returns>
    public static decimal Multiplier(TechnologyCatalog catalog, IReadOnlyDictionary<int, int> levels)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(levels);

        decimal researchTime = decimal.One;

        foreach ((int technologyId, int level) in levels)
        {
            TechnologyInfo? technology = catalog.FindTechnology(technologyId);

            if (technology is null)
            {
                continue;
            }

            foreach (TechnologyEffectInfo effect in technology.Levels.GetValueOrDefault(level)?.Effects ?? [])
            {
                if (effect.EffectType != TechnologyEffectTypes.ResearchTime)
                {
                    continue;
                }

                // An empty multiplier changes nothing; a negative one is broken data, which must
                // never make the research instant.
                if (effect.Value < 0)
                {
                    throw new InvalidOperationException(
                        $"The technology {technology.Code} has a negative multiplier of the research time at level {level}.");
                }

                if (effect.Value > 0)
                {
                    researchTime *= effect.Value;
                }
            }
        }

        return researchTime;
    }

    /// <summary>
    /// Applies the multiplier to a duration. The seconds are a whole number, rounded up,
    /// like the construction time.
    /// </summary>
    /// <param name="durationSeconds">The duration of the level without the multiplier.</param>
    /// <param name="multiplier">The multiplier of the research time.</param>
    /// <returns>The duration the player waits.</returns>
    public static long Scale(long durationSeconds, decimal multiplier) =>
        (long)decimal.Ceiling(durationSeconds * multiplier);
}

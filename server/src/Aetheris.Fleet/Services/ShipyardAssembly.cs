using Aetheris.Fleet.Configuration;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The share of an assembly cost and the technology applied to a stored duration (task 00039).
/// </summary>
/// <remarks>
/// The crew, the assembly time and the upkeep are characteristics of the hull. The assembly
/// time is not the time to manufacture the hull. The credits and the materials are a share of
/// what the fitted modules cost. The shares live in <see cref="FleetSettings"/>. A technology
/// multiplies the time when the record starts.
/// </remarks>
public static class ShipyardAssembly
{
    /// <summary>
    /// Applies the technology to a stored duration. A multiplier that is not positive changes nothing.
    /// </summary>
    /// <param name="baseSeconds">The duration without the technology.</param>
    /// <param name="multiplier">The multiplier of the assembly time.</param>
    /// <returns>The duration the player waits, in game seconds.</returns>
    public static long Scale(long baseSeconds, decimal multiplier)
    {
        decimal factor = multiplier > 0m ? multiplier : 1m;
        long whole = (long)decimal.Ceiling(baseSeconds * factor);

        return whole < 1 ? 1 : whole;
    }

    /// <summary>
    /// Rounds a share of a recipe up to a whole amount. A share that rounds to nothing is nothing.
    /// </summary>
    /// <param name="recipeAmount">The amount of one resource in the recipe of one part.</param>
    /// <param name="share">The share to take.</param>
    /// <returns>The whole amount.</returns>
    public static long Share(decimal recipeAmount, decimal share)
    {
        if (recipeAmount <= 0m || share <= 0m)
        {
            return 0;
        }

        long whole = (long)decimal.Ceiling(recipeAmount * share);

        return whole < 0 ? 0 : whole;
    }
}

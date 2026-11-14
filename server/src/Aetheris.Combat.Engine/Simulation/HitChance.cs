namespace Aetheris.Combat.Engine;

/// <summary>
/// The chance that a shot which is still in sight and still has a living target connects.
/// </summary>
public static class HitChance
{
    /// <summary>
    /// Computes the chance.
    /// </summary>
    /// <param name="accuracy">The accuracy of the weapon against the group of the target.</param>
    /// <param name="distance">The distance from the origin of the shot to the target.</param>
    /// <param name="range">The range of the weapon. Zero skips the distance term.</param>
    /// <param name="distanceFalloff">How much the chance falls at the edge of the range.</param>
    /// <param name="targetSpeed">The speed of the target.</param>
    /// <param name="referenceSpeed">The fastest nominal speed in the engagement. Zero skips the speed term.</param>
    /// <param name="targetSpeedFalloff">How much the chance falls when the target matches that speed.</param>
    /// <param name="radarBonus">The bonus of the shooter.</param>
    /// <param name="ewMalus">The malus on the target.</param>
    /// <param name="fieldHitMultiplier">The multiplier of the field the target stands in.</param>
    /// <returns>The chance, clamped to the unit interval.</returns>
    public static double Compute(
        double accuracy,
        double distance,
        double range,
        double distanceFalloff,
        double targetSpeed,
        double referenceSpeed,
        double targetSpeedFalloff,
        double radarBonus,
        double ewMalus,
        double fieldHitMultiplier)
    {
        double distanceRatio = range > 0 ? distance / range : 0;
        double speedRatio = 0;

        if (referenceSpeed > 0)
        {
            speedRatio = targetSpeed / referenceSpeed;

            if (speedRatio > 1)
            {
                speedRatio = 1;
            }
        }

        double chance = accuracy
            * (1 - (distanceFalloff * distanceRatio))
            * (1 - (targetSpeedFalloff * speedRatio))
            * (1 + radarBonus)
            * (1 - ewMalus)
            * fieldHitMultiplier;

        if (chance < 0)
        {
            return 0;
        }

        if (chance > 1)
        {
            return 1;
        }

        return chance;
    }
}

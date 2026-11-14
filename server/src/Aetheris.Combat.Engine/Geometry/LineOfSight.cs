namespace Aetheris.Combat.Engine;

/// <summary>
/// Whether a straight segment meets a circle. Touching the circle blocks the line.
/// </summary>
public static class LineOfSight
{
    /// <summary>
    /// Tells whether the segment from <paramref name="from"/> to <paramref name="to"/> meets the circle.
    /// </summary>
    /// <param name="from">The start of the segment.</param>
    /// <param name="to">The end of the segment.</param>
    /// <param name="center">The center of the circle.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <returns><see langword="true"/> when the closest point of the segment is inside the circle or on it.</returns>
    public static bool Blocks(Vec3 from, Vec3 to, Vec3 center, double radius)
    {
        if (radius < 0)
        {
            return false;
        }

        double abx = to.X - from.X;
        double aby = to.Y - from.Y;
        double abz = to.Z - from.Z;
        double lengthSquared = (abx * abx) + (aby * aby) + (abz * abz);
        double acx = center.X - from.X;
        double acy = center.Y - from.Y;
        double acz = center.Z - from.Z;
        double along = 0;

        if (lengthSquared > 0)
        {
            along = ((acx * abx) + (acy * aby) + (acz * abz)) / lengthSquared;

            if (along < 0)
            {
                along = 0;
            }
            else if (along > 1)
            {
                along = 1;
            }
        }

        double closestX = from.X + (abx * along);
        double closestY = from.Y + (aby * along);
        double closestZ = from.Z + (abz * along);
        double dx = closestX - center.X;
        double dy = closestY - center.Y;
        double dz = closestZ - center.Z;

        return ((dx * dx) + (dy * dy) + (dz * dz)) <= radius * radius;
    }
}

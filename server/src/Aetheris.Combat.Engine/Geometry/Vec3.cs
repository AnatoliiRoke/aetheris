namespace Aetheris.Combat.Engine;

/// <summary>
/// A point in the field. The third axis is kept so a later space uses the same numbers;
/// the flat field writes zero there.
/// </summary>
/// <param name="X">The first axis.</param>
/// <param name="Y">The second axis.</param>
/// <param name="Z">The third axis.</param>
public readonly record struct Vec3(double X, double Y, double Z)
{
    /// <summary>The squared distance between two points, on all three axes.</summary>
    /// <param name="left">The first point.</param>
    /// <param name="right">The second point.</param>
    /// <returns>The squared distance.</returns>
    public static double DistanceSquared(Vec3 left, Vec3 right)
    {
        double dx = left.X - right.X;
        double dy = left.Y - right.Y;
        double dz = left.Z - right.Z;

        return (dx * dx) + (dy * dy) + (dz * dz);
    }
}

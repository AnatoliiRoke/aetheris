namespace Aetheris.World.Geometry;

/// <summary>
/// A point of the map with three axes (task 00004, decision 22).
/// </summary>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
/// <param name="Z">The z coordinate; zero while the map is flat.</param>
public readonly record struct Point3(double X, double Y, double Z)
{
    /// <summary>
    /// Adds an offset to the point.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The moved point.</returns>
    public Point3 Plus(Point3 offset) => new(X + offset.X, Y + offset.Y, Z + offset.Z);
}

/// <summary>
/// The geometry of the map.
/// </summary>
/// <remarks>
/// Distances are always three-dimensional; with <c>z = 0</c> they equal the flat ones. Nothing
/// relies on the map being flat.
/// </remarks>
public static class WorldGeometry
{
    /// <summary>
    /// Gets the distance between two points.
    /// </summary>
    /// <param name="first">The first point.</param>
    /// <param name="second">The second point.</param>
    /// <returns>The Euclidean distance in three dimensions.</returns>
    public static double Distance(Point3 first, Point3 second)
    {
        double deltaX = first.X - second.X;
        double deltaY = first.Y - second.Y;
        double deltaZ = first.Z - second.Z;

        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    /// <summary>
    /// Gets the point on a straight route at a share of its length, for example the position
    /// of a moving group between two systems.
    /// </summary>
    /// <param name="start">The start of the route.</param>
    /// <param name="end">The end of the route.</param>
    /// <param name="progress">The travelled share, from 0 to 1.</param>
    /// <returns>The point on the route.</returns>
    public static Point3 PointOnRoute(Point3 start, Point3 end, double progress)
    {
        double clamped = Math.Clamp(progress, 0d, 1d);

        return new Point3(
            start.X + ((end.X - start.X) * clamped),
            start.Y + ((end.Y - start.Y) * clamped),
            start.Z + ((end.Z - start.Z) * clamped));
    }
}

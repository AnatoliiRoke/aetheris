using Aetheris.World.Geometry;

namespace Aetheris.World.Generation;

/// <summary>
/// Places the sectors of the galaxy on a square spiral around the first one.
/// </summary>
/// <remarks>
/// A new sector always gets the next index, so it lies on the outermost ring: the galaxy
/// grows at its edge (task 00004, section 2.4, rule 4).
/// </remarks>
public static class SectorLayout
{
    /// <summary>
    /// The number of turns of the spiral after which a side grows by one step.
    /// </summary>
    private const int TurnsPerSideGrowth = 2;

    private static readonly (int X, int Y)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];

    /// <summary>
    /// Gets the grid cell of a sector.
    /// </summary>
    /// <param name="layoutIndex">The index of the sector on the spiral.</param>
    /// <returns>The cell.</returns>
    public static (int X, int Y) GridCell(int layoutIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(layoutIndex);

        int x = 0;
        int y = 0;
        int remaining = layoutIndex;
        int sideLength = 1;
        int turnsAtLength = 0;
        int direction = 0;

        while (remaining > 0)
        {
            for (int step = 0; step < sideLength && remaining > 0; step++)
            {
                x += Directions[direction].X;
                y += Directions[direction].Y;
                remaining--;
            }

            direction = (direction + 1) % Directions.Length;
            turnsAtLength++;

            if (turnsAtLength == TurnsPerSideGrowth)
            {
                sideLength++;
                turnsAtLength = 0;
            }
        }

        return (x, y);
    }

    /// <summary>
    /// Gets the ring of a sector: how many cells it lies from the first one.
    /// </summary>
    /// <param name="layoutIndex">The index of the sector on the spiral.</param>
    /// <returns>The ring.</returns>
    public static int Ring(int layoutIndex)
    {
        (int x, int y) = GridCell(layoutIndex);

        return Math.Max(Math.Abs(x), Math.Abs(y));
    }

    /// <summary>
    /// Gets the origin of a sector in the galaxy.
    /// </summary>
    /// <param name="layoutIndex">The index of the sector on the spiral.</param>
    /// <param name="spacing">The distance between neighbouring sectors.</param>
    /// <returns>The origin; z is zero.</returns>
    public static Point3 Origin(int layoutIndex, double spacing)
    {
        (int x, int y) = GridCell(layoutIndex);

        return new Point3(x * spacing, y * spacing, 0d);
    }
}

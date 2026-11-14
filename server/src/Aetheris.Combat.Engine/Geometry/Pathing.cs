namespace Aetheris.Combat.Engine;

/// <summary>
/// Movement on the flat field. A planet is a circle; the bypass follows the tangent
/// whose heading is closer to the wanted direction.
/// </summary>
public static class Pathing
{
    /// <summary>
    /// Steps from <paramref name="from"/> toward <paramref name="goal"/> by at most <paramref name="step"/>.
    /// The third axis of the result is zero.
    /// </summary>
    /// <param name="from">The start.</param>
    /// <param name="goal">The point to approach.</param>
    /// <param name="step">How far the step may go.</param>
    /// <returns>The new point.</returns>
    public static Vec3 StepToward(Vec3 from, Vec3 goal, double step)
    {
        double dx = goal.X - from.X;
        double dy = goal.Y - from.Y;
        double dz = goal.Z - from.Z;
        double distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);

        if (distanceSquared <= 0 || step <= 0)
        {
            return new Vec3(from.X, from.Y, 0);
        }

        double distance = Math.Sqrt(distanceSquared);
        double travel = step < distance ? step : distance;
        double scale = travel / distance;

        return new Vec3(from.X + (dx * scale), from.Y + (dy * scale), 0);
    }

    /// <summary>
    /// Steps toward <paramref name="goal"/> and, when that step would meet the circle, steps
    /// toward the tangent with the smaller turn instead.
    /// </summary>
    /// <param name="from">The start.</param>
    /// <param name="goal">The point to approach.</param>
    /// <param name="center">The center of the circle.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <param name="step">How far the step may go.</param>
    /// <returns>The new point. The third axis is zero.</returns>
    public static Vec3 AvoidCircle(Vec3 from, Vec3 goal, Vec3 center, double radius, double step)
    {
        if (step <= 0)
        {
            return new Vec3(from.X, from.Y, 0);
        }

        double dx = from.X - center.X;
        double dy = from.Y - center.Y;
        double distanceSquared = (dx * dx) + (dy * dy);

        if (distanceSquared <= 0)
        {
            return new Vec3(from.X + step, from.Y, 0);
        }

        double distance = Math.Sqrt(distanceSquared);

        if (distance <= radius)
        {
            double outward = step / distance;

            return new Vec3(from.X + (dx * outward), from.Y + (dy * outward), 0);
        }

        Vec3 direct = StepToward(from, goal, step);

        if (!LineOfSight.Blocks(from, direct, center, radius))
        {
            return direct;
        }

        double cross = Math.Sqrt(distanceSquared - (radius * radius));
        double along = (radius * radius) / distanceSquared;
        double side = (radius * cross) / distanceSquared;
        double baseX = center.X + (along * dx);
        double baseY = center.Y + (along * dy);
        double perpX = -dy;
        double perpY = dx;
        double firstX = baseX + (side * perpX);
        double firstY = baseY + (side * perpY);
        double secondX = baseX - (side * perpX);
        double secondY = baseY - (side * perpY);
        double wantX = goal.X - from.X;
        double wantY = goal.Y - from.Y;
        double wantLength = Math.Sqrt((wantX * wantX) + (wantY * wantY));

        if (wantLength > 0)
        {
            wantX /= wantLength;
            wantY /= wantLength;
        }

        double firstDot = ((firstX - from.X) * wantX) + ((firstY - from.Y) * wantY);
        double secondDot = ((secondX - from.X) * wantX) + ((secondY - from.Y) * wantY);
        bool firstWins = firstDot > secondDot
            || (firstDot == secondDot && (firstX < secondX || (firstX == secondX && firstY <= secondY)));
        double tangentX = firstWins ? firstX : secondX;
        double tangentY = firstWins ? firstY : secondY;

        return StepToward(from, new Vec3(tangentX, tangentY, 0), step);
    }
}

using Aetheris.Core.Admin;

namespace Aetheris.Production.Api;

/// <summary>
/// The checks of a row of <c>part_accuracy</c> (task 00062).
/// </summary>
/// <remarks>
/// A database constraint cannot see whether the target group is a group of hulls, so the seed
/// and the admin panel share this check. The bounds are zero and one, which are the ends of a
/// share and not a number of the balance.
/// </remarks>
public static class PartAccuracyRules
{
    /// <summary>The row names a group that is not a group of hulls.</summary>
    public const string NotHullGroup = "not_a_hull_group";

    /// <summary>
    /// Checks one accuracy.
    /// </summary>
    /// <param name="accuracy">The accuracy, from zero to one.</param>
    /// <param name="targetIsHullGroup">Whether the target group is a group of hulls.</param>
    /// <returns>The error code, or <see langword="null"/> when the row is acceptable.</returns>
    public static string? Problem(double accuracy, bool targetIsHullGroup)
    {
        if (accuracy < 0 || accuracy > 1)
        {
            return AdminReferenceErrors.OutOfBounds;
        }

        return targetIsHullGroup ? null : NotHullGroup;
    }
}

/// <summary>
/// The bounds of the shutdown order of a slot (task 00062). A smaller number turns off later.
/// </summary>
public static class PartPowerPriority
{
    /// <summary>The smallest shutdown order. It turns off last.</summary>
    public const short LowerBound = 0;

    /// <summary>The largest shutdown order. It turns off first.</summary>
    public const short UpperBound = 1000;
}

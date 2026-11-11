using Aetheris.World.Api;

namespace Aetheris.Production.Api;

/// <summary>
/// The kinds of a place a module of extraction takes from (task 00055).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program: the code of a kind is the class of the object of the
/// map the module takes from, so the fleet matches the place it stands at with the rows of the
/// module without a table of its own. Which module takes what from which kind is data of the
/// production, not a code of the program.
/// </remarks>
public static class ExtractionPlaceKinds
{
    /// <summary>An asteroid field, which has a composition and a mass.</summary>
    public const string AsteroidField = WorldObjectClasses.AsteroidField;

    /// <summary>A nebula, which has a composition and no mass.</summary>
    public const string Nebula = WorldObjectClasses.Nebula;

    /// <summary>A gas giant, which has neither a composition nor a mass.</summary>
    public const string GasGiant = WorldObjectClasses.GasGiant;

    /// <summary>
    /// Gets every kind, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [AsteroidField, Nebula, GasGiant];
}

/// <summary>
/// The ways a module of extraction takes what it takes from a place (task 00055).
/// </summary>
/// <remarks>
/// The ways are logical codes of the program, one reader of each in the fleet. The resources they
/// name are data: a row of a module says the way and, for the ways that need them, the resource or
/// the group of the resources.
/// </remarks>
public static class ExtractionModes
{
    /// <summary>
    /// Every resource of the composition of the place, by its share. The row names no resource.
    /// </summary>
    public const string AllShares = "all_shares";

    /// <summary>
    /// One resource that the row names: the yield times its share in the composition of the place, or
    /// the whole yield at a place that has no composition.
    /// </summary>
    public const string Listed = "listed";

    /// <summary>
    /// The resources of the group that the row names that the place holds: the yield is split evenly
    /// between them.
    /// </summary>
    public const string GroupEven = "group_even";

    /// <summary>
    /// Gets every way, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [AllShares, Listed, GroupEven];
}

namespace Aetheris.World.Api;

/// <summary>
/// The classes of celestial objects (task 00004).
/// </summary>
/// <remarks>
/// <para>
/// A class is the structure the code knows: a body has an owner and deposits, a field has a
/// mass that shrinks, a nebula has a radius. Everything a designer tunes (types, names, weights,
/// resources) is a row of the reference tables.
/// </para>
/// <para>
/// The classes are part of the contract of the module, for the modules that name a place of the
/// map: a fleet works at a field, a nebula or a gas giant.
/// </para>
/// </remarks>
public static class WorldObjectClasses
{
    /// <summary>The star of a system.</summary>
    public const string Star = "star";

    /// <summary>A body that a player may own.</summary>
    public const string Planet = "planet";

    /// <summary>A body that holds stations and is never owned.</summary>
    public const string GasGiant = "gas_giant";

    /// <summary>An asteroid field.</summary>
    public const string AsteroidField = "asteroid_field";

    /// <summary>A nebula.</summary>
    public const string Nebula = "nebula";
}

/// <summary>
/// An amount of one resource (task 00050).
/// </summary>
/// <param name="ResourceId">The resource.</param>
/// <param name="Amount">The amount, a whole number.</param>
public sealed record ResourceAmount(int ResourceId, long Amount);

/// <summary>
/// The yield of one processing cycle on a field or on a nebula (task 00004, task 00050).
/// </summary>
/// <param name="ProcessedMass">The mass taken from the place, rock included.</param>
/// <param name="YieldIds">The resources that go to the hold.</param>
/// <param name="RockMass">The processed mass that stays behind as empty rock, rounding included.</param>
/// <param name="RemainingMass">The mass of the place after the cycle; zero for a nebula, which has no mass.</param>
/// <param name="Depleted">Whether the place is empty and disappears.</param>
public sealed record ExtractionResult(
    long ProcessedMass,
    IReadOnlyList<ResourceAmount> YieldIds,
    long RockMass,
    long RemainingMass,
    bool Depleted);

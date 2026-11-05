namespace Aetheris.Production.Api;

/// <summary>
/// The kinds of the damage a part makes (task 00013, decision 2.11).
/// </summary>
/// <remarks>
/// The kinds are logical codes of the program, so the battle reads them whatever the part is.
/// Their words differ from the old codes of the game entities, so a part attribute never
/// looks like a resource or a class of size (task 00022).
/// </remarks>
public static class PartDamageKinds
{
    /// <summary>The energy damage of the lasers.</summary>
    public const string Energy = "energy";

    /// <summary>The thermal damage of the incinerators.</summary>
    public const string Thermal = "thermal";

    /// <summary>The kinetic damage of the guns.</summary>
    public const string Kinetic = "kinetic";

    /// <summary>The ion damage.</summary>
    public const string Ion = "ion";

    /// <summary>The explosive damage of the rockets and the torpedoes.</summary>
    public const string Explosive = "explosive";

    /// <summary>
    /// Gets every kind, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Energy, Thermal, Kinetic, Ion, Explosive];
}

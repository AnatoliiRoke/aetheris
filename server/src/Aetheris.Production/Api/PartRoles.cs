namespace Aetheris.Production.Api;

/// <summary>
/// The roles of the parts of a ship: what a part may be put on and what a hull may carry
/// (task 00038, sections 3.1 and 3.2).
/// </summary>
/// <remarks>
/// The roles are logical codes of the program, so the constructor of the ships reads the same
/// codes whatever the part is. A hull says what it is with the role of its group, and a part
/// says what it is with the role of its attribute, so a new role is a new code with one reader.
/// </remarks>
public static class PartRoles
{
    /// <summary>
    /// A part of the combat: a weapon, an armour, a shield. Such a part goes only on a combat
    /// hull.
    /// </summary>
    public const string Combat = "combat";

    /// <summary>
    /// A part of the peace: a civilian module that carries no weapon.
    /// </summary>
    public const string Civil = "civil";

    /// <summary>
    /// A part of the transport: a cargo hold, a colony module. Such a part goes only on a
    /// transport hull.
    /// </summary>
    public const string Transport = "transport";

    /// <summary>
    /// A part that fits any hull: a drive, a reactor, a life support. The mandatory components
    /// of a ship are such parts.
    /// </summary>
    public const string Universal = "universal";

    /// <summary>
    /// Gets every role, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Combat, Civil, Transport, Universal];

    /// <summary>
    /// Tells whether the role is known to the program.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns><see langword="true"/> when the role is known.</returns>
    public static bool IsKnown(string? role) =>
        role is not null && All.Contains(role, StringComparer.Ordinal);
}

/// <summary>
/// The roles of the hulls: what a hull is (task 00038, section 3.1).
/// </summary>
/// <remarks>
/// The role of a hull is the role of its group, so all the hulls of a group share it. A combat
/// hull takes the parts of the combat, a transport hull takes the parts of the transport, and a
/// drone carries no hull at all.
/// </remarks>
public static class HullRoles
{
    /// <summary>
    /// A combat hull: a warship. It takes the parts of the combat.
    /// </summary>
    public const string Combat = "combat";

    /// <summary>
    /// A civil hull: a ship of the peace. It takes the parts of the peace.
    /// </summary>
    public const string Civil = "civil";

    /// <summary>
    /// A transport hull: a freighter. It takes the parts of the transport and the parts of the
    /// peace.
    /// </summary>
    public const string Transport = "transport";

    /// <summary>
    /// A drone: a small craft that is carried in the hold of a ship and is never fitted to a hull
    /// (task 00042, decision 3).
    /// </summary>
    public const string Drone = "drone";

    /// <summary>
    /// Gets every role, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Combat, Civil, Transport, Drone];

    /// <summary>
    /// Tells whether the role is known to the program.
    /// </summary>
    /// <param name="role">The role.</param>
    /// <returns><see langword="true"/> when the role is known.</returns>
    public static bool IsKnown(string? role) =>
        role is not null && All.Contains(role, StringComparer.Ordinal);
}

namespace Aetheris.Fleet.Data.Entities;

/// <summary>
/// What a ship carries in its hold (task 00050).
/// </summary>
/// <remarks>
/// The hold of a ship is the room of its hull plus the room of its container modules. The cargo is
/// counted per resource, so a hold holds a part of a field and a part of what the ship picked up
/// before. The row disappears when the whole amount is gone.
/// </remarks>
public sealed class ShipCargoEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the ship.</summary>
    public long ShipId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets how much of the resource the hold has, in tonnes.</summary>
    public long Amount { get; set; }

    /// <summary>Gets or sets the moment of game time the cargo was counted at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// What a fleet carries in the holds of its ships (task 00050).
/// </summary>
/// <remarks>
/// The fleet carries the sum of the holds of its ships. The row is the sum of the rows of the
/// ships, so a fleet with a hold of its own is not a thing yet.
/// </remarks>
public sealed class FleetCargoEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the fleet.</summary>
    public long FleetId { get; set; }

    /// <summary>Gets or sets the resource.</summary>
    public int ResourceId { get; set; }

    /// <summary>Gets or sets how much of the resource the fleet carries, in tonnes.</summary>
    public long Amount { get; set; }

    /// <summary>Gets or sets the moment of game time the cargo was counted at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// The parts a ship carries in its hold (task 00057).
/// </summary>
/// <remarks>
/// A hold takes modules as well as resources: the stack is named by the kind of the part, its mark and
/// the bonus of the race, as the stacks of the parts of a body are. The row disappears when the
/// whole stack is gone. The mass of the stack takes the room of the hold.
/// </remarks>
public sealed class ShipPartCargoEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the ship.</summary>
    public long ShipId { get; set; }

    /// <summary>Gets or sets the kind of the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the mark level of the part.</summary>
    public int MarkLevel { get; set; }

    /// <summary>Gets or sets whether the part carries the bonus of the race that made it.</summary>
    public bool HasRaceBonus { get; set; }

    /// <summary>Gets or sets how many parts the stack holds.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets the moment of game time the cargo was counted at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

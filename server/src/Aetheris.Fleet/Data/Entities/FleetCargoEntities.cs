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

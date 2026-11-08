namespace Aetheris.Fleet.Data.Entities;

/// <summary>
/// A fleet: a group of finished ships of one player that stand on the orbit of one body
/// (task 00040).
/// </summary>
/// <remarks>
/// <para>
/// The fleet has a home body, which is its base: it leaves for a route from there and returns
/// there. The body it stands on now is another fact, because a fleet flies in real time (the
/// flights are a task of a later stage). Until the flights exist the two bodies are the same.
/// </para>
/// <para>
/// The sector is copied from the body, as the data model asks, so a read of the map does not
/// join the tables of the world. The table carries no foreign key into the tables of the
/// other modules (documentation/tech/08, rule 5).
/// </para>
/// </remarks>
public sealed class FleetEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owner of the fleet.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the name of the fleet.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the home body of the fleet: its base.</summary>
    public long HomePlanetId { get; set; }

    /// <summary>Gets or sets the body whose orbit the fleet stands on now.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body the fleet stands on.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the state of the fleet, one of <see cref="FleetStates"/>.</summary>
    public string StateCode { get; set; } = FleetStates.Stationed;

    /// <summary>Gets or sets the moment of game time the fleet was created at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the ships of the fleet.</summary>
    public List<FleetShipEntity> ShipIds { get; set; } = [];
}

/// <summary>
/// A ship in a fleet (task 00040).
/// </summary>
/// <remarks>
/// A ship is in one fleet at most, so the ship is unique in the table. The position is the
/// order the ships joined the fleet in.
/// </remarks>
public sealed class FleetShipEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the fleet.</summary>
    public long FleetId { get; set; }

    /// <summary>Gets or sets the ship.</summary>
    public long ShipId { get; set; }

    /// <summary>Gets or sets the position of the ship in the fleet, from one.</summary>
    public int Position { get; set; }
}

/// <summary>
/// The lock row of the fleets of one player (task 00040).
/// </summary>
/// <remarks>
/// Every change of the fleets of a player, and the dismantling of a ship, locks this row first,
/// so two commands of one player wait on each other: the limit of the fleets is not passed by
/// twenty requests at once, and a ship is not put into a fleet while it is taken apart. The
/// commands of two players do not wait on each other.
/// </remarks>
public sealed class FleetPlayerStateEntity
{
    /// <summary>Gets or sets the player the row belongs to.</summary>
    public string PlayerId { get; set; } = string.Empty;
}

/// <summary>
/// The states of a fleet. They are logical codes, not numbers of the balance.
/// </summary>
public static class FleetStates
{
    /// <summary>The fleet stands on the orbit of a body.</summary>
    public const string Stationed = "stationed";
}

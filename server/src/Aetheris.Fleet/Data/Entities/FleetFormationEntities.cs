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

    /// <summary>
    /// Gets or sets the body whose orbit the fleet stands on now. It is zero when the fleet stands
    /// at a place that is not a body (a field, a nebula, a point of open space) or is under way.
    /// </summary>
    public long PlanetId { get; set; }

    /// <summary>
    /// Gets or sets the object of the map the fleet stands at, or <see langword="null"/> in open
    /// space and for a fleet that has not flown yet (it stands on the orbit of <see cref="PlanetId"/>).
    /// </summary>
    public long? ObjectId { get; set; }

    /// <summary>Gets or sets the star system the fleet stands in, or <see langword="null"/> in open space.</summary>
    public long? SystemId { get; set; }

    /// <summary>Gets or sets the orbit the fleet stands on inside the system, or <see langword="null"/>.</summary>
    public int? Orbit { get; set; }

    /// <summary>
    /// Gets or sets the first axis of the position of the fleet in the galaxy. The position is
    /// empty for a fleet that has not flown yet: it stands on the orbit of <see cref="PlanetId"/>
    /// (task 00053).
    /// </summary>
    public double? PositionX { get; set; }

    /// <summary>Gets or sets the second axis of the position of the fleet in the galaxy.</summary>
    public double? PositionY { get; set; }

    /// <summary>Gets or sets the third axis of the position of the fleet in the galaxy.</summary>
    public double? PositionZ { get; set; }

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
    /// <summary>The fleet stands at a place: on the orbit of a body, at a field, a nebula or a point of space.</summary>
    public const string Stationed = "stationed";

    /// <summary>The fleet is under way to another place (task 00053).</summary>
    public const string Flying = "flying";
}

/// <summary>
/// The flight of a fleet that is under way (task 00053): where it left, where it goes and when it
/// arrives.
/// </summary>
/// <remarks>
/// <para>
/// The row exists while the fleet flies and is removed when the fleet arrives or when the plan is
/// stopped. A fleet has one flight at most. The arrival is an event of the scheduler, so nothing
/// polls the table.
/// </para>
/// <para>
/// The coordinates are three axes of the galaxy. The origin and the destination are kept so that a
/// stopped fleet stands at the point of the route it has reached, and so that the position of a
/// flying fleet can be told at any moment.
/// </para>
/// </remarks>
public sealed class FleetFlightEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the fleet that flies.</summary>
    public long FleetId { get; set; }

    /// <summary>Gets or sets the order of the plan that is the flight.</summary>
    public long FlightPlanOrderId { get; set; }

    /// <summary>Gets or sets the moment of game time the fleet left.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets the moment of game time the fleet arrives.</summary>
    public DateTimeOffset ArrivesAt { get; set; }

    /// <summary>Gets or sets the distance of the flight, in the units of the map.</summary>
    public double Distance { get; set; }

    /// <summary>Gets or sets the speed of the fleet, in light-years per hour.</summary>
    public decimal Speed { get; set; }

    /// <summary>Gets or sets the sector of the place the fleet left.</summary>
    public int FromSectorId { get; set; }

    /// <summary>Gets or sets the star system of the place the fleet left.</summary>
    public long? FromSystemId { get; set; }

    /// <summary>Gets or sets the orbit of the place the fleet left.</summary>
    public int? FromOrbit { get; set; }

    /// <summary>Gets or sets the first axis of the place the fleet left.</summary>
    public double FromX { get; set; }

    /// <summary>Gets or sets the second axis of the place the fleet left.</summary>
    public double FromY { get; set; }

    /// <summary>Gets or sets the third axis of the place the fleet left.</summary>
    public double FromZ { get; set; }

    /// <summary>Gets or sets the sector of the destination.</summary>
    public int ToSectorId { get; set; }

    /// <summary>Gets or sets the star system of the destination, or <see langword="null"/> in open space.</summary>
    public long? ToSystemId { get; set; }

    /// <summary>Gets or sets the object of the map the fleet flies to, or <see langword="null"/> for a point of space.</summary>
    public long? ToObjectId { get; set; }

    /// <summary>Gets or sets the body the fleet flies to, or <see langword="null"/> when the destination is not a body.</summary>
    public long? ToPlanetId { get; set; }

    /// <summary>Gets or sets the orbit of the destination.</summary>
    public int? ToOrbit { get; set; }

    /// <summary>Gets or sets the first axis of the destination.</summary>
    public double ToX { get; set; }

    /// <summary>Gets or sets the second axis of the destination.</summary>
    public double ToY { get; set; }

    /// <summary>Gets or sets the third axis of the destination.</summary>
    public double ToZ { get; set; }
}

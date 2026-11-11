namespace Aetheris.Fleet.Api;

/// <summary>
/// The fleets of a player with the ships that wait to join one (task 00040).
/// </summary>
/// <param name="Limit">How many fleets the spaceports of the player allow.</param>
/// <param name="Used">How many fleets the player has.</param>
/// <param name="HasCredits">Whether the civilization has credits: without them a fleet does not fly.</param>
/// <param name="UpkeepPerHour">The credits the ships of the player cost per hour.</param>
/// <param name="Fleets">The fleets, the oldest first.</param>
/// <param name="FreeShips">The finished ships that are in no fleet, by body.</param>
/// <param name="GameNow">The game time of the answer, so the screen counts the time left of a flight (task 00053).</param>
/// <param name="TimeMultiplier">How many game seconds pass in a real second (task 00053).</param>
public sealed record FleetsViewDto(
    int Limit,
    int Used,
    bool HasCredits,
    decimal UpkeepPerHour,
    IReadOnlyList<FleetDto> Fleets,
    IReadOnlyList<FleetShipDto> FreeShips,
    DateTimeOffset GameNow = default,
    double TimeMultiplier = 1d);

/// <summary>
/// A fleet with its ships.
/// </summary>
/// <param name="Id">The fleet.</param>
/// <param name="Name">The name of the fleet.</param>
/// <param name="HomePlanetId">The home body of the fleet: its base.</param>
/// <param name="PlanetId">The body whose orbit the fleet stands on now.</param>
/// <param name="SectorId">The sector of that body.</param>
/// <param name="StateCode">The state of the fleet.</param>
/// <param name="Crew">The crew of all the ships.</param>
/// <param name="UpkeepPerHour">The credits the ships of the fleet cost per hour.</param>
/// <param name="CreatedAt">The moment of game time the fleet was created at.</param>
/// <param name="Ships">The ships in the order they joined.</param>
/// <param name="HoldCapacity">The hold of all the ships, as the projects of the ships give it.</param>
/// <param name="Integrity">The integrity of all the ships.</param>
/// <param name="DamageMin">The least damage all the weapons make in one round, as the projects of the ships count it.</param>
/// <param name="DamageMax">The greatest damage all the weapons make in one round.</param>
/// <param name="ShieldCapacity">The capacity of all the shields.</param>
/// <param name="ObjectId">The object of the map the fleet stands at, or <see langword="null"/> in open space or before its first flight (task 00053).</param>
/// <param name="SystemId">The star system the fleet stands in, or <see langword="null"/> (task 00053).</param>
/// <param name="PositionX">The first axis of the position of the fleet in the galaxy, or <see langword="null"/> before its first flight (task 00053).</param>
/// <param name="PositionY">The second axis of the position of the fleet in the galaxy (task 00053).</param>
/// <param name="PositionZ">The third axis of the position of the fleet in the galaxy (task 00053).</param>
/// <param name="Flight">The flight of the fleet when it is under way, or <see langword="null"/> (task 00053).</param>
public sealed record FleetDto(
    long Id,
    string Name,
    long HomePlanetId,
    long PlanetId,
    int SectorId,
    string StateCode,
    long Crew,
    decimal UpkeepPerHour,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FleetShipDto> Ships,
    decimal HoldCapacity = 0m,
    decimal Integrity = 0m,
    decimal DamageMin = 0m,
    decimal DamageMax = 0m,
    decimal ShieldCapacity = 0m,
    long? ObjectId = null,
    long? SystemId = null,
    double? PositionX = null,
    double? PositionY = null,
    double? PositionZ = null,
    FleetFlightDto? Flight = null);

/// <summary>
/// A finished ship as the fleets show it.
/// </summary>
/// <param name="Id">The ship.</param>
/// <param name="PlanetId">The body the ship stands on.</param>
/// <param name="DesignId">The project the ship was built from.</param>
/// <param name="Name">The name of the project.</param>
/// <param name="HullNameKey">The localisation key of the name of the hull.</param>
/// <param name="ClassNameKey">The localisation key of the class of the hull.</param>
/// <param name="ConditionPercent">The condition of the ship: the worst condition of its parts, in per cent.</param>
/// <param name="Crew">The crew assigned to the ship.</param>
/// <param name="UpkeepPerHour">The credits the ship costs per hour.</param>
/// <param name="FleetId">The fleet the ship is in, or <see langword="null"/> when it is free.</param>
public sealed record FleetShipDto(
    long Id,
    long PlanetId,
    long DesignId,
    string Name,
    string HullNameKey,
    string ClassNameKey,
    int ConditionPercent,
    long Crew,
    decimal UpkeepPerHour,
    long? FleetId);

/// <summary>
/// What the other modules may read about the fleets of a player, for example the rating
/// (task 00040, section 3.2).
/// </summary>
/// <param name="FleetCount">How many fleets the player has.</param>
/// <param name="ShipCount">How many finished ships the player has, in a fleet or free.</param>
/// <param name="ShipsInFleets">How many of the ships are in a fleet.</param>
/// <param name="CostByResourceTypeId">What the assembly of all the ships paid, by resource.</param>
public sealed record FleetFacts(
    int FleetCount,
    int ShipCount,
    int ShipsInFleets,
    IReadOnlyDictionary<int, long> CostByResourceTypeId);

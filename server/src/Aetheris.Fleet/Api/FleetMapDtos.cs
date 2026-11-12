namespace Aetheris.Fleet.Api;

/// <summary>
/// The fleets of a player that are under way, as the map of the galaxy draws them (task 00059).
/// </summary>
/// <remarks>
/// The map needs the route of every fleet and the moment it began and arrives, so it can draw the
/// line of the route and put the mark of the fleet on the point of the route the time has reached.
/// The position is not sent: the client counts it from the times, exactly as the server counts it
/// in <c>PlaceOnRoute</c>, so the mark does not jump when the answer comes.
/// </remarks>
/// <param name="Fleets">The fleets that are under way, the oldest flight first.</param>
/// <param name="Stationed">The fleets that stand at a place, so the map shows a fleet that arrived (task 00059).</param>
/// <param name="GameNow">The game time of the answer, so the client counts the time left of a flight.</param>
/// <param name="TimeMultiplier">How many game seconds pass in a real second.</param>
public sealed record FleetsInFlightDto(
    IReadOnlyList<FleetInFlightDto> Fleets,
    IReadOnlyList<FleetStationedDto> Stationed,
    DateTimeOffset GameNow = default,
    double TimeMultiplier = 1d);

/// <summary>
/// One fleet of the player that is under way (task 00059).
/// </summary>
/// <param name="FleetId">The fleet.</param>
/// <param name="Name">The name of the fleet.</param>
/// <param name="Flight">The flight of the fleet: where it left, where it goes and when it arrives.</param>
/// <param name="FromSector">The place of the sector the fleet left, in the axes of the galaxy, or <see langword="null"/> when the sector is gone.</param>
/// <param name="ToSector">The place of the sector the fleet flies to, in the axes of the galaxy, or <see langword="null"/> when the sector is gone.</param>
public sealed record FleetInFlightDto(
    long FleetId,
    string Name,
    FleetFlightDto Flight,
    FleetSectorPlaceDto? FromSector,
    FleetSectorPlaceDto? ToSector);

/// <summary>
/// The place of a sector of the map (task 00059): the axes of the galaxy and the axes inside the
/// sector, so the map draws the route of a fleet on the level of the galaxy and on the level of the
/// sector with the same numbers.
/// </summary>
/// <param name="SectorId">The sector.</param>
/// <param name="X">The first axis of the sector in the galaxy.</param>
/// <param name="Y">The second axis of the sector in the galaxy.</param>
/// <param name="Z">The third axis of the sector in the galaxy.</param>
public sealed record FleetSectorPlaceDto(int SectorId, double X, double Y, double Z);

/// <summary>
/// One fleet of the player that stands at a place (task 00059): it arrived, or it never flew, so
/// the map shows it where it stands.
/// </summary>
/// <param name="FleetId">The fleet.</param>
/// <param name="Name">The name of the fleet.</param>
/// <param name="SectorId">The sector the fleet stands in.</param>
/// <param name="SystemId">The star system the fleet stands in, or <see langword="null"/> in open space.</param>
/// <param name="Orbit">The orbit the fleet stands on, or <see langword="null"/> outside a system.</param>
/// <param name="X">The first axis of the place of the fleet in the galaxy.</param>
/// <param name="Y">The second axis of the place of the fleet in the galaxy.</param>
/// <param name="Z">The third axis of the place of the fleet in the galaxy.</param>
public sealed record FleetStationedDto(
    long FleetId,
    string Name,
    int SectorId,
    long? SystemId,
    int? Orbit,
    double X,
    double Y,
    double Z);

using Aetheris.Core.Time;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The fleets of a player that are under way, as the map draws them (task 00059).
/// </summary>
/// <remarks>
/// <para>
/// The map reads the fleets that fly in one request and draws the route of each of them with the
/// mark of the fleet on the point of the route the time has reached. The position is not sent: the
/// client counts it from the times of the flight, exactly as <see cref="FleetFlightService.PlaceOnRoute"/>
/// counts it, so the mark of the fleet stands where the server counts it.
/// </para>
/// <para>
/// The read is one query over the flights of the player with their fleets and one over the sectors
/// of their routes: the parts of the ships of a fleet are not read, because the map shows the fleet
/// and not its ships.
/// </para>
/// </remarks>
public sealed class FleetMapService
{
    private readonly FleetDbContext _dbContext;
    private readonly IWorldModule _world;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetMapService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="world">The world module.</param>
    /// <param name="clock">The game clock.</param>
    public FleetMapService(FleetDbContext dbContext, IWorldModule world, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _world = world;
        _clock = clock;
    }

    /// <summary>
    /// Reads the fleets of the player that are under way and the ones that stand at a place.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The fleets that fly, with the route of each of them, and the fleets that stand.</returns>
    public async Task<FleetsInFlightDto> ReadAsync(string playerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        // The fleets of the player are read with the flight of each of them, in one query: the map
        // needs the name of the fleet, its flight and the place it stands at, and nothing else.
        var fleets = await (
                from fleet in _dbContext.Fleets.AsNoTracking()
                where fleet.PlayerId == playerId
                orderby fleet.CreatedAt, fleet.Id
                select new { Fleet = fleet })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (fleets.Count == 0)
        {
            return new FleetsInFlightDto([], [], _clock.GameNow, _clock.Multiplier);
        }

        long[] fleetIds = [.. fleets.Select(row => row.Fleet.Id)];
        Dictionary<long, FleetFlightEntity> flights = await _dbContext.FleetFlights.AsNoTracking()
            .Where(flight => fleetIds.Contains(flight.FleetId))
            .ToDictionaryAsync(flight => flight.FleetId, cancellationToken)
            .ConfigureAwait(false);

        // The places of the sectors of the routes are read in one query of the world, so the map of
        // the galaxy may draw the route of a fleet between two sectors.
        int[] sectorIds = [.. flights.Values.SelectMany(flight => new[] { flight.FromSectorId, flight.ToSectorId }).Distinct()];
        IReadOnlyDictionary<int, SectorPlaceDto> sectorPlaces = await _world.GetSectorPlacesAsync(sectorIds, cancellationToken).ConfigureAwait(false);

        // The fleets that stand on a body that never flew take the place of that body, and it is
        // read in one query of the world.
        long[] bodyIds = [.. fleets
            .Where(row => !flights.ContainsKey(row.Fleet.Id) && row.Fleet.PlanetId > 0 && row.Fleet.PositionX is null)
            .Select(row => row.Fleet.PlanetId)
            .Distinct()];
        IReadOnlyDictionary<long, WorldPlaceDto> bodyPlaces = await _world.GetPlanetPlacesAsync(bodyIds, cancellationToken).ConfigureAwait(false);

        List<FleetInFlightDto> flying =
        [
            .. fleets
                .Where(row => flights.ContainsKey(row.Fleet.Id))
                .Select(row => new FleetInFlightDto(
                    row.Fleet.Id,
                    row.Fleet.Name,
                    ToDto(flights[row.Fleet.Id]),
                    PlaceOf(sectorPlaces, flights[row.Fleet.Id].FromSectorId),
                    PlaceOf(sectorPlaces, flights[row.Fleet.Id].ToSectorId))),
        ];

        List<FleetStationedDto> stationed =
        [
            .. fleets
                .Where(row => !flights.ContainsKey(row.Fleet.Id))
                .Select(row => ToStationed(row.Fleet, bodyPlaces))
                .Where(row => row is not null)
                .Select(row => row!),
        ];

        return new FleetsInFlightDto(flying, stationed, _clock.GameNow, _clock.Multiplier);
    }

    /// <summary>
    /// The place of a fleet that stands at a place: the place it stands at, or the place of the body
    /// it stands on when it never flew.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="bodyPlaces">The places of the bodies the fleets stand on.</param>
    /// <returns>The fleet as the map reads it, or <see langword="null"/> when it has no place.</returns>
    private static FleetStationedDto? ToStationed(FleetEntity fleet, IReadOnlyDictionary<long, WorldPlaceDto> bodyPlaces)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        if (fleet.PositionX is { } x && fleet.PositionY is { } y && fleet.PositionZ is { } z)
        {
            return new FleetStationedDto(fleet.Id, fleet.Name, fleet.SectorId, fleet.SystemId, fleet.Orbit, x, y, z);
        }

        // The fleet has never flown: it stands on the orbit of its body.
        if (fleet.PlanetId > 0 && bodyPlaces.TryGetValue(fleet.PlanetId, out WorldPlaceDto? place))
        {
            return new FleetStationedDto(fleet.Id, fleet.Name, place.SectorId, place.SystemId, place.Orbit, place.X, place.Y, place.Z);
        }

        return null;
    }

    /// <summary>
    /// The place of a sector, or <see langword="null"/> when the sector is gone: a route without a
    /// sector has no place to draw.
    /// </summary>
    /// <param name="places">The places of the sectors.</param>
    /// <param name="sectorId">The sector.</param>
    /// <returns>The place of the sector, or <see langword="null"/>.</returns>
    private static FleetSectorPlaceDto? PlaceOf(IReadOnlyDictionary<int, SectorPlaceDto> places, int sectorId) =>
        places.TryGetValue(sectorId, out SectorPlaceDto? place)
            ? new FleetSectorPlaceDto(sectorId, place.X, place.Y, place.Z)
            : null;

    /// <summary>
    /// Turns the flight of a fleet into the answer of the map.
    /// </summary>
    /// <param name="flight">The flight.</param>
    /// <returns>The flight as the map reads it.</returns>
    private static FleetFlightDto ToDto(FleetFlightEntity flight)
    {
        ArgumentNullException.ThrowIfNull(flight);

        return new FleetFlightDto(
            flight.FlightPlanOrderId,
            flight.StartedAt,
            flight.ArrivesAt,
            flight.Distance,
            flight.Speed,
            flight.FromSectorId,
            flight.FromSystemId,
            flight.FromOrbit,
            flight.FromX,
            flight.FromY,
            flight.FromZ,
            flight.ToSectorId,
            flight.ToSystemId,
            flight.ToObjectId,
            flight.ToPlanetId,
            flight.ToOrbit,
            flight.ToX,
            flight.ToY,
            flight.ToZ,
            flight.LegKind,
            flight.PrepEndsAt);
    }
}

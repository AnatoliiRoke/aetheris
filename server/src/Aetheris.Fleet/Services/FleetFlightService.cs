using Aetheris.Core.Time;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The places and the times of the flights of the fleets (task 00053).
/// </summary>
/// <remarks>
/// <para>
/// The server counts the time of a flight: the distance comes from the world, the speed from the
/// ships of the fleet by the formula of task 00050 (section 8.2), and the time is the distance over
/// the speed. The client only shows the answer.
/// </para>
/// <para>
/// A fleet that has not flown yet has no position of its own: it stands on the orbit of its body.
/// A fleet that flew carries the place it arrived at, and a fleet under way is at the point of its
/// route that the time has reached.
/// </para>
/// </remarks>
public sealed class FleetFlightService
{
    /// <summary>
    /// The middle of a route: a fleet that stops before it belongs to the sector it left, after it to
    /// the sector it flies to.
    /// </summary>
    private const double MidpointProgress = 0.5d;

    private readonly FleetDbContext _dbContext;
    private readonly IWorldModule _world;
    private readonly FleetSpeedService _speeds;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetFlightService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="world">The world module.</param>
    /// <param name="speeds">The speeds of the fleets.</param>
    /// <param name="clock">The game clock.</param>
    public FleetFlightService(FleetDbContext dbContext, IWorldModule world, FleetSpeedService speeds, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _world = world;
        _speeds = speeds;
        _clock = clock;
    }

    /// <summary>
    /// Finds the place a target names.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when the target names nothing a fleet may fly to.</returns>
    public async Task<WorldPlaceDto?> ResolveAsync(FlightTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind == FlightTargetKinds.Point)
        {
            return await _world
                .GetPointPlaceAsync(target.SectorId, (double)target.X, (double)target.Y, (double)target.Z, cancellationToken)
                .ConfigureAwait(false);
        }

        if ((target.Kind == FlightTargetKinds.Planet || target.Kind == FlightTargetKinds.Object) && target.ObjectId > 0)
        {
            WorldPlaceDto? place = await _world.GetObjectPlaceAsync(target.ObjectId, cancellationToken).ConfigureAwait(false);

            // A fleet does not fly into a star.
            return place is null || place.ObjectClass == WorldObjectClasses.Star ? null : place;
        }

        return null;
    }

    /// <summary>
    /// Finds the place the target of an order of flight names: the body of the fleet for «return»,
    /// the named place otherwise.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="order">The order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when it does not exist.</returns>
    public Task<WorldPlaceDto?> ResolveOrderAsync(FleetEntity fleet, FlightPlanOrderEntity order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(order);

        return order.TypeCode == FlightOrderTypes.Return
            ? _world.GetPlanetPlaceAsync(fleet.HomePlanetId, cancellationToken)
            : ResolveAsync(FlightTarget.Of(order), cancellationToken);
    }

    /// <summary>
    /// Finds the place a fleet is at now.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="flight">The flight of the fleet when it is under way.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when the body the fleet stands on is gone.</returns>
    public Task<WorldPlaceDto?> CurrentPlaceAsync(
        FleetEntity fleet,
        FleetFlightEntity? flight,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);

        if (flight is not null)
        {
            return Task.FromResult<WorldPlaceDto?>(PlaceOnRoute(flight, now));
        }

        if (fleet.PositionX is { } x && fleet.PositionY is { } y && fleet.PositionZ is { } z)
        {
            return Task.FromResult<WorldPlaceDto?>(new WorldPlaceDto(
                fleet.SectorId,
                fleet.SystemId,
                fleet.ObjectId,
                fleet.PlanetId > 0 ? fleet.PlanetId : null,
                fleet.Orbit,
                string.Empty,
                null,
                x,
                y,
                z));
        }

        // The fleet has not flown yet: it stands on the orbit of its body.
        return _world.GetPlanetPlaceAsync(fleet.PlanetId, cancellationToken);
    }

    /// <summary>
    /// The place a fleet under way is at: the point of the route the time has reached.
    /// </summary>
    /// <param name="flight">The flight.</param>
    /// <param name="now">The moment of game time.</param>
    /// <returns>The place on the route.</returns>
    public static WorldPlaceDto PlaceOnRoute(FleetFlightEntity flight, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(flight);

        double total = (flight.ArrivesAt - flight.StartedAt).TotalSeconds;
        double progress = total <= 0d ? 1d : Math.Clamp((now - flight.StartedAt).TotalSeconds / total, 0d, 1d);

        // Inside one system the fleet is between two orbits: it keeps the system and takes the
        // orbit it has reached, because the distance inside a system is counted by the orbits.
        if (flight.FromSystemId is { } systemId && systemId == flight.ToSystemId)
        {
            int fromOrbit = flight.FromOrbit ?? 0;
            int toOrbit = flight.ToOrbit ?? 0;
            int orbit = (int)Math.Round(fromOrbit + ((toOrbit - fromOrbit) * progress), MidpointRounding.AwayFromZero);

            return new WorldPlaceDto(flight.FromSectorId, systemId, null, null, orbit, string.Empty, null, flight.FromX, flight.FromY, flight.FromZ);
        }

        return new WorldPlaceDto(
            progress < MidpointProgress ? flight.FromSectorId : flight.ToSectorId,
            null,
            null,
            null,
            null,
            string.Empty,
            null,
            flight.FromX + ((flight.ToX - flight.FromX) * progress),
            flight.FromY + ((flight.ToY - flight.FromY) * progress),
            flight.FromZ + ((flight.ToZ - flight.FromZ) * progress));
    }

    /// <summary>
    /// Writes a place into the position of a fleet.
    /// </summary>
    /// <param name="fleet">The fleet.</param>
    /// <param name="place">The place the fleet is at.</param>
    public static void Place(FleetEntity fleet, WorldPlaceDto place)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(place);

        fleet.SectorId = place.SectorId;
        fleet.SystemId = place.SystemId;
        fleet.ObjectId = place.ObjectId;
        fleet.Orbit = place.Orbit;
        fleet.PlanetId = place.PlanetId ?? 0;
        fleet.PositionX = place.X;
        fleet.PositionY = place.Y;
        fleet.PositionZ = place.Z;
        fleet.StateCode = FleetStates.Stationed;
    }

    /// <summary>
    /// Estimates the flight of a fleet of the player to a place: the distance and the time with an
    /// empty hold, with the cargo the fleet carries and with a full hold.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="isReturn">Whether the flight is the return to the home body, which names no target.</param>
    /// <param name="target">The target of the flight.</param>
    /// <param name="from">The place the flight starts at, or <see langword="null"/> for the place the fleet is at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The estimate, or <see langword="null"/> when the fleet or the target is unknown.</returns>
    public async Task<FlightEstimateDto?> EstimateAsync(
        string playerId,
        long fleetId,
        bool isReturn,
        FlightTarget? target,
        FlightTarget? from,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        FleetEntity? fleet = await _dbContext.Fleets.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == fleetId && entry.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        if (fleet is null)
        {
            return null;
        }

        WorldPlaceDto? destination = isReturn
            ? await _world.GetPlanetPlaceAsync(fleet.HomePlanetId, cancellationToken).ConfigureAwait(false)
            : target is null ? null : await ResolveAsync(target, cancellationToken).ConfigureAwait(false);

        if (destination is null)
        {
            return null;
        }

        FleetFlightEntity? flight = await _dbContext.FleetFlights.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.FleetId == fleetId, cancellationToken).ConfigureAwait(false);
        WorldPlaceDto? origin = from is null
            ? await CurrentPlaceAsync(fleet, flight, _clock.GameNow, cancellationToken).ConfigureAwait(false)
            : await ResolveAsync(from, cancellationToken).ConfigureAwait(false);

        if (origin is null)
        {
            return null;
        }

        double distance = _world.GetTravelDistance(origin, destination);
        FleetSpeeds speeds = await _speeds.ReadAsync(fleetId, cancellationToken).ConfigureAwait(false);

        return new FlightEstimateDto(
            distance,
            speeds.Empty,
            speeds.Current,
            speeds.Full,
            FleetSpeedService.FlightSeconds(distance, speeds.Empty),
            FleetSpeedService.FlightSeconds(distance, speeds.Current),
            FleetSpeedService.FlightSeconds(distance, speeds.Full),
            speeds.Empty > 0m);
    }
}

/// <summary>
/// The target of a flight as an order or a request names it (task 00053).
/// </summary>
/// <param name="Kind">The kind of the target, one of <see cref="FlightTargetKinds"/>.</param>
/// <param name="SectorId">The sector of a point of space.</param>
/// <param name="SystemId">The system of the target.</param>
/// <param name="ObjectId">The object of the map the target is.</param>
/// <param name="X">The first axis of a point of space, inside its sector.</param>
/// <param name="Y">The second axis of a point of space, inside its sector.</param>
/// <param name="Z">The third axis of a point of space, inside its sector.</param>
public sealed record FlightTarget(string Kind, int SectorId, long SystemId, long ObjectId, decimal X, decimal Y, decimal Z)
{
    /// <summary>
    /// Reads the target out of an order of a plan.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>The target.</returns>
    public static FlightTarget Of(FlightPlanOrderEntity order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new FlightTarget(order.TargetKind, order.TargetSectorId, order.TargetSystemId, order.TargetObjectId, order.TargetX, order.TargetY, order.TargetZ);
    }
}

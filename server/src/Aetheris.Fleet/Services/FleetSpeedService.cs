using Aetheris.Core.Configuration;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The speed of a fleet (task 00053, the formula of task 00050, section 8.2).
/// </summary>
/// <remarks>
/// <para>
/// The speed of a ship grows with the square root of the push of its drives per tonne of its mass:
/// <c>v = K · √(F / m)</c>. The constant <c>K</c> is not a number of the code: it is derived from the
/// reference point of the configuration, so «100 kN on 1000 t give 45 light-years per hour» is data,
/// and a change of the reference point changes every ship.
/// </para>
/// <para>
/// The mass of a ship is the mass of its project (the hull and the modules) plus what its hold carries.
/// A fleet flies at the speed of its slowest ship, so a ship without a drive grounds the fleet.
/// </para>
/// <para>
/// The ships may carry drives of several classes (task 00072), so the speed is counted for every
/// class: the ordinary drives fly inside a system, the hyperdrives fly between the systems and the
/// jump drives jump between them. The speed of a class is the speed of its slowest ship.
/// </para>
/// </remarks>
public sealed class FleetSpeedService
{
    private readonly FleetDbContext _dbContext;
    private readonly ShipDesignService _designs;
    private readonly FleetHoldService _holds;
    private readonly EngineKindsReader _engines;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetSpeedService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="designs">The projects of the ships, for their mass and push.</param>
    /// <param name="holds">The holds of the ships, for what they carry.</param>
    /// <param name="engines">The drives of the ships, for the push of every class.</param>
    /// <param name="config">The game configuration.</param>
    public FleetSpeedService(
        FleetDbContext dbContext,
        ShipDesignService designs,
        FleetHoldService holds,
        EngineKindsReader engines,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(designs);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _designs = designs;
        _holds = holds;
        _engines = engines;
        _config = config;
    }

    /// <summary>
    /// The speed of one ship, in light-years per hour.
    /// </summary>
    /// <param name="pushKilonewtons">The push of the drives of the ship, in kilonewtons.</param>
    /// <param name="massTonnes">The mass of the ship with its cargo, in tonnes.</param>
    /// <param name="settings">The settings with the reference point of the speed.</param>
    /// <returns>The speed, or zero when the ship has no push or no mass.</returns>
    public static decimal ShipSpeed(decimal pushKilonewtons, decimal massTonnes, FleetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (pushKilonewtons <= 0m || massTonnes <= 0m || settings.SpeedReferencePush <= 0m || settings.SpeedReferenceMass <= 0m)
        {
            return 0m;
        }

        // The reference point gives the constant: K = reference speed / sqrt(reference push / reference mass).
        decimal ratio = (pushKilonewtons / massTonnes) / (settings.SpeedReferencePush / settings.SpeedReferenceMass);

        return settings.SpeedReferenceSpeed * (decimal)Math.Sqrt((double)ratio);
    }

    /// <summary>
    /// The speed of a fleet: the speed of its slowest ship.
    /// </summary>
    /// <param name="shipSpeeds">The speeds of the ships.</param>
    /// <returns>The least speed, or zero when the fleet has no ship or a ship cannot fly.</returns>
    public static decimal FleetSpeed(IReadOnlyCollection<decimal> shipSpeeds)
    {
        ArgumentNullException.ThrowIfNull(shipSpeeds);

        return shipSpeeds.Count == 0 ? 0m : shipSpeeds.Min();
    }

    /// <summary>
    /// The time of a flight, in game seconds, rounded up to a whole second and never below one.
    /// </summary>
    /// <param name="distance">The distance, in light-years.</param>
    /// <param name="speedPerHour">The speed, in light-years per hour.</param>
    /// <returns>The time in seconds, or zero when the distance is zero or the fleet cannot fly.</returns>
    public static long FlightSeconds(double distance, decimal speedPerHour)
    {
        if (distance <= 0d || speedPerHour <= 0m)
        {
            return 0;
        }

        double seconds = Math.Ceiling(distance / (double)speedPerHour * FleetExtractionService.SecondsPerHour);

        return Math.Max(1L, (long)seconds);
    }

    /// <summary>
    /// Reads the speeds of a fleet with an empty hold, with the cargo it carries now and with a full
    /// hold.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The three speeds.</returns>
    public async Task<FleetSpeeds> ReadAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        FleetSettings settings = FleetSettings.From(_config);

        List<FleetShipRow> ships = await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Join(_dbContext.Ships, entry => entry.ShipId, ship => ship.Id, (entry, ship) => new FleetShipRow(ship.Id, ship.ShipDesignId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (ships.Count == 0)
        {
            FleetSpeedSet none = new(0m, 0m, 0m);

            return new FleetSpeeds(none, none, none, 0m);
        }

        IReadOnlyDictionary<long, ShipDesignSummaryDto> summaries = await _designs
            .ReadSummariesAsync([.. ships.Select(ship => ship.ShipDesignId)], cancellationToken).ConfigureAwait(false);
        Dictionary<long, ShipHold> holds = (await _holds.ReadAsync(fleetId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(hold => hold.ShipId);
        FleetEngines engines = await _engines.ReadAsync(fleetId, cancellationToken).ConfigureAwait(false);

        // The push of a class comes only from the drives of that class (task 00072): the
        // hyperdrives and the jump drives do not move a ship inside a system, and the
        // gravitational drives do not move it in open space.
        FleetSpeedSet normal = SpeedsOf(ships, summaries, holds, ship => engines.PushOfShip(ship, FlightEngineKinds.Normal), settings);
        FleetSpeedSet normalOpenSpace = SpeedsOf(ships, summaries, holds, ship => engines.PushOfShip(ship, FlightEngineKinds.Normal, inSystem: false), settings);
        FleetSpeedSet hyper = SpeedsOf(ships, summaries, holds, ship => engines.PushOfShip(ship, FlightEngineKinds.Hyper), settings);

        return new FleetSpeeds(normal, normalOpenSpace, hyper, engines.JumpPrepSeconds());
    }

    /// <summary>
    /// Counts the speeds of the fleet for the push of one class of the drives.
    /// </summary>
    /// <remarks>
    /// The cargo belongs to the fleet: the holds are filled one after another, but the fleet is as
    /// loaded as its whole hold is, so for the speed the cargo is spread over the ships by the room
    /// of their holds. A ship that happens to be filled first does not slow the fleet down as a
    /// ship with a full hold. A ship without a drive of the class has no speed, so the fleet of
    /// such a ship has no speed of the class either.
    /// </remarks>
    /// <param name="ships">The ships of the fleet.</param>
    /// <param name="summaries">The summaries of the designs of the ships.</param>
    /// <param name="holds">The holds of the ships.</param>
    /// <param name="pushOf">The push of the class of a ship, by the identifier of the ship.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <returns>The speeds of the fleet: empty, as loaded now and with full holds.</returns>
    private static FleetSpeedSet SpeedsOf(
        IReadOnlyList<FleetShipRow> ships,
        IReadOnlyDictionary<long, ShipDesignSummaryDto> summaries,
        IReadOnlyDictionary<long, ShipHold> holds,
        Func<long, decimal> pushOf,
        FleetSettings settings)
    {
        decimal fleetCapacity = holds.Values.Sum(hold => (decimal)hold.Capacity);
        decimal fleetCarried = holds.Values.Sum(hold => (decimal)hold.Carried);

        List<decimal> empty = [];
        List<decimal> current = [];
        List<decimal> full = [];

        foreach (FleetShipRow ship in ships)
        {
            if (!summaries.TryGetValue(ship.ShipDesignId, out ShipDesignSummaryDto? summary))
            {
                empty.Add(0m);
                current.Add(0m);
                full.Add(0m);

                continue;
            }

            ShipHold? hold = holds.GetValueOrDefault(ship.ShipId);
            decimal capacity = hold?.Capacity ?? 0m;
            decimal carried = fleetCapacity > 0m ? Math.Min(capacity, fleetCarried * capacity / fleetCapacity) : 0m;
            decimal push = pushOf(ship.ShipId);

            empty.Add(ShipSpeed(push, summary.Weight, settings));
            current.Add(ShipSpeed(push, summary.Weight + carried, settings));
            full.Add(ShipSpeed(push, summary.Weight + capacity, settings));
        }

        return new FleetSpeedSet(FleetSpeed(empty), FleetSpeed(current), FleetSpeed(full));
    }

    private sealed record FleetShipRow(long ShipId, long ShipDesignId);
}

/// <summary>
/// The speeds of a fleet, in light-years per hour (task 00053, task 00072).
/// </summary>
/// <param name="Normal">The speeds with the ordinary drives inside a star system.</param>
/// <param name="NormalOpenSpace">The speeds with the ordinary drives in open space, where the gravitational drives do not work.</param>
/// <param name="Hyper">The speeds with the hyperdrives alone.</param>
/// <param name="JumpPrepSeconds">How long the fleet prepares for a jump, in seconds.</param>
public sealed record FleetSpeeds(FleetSpeedSet Normal, FleetSpeedSet NormalOpenSpace, FleetSpeedSet Hyper, decimal JumpPrepSeconds)
{
    /// <summary>Gets the speed with an empty hold.</summary>
    public decimal Empty => Normal.Empty;

    /// <summary>Gets the speed with the cargo the fleet carries now.</summary>
    public decimal Current => Normal.Current;

    /// <summary>Gets the speed with a full hold.</summary>
    public decimal Full => Normal.Full;
}

/// <summary>
/// The three speeds of a fleet in one class of drives (task 00072).
/// </summary>
/// <param name="Empty">The speed with an empty hold.</param>
/// <param name="Current">The speed with the cargo the fleet carries now.</param>
/// <param name="Full">The speed with a full hold.</param>
public sealed record FleetSpeedSet(decimal Empty, decimal Current, decimal Full);

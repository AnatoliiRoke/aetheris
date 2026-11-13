using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The legs of a flight of a fleet (task 00072).
/// </summary>
/// <param name="Leg">The role of the leg, one of <see cref="FlightLegKinds"/>.</param>
/// <param name="Kind">The class of the drives the leg uses, one of <see cref="FlightEngineKinds"/>.</param>
/// <param name="From">The place the leg starts at.</param>
/// <param name="To">The place the leg ends at.</param>
/// <param name="Distance">The distance of the leg, in the units of the map.</param>
public sealed record FlightLeg(
    string Leg,
    string Kind,
    WorldPlaceDto From,
    WorldPlaceDto To,
    double Distance);

/// <summary>
/// Plans the legs of a hyperflight or of a jump (task 00072).
/// </summary>
/// <remarks>
/// <para>
/// A hyperflight or a jump between two systems is made of three legs: to the edge of the system the
/// fleet stands in, through the space between the systems, and from the edge of the system of the
/// target to the target itself. The edge of a system is its last orbit, as the data of the world
/// give it, so the number of the orbits is not a number of the code.
/// </para>
/// <para>
/// The leg to the edge is left out when the fleet stands in open space, and the leg from the edge
/// is left out when the target lies in open space.
/// </para>
/// </remarks>
public sealed class FlightLegPlanner
{
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightLegPlanner"/> class.
    /// </summary>
    /// <param name="world">The world module.</param>
    public FlightLegPlanner(IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <summary>
    /// Plans the legs of a command of the player.
    /// </summary>
    /// <param name="from">The place the fleet stands at.</param>
    /// <param name="to">The place the command names.</param>
    /// <param name="kind">The class of the drives the command uses, one of <see cref="FlightEngineKinds"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The legs of the command, in the order they are flown.</returns>
    public async Task<IReadOnlyList<FlightLeg>> PlanAsync(
        WorldPlaceDto from,
        WorldPlaceDto to,
        string kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        List<FlightLeg> legs = [];

        // The leg to the edge exists when the fleet stands inside a system.
        WorldPlaceDto? edgeOfStart = from.SystemId is { } startSystemId
            ? await _world.GetSystemEdgePlaceAsync(startSystemId, cancellationToken).ConfigureAwait(false)
            : null;

        if (edgeOfStart is not null && edgeOfStart.Orbit != from.Orbit)
        {
            legs.Add(new FlightLeg(
                FlightLegKinds.ToEdge,
                FlightEngineKinds.Normal,
                from,
                edgeOfStart,
                _world.GetTravelDistance(from, edgeOfStart)));
        }

        // The leg between the systems: from the edge of the system of the fleet to the edge of the
        // system of the target. A target in open space has no edge, so the leg ends in the open
        // space itself.
        WorldPlaceDto? edgeOfTarget = to.SystemId is { } targetSystemId
            ? await _world.GetSystemEdgePlaceAsync(targetSystemId, cancellationToken).ConfigureAwait(false)
            : null;

        WorldPlaceDto middleFrom = edgeOfStart ?? from;
        WorldPlaceDto middleTo = edgeOfTarget ?? to;

        legs.Add(new FlightLeg(
            FlightLegKinds.Ftl,
            kind,
            middleFrom,
            middleTo,
            _world.GetTravelDistance(middleFrom, middleTo)));

        // The leg from the edge exists when the target lies inside a system.
        if (edgeOfTarget is not null && edgeOfTarget.Orbit != to.Orbit)
        {
            legs.Add(new FlightLeg(
                FlightLegKinds.FromEdge,
                FlightEngineKinds.Normal,
                edgeOfTarget,
                to,
                _world.GetTravelDistance(edgeOfTarget, to)));
        }

        return legs;
    }
}

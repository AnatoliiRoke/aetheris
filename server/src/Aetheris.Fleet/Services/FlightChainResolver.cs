using Aetheris.Core.Errors;
using Aetheris.Fleet.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Fleet.Services;

/// <summary>
/// Walks the route of a flight plan and counts the legs of every command (task 00072).
/// </summary>
/// <remarks>
/// <para>
/// The legs of a command are counted over the route of the whole plan: the place the fleet stands
/// at when a command begins is the place the command before it ends at, so the chain is walked
/// from the first order to the last. The place of a command is taken from the plan, so the estimate
/// of a plan is the same whatever the fleet does before the plan begins.
/// </para>
/// <para>
/// The chain is used for the estimate of the plan and for the check of the fuel before the fleet
/// leaves, so it never changes the plan itself.
/// </para>
/// <para>
/// A hyperflight or a jump is written into the plan as two or three real commands that share one
/// key: a flight to the edge of the system, the leg between the systems and a flight from the edge
/// to the target. Every one of them names its own end, so the chain walks them as they are.
/// </para>
/// </remarks>
public sealed class FlightChainResolver
{
    private readonly FlightLegPlanner _legs;
    private readonly FleetFlightService _flights;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightChainResolver"/> class.
    /// </summary>
    /// <param name="legs">Plans the legs of a command.</param>
    /// <param name="flights">Finds the places of the map.</param>
    /// <param name="world">The world module, for the distances.</param>
    public FlightChainResolver(FlightLegPlanner legs, FleetFlightService flights, IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(flights);
        ArgumentNullException.ThrowIfNull(world);

        _legs = legs;
        _flights = flights;
        _world = world;
    }

    /// <summary>
    /// Walks the route of a plan and returns the legs of every command.
    /// </summary>
    /// <param name="fleet">The fleet of the plan.</param>
    /// <param name="orders">The orders of the plan, in the order they are carried out in.</param>
    /// <param name="start">The place the fleet stands at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The legs of every command, in the order of the plan.</returns>
    /// <exception cref="GameException">A command names a place that is not on the map.</exception>
    public async Task<IReadOnlyList<FlightLeg>> ResolveAsync(
        FleetEntity fleet,
        IReadOnlyList<FlightPlanOrderEntity> orders,
        WorldPlaceDto start,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fleet);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(start);

        List<FlightLeg> chain = [];
        WorldPlaceDto place = start;

        foreach (FlightPlanOrderEntity order in orders.OrderBy(entry => entry.Position))
        {
            string? command = CommandOf(order);

            // The leg between the systems of a saved hyperflight or jump is a command of its own: it
            // flies from where the command before it ends to the end it names (task 00072).
            if (command is not null && string.Equals(order.Leg, FlightLegKinds.Ftl, StringComparison.Ordinal))
            {
                WorldPlaceDto end = await TargetOfAsync(fleet, order, cancellationToken).ConfigureAwait(false);

                chain.Add(new FlightLeg(FlightLegKinds.Ftl, KindOf(command), place, end, _world.GetTravelDistance(place, end)));
                place = end;

                continue;
            }

            // A hyperflight or a jump that was not spread over its legs yet is planned whole.
            if (command is not null && order.Leg is null)
            {
                WorldPlaceDto target = await TargetOfAsync(fleet, order, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<FlightLeg> legs = await _legs.PlanAsync(place, target, KindOf(command), cancellationToken).ConfigureAwait(false);

                chain.AddRange(legs);
                place = legs.Count > 0 ? legs[^1].To : target;

                continue;
            }

            if (order.TypeCode == FlightOrderTypes.Fly || order.TypeCode == FlightOrderTypes.Return)
            {
                WorldPlaceDto target = await TargetOfAsync(fleet, order, cancellationToken).ConfigureAwait(false);

                chain.Add(new FlightLeg(
                    order.Leg ?? FlightLegKinds.Normal,
                    FlightEngineKinds.Normal,
                    place,
                    target,
                    _world.GetTravelDistance(place, target)));

                place = target;
            }
        }

        return chain;
    }

    /// <summary>
    /// Gets the class of the drives of a command.
    /// </summary>
    /// <param name="command">The command, <see cref="FlightOrderTypes.Hyperfly"/> or <see cref="FlightOrderTypes.Jump"/>.</param>
    /// <returns>The class of the drives.</returns>
    private static string KindOf(string command) =>
        string.Equals(command, FlightOrderTypes.Hyperfly, StringComparison.Ordinal) ? FlightEngineKinds.Hyper : FlightEngineKinds.Jump;

    /// <summary>
    /// Gets the command of the player an order belongs to: a hyperflight or a jump, or
    /// <see langword="null"/> for any other order.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <returns>The type of the command, or <see langword="null"/>.</returns>
    public static string? CommandOf(FlightPlanOrderEntity order)
    {
        ArgumentNullException.ThrowIfNull(order);

        string type = order.Command ?? order.TypeCode;

        return type == FlightOrderTypes.Hyperfly || type == FlightOrderTypes.Jump ? type : null;
    }

    /// <summary>
    /// Finds the place an order flies to.
    /// </summary>
    /// <param name="fleet">The fleet of the plan.</param>
    /// <param name="order">The order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place.</returns>
    /// <exception cref="GameException">The order names a place that is not on the map.</exception>
    private async Task<WorldPlaceDto> TargetOfAsync(
        FleetEntity fleet,
        FlightPlanOrderEntity order,
        CancellationToken cancellationToken)
    {
        WorldPlaceDto? target = await _flights.ResolveOrderAsync(fleet, order, cancellationToken).ConfigureAwait(false);

        return target ?? throw new GameException(FleetErrorCodes.FlightTargetUnknown, "The target of the flight is not a place of the map.");
    }
}

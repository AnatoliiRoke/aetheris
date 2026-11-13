using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.World.Api;

namespace Aetheris.Fleet.Services;

/// <summary>
/// One leg of a flight and what it costs (task 00072).
/// </summary>
/// <param name="Leg">The role of the leg, one of <see cref="FlightLegKinds"/>.</param>
/// <param name="Kind">The class of the drives the leg uses, one of <see cref="FlightEngineKinds"/>.</param>
/// <param name="From">The place the leg starts at.</param>
/// <param name="To">The place the leg ends at.</param>
/// <param name="Distance">The distance of the leg, in the units of the map.</param>
/// <param name="Seconds">How long the leg lasts, in game seconds.</param>
/// <param name="Fuel">The fuel the leg burns, by the resource.</param>
public sealed record FuelLeg(
    string Leg,
    string Kind,
    WorldPlaceDto From,
    WorldPlaceDto To,
    double Distance,
    long Seconds,
    IReadOnlyDictionary<int, decimal> Fuel);

/// <summary>
/// The need of the fuel of a fleet (task 00072).
/// </summary>
/// <param name="Legs">The legs of the flight, one after another.</param>
/// <param name="ReturnLegs">The legs of the flight home.</param>
/// <param name="NeedByResource">The need of every resource, by its identifier.</param>
/// <param name="HaveByResource">What the hold of the fleet holds now, by the identifier of the resource.</param>
public sealed record FuelEstimate(
    IReadOnlyList<FuelLeg> Legs,
    IReadOnlyList<FuelLeg> ReturnLegs,
    IReadOnlyDictionary<int, decimal> NeedByResource,
    IReadOnlyDictionary<int, decimal> HaveByResource)
{
    /// <summary>
    /// Gets the resources the fleet does not have enough of.
    /// </summary>
    /// <returns>The shortage of every resource, by its identifier.</returns>
    public IReadOnlyDictionary<int, decimal> Shortages()
    {
        Dictionary<int, decimal> shortages = [];

        foreach ((int resourceTypeId, decimal need) in NeedByResource)
        {
            decimal have = HaveByResource.GetValueOrDefault(resourceTypeId);

            if (need > have)
            {
                shortages[resourceTypeId] = need - have;
            }
        }

        return shortages;
    }
}

/// <summary>
/// Counts the fuel a fleet burns on its route (task 00072).
/// </summary>
/// <remarks>
/// <para>
/// The fuel of a leg is a mix of resources: the drive burns <c>char_fuel_use</c> units of fuel
/// in a second of its work, and the fuel is split between the resources by their shares. The
/// amount of a resource of a leg is the sum of the drives of the class of the leg: every drive
/// burns its own amount, multiplied by the share of the resource in its fuel.
/// </para>
/// <para>
/// The gravitational drives work only near a star (task 00072): on a leg in open space they give
/// no push and burn no fuel, so their share of the fuel of the leg is zero.
/// </para>
/// <para>
/// Only a flight burns fuel: an extraction, a scan, a wait, a load, an unload and a stay burn
/// nothing. A jump burns the fuel of the time of its preparation, because the drives charge
/// during the preparation and the jump itself takes no time.
/// </para>
/// <para>
/// The mass of the fleet is the mass of its ships and of what their holds carry, so the time of a
/// leg depends on the load of the fleet at the start of the leg.
/// </para>
/// </remarks>
public sealed class FuelPlanner
{
    private readonly EngineKindsReader _engines;
    private readonly FleetSpeedService _speeds;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuelPlanner"/> class.
    /// </summary>
    /// <param name="engines">The drives of the ships, for the fuel they burn.</param>
    /// <param name="speeds">The speeds of the fleet, for the time of the legs.</param>
    public FuelPlanner(EngineKindsReader engines, FleetSpeedService speeds)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(speeds);

        _engines = engines;
        _speeds = speeds;
    }

    /// <summary>
    /// Counts the fuel a fleet burns on its legs.
    /// </summary>
    /// <param name="engines">The drives of the fleet.</param>
    /// <param name="speeds">The speeds of the fleet.</param>
    /// <param name="legs">The legs of the flight, one after another.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <returns>The legs with their fuel and the need of every resource.</returns>
    public static FuelEstimate Estimate(
        FleetEngines engines,
        FleetSpeeds speeds,
        IReadOnlyList<FlightLeg> legs,
        FleetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(settings);

        List<FuelLeg> counted = [];
        Dictionary<int, decimal> need = [];

        foreach (FlightLeg leg in legs)
        {
            long seconds = SecondsOf(leg, speeds, settings);
            Dictionary<int, decimal> fuel = BurnOf(engines, leg.Kind, seconds, IsInSystem(leg));

            counted.Add(new FuelLeg(leg.Leg, leg.Kind, leg.From, leg.To, leg.Distance, seconds, fuel));

            foreach ((int resourceTypeId, decimal amount) in fuel)
            {
                need[resourceTypeId] = need.GetValueOrDefault(resourceTypeId) + amount;
            }
        }

        return new FuelEstimate(counted, [], need, new Dictionary<int, decimal>());
    }

    /// <summary>
    /// Counts the fuel of the legs of a flight and of the flight home.
    /// </summary>
    /// <param name="engines">The drives of the fleet.</param>
    /// <param name="speeds">The speeds of the fleet.</param>
    /// <param name="legs">The legs of the flight.</param>
    /// <param name="returnLegs">The legs of the flight home.</param>
    /// <param name="have">What the hold of the fleet holds now, by the identifier of the resource.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <returns>The need of every resource and the shortage.</returns>
    public static FuelEstimate EstimateWithReturn(
        FleetEngines engines,
        FleetSpeeds speeds,
        IReadOnlyList<FlightLeg> legs,
        IReadOnlyList<FlightLeg> returnLegs,
        IReadOnlyDictionary<int, decimal> have,
        FleetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(legs);
        ArgumentNullException.ThrowIfNull(returnLegs);
        ArgumentNullException.ThrowIfNull(have);
        ArgumentNullException.ThrowIfNull(settings);

        FuelEstimate estimate = Estimate(engines, speeds, legs, settings);
        List<FuelLeg> countedReturn = [];
        Dictionary<int, decimal> need = new(estimate.NeedByResource);

        foreach (FlightLeg leg in returnLegs)
        {
            long seconds = SecondsOf(leg, speeds, settings);
            Dictionary<int, decimal> fuel = BurnOf(engines, leg.Kind, seconds, IsInSystem(leg));

            countedReturn.Add(new FuelLeg(leg.Leg, leg.Kind, leg.From, leg.To, leg.Distance, seconds, fuel));

            foreach ((int resourceTypeId, decimal amount) in fuel)
            {
                need[resourceTypeId] = need.GetValueOrDefault(resourceTypeId) + amount;
            }
        }

        return new FuelEstimate(estimate.Legs, countedReturn, need, have);
    }

    /// <summary>
    /// The time of a leg with full holds, in game seconds: the check before the fleet leaves
    /// counts the slowest and the dearest case, so the fuel never runs out on the way (task 00072, 2.4.6).
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="speeds">The speeds of the fleet.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <returns>The time of the leg.</returns>
    private static long SecondsOf(FlightLeg leg, FleetSpeeds speeds, FleetSettings settings) =>
        SecondsOf(leg, speeds, settings, set => set.Full);

    /// <summary>
    /// The time of a leg, in game seconds, at the load a selector picks.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <param name="speeds">The speeds of the fleet.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <param name="loadOf">Picks the speed of a load out of the speeds of a class: empty, as loaded now or full.</param>
    /// <returns>The time of the leg.</returns>
    public static long SecondsOf(
        FlightLeg leg,
        FleetSpeeds speeds,
        FleetSettings settings,
        Func<FleetSpeedSet, decimal> loadOf)
    {
        ArgumentNullException.ThrowIfNull(leg);
        ArgumentNullException.ThrowIfNull(speeds);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loadOf);

        // A jump takes the time of its preparation: the drives charge and the jump itself is
        // instant, whatever the load.
        if (string.Equals(leg.Kind, FlightEngineKinds.Jump, StringComparison.Ordinal))
        {
            return (long)Math.Ceiling(speeds.JumpPrepSeconds * FactorOf(leg.Distance, settings));
        }

        // A hyperflight flies with the hyperdrives alone, multiplied by the multiplier of the configuration.
        if (string.Equals(leg.Kind, FlightEngineKinds.Hyper, StringComparison.Ordinal))
        {
            decimal speed = loadOf(speeds.Hyper) * settings.HyperSpeedMultiplier;

            return FleetSpeedService.FlightSeconds(leg.Distance, speed);
        }

        FleetSpeedSet normal = IsInSystem(leg) ? speeds.Normal : speeds.NormalOpenSpace;

        return FleetSpeedService.FlightSeconds(leg.Distance, loadOf(normal));
    }

    /// <summary>
    /// Tells whether a leg lies inside one star system, where the gravitational drives work.
    /// </summary>
    /// <param name="leg">The leg.</param>
    /// <returns><see langword="true"/> when both ends of the leg are in the same system.</returns>
    public static bool IsInSystem(FlightLeg leg)
    {
        ArgumentNullException.ThrowIfNull(leg);

        return IsInSystem(leg.From, leg.To);
    }

    /// <summary>
    /// Tells whether a flight between two places stays inside one star system.
    /// </summary>
    /// <param name="from">The place the flight starts at.</param>
    /// <param name="to">The place the flight ends at.</param>
    /// <returns><see langword="true"/> when both places are in the same system.</returns>
    public static bool IsInSystem(WorldPlaceDto from, WorldPlaceDto to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        return from.SystemId is { } systemId && systemId == to.SystemId;
    }

    /// <summary>
    /// The factor of the time of the preparation of a jump: the preparation lasts the same time up
    /// to the reference distance and grows past it.
    /// </summary>
    /// <param name="distance">The distance of the jump, in the units of the map.</param>
    /// <param name="settings">The parameters of the fleet.</param>
    /// <returns>The factor.</returns>
    private static decimal FactorOf(double distance, FleetSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        double reference = Math.Max(1d, settings.JumpReferenceDistance);

        return (decimal)Math.Max(1d, distance / reference);
    }

    /// <summary>
    /// The fuel the drives of a class burn in a time.
    /// </summary>
    /// <param name="engines">The drives of the fleet.</param>
    /// <param name="kind">The class of the drives, one of <see cref="FlightEngineKinds"/>.</param>
    /// <param name="seconds">The time of the work, in game seconds.</param>
    /// <param name="inSystem">Whether the work is inside a star system; the gravitational drives burn nothing in open space (task 00072).</param>
    /// <returns>The fuel by the resource.</returns>
    public static Dictionary<int, decimal> BurnOf(FleetEngines engines, string kind, long seconds, bool inSystem = true)
    {
        ArgumentNullException.ThrowIfNull(engines);

        Dictionary<int, decimal> fuel = [];

        if (seconds <= 0)
        {
            return fuel;
        }

        // The gravitational drives work only near a star, so in open space they burn nothing.
        foreach (FleetDrive drive in engines.Of(kind, inSystem))
        {
            if (drive.FuelUse <= 0m)
            {
                continue;
            }

            decimal burned = drive.FuelUse * drive.Quantity * seconds;

            foreach (FleetFuelShare share in drive.Fuel)
            {
                fuel[share.ResourceTypeId] = fuel.GetValueOrDefault(share.ResourceTypeId) + burned * share.Share;
            }
        }

        return fuel;
    }
}

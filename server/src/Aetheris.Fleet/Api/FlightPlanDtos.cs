using Aetheris.Fleet.Data.Entities;

namespace Aetheris.Fleet.Api;

/// <summary>
/// A flight plan of a fleet with its orders (task 00050).
/// </summary>
/// <param name="Id">The plan.</param>
/// <param name="FleetId">The fleet the plan is made for.</param>
/// <param name="StateCode">The state of the plan.</param>
/// <param name="CreatedAt">The moment of game time the plan was created at.</param>
/// <param name="UpdatedAt">The moment of game time the plan was changed at.</param>
/// <param name="Version">The version of the plan, raised by every change.</param>
/// <param name="Orders">The orders of the plan, in the order they are carried out in.</param>
/// <param name="CurrentPosition">The position of the order the fleet carries out now, or zero before the plan begins (task 00053).</param>
/// <param name="PauseCode">Why the plan stopped by itself, or <see langword="null"/> (task 00053).</param>
public sealed record FlightPlanDto(
    long Id,
    long FleetId,
    string StateCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    IReadOnlyList<FlightPlanOrderDto> Orders,
    int CurrentPosition = 0,
    string? PauseCode = null);

/// <summary>
/// An order of a flight plan (task 00050).
/// </summary>
/// <param name="Id">The order.</param>
/// <param name="Position">The position of the order in the plan, from one.</param>
/// <param name="TypeCode">The type of the order.</param>
/// <param name="TargetKind">The kind of the target of the order.</param>
/// <param name="TargetSectorId">The sector of the target.</param>
/// <param name="TargetSystemId">The system of the target.</param>
/// <param name="TargetObjectId">The object of the target.</param>
/// <param name="TargetX">The first axis of the target.</param>
/// <param name="TargetY">The second axis of the target.</param>
/// <param name="TargetZ">The third axis of the target.</param>
/// <param name="DurationSeconds">How long the order lasts, in game seconds.</param>
/// <param name="Parameters">The parameters of the order as a document.</param>
/// <param name="GroupKey">The key that binds the legs of one command of the player together (task 00072), or <see langword="null"/> for an order that stands alone.</param>
/// <param name="Command">The command of the player the legs come from (task 00072): the hyperflight or the jump, or <see langword="null"/>.</param>
/// <param name="Leg">The role of the order in the group (task 00072), or <see langword="null"/>.</param>
public sealed record FlightPlanOrderDto(
    long Id,
    int Position,
    string TypeCode,
    string TargetKind,
    int TargetSectorId,
    long TargetSystemId,
    long TargetObjectId,
    decimal TargetX,
    decimal TargetY,
    decimal TargetZ,
    long DurationSeconds,
    string Parameters,
    Guid? GroupKey = null,
    string? Command = null,
    string? Leg = null);

/// <summary>
/// What a flight of a fleet to a place takes (task 00053).
/// </summary>
/// <remarks>
/// The time is counted by the server from the distance and the speed of the slowest ship of the
/// fleet, with an empty hold, with the cargo the fleet carries now and with a full hold.
/// </remarks>
/// <param name="Distance">The distance of the flight, in light-years.</param>
/// <param name="EmptySpeed">The speed of the fleet with an empty hold, in light-years per hour.</param>
/// <param name="CurrentSpeed">The speed of the fleet with the cargo it carries now.</param>
/// <param name="FullSpeed">The speed of the fleet with a full hold.</param>
/// <param name="EmptySeconds">The time with an empty hold, in game seconds.</param>
/// <param name="CurrentSeconds">The time with the cargo the fleet carries now, in game seconds.</param>
/// <param name="FullSeconds">The time with a full hold, in game seconds.</param>
/// <param name="CanFly">Whether the fleet can fly at all: every ship of it has a drive.</param>
/// <param name="Legs">The legs of the flight with the fuel every leg burns (task 00072): two or three for a hyperflight or a jump, one for an ordinary flight.</param>
/// <param name="ReturnLegs">The legs of the flight home of the saved plan of the fleet, when the check of the fuel counts it (task 00072).</param>
/// <param name="FuelNeed">The need of every resource of the saved plan of the fleet, with what the hold holds now (task 00072).</param>
/// <param name="Shortages">The resources the saved plan of the fleet does not have enough of (task 00072).</param>
public sealed record FlightEstimateDto(
    double Distance,
    decimal EmptySpeed,
    decimal CurrentSpeed,
    decimal FullSpeed,
    long EmptySeconds,
    long CurrentSeconds,
    long FullSeconds,
    bool CanFly,
    IReadOnlyList<FlightFuelLegDto>? Legs = null,
    IReadOnlyList<FlightFuelLegDto>? ReturnLegs = null,
    IReadOnlyList<FlightFuelResourceDto>? FuelNeed = null,
    IReadOnlyList<FlightFuelShortageDto>? Shortages = null);

/// <summary>
/// The legs of a flight and the fuel they burn (task 00072).
/// </summary>
/// <param name="Legs">The legs of the flight, one after another.</param>
/// <param name="ReturnLegs">The legs of the flight home, when the check of the fuel counts it.</param>
/// <param name="FuelNeed">The need of every resource, with what the hold holds now.</param>
/// <param name="Shortages">The resources the fleet does not have enough of.</param>
public sealed record FlightFuelEstimateDto(
    IReadOnlyList<FlightFuelLegDto> Legs,
    IReadOnlyList<FlightFuelLegDto> ReturnLegs,
    IReadOnlyList<FlightFuelResourceDto> FuelNeed,
    IReadOnlyList<FlightFuelShortageDto> Shortages);

/// <summary>
/// One leg of a flight and what it burns (task 00072).
/// </summary>
/// <param name="Leg">The role of the leg, one of <see cref="FlightLegKinds"/>.</param>
/// <param name="Kind">The class of the drives the leg uses, one of <see cref="FlightEngineKinds"/>.</param>
/// <param name="FromSectorId">The sector the leg starts at.</param>
/// <param name="FromSystemId">The system the leg starts at, or <see langword="null"/> in open space.</param>
/// <param name="FromOrbit">The orbit the leg starts at, or <see langword="null"/>.</param>
/// <param name="ToSectorId">The sector the leg ends at.</param>
/// <param name="ToSystemId">The system the leg ends at, or <see langword="null"/> in open space.</param>
/// <param name="ToOrbit">The orbit the leg ends at, or <see langword="null"/>.</param>
/// <param name="Distance">The distance of the leg, in the units of the map.</param>
/// <param name="Seconds">How long the leg lasts, in game seconds.</param>
/// <param name="Fuel">The fuel the leg burns, by the resource.</param>
public sealed record FlightFuelLegDto(
    string Leg,
    string Kind,
    int FromSectorId,
    long? FromSystemId,
    int? FromOrbit,
    int ToSectorId,
    long? ToSystemId,
    int? ToOrbit,
    double Distance,
    long Seconds,
    IReadOnlyList<FlightFuelResourceDto> Fuel);

/// <summary>
/// One resource of the fuel of a leg (task 00072).
/// </summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Amount">How much of the resource the leg burns, or how much the fleet needs.</param>
/// <param name="Have">How much of the resource the hold of the fleet holds now.</param>
public sealed record FlightFuelResourceDto(int ResourceTypeId, decimal Amount, decimal Have = 0m);

/// <summary>
/// One resource the fleet does not have enough of (task 00072).
/// </summary>
/// <param name="ResourceTypeId">The resource.</param>
/// <param name="Need">How much of the resource the route needs.</param>
/// <param name="Have">How much of the resource the hold of the fleet holds now.</param>
public sealed record FlightFuelShortageDto(int ResourceTypeId, decimal Need, decimal Have);

/// <summary>
/// The flight of a fleet that is under way (task 00053).
/// </summary>
/// <param name="OrderId">The order of the plan that is the flight.</param>
/// <param name="StartedAt">The moment of game time the fleet left.</param>
/// <param name="ArrivesAt">The moment of game time the fleet arrives.</param>
/// <param name="Distance">The distance of the flight, in light-years.</param>
/// <param name="Speed">The speed of the fleet, in light-years per hour.</param>
/// <param name="FromSectorId">The sector the fleet left (task 00059).</param>
/// <param name="FromSystemId">The system the fleet left, or <see langword="null"/> in open space (task 00059).</param>
/// <param name="FromOrbit">The orbit the fleet left, or <see langword="null"/> outside a system (task 00059).</param>
/// <param name="FromX">The first axis of the place the fleet left.</param>
/// <param name="FromY">The second axis of the place the fleet left.</param>
/// <param name="FromZ">The third axis of the place the fleet left.</param>
/// <param name="ToSectorId">The sector of the destination.</param>
/// <param name="ToSystemId">The system of the destination, or <see langword="null"/> in open space.</param>
/// <param name="ToObjectId">The object of the map the fleet flies to, or <see langword="null"/> for a point of space.</param>
/// <param name="ToPlanetId">The body the fleet flies to, or <see langword="null"/>.</param>
/// <param name="ToOrbit">The orbit of the destination, or <see langword="null"/> outside a system (task 00059).</param>
/// <param name="ToX">The first axis of the destination in the galaxy.</param>
/// <param name="ToY">The second axis of the destination in the galaxy.</param>
/// <param name="ToZ">The third axis of the destination in the galaxy.</param>
/// <param name="LegKind">The class of the drives of the flight (task 00072): <c>normal</c>, <c>hyper</c> or <c>jump</c>.</param>
/// <param name="PrepEndsAt">The moment the preparation of a jump is over, or <see langword="null"/> for a flight that is not a jump (task 00072).</param>
public sealed record FleetFlightDto(
    long OrderId,
    DateTimeOffset StartedAt,
    DateTimeOffset ArrivesAt,
    double Distance,
    decimal Speed,
    int FromSectorId,
    long? FromSystemId,
    int? FromOrbit,
    double FromX,
    double FromY,
    double FromZ,
    int ToSectorId,
    long? ToSystemId,
    long? ToObjectId,
    long? ToPlanetId,
    int? ToOrbit,
    double ToX,
    double ToY,
    double ToZ,
    string LegKind = FlightEngineKinds.Normal,
    DateTimeOffset? PrepEndsAt = null);

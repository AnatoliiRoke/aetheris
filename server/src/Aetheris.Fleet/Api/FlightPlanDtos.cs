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
    string Parameters);

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
public sealed record FlightEstimateDto(
    double Distance,
    decimal EmptySpeed,
    decimal CurrentSpeed,
    decimal FullSpeed,
    long EmptySeconds,
    long CurrentSeconds,
    long FullSeconds,
    bool CanFly);

/// <summary>
/// The flight of a fleet that is under way (task 00053).
/// </summary>
/// <param name="OrderId">The order of the plan that is the flight.</param>
/// <param name="StartedAt">The moment of game time the fleet left.</param>
/// <param name="ArrivesAt">The moment of game time the fleet arrives.</param>
/// <param name="Distance">The distance of the flight, in light-years.</param>
/// <param name="Speed">The speed of the fleet, in light-years per hour.</param>
/// <param name="ToSectorId">The sector of the destination.</param>
/// <param name="ToSystemId">The system of the destination, or <see langword="null"/> in open space.</param>
/// <param name="ToObjectId">The object of the map the fleet flies to, or <see langword="null"/> for a point of space.</param>
/// <param name="ToPlanetId">The body the fleet flies to, or <see langword="null"/>.</param>
/// <param name="ToX">The first axis of the destination in the galaxy.</param>
/// <param name="ToY">The second axis of the destination in the galaxy.</param>
/// <param name="ToZ">The third axis of the destination in the galaxy.</param>
public sealed record FleetFlightDto(
    long OrderId,
    DateTimeOffset StartedAt,
    DateTimeOffset ArrivesAt,
    double Distance,
    decimal Speed,
    int ToSectorId,
    long? ToSystemId,
    long? ToObjectId,
    long? ToPlanetId,
    double ToX,
    double ToY,
    double ToZ);

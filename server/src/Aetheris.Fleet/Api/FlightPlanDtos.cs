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
public sealed record FlightPlanDto(
    long Id,
    long FleetId,
    string StateCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version,
    IReadOnlyList<FlightPlanOrderDto> Orders);

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
/// What the extraction of a fleet at a place will take (task 00050).
/// </summary>
/// <remarks>
/// The three times answer three questions: how long the player asked the fleet to work, how long
/// the holds of the fleet will hold the yield, and how long the place itself will last.
/// </remarks>
/// <param name="CycleSeconds">The length of a cycle of extraction, in game seconds.</param>
/// <param name="ResourceTonsPerCycle">The yield of the fleet in one cycle, in tonnes.</param>
/// <param name="ProcessedTonsPerCycle">The mass the fleet takes from the place in one cycle, in tonnes.</param>
/// <param name="FreeHoldTons">The room that is left in the holds of the fleet, in tonnes.</param>
/// <param name="PlaceMassTons">The mass that is left in the place, in tonnes.</param>
/// <param name="HoldFullSeconds">How long the holds of the fleet will hold the yield, in game seconds.</param>
/// <param name="PlaceEmptySeconds">How long the place will last, in game seconds.</param>
public sealed record ExtractionEstimateDto(
    int CycleSeconds,
    long ResourceTonsPerCycle,
    long ProcessedTonsPerCycle,
    long FreeHoldTons,
    long PlaceMassTons,
    long HoldFullSeconds,
    long PlaceEmptySeconds);

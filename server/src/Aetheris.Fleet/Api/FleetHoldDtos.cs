namespace Aetheris.Fleet.Api;

/// <summary>
/// What the holds of the ships of a fleet carry (task 00056).
/// </summary>
/// <remarks>
/// The list of the items is the part of the contract that grows: a thing that is not a resource joins it as
/// an item with its own reference, and the readers of the contract that know only resources keep working.
/// </remarks>
/// <param name="FleetId">The fleet.</param>
/// <param name="Capacity">The room of all the holds of the fleet, in tonnes.</param>
/// <param name="Carried">What the holds carry now, in tonnes.</param>
/// <param name="Items">What is in the holds, the greatest amount first; an item with nothing in it is not listed.</param>
/// <param name="Parts">The modules in the holds, the greatest number first (task 00057).</param>
public sealed record FleetHoldDto(
    long FleetId,
    long Capacity,
    long Carried,
    IReadOnlyList<FleetHoldItemDto> Items,
    IReadOnlyList<FleetHoldPartDto> Parts);

/// <summary>
/// One kind of resource in the holds of a fleet (task 00056).
/// </summary>
/// <param name="ResourceId">The resource.</param>
/// <param name="Amount">How much of it the fleet carries, in tonnes.</param>
public sealed record FleetHoldItemDto(
    int ResourceId,
    long Amount);

/// <summary>
/// One kind of module in the holds of a fleet (task 00057).
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="Quantity">How many parts the fleet carries.</param>
public sealed record FleetHoldPartDto(
    int PartTypeId,
    int MarkLevel,
    bool HasRaceBonus,
    long Quantity);

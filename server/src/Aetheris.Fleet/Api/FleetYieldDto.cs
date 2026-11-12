namespace Aetheris.Fleet.Api;

/// <summary>
/// What a fleet took from a place in one cycle of extraction (task 00060).
/// </summary>
/// <remarks>
/// The yield is a fact of the fleet, and the report of the fleet is written from it. The type of the
/// resource is named by its identifier, because the fleet does not know the names of the resources
/// (documentation/tech/08, rule 5).
/// </remarks>
/// <param name="ResourceId">The type of the resource.</param>
/// <param name="Amount">How much of it the fleet took.</param>
public sealed record FleetYieldDto(int ResourceId, long Amount);

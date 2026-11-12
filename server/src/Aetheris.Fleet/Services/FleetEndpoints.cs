using System.Globalization;
using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Data.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The reads of the fleet for a player (task 00038, section 3.2).
/// </summary>
public static class FleetEndpoints
{
    /// <summary>
    /// The route prefix of the reads of the module.
    /// </summary>
    public const string Prefix = "/api/v1/fleet";

    /// <summary>
    /// Maps the reads of the module. They need a signed-in player and change nothing.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder fleet = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        fleet.MapGet("/designs", async (
            HttpContext context,
            ClaimsPrincipal user,
            ShipDesignService designs,
            CancellationToken cancellationToken) =>
            Results.Json(
                await designs
                    .ReadAsync(
                        PlayerId(user),
                        Body(context.Request.Query),
                        ReadFlag(context.Request.Query, "include_archived"),
                        cancellationToken)
                    .ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/designs/{id:long}", async (
            long id,
            ClaimsPrincipal user,
            ShipDesignService designs,
            CancellationToken cancellationToken) =>
        {
            ShipDesignDto? design = await designs.ReadOneAsync(PlayerId(user), id, cancellationToken).ConfigureAwait(false);

            return design is null
                ? Results.NotFound()
                : Results.Json(design, FleetJson.Options);
        });

        fleet.MapGet("/ship-catalog", async (
            [FromQuery(Name = "planet_id")] long? planetId,
            ClaimsPrincipal user,
            ShipDesignService designs,
            CancellationToken cancellationToken) =>
            Results.Json(
                await designs.ReadCatalogAsync(PlayerId(user), planetId ?? 0, cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/shipyards", async (
            HttpContext context,
            ClaimsPrincipal user,
            ShipyardQueueService queue,
            CancellationToken cancellationToken) =>
            Results.Json(
                await queue.ReadAsync(PlayerId(user), RequiredBody(context.Request.Query), null, cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/shipyards/{planetBuildingId:long}", async (
            long planetBuildingId,
            HttpContext context,
            ClaimsPrincipal user,
            ShipyardQueueService queue,
            CancellationToken cancellationToken) =>
            Results.Json(
                await queue.ReadAsync(PlayerId(user), RequiredBody(context.Request.Query), planetBuildingId, cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/ships", async (
            HttpContext context,
            ClaimsPrincipal user,
            ShipyardQueueService queue,
            CancellationToken cancellationToken) =>
            Results.Json(
                await queue
                    .ReadShipsAsync(
                        PlayerId(user),
                        Body(context.Request.Query),
                        ReadFlag(context.Request.Query, "free"),
                        cancellationToken)
                    .ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/fleets", async (
            ClaimsPrincipal user,
            FleetFormationService fleets,
            CancellationToken cancellationToken) =>
            Results.Json(
                await fleets.ReadAsync(PlayerId(user), cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        // The fleets that are under way, as the map draws them (task 00059): the route of every fleet
        // and the moment it arrives, in one read.
        fleet.MapGet("/fleets/in-flight", async (
            ClaimsPrincipal user,
            FleetMapService fleets,
            CancellationToken cancellationToken) =>
            Results.Json(
                await fleets.ReadAsync(PlayerId(user), cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        // What the holds of a fleet carry (task 00056): read when the player opens the window of the hold.
        fleet.MapGet("/fleets/{fleetId:long}/hold", async (
            long fleetId,
            ClaimsPrincipal user,
            FleetHoldService holds,
            CancellationToken cancellationToken) =>
            Results.Json(
                await holds.ReadForPlayerAsync(PlayerId(user), fleetId, cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/flight-plans", async (
            ClaimsPrincipal user,
            FlightPlanService plans,
            CancellationToken cancellationToken) =>
            Results.Json(
                await plans.ReadAsync(PlayerId(user), cancellationToken).ConfigureAwait(false),
                FleetJson.Options));

        fleet.MapGet("/flight-plans/fleet/{fleetId:long}", async (
            long fleetId,
            ClaimsPrincipal user,
            FlightPlanService plans,
            CancellationToken cancellationToken) =>
        {
            FlightPlanDto? plan = await plans.ReadForFleetAsync(PlayerId(user), fleetId, cancellationToken).ConfigureAwait(false);

            return plan is null
                ? Results.NotFound()
                : Results.Json(plan, FleetJson.Options);
        });

        // The time of a flight is counted by the server (task 00053): the distance comes from the world and
        // the speed from the ships of the fleet, so the screen shows the same time the fleet will take.
        fleet.MapGet("/flight-plans/estimate", async (
            HttpContext context,
            ClaimsPrincipal user,
            FleetFlightService flights,
            CancellationToken cancellationToken) =>
        {
            IQueryCollection query = context.Request.Query;
            long? fleetId = Number(query, "fleet_id");

            if (fleetId is null)
            {
                throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet is missing.");
            }

            bool isReturn = string.Equals(query["type_code"], FlightOrderTypes.Return, StringComparison.Ordinal);
            FlightEstimateDto? estimate = await flights
                .EstimateAsync(
                    PlayerId(user),
                    fleetId.Value,
                    isReturn,
                    ReadTarget(query, "target"),
                    ReadTarget(query, "from"),
                    cancellationToken)
                .ConfigureAwait(false);

            return estimate is null
                ? Results.NotFound()
                : Results.Json(estimate, FleetJson.Options);
        });

        return endpoints;
    }

    /// <summary>
    /// Reads a number out of the query of a request.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="name">The name of the parameter.</param>
    /// <returns>The number, or <see langword="null"/> when the parameter is missing or not a number.</returns>
    private static long? Number(IQueryCollection query, string name) =>
        long.TryParse(query[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
            ? value
            : null;

    /// <summary>
    /// Reads the target of a flight out of the query of a request: the kind and the places it names,
    /// each parameter with the prefix of the target.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="prefix">The prefix of the parameters: <c>target</c> or <c>from</c>.</param>
    /// <returns>The target, or <see langword="null"/> when the query names no kind.</returns>
    private static FlightTarget? ReadTarget(IQueryCollection query, string prefix)
    {
        string kind = query[prefix + "_kind"].ToString().Trim().ToLowerInvariant();

        if (kind.Length == 0)
        {
            return null;
        }

        return new FlightTarget(
            kind,
            (int)(Number(query, prefix + "_sector_id") ?? 0),
            Number(query, prefix + "_system_id") ?? 0,
            Number(query, prefix + "_object_id") ?? 0,
            Decimal(query, prefix + "_x"),
            Decimal(query, prefix + "_y"),
            Decimal(query, prefix + "_z"));
    }

    private static decimal Decimal(IQueryCollection query, string name) =>
        decimal.TryParse(query[name], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : 0m;

    private static long RequiredBody(IQueryCollection query) =>
        Body(query) ?? throw new GameException(FleetErrorCodes.PlanetUnknown, "The body is missing.");

    /// <summary>
    /// Reads the body whose projects are wanted from the query of a request.
    /// </summary>
    private static long? Body(IQueryCollection query) =>
        long.TryParse(query["planet_id"], NumberStyles.Integer, CultureInfo.InvariantCulture, out long planetId)
            ? planetId
            : null;

    /// <summary>
    /// Reads a flag from the query of a request.
    /// </summary>
    private static bool ReadFlag(IQueryCollection query, string name) =>
        bool.TryParse(query[name], out bool value) && value;

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");
}

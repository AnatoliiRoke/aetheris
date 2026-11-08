using System.Globalization;
using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Fleet.Api;
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

        return endpoints;
    }

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

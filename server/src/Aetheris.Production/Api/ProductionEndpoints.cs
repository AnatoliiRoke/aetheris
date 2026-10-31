using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Production.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Production.Api;

/// <summary>
/// The reads of the production for a player (task 00013, section 3.1).
/// </summary>
public static class ProductionEndpoints
{
    /// <summary>
    /// The route prefix.
    /// </summary>
    public const string Prefix = "/api/v1/production";

    /// <summary>
    /// Maps the reads. They need a signed-in player and count against the command rate limit.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder production = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        // The factories of a body, so the client can pick one.
        production.MapGet("/factories", async (
            [FromQuery(Name = "planet_id")] long planetId,
            ClaimsPrincipal user,
            ProductionQueueService queue,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<ProductionFactorySummaryDto> factories = await queue
                .ReadFactoriesAsync(PlayerId(user), planetId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(factories, ProductionJson.Options);
        });

        // One factory with its recipes, its queue and the storage of the parts.
        production.MapGet("/factories/{planetBuildingId:long}", async (
            [FromQuery(Name = "planet_id")] long planetId,
            long planetBuildingId,
            ClaimsPrincipal user,
            ProductionQueueService queue,
            CancellationToken cancellationToken) =>
        {
            ProductionFactoryDto factory = await queue
                .ReadFactoryAsync(PlayerId(user), planetId, planetBuildingId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(factory, ProductionJson.Options);
        });

        // Every batch of every factory of a body, so one screen shows the whole production.
        production.MapGet("/orders", async (
            [FromQuery(Name = "planet_id")] long planetId,
            ClaimsPrincipal user,
            ProductionQueueService queue,
            CancellationToken cancellationToken) =>
        {
            ProductionActiveOrdersDto orders = await queue
                .ReadActiveOrdersAsync(PlayerId(user), planetId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(orders, ProductionJson.Options);
        });

        // The storage of the parts of a body.
        production.MapGet("/stock", async (
            [FromQuery(Name = "planet_id")] long planetId,
            ClaimsPrincipal user,
            ProductionQueueService queue,
            CancellationToken cancellationToken) =>
        {
            ProductionStockDto stock = await queue
                .ReadStockAsync(PlayerId(user), planetId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(stock, ProductionJson.Options);
        });

        return endpoints;
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");
}

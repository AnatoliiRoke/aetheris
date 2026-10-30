using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Processes.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Processes.Api;

/// <summary>
/// The reads of the processes for a player (task 00014, section 3.2).
/// </summary>
public static class ProcessesEndpoints
{
    /// <summary>
    /// The route prefix.
    /// </summary>
    public const string Prefix = "/api/v1/processes";

    /// <summary>
    /// Maps the reads. They need a signed-in player and count against the command rate limit.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder processes = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        // The buildings of a body that run a process, so the client can pick one.
        processes.MapGet("/buildings", async (
            [FromQuery(Name = "planet_id")] long planetId,
            ClaimsPrincipal user,
            ProcessQueueService queue,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<ProcessBuildingSummaryDto> buildings = await queue
                .ReadBuildingsAsync(PlayerId(user), planetId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(buildings, ProcessesJson.Options);
        });

        // One building with its recipes and its queue.
        processes.MapGet("/buildings/{planetBuildingId:long}", async (
            [FromQuery(Name = "planet_id")] long planetId,
            long planetBuildingId,
            ClaimsPrincipal user,
            ProcessQueueService queue,
            CancellationToken cancellationToken) =>
        {
            ProcessBuildingDto building = await queue
                .ReadBuildingAsync(PlayerId(user), planetId, planetBuildingId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(building, ProcessesJson.Options);
        });

        return endpoints;
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");
}

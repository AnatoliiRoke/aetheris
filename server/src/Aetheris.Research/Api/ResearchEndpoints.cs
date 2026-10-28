using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Research.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Research.Api;

/// <summary>
/// The reads of the tree of the technologies for a player (task 00012, section 3.2).
/// </summary>
public static class ResearchEndpoints
{
    /// <summary>
    /// The route prefix.
    /// </summary>
    public const string Prefix = "/api/v1/research";

    /// <summary>
    /// Maps the reads. They need a signed-in player and count against the command rate limit.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder research = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        // The tree of the technologies with the queue of the researches; the body the research
        // starts from may be named, and then the science centre of that body opens the
        // technologies.
        research.MapGet("/", async (
            long? planetId,
            ClaimsPrincipal user,
            ResearchQueueService queue,
            ResearchTreeBuilder builder,
            CancellationToken cancellationToken) =>
        {
            ResearchTreeSnapshot snapshot = await queue
                .ReadAsync(PlayerId(user), planetId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(builder.Build(snapshot), ResearchJson.Options);
        });

        return endpoints;
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");
}

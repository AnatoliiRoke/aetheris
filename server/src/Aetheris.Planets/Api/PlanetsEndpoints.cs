using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Planets.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Planets.Api;

/// <summary>
/// The reads of a body for its owner (task 00011, section 3.1).
/// </summary>
public static class PlanetsEndpoints
{
    /// <summary>
    /// The route prefix.
    /// </summary>
    public const string Prefix = "/api/v1/planets";

    /// <summary>
    /// Maps the reads. They need a signed-in player and count against the command rate limit.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder planets = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        // The buildings, the queue, the balance, the storage and the energy of a body; every
        // building carries its next level and whether it can be queued now.
        planets.MapGet("/{planetId:long}", async (long planetId, ClaimsPrincipal user, PlanetQueryService queries, CancellationToken cancellationToken) =>
        {
            PlanetOverviewDto? overview = await queries.GetAsync(PlayerId(user), planetId, cancellationToken).ConfigureAwait(false);

            return overview is null ? NotFound() : Results.Json(overview, PlanetsJson.Options);
        });

        // Whether a closed subject is available on a body and why not (decision 3).
        planets.MapGet("/{planetId:long}/requirements/{subjectType}/{subjectId:int}", async (
            long planetId,
            string subjectType,
            int subjectId,
            int? level,
            ClaimsPrincipal user,
            PlanetQueryService queries,
            CancellationToken cancellationToken) =>
        {
            string playerId = PlayerId(user);

            if (await queries.GetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false) is null)
            {
                return NotFound();
            }

            AvailabilityDto availability = await queries
                .CheckAsync(subjectType, subjectId, level ?? 1, planetId, playerId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(availability, PlanetsJson.Options);
        });

        return endpoints;
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");

    private static IResult NotFound() => Results.Json(new { error = GameErrorCodes.NotFound }, statusCode: StatusCodes.Status404NotFound);
}

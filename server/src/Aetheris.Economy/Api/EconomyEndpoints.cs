using System.Security.Claims;
using Aetheris.Core.Security;
using Aetheris.Economy.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Economy.Api;

/// <summary>
/// The reads of the economy for a player (task 00015, section 3.2).
/// </summary>
public static class EconomyEndpoints
{
    /// <summary>
    /// The route prefix.
    /// </summary>
    public const string Prefix = "/api/v1/economy";

    /// <summary>
    /// Maps the reads. They need a signed-in player and count against the command rate limit.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder economy = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        // The populations of the bodies of the player and the balances of the currencies, counted
        // in one pass with the bonuses and the effects read once.
        economy.MapGet("/", async (
            ClaimsPrincipal user,
            EconomyService service,
            CancellationToken cancellationToken) =>
        {
            EconomyOverviewDto overview = await service
                .GetOverviewAsync(PlayerId(user), cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(overview, EconomyJson.Options);
        });

        // The population of one body of the player.
        economy.MapGet("/population", async (
            [FromQuery(Name = "planet_id")] long planetId,
            ClaimsPrincipal user,
            EconomyService service,
            CancellationToken cancellationToken) =>
        {
            PopulationFacts? facts = await service
                .GetPopulationAsync(PlayerId(user), planetId, cancellationToken)
                .ConfigureAwait(false);

            return facts is null ? Results.NotFound() : Results.Json(facts, EconomyJson.Options);
        });

        return endpoints;
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new InvalidOperationException("The request has no player.");
}

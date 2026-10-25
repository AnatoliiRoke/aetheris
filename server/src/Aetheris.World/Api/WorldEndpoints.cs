using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.World.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.World.Api;

/// <summary>
/// A new sector requested by an administrator.
/// </summary>
/// <param name="Seed">The seed, or <see langword="null"/> to derive it from the galaxy seed.</param>
/// <param name="IsNewbie">Whether newcomers start there.</param>
public sealed record AddSectorRequest(
    [property: JsonPropertyName("seed")] long? Seed,
    [property: JsonPropertyName("is_newbie")] bool IsNewbie);

/// <summary>
/// A new generation of an empty sector requested by an administrator.
/// </summary>
/// <param name="Seed">The new seed, or <see langword="null"/> to keep the old one.</param>
public sealed record RegenerateSectorRequest([property: JsonPropertyName("seed")] long? Seed);

/// <summary>
/// The endpoints of the world: the reads of a player and the actions of an administrator.
/// </summary>
public static class WorldEndpoints
{
    /// <summary>
    /// The route prefix of the reads of a player.
    /// </summary>
    public const string Prefix = "/api/v1/world";

    /// <summary>
    /// Maps the reads of a player. They need a signed-in player and count against the command
    /// rate limit of the player (task 00004, section 2.7).
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder MapWorldEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder world = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        world.MapGet("/reference", async (WorldQueryService queries, CancellationToken cancellationToken) =>
            Results.Json(await queries.GetReferenceAsync(cancellationToken).ConfigureAwait(false), WorldJson.Options));

        world.MapGet("/sectors", async (WorldQueryService queries, CancellationToken cancellationToken) =>
            Results.Json(await queries.GetSectorsAsync(cancellationToken).ConfigureAwait(false), WorldJson.Options));

        world.MapGet("/sectors/{sectorId:int}/systems", async (int sectorId, ClaimsPrincipal user, WorldQueryService queries, CancellationToken cancellationToken) =>
            Results.Json(await queries.GetSystemsAsync(PlayerId(user), sectorId, cancellationToken).ConfigureAwait(false), WorldJson.Options));

        world.MapGet("/sectors/{sectorId:int}/free-planets", async (int sectorId, int? objectTypeId, ClaimsPrincipal user, WorldQueryService queries, CancellationToken cancellationToken) =>
            Results.Json(await queries.GetFreePlanetsAsync(PlayerId(user), sectorId, objectTypeId, cancellationToken).ConfigureAwait(false), WorldJson.Options));

        world.MapGet("/systems/{systemId:long}", async (long systemId, ClaimsPrincipal user, WorldQueryService queries, CancellationToken cancellationToken) =>
        {
            SystemContentDto? content = await queries.GetSystemContentAsync(PlayerId(user), systemId, cancellationToken).ConfigureAwait(false);

            return content is null ? Error(StatusCodes.Status404NotFound, GameErrorCodes.NotFound) : Results.Json(content, WorldJson.Options);
        });

        return endpoints;
    }

    /// <summary>
    /// Maps the actions of an administrator on the secured admin group of the host, which
    /// already demands the second factor, applies the admin rate limit and writes the audit
    /// record of every request (task 00004, section 2.5).
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder MapWorldAdminEndpoints(this RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapPost("/world/sectors", AddSectorAsync)
            .WithName("admin.world_sector_added")
            .RequireAuthorization(SecurityPolicies.AdminOwner);

        secured.MapPost("/world/sectors/{sectorId:int}/regenerate", RegenerateSectorAsync)
            .WithName("admin.world_sector_regenerated")
            .RequireAuthorization(SecurityPolicies.AdminOwner);

        secured.MapGet("/world/sectors/{sectorId:int}/stats", ReadStatisticsAsync)
            .WithName("admin.world_sector_stats");

        return secured;
    }

    private static async Task<IResult> AddSectorAsync(
        AddSectorRequest request,
        ClaimsPrincipal principal,
        IWorldModule world,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        try
        {
            SectorSummary sector = await world.AddSectorAsync(request.Seed, request.IsNewbie, AdminId(principal), cancellationToken)
                .ConfigureAwait(false);

            audit.Record(Target(sector.Id), null, sector.Seed.ToString(CultureInfo.InvariantCulture));

            return Results.Json(sector, WorldJson.Options);
        }
        catch (GameException exception)
        {
            return Error(StatusCodes.Status422UnprocessableEntity, exception.Code);
        }
    }

    private static async Task<IResult> RegenerateSectorAsync(
        int sectorId,
        RegenerateSectorRequest request,
        ClaimsPrincipal principal,
        IWorldModule world,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        SectorStatistics? before = await world.GetSectorStatisticsAsync(sectorId, cancellationToken).ConfigureAwait(false);

        if (before is null)
        {
            return Error(StatusCodes.Status404NotFound, GameErrorCodes.NotFound);
        }

        try
        {
            SectorSummary sector = await world.RegenerateSectorAsync(sectorId, request.Seed, AdminId(principal), cancellationToken)
                .ConfigureAwait(false);

            audit.Record(
                Target(sectorId),
                before.Seed.ToString(CultureInfo.InvariantCulture),
                sector.Seed.ToString(CultureInfo.InvariantCulture));

            return Results.Json(sector, WorldJson.Options);
        }
        catch (GameException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Code);
        }
    }

    private static async Task<IResult> ReadStatisticsAsync(
        int sectorId,
        IWorldModule world,
        IAdminAuditRecorder audit,
        CancellationToken cancellationToken)
    {
        SectorStatistics? statistics = await world.GetSectorStatisticsAsync(sectorId, cancellationToken).ConfigureAwait(false);

        audit.Record(Target(sectorId), null, null);

        return statistics is null
            ? Error(StatusCodes.Status404NotFound, GameErrorCodes.NotFound)
            : Results.Json(statistics, WorldJson.Options);
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");

    private static string AdminId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(AdminRoles.AdminIdClaimType) ?? WorldGenerationService.SystemActor;

    private static string Target(int sectorId) => "sector:" + sectorId.ToString(CultureInfo.InvariantCulture);

    private static IResult Error(int statusCode, string code) => Results.Json(new { error = code }, statusCode: statusCode);
}

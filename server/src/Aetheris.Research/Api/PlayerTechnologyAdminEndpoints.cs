using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Research.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Research.Api;

/// <summary>A technology of a body.</summary>
/// <param name="TechnologyId">The technology.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="BranchId">The branch.</param>
/// <param name="BranchNameKey">The localisation key of the branch.</param>
/// <param name="Level">The level of the body, zero when it has none.</param>
/// <param name="MaxLevel">The largest level.</param>
public sealed record AdminTechnologyRow(int TechnologyId, string NameKey, int BranchId, string BranchNameKey, int Level, int MaxLevel);

/// <summary>A record of the research queue of a player.</summary>
/// <param name="Id">The record.</param>
/// <param name="PlanetId">The body the research starts from.</param>
/// <param name="TechnologyId">The technology.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="TargetLevel">The level the record researches.</param>
/// <param name="Position">The place in the queue.</param>
/// <param name="FinishesAt">The end, or <see langword="null"/> while the record waits.</param>
public sealed record AdminResearchQueueRow(int Id, long PlanetId, int TechnologyId, string NameKey, int TargetLevel, int Position, DateTimeOffset? FinishesAt);

/// <summary>The technologies of a body and the research queue of the player.</summary>
/// <param name="Technologies">The technologies.</param>
/// <param name="Queue">The queue of the whole civilization.</param>
public sealed record AdminTechnologyView(IReadOnlyList<AdminTechnologyRow> Technologies, IReadOnlyList<AdminResearchQueueRow> Queue);

/// <summary>An absolute level of a technology.</summary>
/// <param name="TechnologyId">The technology.</param>
/// <param name="Level">The level.</param>
/// <param name="Reason">Why the administrator changed it.</param>
public sealed record AdminTechnologyLevelSet(
    [property: System.Text.Json.Serialization.JsonPropertyName("technology_id")] int TechnologyId,
    [property: System.Text.Json.Serialization.JsonPropertyName("level")] int Level,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>A reason of a cancel.</summary>
/// <param name="Reason">Why the administrator cancelled the record.</param>
public sealed record AdminResearchCancelRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The technologies of a player in the admin panel.
/// </summary>
public static class PlayerTechnologyAdminEndpoints
{
    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapGet("/research/players/{playerId}/planets/{planetId:long}", async (
            string playerId,
            long planetId,
            PlayerTechnologyAdminService service,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"technology:{playerId}:{planetId}", null, null);

            try
            {
                AdminTechnologyView view = await service.ReadAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                return Results.Json(view, ResearchJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_technologies_read");

        secured.MapPut("/research/players/{playerId}/planets/{planetId:long}/technologies", async (
            string playerId,
            long planetId,
            AdminTechnologyLevelSet request,
            PlayerTechnologyAdminService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"technology:{playerId}:{planetId}:{request.TechnologyId}";
            audit.Record(target, null, null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                (long before, long after) = await service
                    .SetLevelAsync(playerId, planetId, request.TechnologyId, request.Level, cancellationToken)
                    .ConfigureAwait(false);

                audit.Record(target, before.ToString(CultureInfo.InvariantCulture), after.ToString(CultureInfo.InvariantCulture));
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { level = after }, ResearchJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_technology_updated");

        secured.MapPost("/research/players/{playerId}/queue/{queueItemId:int}/cancel", async (
            string playerId,
            int queueItemId,
            AdminResearchCancelRequest request,
            ResearchQueueService queue,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"research:{playerId}:{queueItemId}";
            audit.Record(target, queueItemId.ToString(CultureInfo.InvariantCulture), null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                await queue.CancelAsync(playerId, queueItemId, cancellationToken).ConfigureAwait(false);
                audit.Record(target, queueItemId.ToString(CultureInfo.InvariantCulture), "cancelled");
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { cancelled = true }, ResearchJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_research_cancelled");

        return secured;
    }

    private static IResult? ReasonError(string? reason, IAdminQueryLimits limits)
    {
        if (AdminReason.Check(reason, limits) is not { } reasonError)
        {
            return null;
        }

        return Results.Json(
            new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["reason"] = [reasonError] } },
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult Failure(GameException exception) =>
        Results.Json(
            new { error = exception.Code },
            ResearchJson.Options,
            statusCode: exception.Code == GameErrorCodes.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity);
}

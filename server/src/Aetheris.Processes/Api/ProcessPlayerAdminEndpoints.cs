using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Processes.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Processes.Api;

/// <summary>An order of a process.</summary>
/// <param name="Id">The order.</param>
/// <param name="PlanetBuildingId">The building.</param>
/// <param name="ProcessRecipeId">The recipe.</param>
/// <param name="NameKey">The localisation key of the recipe.</param>
/// <param name="Quantity">How many units the order holds.</param>
/// <param name="ProducedQuantity">How many units are already made.</param>
/// <param name="Position">The place in the queue.</param>
/// <param name="FinishesAt">The end, or <see langword="null"/> while the order waits.</param>
public sealed record AdminProcessOrderRow(long Id, long PlanetBuildingId, int ProcessRecipeId, string NameKey, long Quantity, long ProducedQuantity, int Position, DateTimeOffset? FinishesAt);

/// <summary>A reason of a cancel.</summary>
/// <param name="Reason">Why the administrator cancelled the order.</param>
public sealed record AdminProcessCancelRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The process queues of a player in the admin panel.
/// </summary>
public static class ProcessPlayerAdminEndpoints
{
    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        RouteGroupBuilder group = secured.MapGroup("/processes/players/{playerId}/planets/{planetId:long}");

        group.MapGet("/orders", async (
            string playerId,
            long planetId,
            ProcessPlayerAdminService service,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"process:{playerId}:{planetId}", null, null);

            try
            {
                IReadOnlyList<AdminProcessOrderRow> orders = await service.ReadAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                return Results.Json(orders, ProcessesJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_process_read");

        group.MapPost("/orders/{orderId:long}/cancel", async (
            string playerId,
            long planetId,
            long orderId,
            AdminProcessCancelRequest request,
            ProcessPlayerAdminService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"process_order:{playerId}:{planetId}:{orderId}";
            audit.Record(target, orderId.ToString(CultureInfo.InvariantCulture), null);

            if (AdminReason.Check(request.Reason, limits) is { } reasonError)
            {
                return Results.Json(
                    new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["reason"] = [reasonError] } },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await service.CancelAsync(playerId, planetId, orderId, cancellationToken).ConfigureAwait(false);
                audit.Record(target, orderId.ToString(CultureInfo.InvariantCulture), "cancelled");
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { cancelled = true }, ProcessesJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_process_cancelled");

        return secured;
    }

    private static IResult Failure(GameException exception) =>
        Results.Json(
            new { error = exception.Code },
            ProcessesJson.Options,
            statusCode: exception.Code == GameErrorCodes.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity);
}

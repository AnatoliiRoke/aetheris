using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Production.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Production.Api;

/// <summary>A stack of parts on a body.</summary>
/// <param name="Id">The stack.</param>
/// <param name="PartTypeId">The part.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="MarkLevel">The mark of the part.</param>
/// <param name="HasRaceBonus">Whether the parts carry the bonus of the race.</param>
/// <param name="ConditionPercent">The condition in per cent.</param>
/// <param name="Quantity">How many parts the stack holds.</param>
public sealed record AdminPartStackRow(long Id, int PartTypeId, string NameKey, int MarkLevel, bool HasRaceBonus, int ConditionPercent, long Quantity);

/// <summary>A part the administrator may grant.</summary>
/// <param name="Id">The part.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="MarkLevel">The mark of the part.</param>
/// <param name="IsArchived">Whether the part is archived.</param>
public sealed record AdminPartTypeRow(int Id, string NameKey, int MarkLevel, bool IsArchived);

/// <summary>An order of a factory.</summary>
/// <param name="Id">The order.</param>
/// <param name="PlanetBuildingId">The factory.</param>
/// <param name="RecipeId">The recipe.</param>
/// <param name="NameKey">The localisation key of the recipe.</param>
/// <param name="Quantity">How many units were ordered.</param>
/// <param name="ProducedQuantity">How many units are already made.</param>
/// <param name="Position">The place in the queue.</param>
/// <param name="FinishesAt">The end, or <see langword="null"/> while the order waits.</param>
public sealed record AdminProductionOrderRow(long Id, long PlanetBuildingId, int RecipeId, string NameKey, long Quantity, long ProducedQuantity, int Position, DateTimeOffset? FinishesAt);

/// <summary>The parts and the factory orders of a body.</summary>
/// <param name="Stacks">The stacks.</param>
/// <param name="PartTypes">The parts of the catalog.</param>
/// <param name="Orders">The orders.</param>
public sealed record AdminProductionView(IReadOnlyList<AdminPartStackRow> Stacks, IReadOnlyList<AdminPartTypeRow> PartTypes, IReadOnlyList<AdminProductionOrderRow> Orders);

/// <summary>An absolute quantity of a stack.</summary>
/// <param name="Quantity">The quantity.</param>
/// <param name="Reason">Why the administrator changed it.</param>
public sealed record AdminPartQuantitySet(
    [property: System.Text.Json.Serialization.JsonPropertyName("quantity")] long Quantity,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>A grant of parts into a stack.</summary>
/// <param name="PartTypeId">The part.</param>
/// <param name="Quantity">How many parts to add.</param>
/// <param name="HasRaceBonus">Whether the parts carry the bonus of the race.</param>
/// <param name="ConditionPercent">The condition in per cent, or <see langword="null"/> for a new part.</param>
/// <param name="Reason">Why the administrator granted them.</param>
public sealed record AdminPartGrant(
    [property: System.Text.Json.Serialization.JsonPropertyName("part_type_id")] int PartTypeId,
    [property: System.Text.Json.Serialization.JsonPropertyName("quantity")] long Quantity,
    [property: System.Text.Json.Serialization.JsonPropertyName("has_race_bonus")] bool HasRaceBonus,
    [property: System.Text.Json.Serialization.JsonPropertyName("condition_percent")] int? ConditionPercent,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>A reason of a cancel.</summary>
/// <param name="Reason">Why the administrator cancelled the order.</param>
public sealed record AdminProductionCancelRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The parts and the factory queues of a player in the admin panel.
/// </summary>
public static class ProductionPlayerAdminEndpoints
{
    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        RouteGroupBuilder group = secured.MapGroup("/production/players/{playerId}/planets/{planetId:long}");

        group.MapGet("/parts", async (
            string playerId,
            long planetId,
            ProductionPlayerAdminService service,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"parts:{playerId}:{planetId}", null, null);

            try
            {
                AdminProductionView view = await service.ReadAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                return Results.Json(view, ProductionJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_parts_read");

        group.MapGet("/orders", async (
            string playerId,
            long planetId,
            ProductionPlayerAdminService service,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"production:{playerId}:{planetId}", null, null);

            try
            {
                AdminProductionView view = await service.ReadAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

                return Results.Json(view, ProductionJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_production_read");

        group.MapPut("/parts/{stackId:long}", async (
            string playerId,
            long planetId,
            long stackId,
            AdminPartQuantitySet request,
            ProductionPlayerAdminService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"part:{playerId}:{planetId}:{stackId}";
            audit.Record(target, null, null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                (long before, long after) = await service
                    .SetQuantityAsync(playerId, planetId, stackId, request.Quantity, cancellationToken)
                    .ConfigureAwait(false);

                audit.Record(target, before.ToString(CultureInfo.InvariantCulture), after.ToString(CultureInfo.InvariantCulture));
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { quantity = after }, ProductionJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_part_updated");

        group.MapPost("/parts", async (
            string playerId,
            long planetId,
            AdminPartGrant request,
            ProductionPlayerAdminService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"part:{playerId}:{planetId}:{request.PartTypeId}";
            audit.Record(target, null, null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                await service.GrantAsync(
                    playerId,
                    planetId,
                    request.PartTypeId,
                    request.Quantity,
                    request.HasRaceBonus,
                    request.ConditionPercent,
                    cancellationToken).ConfigureAwait(false);

                audit.Record(target, null, request.Quantity.ToString(CultureInfo.InvariantCulture));
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { granted = true }, ProductionJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_part_granted");

        group.MapPost("/orders/{orderId:long}/cancel", async (
            string playerId,
            long planetId,
            long orderId,
            AdminProductionCancelRequest request,
            ProductionPlayerAdminService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"production_order:{playerId}:{planetId}:{orderId}";
            audit.Record(target, orderId.ToString(CultureInfo.InvariantCulture), null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                await service.CancelOrderAsync(playerId, planetId, orderId, cancellationToken).ConfigureAwait(false);
                audit.Record(target, orderId.ToString(CultureInfo.InvariantCulture), "cancelled");
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { cancelled = true }, ProductionJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_production_cancelled");

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
            ProductionJson.Options,
            statusCode: exception.Code == GameErrorCodes.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity);
}

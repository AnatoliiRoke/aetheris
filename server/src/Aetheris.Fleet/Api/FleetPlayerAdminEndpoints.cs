using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Fleet.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Fleet.Api;

/// <summary>A new name of a ship project.</summary>
/// <param name="Name">The name.</param>
/// <param name="Reason">Why the administrator renamed it.</param>
public sealed record AdminDesignRename(
    [property: System.Text.Json.Serialization.JsonPropertyName("name")] string? Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>A reason of an archive.</summary>
/// <param name="Reason">Why the administrator archived the project.</param>
public sealed record AdminDesignArchiveRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>An absolute count of the finished ships of one project.</summary>
/// <param name="Quantity">The count.</param>
/// <param name="Reason">Why the administrator changed it.</param>
public sealed record AdminShipQuantitySet(
    [property: System.Text.Json.Serialization.JsonPropertyName("quantity")] long Quantity,
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The ship projects of a player in the admin panel. The slots stay as the constructor saved them.
/// </summary>
public static class FleetPlayerAdminEndpoints
{
    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        RouteGroupBuilder group = secured.MapGroup("/fleet/players/{playerId}/designs");

        group.MapGet(string.Empty, async (
            string playerId,
            ShipDesignService designs,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"designs:{playerId}", null, null);
            IReadOnlyList<ShipDesignDto> rows = await designs.ReadAsync(playerId, null, includeArchived: true, cancellationToken).ConfigureAwait(false);

            return Results.Json(rows, FleetJson.Options);
        })
            .WithName("admin.player_designs_read");

        group.MapPut("/{designId:long}", async (
            string playerId,
            long designId,
            AdminDesignRename request,
            ShipDesignService designs,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"design:{playerId}:{designId}";
            audit.Record(target, null, null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                ShipDesignDto design = await designs.RenameAsync(playerId, designId, request.Name ?? string.Empty, cancellationToken).ConfigureAwait(false);
                audit.Record(target, null, design.Name);
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(design, FleetJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_design_updated");

        group.MapPost("/{designId:long}/archive", async (
            string playerId,
            long designId,
            AdminDesignArchiveRequest request,
            ShipDesignService designs,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"design:{playerId}:{designId}";
            audit.Record(target, "active", null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                await designs.ArchiveAsync(playerId, designId, cancellationToken).ConfigureAwait(false);
                audit.Record(target, "active", "archived");
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { archived = true }, FleetJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_design_archived");

        RouteGroupBuilder ships = secured.MapGroup("/fleet/players/{playerId}/ships");

        ships.MapGet(string.Empty, async (
            string playerId,
            PlayerShipAdminService service,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"ships:{playerId}", null, null);
            IReadOnlyList<PlayerShipQuantityRow> rows = await service.ReadAsync(playerId, cancellationToken).ConfigureAwait(false);

            return Results.Json(rows, FleetJson.Options);
        })
            .WithName("admin.player_ships_read");

        ships.MapPut("/{designId:long}", async (
            string playerId,
            long designId,
            AdminShipQuantitySet request,
            PlayerShipAdminService service,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"ships:{playerId}:{designId}";
            audit.Record(target, null, null);

            if (ReasonError(request.Reason, limits) is { } refused)
            {
                return refused;
            }

            try
            {
                (long before, long after) = await service
                    .SetQuantityAsync(playerId, designId, request.Quantity, cancellationToken)
                    .ConfigureAwait(false);

                audit.Record(target, before.ToString(CultureInfo.InvariantCulture), after.ToString(CultureInfo.InvariantCulture));
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { quantity = after }, FleetJson.Options);
            }
            catch (GameException exception)
            {
                return Failure(exception);
            }
        })
            .WithName("admin.player_ship_quantity_set");

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
            FleetJson.Options,
            statusCode: exception.Code == GameErrorCodes.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity);
}

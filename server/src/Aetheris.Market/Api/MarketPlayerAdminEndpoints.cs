using System.Globalization;
using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Market.Services;
using Aetheris.Production.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Market.Api;

/// <summary>A name the market screen uses for a resource or a part.</summary>
/// <param name="Id">The resource or the part.</param>
/// <param name="NameKey">The localisation key of the name.</param>
public sealed record AdminMarketName(int Id, string NameKey);

/// <summary>The lots, the deals and the deliveries of a player.</summary>
/// <param name="Lots">The open lots.</param>
/// <param name="Deals">One page of the deals.</param>
/// <param name="Deliveries">The deliveries.</param>
/// <param name="Resources">The names of the resources.</param>
/// <param name="Parts">The names of the parts.</param>
public sealed record AdminMarketView(
    IReadOnlyList<LotDto> Lots,
    DealsDto Deals,
    DeliveriesDto Deliveries,
    IReadOnlyList<AdminMarketName> Resources,
    IReadOnlyList<AdminMarketName> Parts);

/// <summary>A reason of a cancel.</summary>
/// <param name="Reason">Why the administrator cancelled the lot.</param>
public sealed record AdminLotCancelRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("reason")] string? Reason);

/// <summary>
/// The market of a player in the admin panel. The lots can be cancelled. The deals and the
/// deliveries are a record of what already happened.
/// </summary>
public static class MarketPlayerAdminEndpoints
{
    /// <summary>
    /// Maps the endpoints.
    /// </summary>
    /// <param name="secured">The secured admin group.</param>
    /// <returns>The same group.</returns>
    public static RouteGroupBuilder Map(RouteGroupBuilder secured)
    {
        ArgumentNullException.ThrowIfNull(secured);

        secured.MapGet("/market/players/{playerId}", async (
            string playerId,
            int? offset,
            MarketReadService reads,
            PartStockService parts,
            IWorldModule world,
            IAdminAuditRecorder audit,
            CancellationToken cancellationToken) =>
        {
            audit.Record($"market:{playerId}", null, null);

            IReadOnlyList<LotDto> lots = await reads.MyLotsAsync(playerId, cancellationToken).ConfigureAwait(false);
            DealsDto deals = await reads.DealsAsync(playerId, offset ?? 0, cancellationToken).ConfigureAwait(false);
            DeliveriesDto deliveries = await reads.DeliveriesAsync(playerId, cancellationToken).ConfigureAwait(false);
            ResourceCatalog catalog = await world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyDictionary<int, PartFacts> partFacts = await parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

            AdminMarketView view = new(
                lots,
                deals,
                deliveries,
                [.. catalog.Resources.Select(resource => new AdminMarketName(resource.Id, resource.NameKey))],
                [.. partFacts.Values.Select(part => new AdminMarketName(part.PartTypeId, part.NameKey))]);

            return Results.Json(view, MarketJson.Options);
        })
            .WithName("admin.player_market_read");

        secured.MapPost("/market/players/{playerId}/lots/{lotId:long}/cancel", async (
            string playerId,
            long lotId,
            AdminLotCancelRequest request,
            MarketTradeService trade,
            IAdminAuditRecorder audit,
            IAdminQueryLimits limits,
            CancellationToken cancellationToken) =>
        {
            string target = $"lot:{playerId}:{lotId}";
            audit.Record(target, lotId.ToString(CultureInfo.InvariantCulture), null);

            if (AdminReason.Check(request.Reason, limits) is { } reasonError)
            {
                return Results.Json(
                    new { error = GameErrorCodes.ValidationFailed, details = new Dictionary<string, string[]> { ["reason"] = [reasonError] } },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await trade.CancelLotAsync(playerId, lotId, cancellationToken).ConfigureAwait(false);
                audit.Record(target, lotId.ToString(CultureInfo.InvariantCulture), "cancelled");
                audit.RecordReason(request.Reason!.Trim());

                return Results.Json(new { cancelled = true }, MarketJson.Options);
            }
            catch (GameException exception)
            {
                return Results.Json(
                    new { error = exception.Code },
                    MarketJson.Options,
                    statusCode: exception.Code == GameErrorCodes.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status422UnprocessableEntity);
            }
        })
            .WithName("admin.player_lot_cancelled");

        return secured;
    }
}

using System.Globalization;
using System.Security.Claims;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Market.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Aetheris.Market.Services;

/// <summary>
/// The reads of the market for a player (task 00024, section 4.1).
/// </summary>
public static class MarketEndpoints
{
    /// <summary>
    /// The route prefix of the reads of the module.
    /// </summary>
    public const string Prefix = "/api/v1/market";

    /// <summary>
    /// Maps the reads of the module. They need a signed-in player and change nothing.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder market = endpoints.MapGroup(Prefix)
            .RequireAuthorization(SecurityPolicies.Player)
            .RequireRateLimiting(RateLimitGroups.Command);

        market.MapGet("/lots", async (
            HttpContext context,
            ClaimsPrincipal user,
            MarketReadService reads,
            CancellationToken cancellationToken) =>
            Results.Json(
                await reads.LotsAsync(PlayerId(user), ReadFilter(context.Request.Query), cancellationToken).ConfigureAwait(false),
                MarketJson.Options));

        market.MapGet("/lot-groups", async (
            HttpContext context,
            ClaimsPrincipal user,
            MarketReadService reads,
            CancellationToken cancellationToken) =>
            Results.Json(
                await reads.LotGroupsAsync(PlayerId(user), ReadFilter(context.Request.Query), cancellationToken).ConfigureAwait(false),
                MarketJson.Options));

        market.MapGet("/overview", async (ClaimsPrincipal user, MarketReadService reads, CancellationToken cancellationToken) =>
            Results.Json(await reads.OverviewAsync(PlayerId(user), cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/rate-history", async (MarketReadService reads, CancellationToken cancellationToken) =>
            Results.Json(await reads.RateHistoryAsync(cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/price-limit", async (
            [FromQuery(Name = "resource_type_id")] int? resourceTypeId,
            [FromQuery(Name = "part_type_id")] int? partTypeId,
            MarketReadService reads,
            CancellationToken cancellationToken) =>
            Results.Json(await reads.PriceLimitAsync(resourceTypeId, partTypeId, cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/my-lots", async (ClaimsPrincipal user, MarketReadService reads, CancellationToken cancellationToken) =>
            Results.Json(await reads.MyLotsAsync(PlayerId(user), cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/deliveries", async (ClaimsPrincipal user, MarketReadService reads, CancellationToken cancellationToken) =>
            Results.Json(await reads.DeliveriesAsync(PlayerId(user), cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/deals", async ([FromQuery(Name = "offset")] int? offset, ClaimsPrincipal user, MarketReadService reads, CancellationToken cancellationToken) =>
            Results.Json(await reads.DealsAsync(PlayerId(user), offset ?? 0, cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/rate", async (MarketReadService reads, CancellationToken cancellationToken) =>
            Results.Json(await reads.RateAsync(cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/quote", async (
            [FromQuery(Name = "lot_id")] long lotId,
            [FromQuery(Name = "planet_id")] long planetId,
            ClaimsPrincipal user,
            MarketReadService reads,
            CancellationToken cancellationToken) =>
            Results.Json(await reads.QuoteAsync(PlayerId(user), lotId, planetId, cancellationToken).ConfigureAwait(false), MarketJson.Options));

        market.MapGet("/limits", async (ClaimsPrincipal user, MarketTradeService trade, CancellationToken cancellationToken) =>
            Results.Json(await trade.LimitAsync(PlayerId(user), cancellationToken).ConfigureAwait(false), MarketJson.Options));

        return endpoints;
    }

    /// <summary>
    /// Reads what a player looks for from the query of a request: the lists are separated by commas.
    /// </summary>
    private static LotFilter ReadFilter(IQueryCollection query)
    {
        static List<int>? Ids(string? text) =>
            text is null ? null : [.. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out int id) ? id : 0).Where(id => id > 0)];

        static long? Number(string? text) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value) ? value : null;

        return new LotFilter(
            MarketLevelId: (int?)Number(query["market_level_id"]),
            ResourceTypeIds: Ids(query["resource_type_ids"]),
            PartTypeIds: Ids(query["part_type_ids"]),
            SellerPlayerId: query["seller_player_id"],
            MinPrice: Number(query["min_price"]),
            MaxPrice: Number(query["max_price"]),
            MaxSum: Number(query["max_sum"]),
            Offset: (int)(Number(query["offset"]) ?? 0),
            Limit: (int)(Number(query["limit"]) ?? 0),
            PlanetId: Number(query["planet_id"]),
            Sort: string.IsNullOrEmpty(query["sort"]) ? LotSorts.Created : query["sort"].ToString(),
            Descending: !string.Equals(query["direction"], "asc", StringComparison.Ordinal),
            ItemOrder: ((string?)query["item_order"])?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string PlayerId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The request has no player.");
}

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
    /// Maps the reads of the module.
    /// </summary>
    /// <param name="endpoints">The endpoint builder.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// The reads of the market come with the game part of task 00024: the lots, the deals, the
    /// deliveries and the exchange are not in the game yet, so the module maps no endpoint of a
    /// player at the moment.
    /// </remarks>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints;
    }
}

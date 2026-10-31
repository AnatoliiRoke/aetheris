using Aetheris.Market.Api;

namespace Aetheris.Market.Services;

/// <summary>
/// The event types the market sends to a player (documentation/tech/04, section 5).
/// </summary>
public static class MarketEventTypes
{
    /// <summary>The lot of the seller is bought.</summary>
    public const string Sold = "market_sold";

    /// <summary>The goods of the buyer have arrived.</summary>
    public const string Delivered = "market_delivered";
}

/// <summary>
/// The events of the market for a player: a lot of the player was bought, or the goods the player
/// bought arrived (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// The server has no channel of the events of one player yet, so this is the place a channel
/// plugs into: until then <see cref="NullMarketEvents"/> takes the events and does nothing.
/// </remarks>
public interface IMarketEvents
{
    /// <summary>
    /// Tells the seller that a lot was bought.
    /// </summary>
    /// <param name="sellerPlayerId">The seller.</param>
    /// <param name="deal">The deal.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event is handed over.</returns>
    Task SoldAsync(string sellerPlayerId, DealDto deal, CancellationToken cancellationToken);

    /// <summary>
    /// Tells the buyer that the goods have arrived.
    /// </summary>
    /// <param name="buyerPlayerId">The buyer.</param>
    /// <param name="delivery">The delivery.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event is handed over.</returns>
    Task DeliveredAsync(string buyerPlayerId, DeliveryDto delivery, CancellationToken cancellationToken);
}

/// <summary>
/// The events of the market while the server has no channel of the events of one player: they
/// are taken and dropped.
/// </summary>
public sealed class NullMarketEvents : IMarketEvents
{
    /// <inheritdoc />
    public Task SoldAsync(string sellerPlayerId, DealDto deal, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task DeliveredAsync(string buyerPlayerId, DeliveryDto delivery, CancellationToken cancellationToken) => Task.CompletedTask;
}

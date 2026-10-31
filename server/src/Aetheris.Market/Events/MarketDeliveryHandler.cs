using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Market.Services;

namespace Aetheris.Market.Events;

/// <summary>
/// Lands a delivery of the market when its moment comes (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// The handler runs in the transaction of the event, so the goods and the state of the delivery
/// are committed together, and a delivery that has landed lands no more.
/// </remarks>
public sealed class MarketDeliveryHandler : IScheduledEventHandler
{
    private readonly MarketTradeService _trade;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDeliveryHandler"/> class.
    /// </summary>
    /// <param name="trade">The commands of the market.</param>
    public MarketDeliveryHandler(MarketTradeService trade)
    {
        ArgumentNullException.ThrowIfNull(trade);

        _trade = trade;
    }

    /// <inheritdoc />
    public string EventType => MarketTradeService.DeliveryEventType;

    /// <inheritdoc />
    public async Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledEvent);

        MarketTradeService.DeliveryPayload payload =
            JsonSerializer.Deserialize<MarketTradeService.DeliveryPayload>(scheduledEvent.Payload)
            ?? throw new InvalidOperationException($"The event {scheduledEvent.Id} has no body.");

        await _trade.DeliverAsync(payload.DeliveryId, cancellationToken).ConfigureAwait(false);
    }
}

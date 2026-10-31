using Aetheris.Market.Api;
using Aetheris.Market.Data.Entities;

namespace Aetheris.Market.Services;

/// <summary>
/// Turns the rows of the market into what the API sends.
/// </summary>
public static class MarketMapper
{
    /// <summary>Maps a lot.</summary>
    /// <param name="lot">The row.</param>
    /// <returns>The DTO.</returns>
    public static LotDto ToDto(MarketLotEntity lot)
    {
        ArgumentNullException.ThrowIfNull(lot);

        return new LotDto(
            lot.Id,
            lot.SellerPlayerId,
            lot.SellerPlanetId,
            lot.MarketLevelId,
            lot.ResourceTypeId,
            lot.PartTypeId,
            lot.Quantity,
            lot.PricePerUnit,
            lot.Quantity * lot.PricePerUnit,
            lot.State,
            lot.CreatedAt,
            lot.PartMarkLevel,
            lot.PartHasRaceBonus,
            lot.PartConditionPercent);
    }

    /// <summary>Maps a deal.</summary>
    /// <param name="deal">The row.</param>
    /// <returns>The DTO.</returns>
    public static DealDto ToDto(MarketDealEntity deal)
    {
        ArgumentNullException.ThrowIfNull(deal);

        return new DealDto(
            deal.Id,
            deal.LotId,
            deal.SellerPlayerId,
            deal.BuyerPlayerId,
            deal.ResourceTypeId,
            deal.PartTypeId,
            deal.Quantity,
            deal.PricePerUnit,
            deal.Sum,
            deal.Commission,
            deal.CreatedAt);
    }

    /// <summary>Maps a delivery.</summary>
    /// <param name="delivery">The row.</param>
    /// <returns>The DTO.</returns>
    public static DeliveryDto ToDto(MarketDeliveryEntity delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return new DeliveryDto(
            delivery.Id,
            delivery.DealId,
            delivery.BuyerPlayerId,
            delivery.DestinationPlanetId,
            delivery.ResourceTypeId,
            delivery.PartTypeId,
            delivery.Quantity,
            delivery.EnergyRodAmount,
            delivery.ArrivesAt,
            delivery.State);
    }

    /// <summary>Maps an exchange.</summary>
    /// <param name="exchange">The row.</param>
    /// <returns>The DTO.</returns>
    public static ExchangeDto ToDto(MarketExchangeEntity exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);

        return new ExchangeDto(
            exchange.Id,
            exchange.FromResourceTypeId,
            exchange.ToResourceTypeId,
            exchange.FromAmount,
            exchange.ToAmount,
            exchange.Rate,
            exchange.CommissionPercent,
            exchange.CreatedAt);
    }
}

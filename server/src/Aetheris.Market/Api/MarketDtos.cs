namespace Aetheris.Market.Api;

/// <summary>A lot of the market as a player sees it.</summary>
/// <param name="Id">The lot.</param>
/// <param name="SellerPlayerId">The seller.</param>
/// <param name="SellerPlanetId">The body the goods wait on.</param>
/// <param name="MarketLevelId">The level of the market.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units the lot holds.</param>
/// <param name="PricePerUnit">The price of a unit in the trade currency.</param>
/// <param name="TotalPrice">The price of the whole lot.</param>
/// <param name="State">The state of the lot.</param>
/// <param name="CreatedAt">When the lot was put up.</param>
/// <param name="PartMarkLevel">The mark of the parts of the lot.</param>
/// <param name="PartHasRaceBonus">Whether the parts carry the bonus of the race.</param>
/// <param name="PartConditionPercent">The condition of the parts of the lot.</param>
public sealed record LotDto(
    long Id,
    string SellerPlayerId,
    long SellerPlanetId,
    int MarketLevelId,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long PricePerUnit,
    long TotalPrice,
    string State,
    DateTimeOffset CreatedAt,
    int? PartMarkLevel,
    bool? PartHasRaceBonus,
    int? PartConditionPercent);

/// <summary>A deal of the market.</summary>
/// <param name="Id">The deal.</param>
/// <param name="LotId">The lot.</param>
/// <param name="SellerPlayerId">The seller.</param>
/// <param name="BuyerPlayerId">The buyer.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="PricePerUnit">The price of a unit.</param>
/// <param name="Sum">The sum of the deal.</param>
/// <param name="Commission">The commission the seller paid.</param>
/// <param name="CreatedAt">When the deal happened.</param>
public sealed record DealDto(
    long Id,
    long LotId,
    string SellerPlayerId,
    string BuyerPlayerId,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long PricePerUnit,
    long Sum,
    long Commission,
    DateTimeOffset CreatedAt);

/// <summary>A delivery of the market.</summary>
/// <param name="Id">The delivery.</param>
/// <param name="DealId">The deal.</param>
/// <param name="BuyerPlayerId">The buyer.</param>
/// <param name="DestinationPlanetId">The body the goods arrive at.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="EnergyRodAmount">The delivery fee the buyer paid.</param>
/// <param name="ArrivesAt">When the goods arrive.</param>
/// <param name="State">The state of the delivery.</param>
public sealed record DeliveryDto(
    long Id,
    long DealId,
    string BuyerPlayerId,
    long DestinationPlanetId,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long EnergyRodAmount,
    DateTimeOffset ArrivesAt,
    string State);

/// <summary>What a purchase would cost, without doing it.</summary>
/// <param name="Sum">The price of the lot.</param>
/// <param name="Commission">The commission the seller pays out of the sum.</param>
/// <param name="EnergyResourceTypeId">The resource of the delivery fee.</param>
/// <param name="EnergyRodAmount">The delivery fee.</param>
/// <param name="DeliverySeconds">The time of the delivery in seconds.</param>
public sealed record QuoteDto(
    long Sum,
    long Commission,
    int EnergyResourceTypeId,
    long EnergyRodAmount,
    long DeliverySeconds);

/// <summary>The rate of the exchange now.</summary>
/// <param name="Rate">Credits for one unit of the trade currency.</param>
/// <param name="CommissionPercent">The commission of the exchange in per cent.</param>
/// <param name="CreditsResourceTypeId">The credits.</param>
/// <param name="TradeResourceTypeId">The trade currency.</param>
public sealed record RateDto(decimal Rate, decimal CommissionPercent, int CreditsResourceTypeId, int TradeResourceTypeId);

/// <summary>An exchange of the credits and the trade currency.</summary>
/// <param name="Id">The exchange.</param>
/// <param name="FromResourceTypeId">The currency given.</param>
/// <param name="ToResourceTypeId">The currency got.</param>
/// <param name="FromAmount">How much was given.</param>
/// <param name="ToAmount">How much was got.</param>
/// <param name="Rate">The rate of the moment.</param>
/// <param name="CommissionPercent">The commission in per cent.</param>
/// <param name="CreatedAt">When the exchange happened.</param>
public sealed record ExchangeDto(
    long Id,
    int FromResourceTypeId,
    int ToResourceTypeId,
    long FromAmount,
    long ToAmount,
    decimal Rate,
    decimal CommissionPercent,
    DateTimeOffset CreatedAt);

/// <summary>How many lots a civilization has and may have.</summary>
/// <param name="Used">The lots on the market.</param>
/// <param name="Limit">The lots the trade centres allow.</param>
public sealed record LotLimitDto(int Used, int Limit);

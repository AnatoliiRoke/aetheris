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

/// <summary>A player as the other players see them: the nickname and the race.</summary>
/// <param name="PlayerId">The player.</param>
/// <param name="Nickname">The nickname.</param>
/// <param name="RaceNameKey">The localisation key of the name of the race, or <see langword="null"/> when the player has no civilization.</param>
/// <param name="RaceColorKey">The colour key of the race, or <see langword="null"/>.</param>
public sealed record PlayerCardDto(string PlayerId, string Nickname, string? RaceNameKey, string? RaceColorKey);

/// <summary>A lot in the list of the lots a player may buy.</summary>
/// <param name="Id">The lot.</param>
/// <param name="Seller">The seller.</param>
/// <param name="SellerPlanetId">The body the goods wait on.</param>
/// <param name="MarketLevelId">The level of the market.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units the lot holds.</param>
/// <param name="PricePerUnit">The price of a unit in the trade currency.</param>
/// <param name="TotalPrice">The price of the whole lot.</param>
/// <param name="CreatedAt">When the lot was put up.</param>
/// <param name="PartConditionPercent">The condition of the parts of the lot.</param>
/// <param name="DeliverySeconds">The time of the delivery to the chosen body, or <see langword="null"/> without one.</param>
/// <param name="EnergyRodAmount">The delivery fee for the chosen body, or <see langword="null"/> without one.</param>
/// <param name="UnavailableReason">The code of the reason the player cannot buy the lot now, or <see langword="null"/>.</param>
public sealed record LotListItemDto(
    long Id,
    PlayerCardDto Seller,
    long SellerPlanetId,
    int MarketLevelId,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long PricePerUnit,
    long TotalPrice,
    DateTimeOffset CreatedAt,
    int? PartConditionPercent,
    long? DeliverySeconds,
    long? EnergyRodAmount,
    string? UnavailableReason);

/// <summary>The lots a player may buy with what the player needs to read them.</summary>
/// <param name="Items">The lots.</param>
/// <param name="EnergyRodBalance">The energy rods on the chosen body, or <see langword="null"/> without one.</param>
/// <param name="DestinationHasPortal">Whether the chosen body has a portal, or <see langword="null"/> without one.</param>
public sealed record LotsDto(IReadOnlyList<LotListItemDto> Items, long? EnergyRodBalance, bool? DestinationHasPortal);

/// <summary>A delivery in the list of the deliveries of a player.</summary>
/// <param name="Id">The delivery.</param>
/// <param name="Counterparty">The other side of the deal.</param>
/// <param name="SourcePlanetId">The body the goods left.</param>
/// <param name="DestinationPlanetId">The body the goods arrive at.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="EnergyRodAmount">The delivery fee the buyer paid.</param>
/// <param name="StartedAt">When the deal was made and the goods left.</param>
/// <param name="ArrivesAt">When the goods arrive.</param>
/// <param name="State">The state of the delivery.</param>
public sealed record DeliveryItemDto(
    long Id,
    PlayerCardDto Counterparty,
    long SourcePlanetId,
    long DestinationPlanetId,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long EnergyRodAmount,
    DateTimeOffset StartedAt,
    DateTimeOffset ArrivesAt,
    string State);

/// <summary>The deliveries of a player.</summary>
/// <param name="Incoming">The goods that travel to the player or have just arrived.</param>
/// <param name="Outgoing">The goods of the sold lots of the player.</param>
public sealed record DeliveriesDto(IReadOnlyList<DeliveryItemDto> Incoming, IReadOnlyList<DeliveryItemDto> Outgoing);

/// <summary>A deal in the history of a player.</summary>
/// <param name="Id">The deal.</param>
/// <param name="Direction">Whether the player bought or sold.</param>
/// <param name="Counterparty">The other side of the deal.</param>
/// <param name="ResourceTypeId">The resource, or <see langword="null"/> for a part.</param>
/// <param name="PartTypeId">The part, or <see langword="null"/> for a resource.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="PricePerUnit">The price of a unit.</param>
/// <param name="Sum">The sum of the deal.</param>
/// <param name="Commission">The commission the seller paid.</param>
/// <param name="CreatedAt">When the deal happened.</param>
public sealed record DealItemDto(
    long Id,
    string Direction,
    PlayerCardDto Counterparty,
    int? ResourceTypeId,
    int? PartTypeId,
    long Quantity,
    long PricePerUnit,
    long Sum,
    long Commission,
    DateTimeOffset CreatedAt);

/// <summary>A level of the market as the screen shows it.</summary>
/// <param name="Id">The level.</param>
/// <param name="Visibility">Who sees the lots of the level.</param>
/// <param name="CommissionPercent">The commission of the level in per cent.</param>
/// <param name="IsActive">Whether the level takes lots.</param>
/// <param name="SortOrder">The order of the level.</param>
public sealed record MarketLevelDto(int Id, string Visibility, decimal CommissionPercent, bool IsActive, int SortOrder);

/// <summary>Whether a body of the player has what the market asks for.</summary>
/// <param name="PlanetId">The body.</param>
/// <param name="HasTradeCentre">Whether the body has a trade centre, so a lot may be put up from it.</param>
/// <param name="HasPortal">Whether the body has a portal, so goods may leave it and arrive at it.</param>
public sealed record MarketPlanetDto(long PlanetId, bool HasTradeCentre, bool HasPortal);

/// <summary>What the screen of the market needs to start.</summary>
/// <param name="Levels">The levels of the market.</param>
/// <param name="CreditsResourceTypeId">The credits.</param>
/// <param name="TradeResourceTypeId">The trade currency.</param>
/// <param name="EnergyResourceTypeId">The resource of the delivery fee, or <see langword="null"/> when the data has none.</param>
/// <param name="LotLimit">The lots used and allowed.</param>
/// <param name="Planets">The bodies of the player with the buildings of the market on them.</param>
/// <param name="GameNow">The moment of game time of the answer, so the timers of the screen count from it.</param>
/// <param name="TimeMultiplier">How many game seconds pass in a real second.</param>
public sealed record MarketOverviewDto(
    IReadOnlyList<MarketLevelDto> Levels,
    int CreditsResourceTypeId,
    int TradeResourceTypeId,
    int? EnergyResourceTypeId,
    LotLimitDto LotLimit,
    IReadOnlyList<MarketPlanetDto> Planets,
    DateTimeOffset GameNow,
    double TimeMultiplier);

/// <summary>A rate of the exchange at the end of a day.</summary>
/// <param name="Day">The day of game time.</param>
/// <param name="Rate">The rate: credits for one unit of the trade currency.</param>
public sealed record RatePointDto(DateTimeOffset Day, decimal Rate);

/// <summary>The history of the rate of the exchange.</summary>
/// <param name="Points">The rate at the end of every day of the window that has an exchange, oldest first.</param>
/// <param name="DayChangePercent">How the rate changed in a day, or <see langword="null"/> when there is nothing to compare.</param>
public sealed record RateHistoryDto(IReadOnlyList<RatePointDto> Points, decimal? DayChangePercent);

/// <summary>The lowest and the highest price of one unit of an item.</summary>
/// <param name="Min">The lowest price.</param>
/// <param name="Max">The highest price.</param>
public sealed record PriceLimitDto(long Min, long Max);

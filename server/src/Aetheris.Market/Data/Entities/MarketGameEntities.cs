namespace Aetheris.Market.Data.Entities;

/// <summary>
/// A lot of the market: what a seller offers, for how much and where the goods wait
/// (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// <para>
/// The lot holds the goods of the seller until they are bought or the lot is cancelled. The
/// goods are taken from the stock of the planet of the seller when the lot is put up, so the
/// seller cannot sell the same goods twice.
/// </para>
/// <para>
/// A lot has no life time: it waits until it is bought or cancelled (decision 4).
/// </para>
/// </remarks>
public sealed class MarketLotEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the seller of the lot.</summary>
    public string SellerPlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the planet of the seller the goods wait on.</summary>
    public long SellerPlanetId { get; set; }

    /// <summary>Gets or sets the sector of the planet of the seller.</summary>
    public int SellerSectorId { get; set; }

    /// <summary>Gets or sets the level of the market the lot is on.</summary>
    public int MarketLevelId { get; set; }

    /// <summary>Gets or sets the resource of the lot, or <see langword="null"/> when the lot is of a part.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part of the lot, or <see langword="null"/> when the lot is of a resource.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets how many units the lot holds.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets the price of one unit of the lot in the trade currency.</summary>
    public long PricePerUnit { get; set; }

    /// <summary>
    /// Gets or sets the state of the lot: it waits, it is sold or it is cancelled. The state is
    /// a logical code of the program, so the program reads the same codes whatever the lot is.
    /// </summary>
    public string State { get; set; } = MarketLotStates.Open;

    /// <summary>Gets or sets the moment of game time the lot was put up at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the mark of the parts of the lot, or <see langword="null"/> when the lot is
    /// of a resource.
    /// </summary>
    public int? PartMarkLevel { get; set; }

    /// <summary>
    /// Gets or sets whether the parts of the lot carry the bonus of the race that made them, or
    /// <see langword="null"/> when the lot is of a resource.
    /// </summary>
    public bool? PartHasRaceBonus { get; set; }

    /// <summary>
    /// Gets or sets the condition of the parts of the lot in per cent, or <see langword="null"/>
    /// when the lot is of a resource.
    /// </summary>
    public int? PartConditionPercent { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; a change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// The states of a lot of the market.
/// </summary>
public static class MarketLotStates
{
    /// <summary>The lot waits for a buyer.</summary>
    public const string Open = "open";

    /// <summary>The lot is bought and waits for the delivery.</summary>
    public const string Sold = "sold";

    /// <summary>The seller cancelled the lot and the goods went back to the stock.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>
/// A deal of the market: the record of every purchase, including the ones with the price of zero
/// (task 00024, section 4.1).
/// </summary>
/// <remarks>
/// The journal of the deals shows the seller, the buyer and the sum, so the balance between a
/// pair of accounts can be checked (documentation/tech/05).
/// </remarks>
public sealed class MarketDealEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the lot of the deal.</summary>
    public long LotId { get; set; }

    /// <summary>Gets or sets the seller of the deal.</summary>
    public string SellerPlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the buyer of the deal.</summary>
    public string BuyerPlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the resource of the deal, or <see langword="null"/> when the deal is of a part.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part of the deal, or <see langword="null"/> when the deal is of a resource.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets how many units the deal holds.</summary>
    public long Quantity { get; set; }

    /// <summary>Gets or sets the price of one unit of the deal in the trade currency.</summary>
    public long PricePerUnit { get; set; }

    /// <summary>Gets or sets the sum of the deal in the trade currency.</summary>
    public long Sum { get; set; }

    /// <summary>
    /// Gets or sets the commission of the level of the market, which the seller pays out of the
    /// sum: it is taken away and never reaches the buyers.
    /// </summary>
    public long Commission { get; set; }

    /// <summary>Gets or sets the moment of game time the deal happened at.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A delivery of the market: the goods of a deal travel through a portal to the planet of the
/// buyer (task 00024, section 4.1).
/// </summary>
public sealed class MarketDeliveryEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the deal of the delivery.</summary>
    public long DealId { get; set; }

    /// <summary>Gets or sets the buyer of the delivery.</summary>
    public string BuyerPlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the planet of the buyer the goods arrive at.</summary>
    public long DestinationPlanetId { get; set; }

    /// <summary>Gets or sets the sector of the planet of the buyer.</summary>
    public int DestinationSectorId { get; set; }

    /// <summary>Gets or sets the resource of the delivery, or <see langword="null"/> when the delivery is of a part.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part of the delivery, or <see langword="null"/> when the delivery is of a resource.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets how many units the delivery holds.</summary>
    public long Quantity { get; set; }

    /// <summary>
    /// Gets or sets how many energy rods the buyer pays for the delivery; they are taken from
    /// the stock of the planet of the buyer when the deal happens.
    /// </summary>
    public long EnergyRodAmount { get; set; }

    /// <summary>Gets or sets the moment of game time the goods arrive at.</summary>
    public DateTimeOffset ArrivesAt { get; set; }

    /// <summary>
    /// Gets or sets the state of the delivery: it travels, it has arrived or it was refused.
    /// The state is a logical code of the program.
    /// </summary>
    public string State { get; set; } = MarketDeliveryStates.Travelling;

    /// <summary>Gets or sets the version of the row, raised by every change; a change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// The states of a delivery of the market.
/// </summary>
public static class MarketDeliveryStates
{
    /// <summary>The goods travel through the portal.</summary>
    public const string Travelling = "travelling";

    /// <summary>The goods arrived at the planet of the buyer.</summary>
    public const string Arrived = "arrived";
}

/// <summary>
/// An exchange of the credits and the trade currency: the record of every swap
/// (task 00024, section 4.1).
/// </summary>
public sealed class MarketExchangeEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the player who made the exchange.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency the player gave.</summary>
    public int FromResourceTypeId { get; set; }

    /// <summary>Gets or sets the currency the player got.</summary>
    public int ToResourceTypeId { get; set; }

    /// <summary>Gets or sets how much of the first currency the player gave.</summary>
    public long FromAmount { get; set; }

    /// <summary>Gets or sets how much of the second currency the player got.</summary>
    public long ToAmount { get; set; }

    /// <summary>Gets or sets the rate of the exchange: how much of the second currency one unit of the first is worth.</summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets the commission of the exchange in per cent: the part of the sum that the
    /// exchange takes and that never reaches the player.
    /// </summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>Gets or sets the moment of game time the exchange happened at.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A row of the history of the rate of the exchange: the rate of a moment, kept so the rate
/// follows the flow of the market (task 00024, section 4.1).
/// </summary>
public sealed class MarketRateEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the rate of the exchange.</summary>
    public decimal Rate { get; set; }

    /// <summary>Gets or sets the moment of game time the rate was counted at.</summary>
    public DateTimeOffset RecordedAt { get; set; }
}

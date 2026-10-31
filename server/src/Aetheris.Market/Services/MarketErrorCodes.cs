namespace Aetheris.Market.Services;

/// <summary>
/// The error and reason codes of the market module, sent to the client as localisation keys
/// (task 00024).
/// </summary>
public static class MarketErrorCodes
{
    /// <summary>The body is not a colony of the player.</summary>
    public const string PlanetUnknown = "market_planet_unknown";

    /// <summary>The level of the market does not exist or is archived.</summary>
    public const string LevelUnknown = "market_level_unknown";

    /// <summary>The level of the market is inactive: it takes no lots.</summary>
    public const string LevelInactive = "market_level_inactive";

    /// <summary>The body has no trade centre.</summary>
    public const string TradeCentreMissing = "market_trade_centre_missing";

    /// <summary>The body has no portal.</summary>
    public const string PortalMissing = "market_portal_missing";

    /// <summary>The portal of the planet of the seller is gone, so the goods cannot leave.</summary>
    public const string SellerPortalMissing = "market_seller_portal_missing";

    /// <summary>The civilization has as many lots as its trade centres allow.</summary>
    public const string LotLimitReached = "market_lot_limit_reached";

    /// <summary>The item is not a resource of a body or a part that may be sold.</summary>
    public const string ItemUnknown = "market_item_unknown";

    /// <summary>The item has no reference price, so no price can be checked.</summary>
    public const string NoReferencePrice = "market_no_reference_price";

    /// <summary>The price is outside the limit of the item.</summary>
    public const string PriceOutOfRange = "market_price_out_of_range";

    /// <summary>The quantity or the price is not valid.</summary>
    public const string AmountInvalid = "market_amount_invalid";

    /// <summary>The lot does not exist or does not belong to the player.</summary>
    public const string LotUnknown = "market_lot_unknown";

    /// <summary>The lot is sold or cancelled already.</summary>
    public const string LotUnavailable = "market_lot_unavailable";

    /// <summary>The buyer is the seller.</summary>
    public const string OwnLot = "market_own_lot";

    /// <summary>The player does not see the lot.</summary>
    public const string LotNotVisible = "market_lot_not_visible";

    /// <summary>The currency is not one the exchange swaps.</summary>
    public const string CurrencyUnknown = "market_currency_unknown";

    /// <summary>The player has less trade currency than the lot costs.</summary>
    public const string TradeCurrencyShort = "market_trade_currency_short";

    /// <summary>The chosen body has fewer energy rods than the delivery costs.</summary>
    public const string EnergyShort = "market_energy_short";

    /// <summary>The delivery fee has no resource in the data.</summary>
    public const string EnergyResourceMissing = "market_energy_resource_missing";
}

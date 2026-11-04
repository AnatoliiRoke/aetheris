using Aetheris.Core.Configuration;

namespace Aetheris.Market.Configuration;

/// <summary>
/// The configuration keys of the market module with their starting values (task 00024).
/// </summary>
/// <remarks>
/// The levels of the market and the reference prices live in the tables of the module; these are
/// the limits around them. The host adds these definitions to its seed, so the admin panel checks
/// the bounds like for every other parameter. The values are the starting ones: the simulator
/// adjusts them later.
/// </remarks>
public static class MarketConfigKeys
{
    /// <summary>How far the price of a lot may go from the reference price while the deals are few, in per cent.</summary>
    public const string ReferenceBandPercent = "market.price.reference_band_percent";

    /// <summary>How many deals of an item in the window make the average price of the deals the base of the limit.</summary>
    public const string DealThreshold = "market.price.deal_threshold";

    /// <summary>The lowest price of a lot in per cent of the average price of the deals.</summary>
    public const string AverageMinPercent = "market.price.average_min_percent";

    /// <summary>The highest price of a lot in per cent of the average price of the deals.</summary>
    public const string AverageMaxPercent = "market.price.average_max_percent";

    /// <summary>The window of the average price of the deals, in days.</summary>
    public const string AverageWindowDays = "market.price.average_window_days";

    /// <summary>How long an arrived delivery stays in the list of the deliveries, in hours.</summary>
    public const string DeliveryRecentHours = "market.delivery.recent_hours";

    /// <summary>The largest number of rows one read of the market returns.</summary>
    public const string PageSize = "market.list.page_size";

    /// <summary>The rate of the exchange when the flow of the market is even: credits for one unit of the trade currency.</summary>
    public const string ExchangeBaseRate = "market.exchange.base_rate";

    /// <summary>The lowest rate of the exchange.</summary>
    public const string ExchangeMinRate = "market.exchange.min_rate";

    /// <summary>The highest rate of the exchange.</summary>
    public const string ExchangeMaxRate = "market.exchange.max_rate";

    /// <summary>How far the rate moves from the base rate when all the flow goes one way, in per cent.</summary>
    public const string ExchangeSensitivityPercent = "market.exchange.sensitivity_percent";

    /// <summary>The window of the flow that moves the rate, in hours.</summary>
    public const string ExchangeWindowHours = "market.exchange.window_hours";

    /// <summary>How many days of the history of the rate the chart of the exchange shows.</summary>
    public const string RateHistoryDays = "market.exchange.history_days";

    /// <summary>The commission of the exchange in per cent; it is taken away and reaches nobody.</summary>
    public const string ExchangeCommissionPercent = "market.exchange.commission_percent";

    /// <summary>
    /// Gets the definitions of the parameters with their starting values and bounds.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(ReferenceBandPercent, "20", "0", "100", "How far the price of a lot may go from the reference price while the deals are few, in per cent."),
        new(DealThreshold, "20", "0", "1000000", "How many deals of an item in the window make the average price of the deals the base of the limit."),
        new(AverageMinPercent, "75", "0", "100", "The lowest price of a lot in per cent of the average price of the deals."),
        new(AverageMaxPercent, "150", "100", "10000", "The highest price of a lot in per cent of the average price of the deals."),
        new(AverageWindowDays, "7", "1", "365", "The window of the average price of the deals, in days."),
        new(DeliveryRecentHours, "24", "0", "720", "How long a delivery that has arrived stays in the list of the deliveries, in hours."),
        new(PageSize, "40", "1", "1000", "The largest number of rows one read of the market returns."),
        new(ExchangeBaseRate, "100", "1", "1000000", "The rate of the exchange when the flow of the market is even: credits for one unit of the trade currency."),
        new(ExchangeMinRate, "50", "1", "1000000", "The lowest rate of the exchange."),
        new(ExchangeMaxRate, "200", "1", "1000000", "The highest rate of the exchange."),
        new(ExchangeSensitivityPercent, "50", "0", "100", "How far the rate moves from the base rate when all the flow goes one way, in per cent."),
        new(ExchangeWindowHours, "24", "1", "720", "The window of the flow that moves the rate, in hours."),
        new(RateHistoryDays, "7", "1", "90", "How many days of the history of the rate the chart of the exchange shows."),
        new(ExchangeCommissionPercent, "5", "0", "100", "The commission of the exchange in per cent; it is taken away and reaches nobody."),
    ];
}

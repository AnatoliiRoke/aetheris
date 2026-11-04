using System.Globalization;
using Aetheris.Core.Configuration;

namespace Aetheris.Market.Configuration;

/// <summary>
/// The configuration of the market module, read from the configuration of the game (task 00024).
/// </summary>
/// <param name="ReferenceBandPercent">How far a price may go from the reference price, in per cent.</param>
/// <param name="DealThreshold">How many deals make their average the base of the limit.</param>
/// <param name="AverageMinPercent">The lowest price in per cent of the average.</param>
/// <param name="AverageMaxPercent">The highest price in per cent of the average.</param>
/// <param name="AverageWindowDays">The window of the average, in days.</param>
/// <param name="DeliveryRecentHours">How long an arrived delivery stays in the list, in hours.</param>
/// <param name="PageSize">The largest number of rows of a read.</param>
/// <param name="ExchangeBaseRate">The rate with an even flow.</param>
/// <param name="ExchangeMinRate">The lowest rate.</param>
/// <param name="ExchangeMaxRate">The highest rate.</param>
/// <param name="ExchangeSensitivityPercent">How far the flow moves the rate, in per cent.</param>
/// <param name="ExchangeWindowHours">The window of the flow, in hours.</param>
/// <param name="RateHistoryDays">The days of the chart of the rate.</param>
/// <param name="ExchangeCommissionPercent">The commission of the exchange, in per cent.</param>
public sealed record MarketSettings(
    decimal ReferenceBandPercent,
    long DealThreshold,
    decimal AverageMinPercent,
    decimal AverageMaxPercent,
    int AverageWindowDays,
    int DeliveryRecentHours,
    int PageSize,
    decimal ExchangeBaseRate,
    decimal ExchangeMinRate,
    decimal ExchangeMaxRate,
    decimal ExchangeSensitivityPercent,
    int ExchangeWindowHours,
    int RateHistoryDays,
    decimal ExchangeCommissionPercent)
{
    /// <summary>
    /// Reads the settings from the configuration of the game.
    /// </summary>
    /// <param name="config">The configuration of the game.</param>
    /// <returns>The settings.</returns>
    public static MarketSettings From(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new MarketSettings(
            Number(config, MarketConfigKeys.ReferenceBandPercent),
            (long)Number(config, MarketConfigKeys.DealThreshold),
            Number(config, MarketConfigKeys.AverageMinPercent),
            Number(config, MarketConfigKeys.AverageMaxPercent),
            (int)Number(config, MarketConfigKeys.AverageWindowDays),
            (int)Number(config, MarketConfigKeys.DeliveryRecentHours),
            (int)Number(config, MarketConfigKeys.PageSize),
            Number(config, MarketConfigKeys.ExchangeBaseRate),
            Number(config, MarketConfigKeys.ExchangeMinRate),
            Number(config, MarketConfigKeys.ExchangeMaxRate),
            Number(config, MarketConfigKeys.ExchangeSensitivityPercent),
            (int)Number(config, MarketConfigKeys.ExchangeWindowHours),
            (int)Number(config, MarketConfigKeys.RateHistoryDays),
            Number(config, MarketConfigKeys.ExchangeCommissionPercent));
    }

    private static decimal Number(IGameConfig config, string key) =>
        decimal.Parse(config.GetRaw(key) ?? Default(key), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string Default(string key) =>
        MarketConfigKeys.Definitions.First(definition => string.Equals(definition.Key, key, StringComparison.Ordinal)).DefaultValue;
}

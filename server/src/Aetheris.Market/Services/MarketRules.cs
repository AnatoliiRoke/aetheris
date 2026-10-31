using Aetheris.Market.Configuration;

namespace Aetheris.Market.Services;

/// <summary>
/// The formulas of the market: the limit of a price, the commission, the fee and the time of a
/// delivery and the rate of the exchange (task 00024, section 3). They hold the shape of the
/// formulas only; every number comes from <see cref="MarketSettings"/>.
/// </summary>
public static class MarketRules
{
    private const decimal Hundred = 100m;

    /// <summary>
    /// Gets the lowest and the highest price of one unit of an item.
    /// </summary>
    /// <param name="referencePrice">The reference price of the item.</param>
    /// <param name="dealCount">How many deals of the item the window holds.</param>
    /// <param name="averagePrice">The average price of the deals of the window, or <see langword="null"/>.</param>
    /// <param name="settings">The settings of the market.</param>
    /// <returns>The lowest and the highest price.</returns>
    /// <remarks>
    /// While the deals are few the limit is a band around the reference price; from the
    /// threshold on it is a band around the average price of the deals.
    /// </remarks>
    public static (long Min, long Max) PriceLimit(long referencePrice, long dealCount, decimal? averagePrice, MarketSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        decimal lowShare = settings.ReferenceBandPercent;
        decimal highShare = settings.ReferenceBandPercent;
        decimal middle = referencePrice;

        if (dealCount >= settings.DealThreshold && averagePrice is { } average && average > 0)
        {
            middle = average;
            lowShare = Hundred - settings.AverageMinPercent;
            highShare = settings.AverageMaxPercent - Hundred;
        }

        return ((long)decimal.Floor(middle * (Hundred - lowShare) / Hundred), (long)decimal.Ceiling(middle * (Hundred + highShare) / Hundred));
    }

    /// <summary>
    /// Gets the commission a level takes out of a sum: the part that disappears.
    /// </summary>
    /// <param name="sum">The sum of the deal.</param>
    /// <param name="percent">The commission of the level in per cent.</param>
    /// <returns>The commission, never above the sum.</returns>
    public static long Commission(long sum, decimal percent) =>
        Math.Min(sum, (long)decimal.Floor(sum * percent / Hundred));

    /// <summary>
    /// Gets the share of a sum that is left after a commission.
    /// </summary>
    /// <param name="commissionPercent">The commission in per cent.</param>
    /// <returns>The share, from zero to one.</returns>
    public static decimal KeptShare(decimal commissionPercent) => (Hundred - commissionPercent) / Hundred;

    /// <summary>
    /// Gets the delivery fee of a lot of resources.
    /// </summary>
    /// <param name="quantity">How many units.</param>
    /// <param name="weight">The weight of one unit in tonnes.</param>
    /// <param name="perTonne">The fee of a tonne.</param>
    /// <returns>The fee.</returns>
    public static long ResourceEnergy(long quantity, decimal weight, decimal perTonne) =>
        (long)decimal.Ceiling(quantity * weight * perTonne);

    /// <summary>
    /// Gets the delivery fee of a lot of parts.
    /// </summary>
    /// <param name="quantity">How many parts.</param>
    /// <param name="perPart">The fee of a part.</param>
    /// <returns>The fee.</returns>
    public static long PartEnergy(long quantity, decimal perPart) =>
        (long)decimal.Ceiling(quantity * perPart);

    /// <summary>
    /// Gets the time of a delivery in seconds.
    /// </summary>
    /// <param name="distance">The distance between the two bodies.</param>
    /// <param name="portalFactor">The speed of the slower of the two portals; never below one.</param>
    /// <param name="settings">The settings of the market.</param>
    /// <returns>The seconds, at least one.</returns>
    public static long DeliverySeconds(double distance, long portalFactor, MarketSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        decimal factor = Math.Max(1, portalFactor);
        decimal seconds = (settings.DeliveryBaseSeconds + ((decimal)distance * settings.DeliverySecondsPerDistance)) / factor;

        return Math.Max(1, (long)decimal.Ceiling(seconds));
    }

    /// <summary>
    /// Gets the rate of the exchange: it climbs when more trade currency is bought than sold
    /// in the window and falls in the other case, within the lowest and the highest rate.
    /// </summary>
    /// <param name="boughtTrade">The trade currency bought with credits in the window.</param>
    /// <param name="soldTrade">The trade currency sold for credits in the window.</param>
    /// <param name="settings">The settings of the market.</param>
    /// <returns>Credits for one unit of the trade currency.</returns>
    public static decimal ExchangeRate(long boughtTrade, long soldTrade, MarketSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        long flow = boughtTrade + soldTrade;
        decimal imbalance = flow == 0 ? 0m : (decimal)(boughtTrade - soldTrade) / flow;
        decimal rate = settings.ExchangeBaseRate * (1m + (settings.ExchangeSensitivityPercent / Hundred * imbalance));

        return Math.Clamp(rate, settings.ExchangeMinRate, settings.ExchangeMaxRate);
    }
}

using Aetheris.Market.Configuration;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Market.Services;

/// <summary>
/// The credits and the trade currency of the game: the two currencies the exchange swaps.
/// </summary>
/// <param name="CreditsResourceTypeId">The currency that earns the income of the population.</param>
/// <param name="TradeResourceTypeId">The currency that allows a debt.</param>
public sealed record MarketCurrencies(int CreditsResourceTypeId, int TradeResourceTypeId);

/// <summary>
/// The weight of one unit of the resources and of the parts, in tonnes.
/// </summary>
/// <param name="Resources">The weight by resource.</param>
/// <param name="Parts">The weight by part.</param>
public sealed record MarketWeights(IReadOnlyDictionary<int, decimal> Resources, IReadOnlyDictionary<int, decimal> Parts)
{
    /// <summary>
    /// Gets the weight of one unit of an item.
    /// </summary>
    /// <param name="resourceTypeId">The resource, or <see langword="null"/>.</param>
    /// <param name="partTypeId">The part, or <see langword="null"/>.</param>
    /// <returns>The weight in tonnes; zero for an unknown item.</returns>
    public decimal UnitWeight(int? resourceTypeId, int? partTypeId) =>
        resourceTypeId is { } resourceId
            ? Resources.GetValueOrDefault(resourceId)
            : partTypeId is { } partId ? Parts.GetValueOrDefault(partId) : 0m;
}

/// <summary>
/// The reads of the reference data of the market: the levels, the reference prices, the average
/// price of the deals, the currencies and the rate of the exchange (task 00024).
/// </summary>
public sealed class MarketCatalog
{
    private readonly MarketDbContext _db;
    private readonly IWorldModule _world;
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketCatalog"/> class.
    /// </summary>
    /// <param name="db">The context of the module.</param>
    /// <param name="world">The world module, for the currencies.</param>
    /// <param name="parts">The storage of the parts, for the weight of a part.</param>
    public MarketCatalog(MarketDbContext db, IWorldModule world, PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(parts);

        _db = db;
        _world = world;
        _parts = parts;
    }

    /// <summary>
    /// Gets the weight of one unit of every resource and of every part, in tonnes.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The weights.</returns>
    public async Task<MarketWeights> WeightsAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

        return new MarketWeights(
            catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.Weight),
            parts.ToDictionary(part => part.Key, part => part.Value.Weight));
    }

    /// <summary>
    /// Finds the resource of the delivery fee by its code in the configuration.
    /// </summary>
    /// <param name="code">The code of the resource.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The resource, or <see langword="null"/> when the data has none with the code.</returns>
    public async Task<int?> EnergyResourceAsync(string code, CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        return catalog.Resources.FirstOrDefault(resource => string.Equals(resource.Code, code, StringComparison.Ordinal))?.Id;
    }

    /// <summary>
    /// Tells whether a player sees the lots of a level.
    /// </summary>
    /// <param name="level">The level of the market.</param>
    /// <param name="viewerSectorId">The sector the player looks from.</param>
    /// <param name="lotSectorId">The sector of the seller.</param>
    /// <returns><see langword="true"/> when the level is active and the player sees its lots.</returns>
    public static bool Sees(MarketLevelEntity level, int viewerSectorId, int lotSectorId)
    {
        ArgumentNullException.ThrowIfNull(level);

        if (!level.IsActive)
        {
            return false;
        }

        return level.Visibility switch
        {
            MarketVisibilities.Galactic => true,
            MarketVisibilities.Sector => viewerSectorId == lotSectorId,
            _ => false,
        };
    }

    /// <summary>
    /// Finds a level that is not archived.
    /// </summary>
    /// <param name="levelId">The level.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level, or <see langword="null"/>.</returns>
    public Task<MarketLevelEntity?> FindLevelAsync(int levelId, CancellationToken cancellationToken) =>
        _db.MarketLevels.AsNoTracking().FirstOrDefaultAsync(level => level.Id == levelId && !level.IsArchived, cancellationToken);

    /// <summary>
    /// Gets every level of the market, archived ones included, so a lot of an old level still shows.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels by their identifiers.</returns>
    public async Task<IReadOnlyDictionary<int, MarketLevelEntity>> LevelsAsync(CancellationToken cancellationToken) =>
        await _db.MarketLevels.AsNoTracking().ToDictionaryAsync(level => level.Id, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gets the reference price of an item.
    /// </summary>
    /// <param name="resourceTypeId">The resource, or <see langword="null"/>.</param>
    /// <param name="partTypeId">The part, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The price, or <see langword="null"/> when the item has none.</returns>
    public Task<long?> ReferencePriceAsync(int? resourceTypeId, int? partTypeId, CancellationToken cancellationToken) =>
        _db.ReferencePrices.AsNoTracking()
            .Where(price => !price.IsArchived && price.ResourceTypeId == resourceTypeId && price.PartTypeId == partTypeId)
            .Select(price => (long?)price.Price)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Counts the deals of an item since a moment and their average price. Deals of the price of
    /// zero are gifts and take no part.
    /// </summary>
    /// <param name="resourceTypeId">The resource, or <see langword="null"/>.</param>
    /// <param name="partTypeId">The part, or <see langword="null"/>.</param>
    /// <param name="since">The start of the window.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the deals and the average price of a unit.</returns>
    public async Task<(long Count, decimal? Average)> DealStatisticsAsync(int? resourceTypeId, int? partTypeId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var totals = await _db.MarketDeals.AsNoTracking()
            .Where(deal => deal.CreatedAt >= since && deal.PricePerUnit > 0 && deal.ResourceTypeId == resourceTypeId && deal.PartTypeId == partTypeId)
            .GroupBy(_ => 1)
            .Select(group => new { Count = group.LongCount(), Sum = group.Sum(deal => deal.Sum), Quantity = group.Sum(deal => deal.Quantity) })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return totals is null || totals.Quantity == 0 ? (0, null) : (totals.Count, (decimal)totals.Sum / totals.Quantity);
    }

    /// <summary>
    /// Finds the two currencies of the exchange in the data.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The credits and the trade currency.</returns>
    /// <exception cref="InvalidOperationException">The data has no such currency.</exception>
    public async Task<MarketCurrencies> CurrenciesAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog catalog = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        List<ResourceTypeFact> currencies = [.. catalog.Resources.Where(resource => resource.IsCivilizationScope && !resource.IsArchived)];
        ResourceTypeFact credits = currencies.FirstOrDefault(resource => resource.EarnsPopulationIncome)
            ?? throw new InvalidOperationException("The data has no currency that earns the income of the population.");
        ResourceTypeFact trade = currencies.FirstOrDefault(resource => resource.AllowsDebt)
            ?? throw new InvalidOperationException("The data has no currency that allows a debt.");

        return new MarketCurrencies(credits.Id, trade.Id);
    }

    /// <summary>
    /// Counts the rate of the exchange from the flow of the window.
    /// </summary>
    /// <param name="currencies">The currencies of the exchange.</param>
    /// <param name="settings">The settings of the market.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Credits for one unit of the trade currency.</returns>
    public async Task<decimal> RateAsync(MarketCurrencies currencies, MarketSettings settings, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(currencies);
        ArgumentNullException.ThrowIfNull(settings);

        DateTimeOffset since = now.AddHours(-settings.ExchangeWindowHours);
        var flow = await _db.MarketExchanges.AsNoTracking()
            .Where(exchange => exchange.CreatedAt >= since)
            .GroupBy(exchange => exchange.FromResourceTypeId == currencies.TradeResourceTypeId)
            .Select(group => new { SoldTrade = group.Key, Given = group.Sum(exchange => exchange.FromAmount), Got = group.Sum(exchange => exchange.ToAmount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        long sold = flow.Where(row => row.SoldTrade).Sum(row => row.Given);
        long bought = flow.Where(row => !row.SoldTrade).Sum(row => row.Got);

        return MarketRules.ExchangeRate(bought, sold, settings);
    }
}

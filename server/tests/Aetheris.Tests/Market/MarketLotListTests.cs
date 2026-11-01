using Aetheris.Market;
using Aetheris.Market.Api;
using Aetheris.Market.Data;
using Aetheris.Market.Data.Entities;
using Aetheris.Market.Services;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Market;

/// <summary>
/// Tests of the list of the lots (task 00026): with hundreds of lots the server filters, sorts and
/// cuts the pages, so the order and the search hold over every lot and not over one page.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MarketLotListTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketLotListTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public MarketLotListTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The server sorts the whole list, cuts it into pages that continue each other, filters by the
    /// items and by the price, and groups the lots by item.
    /// </summary>
    /// <returns>A task that completes when the assertions have been evaluated.</returns>
    [Fact]
    public async Task The_server_sorts_filters_pages_and_groups_the_whole_list()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer viewer = await PlanetsTestData.FoundAsync(_factory);
        string viewerId = viewer.Account.UserId;
        int ironId;
        int copperId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            await services.GetRequiredService<IMarketModule>().EnsureAsync(Token);
            ironId = await PlanetsTestData.ResourceIdAsync(services, "resource_1");
            copperId = await PlanetsTestData.ResourceIdAsync(services, "resource_6");
            int sectorId = (await services.GetRequiredService<IWorldModule>().GetOwnedPlanetsAsync(viewerId, Token)).Single().SectorId;
            int levelId = (await services.GetRequiredService<MarketDbContext>().MarketLevels.FirstAsync(level => level.Visibility == MarketVisibilities.Galactic, Token)).Id;
            MarketDbContext db = services.GetRequiredService<MarketDbContext>();

            for (int index = 0; index < 250; index++)
            {
                db.MarketLots.Add(new MarketLotEntity
                {
                    SellerPlayerId = "seller",
                    SellerPlanetId = 1,
                    SellerSectorId = sectorId,
                    MarketLevelId = levelId,
                    ResourceTypeId = index % 2 == 0 ? ironId : copperId,
                    Quantity = 10 + index,
                    PricePerUnit = 1 + ((index * 37) % 200),
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-index),
                });
            }

            await db.SaveChangesAsync(Token);
        }

        using IServiceScope read = _factory.Services.CreateScope();
        MarketReadService reads = read.ServiceProvider.GetRequiredService<MarketReadService>();

        LotsDto first = await reads.LotsAsync(viewerId, new LotFilter(Sort: LotSorts.Price, Descending: false, Limit: 100), Token);
        LotsDto second = await reads.LotsAsync(viewerId, new LotFilter(Sort: LotSorts.Price, Descending: false, Limit: 100, Offset: 100), Token);

        Assert.Equal(250, first.Total);
        Assert.Equal(100, first.Items.Count);
        Assert.Equal(first.Items.Select(lot => lot.PricePerUnit).Order(), first.Items.Select(lot => lot.PricePerUnit));
        Assert.True(second.Items[0].PricePerUnit >= first.Items[^1].PricePerUnit);

        LotsDto expensive = await reads.LotsAsync(viewerId, new LotFilter(Sort: LotSorts.Total, Descending: true, Limit: 5), Token);

        Assert.Equal(expensive.Items.Select(lot => lot.TotalPrice).OrderDescending(), expensive.Items.Select(lot => lot.TotalPrice));

        LotsDto iron = await reads.LotsAsync(viewerId, new LotFilter(ResourceTypeIds: [ironId], PartTypeIds: [], MinPrice: 100), Token);

        Assert.All(iron.Items, lot => Assert.True(lot.ResourceTypeId == ironId && lot.PricePerUnit >= 100));
        Assert.Equal(iron.Total, iron.Items.Count);

        LotsDto none = await reads.LotsAsync(viewerId, new LotFilter(ResourceTypeIds: [], PartTypeIds: []), Token);

        Assert.Equal(0, none.Total);

        LotsDto byItem = await reads.LotsAsync(viewerId, new LotFilter(Sort: LotSorts.Item, Descending: false, ItemOrder: [$"r{copperId}", $"r{ironId}"], Limit: 100), Token);

        LotsDto lastPage = await reads.LotsAsync(viewerId, new LotFilter(Sort: LotSorts.Item, Descending: false, ItemOrder: [$"r{copperId}", $"r{ironId}"], Limit: 100, Offset: 200), Token);

        // 125 lots of the first item come first, then the lots of the second one.
        Assert.All(byItem.Items, lot => Assert.Equal(copperId, lot.ResourceTypeId));
        Assert.Equal(ironId, lastPage.Items[^1].ResourceTypeId);

        IReadOnlyList<LotGroupDto> groups = await reads.LotGroupsAsync(viewerId, new LotFilter(), Token);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.Equal(125, group.LotCount));
        Assert.Equal(Enumerable.Range(0, 250).Where(index => index % 2 == 0).Min(index => 1 + ((index * 37) % 200)), groups.First(group => group.ResourceTypeId == ironId).MinPricePerUnit);
    }
}

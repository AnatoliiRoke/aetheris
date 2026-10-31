using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the group of a part in the stock of a body, which the storage screen groups the
/// parts by (task 00020).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductionStockGroupTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionStockGroupTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProductionStockGroupTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A stack of the stock carries the group of its part and the key of the name of the group,
    /// both read from the catalog and never from a code.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stack_carries_the_group_of_its_part()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using (IServiceScope reset = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(reset.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
            await reset.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        }

        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        await ProductionTestData.BuildStorageAsync(player, services);

        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(services);
        IProductionModule production = services.GetRequiredService<IProductionModule>();

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token);
        await ProductionTestData.CompleteAsync(player, services, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        ProductionStackDto stack = Assert.Single(stock.Stacks);

        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);
        PartTypeInfo part = catalog.FindPart(stack.PartTypeId)!;

        Assert.Equal(part.PartGroupId, stack.PartGroupId);
        Assert.Equal(catalog.FindGroup(part.PartGroupId)!.NameKey, stack.PartGroupNameKey);
    }
}

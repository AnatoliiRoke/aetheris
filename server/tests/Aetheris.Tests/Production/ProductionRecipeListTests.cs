using Aetheris.Core.Errors;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the list of every recipe of every factory that the production screen shows
/// (task 00032).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductionRecipeListTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionRecipeListTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProductionRecipeListTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The list holds every offered recipe of every factory; a recipe whose factory the body has
    /// not built is closed and the reason names the factory, and a recipe of a part names the
    /// group of the part for the filter of the screen.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_list_holds_every_recipe_and_closes_those_of_a_factory_not_built()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProductionQueueService queue = services.GetRequiredService<ProductionQueueService>();

        ProductionRecipeListDto before = await queue.ReadRecipesAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(await services.GetRequiredService<ProductionDbContext>().Recipes.CountAsync(recipe => !recipe.IsArchived, Token), before.Recipes.Count);
        Assert.All(
            before.Recipes.Where(recipe => recipe.BuildingTypeId is not null && before.Factories.All(factory => factory.BuildingTypeId != recipe.BuildingTypeId)),
            recipe =>
            {
                Assert.False(recipe.IsAvailable);
                Assert.Equal(ProductionErrorCodes.FactoryNotBuilt, recipe.Reasons[0].Code);
                Assert.Equal(recipe.BuildingTypeId, recipe.Reasons[0].TargetId);
            });
        Assert.All(before.Recipes.Where(recipe => recipe.PartTypeId is not null), recipe => Assert.NotNull(recipe.PartGroupNameKey));
        Assert.All(before.Recipes.Where(recipe => recipe.PartTypeId is null), recipe => Assert.Null(recipe.PartGroupId));

        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(services);
        await ProductionTestData.BuildFactoryAsync(player, services, recipe);

        ProductionRecipeListDto after = await queue.ReadRecipesAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(before.Recipes.Count, after.Recipes.Count);
        Assert.DoesNotContain(
            after.Recipes.Where(entry => entry.RecipeId == recipe.Id),
            entry => entry.Reasons.Any(reason => reason.Code == ProductionErrorCodes.FactoryNotBuilt));
        Assert.Contains(after.Factories, factory => factory.BuildingTypeId == recipe.BuildingTypeId);
    }

    /// <summary>
    /// The list of a body is read only by its owner.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stranger_does_not_read_the_list()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        GameException failure = await Assert.ThrowsAsync<GameException>(() =>
            scope.ServiceProvider.GetRequiredService<ProductionQueueService>().ReadRecipesAsync(stranger.Account.UserId, owner.PlanetId, Token));

        Assert.Equal(ProductionErrorCodes.PlanetUnknown, failure.Code);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope reset = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(reset.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await reset.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

using System.Net;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the read of every batch of every factory of a body, which the queue of the production
/// on the screen of the body shows (task 00020, section 7.5).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductionActiveOrdersTests
{
    private const string SecondFactoryRecipeCode = "recipe_active_orders_test";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionActiveOrdersTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProductionActiveOrdersTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The batches of two factories stand in one list in the order of the queues, the places of
    /// the two queues add up, and every batch is the very row of the database.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_batches_of_several_factories_stand_in_one_list_and_match_the_database()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        (long firstFactoryId, long secondFactoryId, RecipeInfo firstRecipe, RecipeInfo secondRecipe) = await PrepareTwoFactoriesAsync(player, services);
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        string playerId = player.Account.UserId;

        await production.EnqueueAsync(playerId, player.PlanetId, firstFactoryId, firstRecipe.Id, 3, Token);
        await production.EnqueueAsync(playerId, player.PlanetId, firstFactoryId, firstRecipe.Id, 2, Token);
        await production.EnqueueAsync(playerId, player.PlanetId, secondFactoryId, secondRecipe.Id, 4, Token);

        HttpResponseMessage response = await player.Client.GetAsync($"/api/v1/production/orders?planet_id={player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement root = body.RootElement;
        JsonElement[] orders = [.. root.GetProperty("orders").EnumerateArray()];

        Assert.Equal(player.PlanetId, root.GetProperty("planet_id").GetInt64());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("game_now").GetString()));
        Assert.Equal(3, orders.Length);
        Assert.Equal(3, root.GetProperty("queue_used_slots").GetInt32());

        // Every factory has its own queue, so the places of the body are their sum.
        int baseSlots = ProductionSettings.From(services.GetRequiredService<IGameConfig>()).QueueBaseSlots;
        Assert.Equal(baseSlots * 2, root.GetProperty("queue_slots").GetInt32());

        // The batches follow the order of the queues: the one under way of every factory first.
        Assert.Equal([1, 1, 2], orders.Select(order => order.GetProperty("position").GetInt32()));
        Assert.Equal(
            new[] { firstFactoryId, secondFactoryId }.Order(),
            orders.Take(2).Select(order => order.GetProperty("planet_building_id").GetInt64()).Order());

        // The batches are the rows of the database and the batches of the factories one by one.
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        List<ProductionOrderEntity> rows = await dbContext.ProductionOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId)
            .ToListAsync(Token);

        Assert.Equal(
            rows.OrderBy(row => row.Id).Select(row => (row.Id, row.PlanetBuildingId, row.RecipeId, row.Quantity, row.ProducedQuantity, row.Position, row.IsBlocked)),
            orders.OrderBy(order => order.GetProperty("order_id").GetInt64()).Select(order => (
                order.GetProperty("order_id").GetInt64(),
                order.GetProperty("planet_building_id").GetInt64(),
                order.GetProperty("recipe_id").GetInt32(),
                order.GetProperty("quantity").GetInt64(),
                order.GetProperty("produced_quantity").GetInt64(),
                order.GetProperty("position").GetInt32(),
                order.GetProperty("is_blocked").GetBoolean())));

        ProductionFactoryDto first = await production.GetFactoryAsync(playerId, player.PlanetId, firstFactoryId, Token);
        ProductionFactoryDto second = await production.GetFactoryAsync(playerId, player.PlanetId, secondFactoryId, Token);
        Assert.Equal(
            first.Queue.Concat(second.Queue).Select(order => order.OrderId).Order(),
            orders.Select(order => order.GetProperty("order_id").GetInt64()).Order());

        // The names are the keys of the recipe and of the factory, never texts of the code.
        JsonElement onSecond = orders.Single(order => order.GetProperty("planet_building_id").GetInt64() == secondFactoryId);
        Assert.Equal(secondRecipe.NameKey, onSecond.GetProperty("name_key").GetString());
        Assert.False(string.IsNullOrEmpty(onSecond.GetProperty("building_name_key").GetString()));
        Assert.Equal(secondRecipe.DurationSeconds, onSecond.GetProperty("unit_seconds").GetInt64());
        Assert.NotEqual(JsonValueKind.Null, onSecond.GetProperty("next_unit_at").ValueKind);
    }

    /// <summary>
    /// A cancelled batch leaves the list at once and frees its place, and the batch of another
    /// body of the same player is not in the list.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_cancelled_batch_leaves_the_list_and_frees_its_place()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long factoryId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(services);
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        string playerId = player.Account.UserId;

        ProductionActiveOrdersDto empty = await production.GetActiveOrdersAsync(playerId, player.PlanetId, Token);
        Assert.Empty(empty.Orders);
        Assert.Equal(0, empty.QueueUsedSlots);
        Assert.True(empty.QueueSlots > 0);

        ProductionFactoryDto queued = await production.EnqueueAsync(playerId, player.PlanetId, factoryId, recipe.Id, 2, Token);
        ProductionActiveOrdersDto one = await production.GetActiveOrdersAsync(playerId, player.PlanetId, Token);

        Assert.Equal(queued.Queue[0].OrderId, Assert.Single(one.Orders).OrderId);
        Assert.Equal(1, one.QueueUsedSlots);
        Assert.Equal(empty.QueueSlots, one.QueueSlots);

        await production.CancelAsync(playerId, player.PlanetId, factoryId, queued.Queue[0].OrderId, Token);
        ProductionActiveOrdersDto after = await production.GetActiveOrdersAsync(playerId, player.PlanetId, Token);

        Assert.Empty(after.Orders);
        Assert.Equal(0, after.QueueUsedSlots);
    }

    /// <summary>
    /// A stranger and a client without a session see no batch of the body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stranger_and_a_guest_read_no_batch()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient guest = _factory.CreateBrowserClient();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long factoryId = await ProductionTestData.BuildFactoryAsync(owner, services);
        await ProductionTestData.GiveEveryResourceAsync(owner, services);
        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(services);

        await services.GetRequiredService<IProductionModule>()
            .EnqueueAsync(owner.Account.UserId, owner.PlanetId, factoryId, recipe.Id, 1, Token);

        string url = $"/api/v1/production/orders?planet_id={owner.PlanetId}";
        HttpResponseMessage own = await owner.Client.GetAsync(url, Token);
        HttpResponseMessage foreign = await stranger.Client.GetAsync(url, Token);
        HttpResponseMessage anonymous = await guest.GetAsync(url, Token);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        // The body of another player is refused as the other reads of the module refuse it.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        string foreignBody = await foreign.Content.ReadAsStringAsync(Token);
        Assert.Contains(ProductionErrorCodes.PlanetUnknown, foreignBody, StringComparison.Ordinal);
        Assert.DoesNotContain("order_id", foreignBody, StringComparison.Ordinal);
        Assert.DoesNotContain("name_key", foreignBody, StringComparison.Ordinal);
    }

    private async Task<(long FirstFactoryId, long SecondFactoryId, RecipeInfo FirstRecipe, RecipeInfo SecondRecipe)> PrepareTwoFactoriesAsync(
        PlanetsTestPlayer player,
        IServiceProvider services)
    {
        long firstFactoryId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        await ProductionTestData.BuildStorageAsync(player, services);

        // A recipe of another building type is a second factory, so the data alone opens it.
        Aetheris.Planets.IPlanetsModule planets = services.GetRequiredService<Aetheris.Planets.IPlanetsModule>();
        RecipeInfo firstRecipe = await ProductionTestData.FirstRecipeAsync(services);
        int secondTypeId = await planets.FindBuildingTypeIdAsync("building_21", Token)
            ?? throw new InvalidOperationException("The second building type does not exist.");

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        RecipeEntity template = await dbContext.Recipes.AsNoTracking()
            .Include(recipe => recipe.IngredientIds)
            .SingleAsync(recipe => recipe.Id == firstRecipe.Id, Token);

        RecipeEntity second = new()
        {
            Code = SecondFactoryRecipeCode,
            NameKey = "production.recipe." + SecondFactoryRecipeCode + ".name",
            DescriptionKey = "production.recipe." + SecondFactoryRecipeCode + ".description",
            BuildingTypeId = secondTypeId,
            ResourceTypeId = template.ResourceTypeId,
            PartTypeId = template.PartTypeId,
            OutputQuantity = template.OutputQuantity,
            DurationSeconds = template.DurationSeconds + 1,
            SortOrder = template.SortOrder + 1000,
            IngredientIds = [.. template.IngredientIds.Select(ingredient => new RecipeIngredientEntity
            {
                ResourceTypeId = ingredient.ResourceTypeId,
                PartTypeId = ingredient.PartTypeId,
                Amount = ingredient.Amount,
            })],
        };

        dbContext.Recipes.Add(second);
        await dbContext.SaveChangesAsync(Token);
        services.GetRequiredService<ProductionCatalogCache>().Invalidate();

        PlanetsDbContext planetsContext = services.GetRequiredService<PlanetsDbContext>();
        long secondFactoryId = await planetsContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == player.PlanetId && building.BuildingTypeId == secondTypeId)
            .Select(building => building.Id)
            .SingleAsync(Token);

        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(dbContext, services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);
        RecipeInfo secondRecipe = catalog.Recipes.Single(recipe => string.Equals(recipe.Code, SecondFactoryRecipeCode, StringComparison.Ordinal));

        return (firstFactoryId, secondFactoryId, firstRecipe, secondRecipe);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

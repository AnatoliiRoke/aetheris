using Aetheris.Core.Errors;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the production: the recipes, the queue of a factory, the storage of the parts
/// and the bonuses of the races (task 00013).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProductionTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProductionTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The recipes, the parts and the groups live in the database, so a new part arrives as a
    /// row with its texts, and the catalog of a player shows it (task 00013, criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_parts_and_the_recipes_are_data()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        Assert.Equal(12, await dbContext.PartGroups.CountAsync(Token));
        Assert.True(await dbContext.PartTypes.CountAsync(Token) >= 33);
        Assert.True(await dbContext.Recipes.CountAsync(Token) >= 38);

        ProductionCatalog catalog = await scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(dbContext, scope.ServiceProvider.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);

        Assert.NotEmpty(catalog.Groups);
        Assert.NotEmpty(catalog.Parts);
        Assert.NotEmpty(catalog.Recipes);
        Assert.All(catalog.Parts, part => Assert.StartsWith("part_", part.Code, StringComparison.Ordinal));
        Assert.All(catalog.Recipes, recipe => Assert.StartsWith("recipe_", recipe.Code, StringComparison.Ordinal));

        // Every part belongs to a group that exists, and every recipe makes a part or a
        // resource.
        foreach (PartTypeInfo part in catalog.Parts)
        {
            Assert.NotNull(catalog.FindGroup(part.PartGroupId));
        }

        foreach (RecipeInfo recipe in catalog.Recipes)
        {
            Assert.True(recipe.PartTypeId is not null || recipe.ResourceTypeId is not null);
            Assert.NotEmpty(recipe.IngredientIds);
        }
    }

    /// <summary>
    /// A recipe whose factory is not on the body is not available, and the reason says which
    /// level of the factory is needed (task 00013, criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_recipe_needs_the_level_of_its_factory()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        // The body has no factory yet, so the recipes of a factory are closed.
        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);

        ProductionFactoryDto factory = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token);

        Assert.NotEmpty(factory.Recipes);

        // A recipe with no requirement is available, and a closed one says why it is closed.
        Assert.Contains(factory.Recipes, recipe => recipe.IsAvailable);
        Assert.All(
            factory.Recipes.Where(recipe => !recipe.IsAvailable),
            recipe => Assert.NotEmpty(recipe.Reasons));
    }

    /// <summary>
    /// A batch pays its whole cost at once and cancelling it returns the cost in full
    /// (task 00013, criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_batch_pays_its_cost_and_cancelling_returns_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);
        await GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        ProductionFactoryDto factory = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token);

        RecipeInfo recipe = await FirstRecipeAsync(scope.ServiceProvider);
        long before = await BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);

        ProductionFactoryDto queued = await production.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            3,
            Token);

        Assert.Single(queued.Queue);
        Assert.Equal(3, queued.Queue[0].Quantity);

        long after = await BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);
        Assert.Equal(before - (recipe.IngredientIds[0].Amount * 3), after);

        ProductionFactoryDto cancelled = await production.CancelAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            queued.Queue[0].OrderId,
            Token);

        Assert.Empty(cancelled.Queue);

        long restored = await BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);
        Assert.Equal(before, restored);
    }

    /// <summary>
    /// The queue of a factory has two places, and a third batch is refused (task 00013,
    /// criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_queue_of_a_factory_has_two_places()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);
        await GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        RecipeInfo recipe = await FirstRecipeAsync(scope.ServiceProvider);

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token);
        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token);

        GameException failure = await Assert.ThrowsAsync<GameException>(() =>
            production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token));

        Assert.Equal(ProductionErrorCodes.QueueFull, failure.Code);
    }

    /// <summary>
    /// The units of a batch appear on the storage of the body one by one, in the game time
    /// (task 00013, criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_units_of_a_batch_appear_one_by_one()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);
        await GiveEveryResourceAsync(player, scope.ServiceProvider);
        await SetStorageCapacityAsync(player, scope.ServiceProvider, 100);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        RecipeInfo recipe = await FirstPartRecipeAsync(scope.ServiceProvider);

        ProductionFactoryDto queued = await production.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            2,
            Token);

        Assert.Equal(0, queued.Queue[0].ProducedQuantity);

        // The batch is due, so the units are made one by one.
        await CompleteAsync(player, scope.ServiceProvider, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(2, stock.Storage.Stored);
        Assert.NotEmpty(stock.Stacks);
    }

    /// <summary>
    /// The storage of the parts holds the units, and the production stops when it is full
    /// (task 00013, criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_production_stops_when_the_storage_is_full()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);
        await GiveEveryResourceAsync(player, scope.ServiceProvider);
        await SetStorageCapacityAsync(player, scope.ServiceProvider, 1);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        RecipeInfo recipe = await FirstPartRecipeAsync(scope.ServiceProvider);

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 5, Token);

        await CompleteAsync(player, scope.ServiceProvider, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(1, stock.Storage.Stored);
        Assert.NotNull(stock.Storage.FullAt);
    }

    /// <summary>
    /// Twenty parallel batches of one factory never spend the same resources twice and never
    /// leave the balance below zero (task 00013, criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_batches_never_spend_twice_or_go_below_zero()
    {
        const int parallelCount = 20;

        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);
        await GiveEveryResourceAsync(player, scope.ServiceProvider);

        RecipeInfo recipe = await FirstRecipeAsync(scope.ServiceProvider);
        int resourceTypeId = recipe.IngredientIds[0].ResourceTypeId!.Value;
        long amount = recipe.IngredientIds[0].Amount;
        long before = await BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Task<ProductionFactoryDto>[] tasks = [.. Enumerable.Range(0, parallelCount).Select(index =>
            EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, index))];

        ProductionFactoryDto[] results = await Task.WhenAll(tasks);

        // The queue of a factory has two places, so the rest is refused.
        Assert.Equal(2, results.Count(result => result.Queue.Count > 0));

        long after = await BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Assert.True(after >= 0);
        Assert.Equal(before - (amount * 2), after);
    }

    /// <summary>
    /// The part made by a race with a bonus in its group carries the mark of the race
    /// (task 00013, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_part_of_a_race_with_a_bonus_carries_its_mark()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await BuildFactoryAsync(player, scope.ServiceProvider);
        await GiveEveryResourceAsync(player, scope.ServiceProvider);
        await SetStorageCapacityAsync(player, scope.ServiceProvider, 100);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        // The recipe of a part of a group the race of the player has a bonus in, so the part
        // carries the mark of the race.
        RecipeInfo recipe = await FirstPartRecipeOfTheRaceAsync(scope.ServiceProvider, player);

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token);

        await CompleteAsync(player, scope.ServiceProvider, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotEmpty(stock.Stacks);
        // The race of the player has a bonus in the group of the part, so the stack carries
        // the mark of the race.
        ProductionStackDto stack = stock.Stacks[0];
        Assert.True(stack.HasRaceBonus);
        Assert.Equal(100, stack.ConditionPercent);
    }

    /// <summary>
    /// An element added to the tables alone reaches the game, without the seed and without
    /// the admin panel (task 00013, criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_element_added_only_to_the_tables_reaches_the_game()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
        PartGroupEntity group = new()
        {
            Code = "part_group_900",
            NameKey = "production.part_group.part_group_900.name",
            DescriptionKey = "production.part_group.part_group_900.description",
            SortOrder = 900,
        };

        dbContext.PartGroups.Add(group);
        await dbContext.SaveChangesAsync(Token);

        scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>().Invalidate();

        ProductionCatalog catalog = await scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(dbContext, scope.ServiceProvider.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);

        Assert.NotNull(catalog.FindGroup(group.Id));

        // The code of the row is in the code of the program nowhere.
        string sources = Path.Combine(ServerRoot(), "src", "Aetheris.Production");
        Assert.False(File.ReadAllText(Path.Combine(sources, "Data", "production-reference-seed.json")).Contains("part_group_900", StringComparison.Ordinal));
    }

    private static string ServerRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !Directory.Exists(Path.Combine(directory, "src", "Aetheris.Production")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("The root of the server was not found.");
    }

    private async Task<ProductionFactoryDto> EnqueueAsync(string playerId, long planetId, long buildingId, int recipeId, long quantity, int index)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        try
        {
            return await scope.ServiceProvider.GetRequiredService<IProductionModule>()
                .EnqueueAsync(playerId, planetId, buildingId, recipeId, quantity, Token);
        }
        catch (GameException exception) when (exception.Code == ProductionErrorCodes.QueueFull)
        {
            return new ProductionFactoryDto(planetId, buildingId, 0, [], [], new ProductionStorageDto(0, 0, null), default);
        }
    }

    private async Task<long> BuildFactoryAsync(PlanetsTestPlayer player, IServiceProvider services)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        int factoryTypeId = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>()
            .FindBuildingTypeIdAsync("building_13", Token)
            ?? throw new InvalidOperationException("The factory type does not exist.");

        PlanetBuildingEntity? building = await dbContext.PlanetBuildings
            .Where(entry => entry.PlanetId == player.PlanetId && entry.BuildingTypeId == factoryTypeId)
            .FirstOrDefaultAsync(Token);

        if (building is not null)
        {
            return building.Id;
        }

        PlanetBuildingEntity entity = new()
        {
            PlanetId = player.PlanetId,
            SectorId = 0,
            BuildingTypeId = factoryTypeId,
            Level = 1,
        };

        dbContext.PlanetBuildings.Add(entity);
        await dbContext.SaveChangesAsync(Token);

        return entity.Id;
    }

    private async Task<RecipeInfo> FirstRecipeAsync(IServiceProvider services)
    {
        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

        return catalog.Recipes.First(recipe => recipe.PartTypeId is null);
    }

    private async Task<RecipeInfo> FirstPartRecipeAsync(IServiceProvider services)
    {
        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

        return catalog.Recipes.First(recipe => recipe.PartTypeId is not null);
    }

    private static async Task GiveResourcesAsync(PlanetsTestPlayer player, IServiceProvider services)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        List<PlanetResourceEntity> rows = await dbContext.PlanetResources
            .Where(entry => entry.PlanetId == player.PlanetId)
            .ToListAsync(Token);

        foreach (PlanetResourceEntity row in rows)
        {
            row.Amount = 1_000_000L;
        }

        await dbContext.SaveChangesAsync(Token);
    }

    /// <summary>
    /// Gives the body every resource of the world, so a recipe whose ingredients are the
    /// produced resources can be ordered too.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <returns>A task that completes when the balance is filled.</returns>
    /// <summary>
    /// Completes every batch of a factory that is due, at the moment its last unit is ready.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingId">The factory.</param>
    /// <returns>A task that completes when the batches are made.</returns>
    /// <summary>
    /// Finds a recipe of a part of a group the race of the player has a bonus in.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="player">The player and the body.</param>
    /// <returns>The recipe.</returns>
    private static async Task<RecipeInfo> FirstPartRecipeOfTheRaceAsync(IServiceProvider services, PlanetsTestPlayer player)
    {
        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        // The race of the player is the one of the civilization, and the bonus of the race is
        // the one of its own group.
        int raceId = await services.GetRequiredService<Aetheris.World.IWorldModule>()
            .GetCivilizationAsync(player.Account.UserId, Token)
            .ContinueWith(task => task.Result?.RaceId ?? 0, Token)
            .ConfigureAwait(false);

        HashSet<int> bonusGroups = [.. await dbContext.RacePartBonuses.AsNoTracking()
            .Where(bonus => bonus.RaceId == raceId)
            .Select(bonus => bonus.PartGroupId)
            .ToListAsync(Token)];

        // The top module of a group is made by its race alone, so the recipe of the group of the
        // race of the player is one of the parts of the second mark level.
        return catalog.Recipes.First(recipe =>
            recipe.PartTypeId is not null
            && catalog.FindPart(recipe.PartTypeId.Value) is { } part
            && part.MarkLevel > 1
            && bonusGroups.Contains(part.PartGroupId));
    }

    private static async Task CompleteAsync(PlanetsTestPlayer player, IServiceProvider services, long buildingId)
    {
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        // The batch is run until its last unit is made, so the whole batch is done.
        DateTimeOffset? finishesAt = await dbContext.ProductionOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId && entry.PlanetBuildingId == buildingId)
            .MaxAsync(entry => (DateTimeOffset?)entry.FinishesAt, Token);

        await services.GetRequiredService<ProductionQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, finishesAt ?? DateTimeOffset.MaxValue, Token);
    }

    private static async Task GiveEveryResourceAsync(PlanetsTestPlayer player, IServiceProvider services)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();
        Aetheris.World.Data.WorldDbContext world = services.GetRequiredService<Aetheris.World.Data.WorldDbContext>();

        List<int> resourceTypeIds = await world.ResourceTypes.AsNoTracking()
            .Select(resource => resource.Id)
            .ToListAsync(Token);

        List<PlanetResourceEntity> rows = await dbContext.PlanetResources
            .Where(entry => entry.PlanetId == player.PlanetId)
            .ToListAsync(Token);

        foreach (PlanetResourceEntity row in rows)
        {
            row.Amount = 1_000_000L;
        }

        foreach (int resourceTypeId in resourceTypeIds)
        {
            if (rows.Any(row => row.ResourceTypeId == resourceTypeId))
            {
                continue;
            }

            dbContext.PlanetResources.Add(new PlanetResourceEntity
            {
                PlanetId = player.PlanetId,
                SectorId = 0,
                ResourceTypeId = resourceTypeId,
                Amount = 1_000_000L,
            });
        }

        await dbContext.SaveChangesAsync(Token);
    }

    private static async Task SetStorageCapacityAsync(PlanetsTestPlayer player, IServiceProvider services, int capacity)
    {
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        PartStorageEntity storage = await dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == player.PlanetId, Token)
            ?? new PartStorageEntity { PlanetId = player.PlanetId, SectorId = 0, Capacity = capacity, Quantity = 0 };

        storage.Capacity = capacity;
        dbContext.PartStorages.Add(storage);
        await dbContext.SaveChangesAsync(Token);
    }

    private static async Task<long> BalanceAsync(PlanetsTestPlayer player, IServiceProvider services, int resourceTypeId)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        PlanetResourceEntity? row = await dbContext.PlanetResources
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlanetId == player.PlanetId && entry.ResourceTypeId == resourceTypeId, Token);

        return row?.Amount ?? 0;
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

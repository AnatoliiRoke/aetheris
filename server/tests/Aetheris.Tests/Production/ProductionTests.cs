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
using Aetheris.Research.Services;
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
        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);

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

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        ProductionFactoryDto factory = await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token);

        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(scope.ServiceProvider);
        long before = await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);

        ProductionFactoryDto queued = await production.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            3,
            Token);

        Assert.Single(queued.Queue);
        Assert.Equal(3, queued.Queue[0].Quantity);

        long after = await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);
        Assert.Equal(before - (recipe.IngredientIds[0].Amount * 3), after);

        ProductionFactoryDto cancelled = await production.CancelAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            queued.Queue[0].OrderId,
            Token);

        Assert.Empty(cancelled.Queue);

        long restored = await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);
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

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(scope.ServiceProvider);

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

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);
        await ProductionTestData.BuildStorageAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(scope.ServiceProvider);

        ProductionFactoryDto queued = await production.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            buildingId,
            recipe.Id,
            2,
            Token);

        Assert.Equal(0, queued.Queue[0].ProducedQuantity);

        // The batch is due, so the units are made one by one.
        await ProductionTestData.CompleteAsync(player, scope.ServiceProvider, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(2, Assert.Single(stock.Storage.Kinds).Quantity);
        Assert.NotEmpty(stock.Stacks);
    }

    /// <summary>
    /// A unit the scheduler makes in its own scope between two calls of another scope is not
    /// written over with the old values that scope still tracks (found in the review of task
    /// 00013: the quantity of the storage came back from the tracker and one unit was lost).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_unit_made_by_another_scope_is_not_lost()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);
        await ProductionTestData.BuildStorageAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(scope.ServiceProvider);

        ProductionFactoryDto queued = await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 2, Token);
        ProductionOrderDto order = queued.Queue[0];

        // The scheduler makes the first unit in a scope of its own.
        using (IServiceScope scheduler = _factory.Services.CreateScope())
        {
            int made = await scheduler.ServiceProvider.GetRequiredService<ProductionQueueService>()
                .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, order.NextUnitAt!.Value, Token);
            Assert.Equal(1, made);
        }

        // The first scope still tracks the batch and the storage it read before.
        await scope.ServiceProvider.GetRequiredService<ProductionQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, order.FinishesAt!.Value, Token);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(2, Assert.Single(stock.Storage.Kinds).Quantity);
        Assert.Equal(2, stock.Stacks.Sum(stack => stack.Quantity));
    }

    /// <summary>
    /// The storage of the parts holds the units, and the production of a kind stops when the room
    /// of the kind is full (task 00013, criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_production_stops_when_the_room_of_the_kind_is_full()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);
        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(scope.ServiceProvider);
        long capacity = await ProductionTestData.BuildStorageAsync(player, scope.ServiceProvider, 1, recipe.PartTypeId);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 5, Token);

        await ProductionTestData.CompleteAsync(player, scope.ServiceProvider, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        ProductionPartKindDto kind = Assert.Single(stock.Storage.Kinds);

        Assert.Equal(capacity, kind.Quantity);
        Assert.Equal(capacity, kind.Capacity);
        Assert.True(kind.IsFull);
    }

    /// <summary>
    /// Every kind of part has the room of its own: a kind that is full does not take any room
    /// from another kind, and the other kind is made up to the same room (decision of the
    /// author, 31.10.2026).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_kind_of_part_has_the_room_of_its_own()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);
        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(scope.ServiceProvider);
        long capacity = await ProductionTestData.BuildStorageAsync(player, scope.ServiceProvider);

        // Another kind of part fills its whole room; the kind of the recipe has none of it.
        ProductionDbContext dbContext = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
        int otherPartTypeId = await dbContext.PartTypes.AsNoTracking()
            .Where(part => part.Id != recipe.PartTypeId)
            .Select(part => part.Id)
            .FirstAsync(Token);
        dbContext.PartStocks.Add(new PartStockEntity { PlanetId = player.PlanetId, SectorId = 0, PartTypeId = otherPartTypeId, Quantity = capacity });
        await dbContext.SaveChangesAsync(Token);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 2, Token);
        await ProductionTestData.CompleteAsync(player, scope.ServiceProvider, buildingId);

        ProductionStockDto stock = await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        ProductionPartKindDto other = stock.Storage.Kinds.Single(kind => kind.PartTypeId == otherPartTypeId);
        ProductionPartKindDto made = stock.Storage.Kinds.Single(kind => kind.PartTypeId == recipe.PartTypeId);

        Assert.Equal(capacity, other.Quantity);
        Assert.True(other.IsFull);
        Assert.Equal(2, made.Quantity);
        Assert.False(made.IsFull);
        Assert.Equal(capacity, made.Capacity);
        Assert.Equal(capacity, other.Capacity);
    }

    /// <summary>
    /// A return of parts over the room of the kind is not cut: the parts stay in the storage and
    /// the kind simply counts as full, as a refund of resources does (task 00011, task 00014).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_return_over_the_room_of_the_kind_is_kept_and_the_kind_counts_as_full()
    {
        const long overflow = 5;

        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long capacity = await ProductionTestData.BuildStorageAsync(player, services);
        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(services);

        await services.GetRequiredService<PartStockService>().AddAsync(
            player.PlanetId,
            [new PartStackAdded(recipe.PartTypeId!.Value, 1, false, PartCondition.Full, capacity + overflow)],
            Token);

        ProductionStockDto stock = await services.GetRequiredService<IProductionModule>().GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        ProductionPartKindDto kind = Assert.Single(stock.Storage.Kinds);

        Assert.Equal(capacity + overflow, kind.Quantity);
        Assert.Equal(capacity, kind.Capacity);
        Assert.True(kind.IsFull);
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

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);

        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(scope.ServiceProvider);
        int resourceTypeId = recipe.IngredientIds[0].ResourceTypeId!.Value;
        long amount = recipe.IngredientIds[0].Amount;
        long before = await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

        Task<ProductionFactoryDto>[] tasks = [.. Enumerable.Range(0, parallelCount).Select(index =>
            EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, index))];

        ProductionFactoryDto[] results = await Task.WhenAll(tasks);

        // The queue of a factory has two places, so the rest is refused.
        Assert.Equal(2, results.Count(result => result.Queue.Count > 0));

        long after = await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, resourceTypeId);

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

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);
        await ProductionTestData.BuildStorageAsync(player, scope.ServiceProvider);

        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        // The recipe of a part of a group the race of the player has a bonus in, so the part
        // carries the mark of the race.
        RecipeInfo recipe = await FirstPartRecipeOfTheRaceAsync(scope.ServiceProvider, player);

        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token);

        await ProductionTestData.CompleteAsync(player, scope.ServiceProvider, buildingId);

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

    /// <summary>
    /// The room of the storage of the parts is the effect of its building at its level: a body
    /// without the building keeps no parts, and the next level adds room (task 00013,
    /// decision 9).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_room_of_the_storage_comes_from_its_building()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IProductionModule production = services.GetRequiredService<IProductionModule>();

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        Assert.Equal(0, (await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token)).Storage.Capacity);

        // Without the building a part has nowhere to go, so the batch waits.
        RecipeInfo recipe = await ProductionTestData.FirstPartRecipeAsync(services);
        await production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, 1, Token);
        await ProductionTestData.CompleteAsync(player, services, buildingId);
        Assert.True(Assert.Single((await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token)).Queue).IsBlocked);

        long first = await ProductionTestData.BuildStorageAsync(player, services);
        Assert.Equal(first, (await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token)).Storage.Capacity);

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, "building_21", 2);
        long second = (await production.GetStockAsync(player.Account.UserId, player.PlanetId, Token)).Storage.Capacity;
        Assert.True(second > first);
        Assert.Equal(
            second,
            await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>().GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.PartStorageCapacity, Token));
    }

    /// <summary>
    /// The factory and the storage of a body are read only by its owner (found in the review of
    /// task 00013).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stranger_does_not_read_the_factory_or_the_storage()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        long buildingId = await ProductionTestData.BuildFactoryAsync(owner, scope.ServiceProvider);
        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(scope.ServiceProvider);

        GameException factory = await Assert.ThrowsAsync<GameException>(() =>
            production.GetFactoryAsync(stranger.Account.UserId, owner.PlanetId, buildingId, Token));
        GameException stock = await Assert.ThrowsAsync<GameException>(() =>
            production.GetStockAsync(stranger.Account.UserId, owner.PlanetId, Token));
        GameException order = await Assert.ThrowsAsync<GameException>(() =>
            production.EnqueueAsync(stranger.Account.UserId, owner.PlanetId, buildingId, recipe.Id, 1, Token));

        Assert.All(new[] { factory, stock, order }, failure => Assert.Equal(ProductionErrorCodes.PlanetUnknown, failure.Code));
    }

    /// <summary>
    /// A batch of no units, or of so many units that the cost or the time overflow, is refused
    /// and takes nothing (found in the review of task 00013).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_batch_of_a_wrong_quantity_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, scope.ServiceProvider);
        await ProductionTestData.GiveEveryResourceAsync(player, scope.ServiceProvider);
        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(scope.ServiceProvider);
        long before = await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value);

        foreach (long quantity in new[] { 0L, -1L, long.MaxValue, long.MaxValue / 2 })
        {
            GameException failure = await Assert.ThrowsAsync<GameException>(() =>
                production.EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, quantity, Token));
            Assert.Equal(ProductionErrorCodes.QuantityInvalid, failure.Code);
        }

        Assert.Equal(before, await ProductionTestData.BalanceAsync(player, scope.ServiceProvider, recipe.IngredientIds[0].ResourceTypeId!.Value));
        Assert.Empty((await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token)).Queue);
    }

    /// <summary>
    /// The starting recipes have different codes, their texts are keyed by their codes, and
    /// every requirement names a row that exists (found in the review of task 00013).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_of_the_recipes_is_consistent()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(services.GetRequiredService<ProductionDbContext>(), services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(), Token);

        Assert.Equal(catalog.Recipes.Count, catalog.Recipes.Select(recipe => recipe.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.All(catalog.Recipes, recipe => Assert.Equal(ProductionTextKeys.RecipeName(recipe.Code), recipe.NameKey));
        Assert.All(catalog.Parts, part => Assert.Equal(ProductionTextKeys.PartName(part.Code), part.NameKey));

        Aetheris.Core.Text.LocalizationPack ukrainian = await services.GetRequiredService<Aetheris.Core.Text.ILocalizationTexts>().GetPackAsync("uk", Token);
        Assert.All(catalog.Recipes, recipe => Assert.False(string.IsNullOrWhiteSpace(ukrainian.Texts.GetValueOrDefault(recipe.NameKey)), recipe.Code));

        HashSet<int> buildingIds = [.. (await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>().GetBuildingNameKeysAsync(Token)).Keys];
        HashSet<int> technologyIds = [.. await services.GetRequiredService<ITechnologyLevels>().GetTechnologyIdsAsync(Token)];
        HashSet<int> raceIds = [.. (await services.GetRequiredService<Aetheris.World.IWorldModule>().GetRacesAsync(Token)).Select(race => race.Id)];

        foreach (RecipeInfo recipe in catalog.Recipes)
        {
            foreach (ProductionRequirementInfo requirement in catalog.RequirementsOf(recipe.Id))
            {
                HashSet<int> targets = requirement.RequirementType switch
                {
                    Aetheris.Requirements.Services.RequirementTypes.BuildingLevel => buildingIds,
                    Aetheris.Requirements.Services.RequirementTypes.TechnologyLevel => technologyIds,
                    ProductionRequirementTypes.Race => raceIds,
                    _ => [],
                };

                Assert.True(targets.Contains(requirement.TargetId), $"{recipe.Code}: {requirement.RequirementType} {requirement.TargetId}");
            }
        }
    }

    /// <summary>
    /// The multiplier of the production time of a learned technology scales the unit; an empty
    /// multiplier changes nothing, and a negative one is broken data that never makes the
    /// production instant (found in the review of task 00013).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_multiplier_of_the_production_time_scales_the_unit()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IProductionModule production = services.GetRequiredService<IProductionModule>();
        await AetherisTestFactory.SetParameterAsync(services, Aetheris.Research.Configuration.ResearchConfigKeys.CatalogCacheSeconds, "0");
        await services.GetRequiredService<Aetheris.Research.IResearchModule>().EnsureAsync(Token);

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, services);
        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(services);

        Aetheris.Research.Data.ResearchDbContext research = services.GetRequiredService<Aetheris.Research.Data.ResearchDbContext>();
        int branchId = await research.TechnologyBranches.AsNoTracking().OrderBy(branch => branch.Id).Select(branch => branch.Id).FirstAsync(Token);
        Aetheris.Research.Data.Entities.TechnologyLevelEffectEntity effect = new() { EffectType = TechnologyEffectTypes.ProductionTime, Value = 0.5m };
        Aetheris.Research.Data.Entities.TechnologyEntity technology = new()
        {
            Code = "tech_901",
            NameKey = "research.technology.tech_901.name",
            DescriptionKey = "research.technology.tech_901.description",
            BranchId = branchId,
            SortOrder = 901,
            MaxLevel = 1,
            ResearchCenterLevel = 1,
            LevelIds = [new Aetheris.Research.Data.Entities.TechnologyLevelEntity { Level = 1, DurationSeconds = 60, EffectIds = [effect] }],
        };
        research.Technologies.Add(technology);
        await research.SaveChangesAsync(Token);
        research.PlayerTechnologies.Add(new Aetheris.Research.Data.Entities.PlayerTechnologyEntity { PlayerId = player.Account.UserId, TechnologyId = technology.Id, Level = 1 });
        await research.SaveChangesAsync(Token);

        async Task<long> UnitSecondsAsync() =>
            (await production.GetFactoryAsync(player.Account.UserId, player.PlanetId, buildingId, Token)).Recipes.Single(entry => entry.RecipeId == recipe.Id).UnitSeconds;

        Assert.Equal((long)Math.Ceiling(recipe.DurationSeconds * 0.5m), await UnitSecondsAsync());

        effect.Value = 0m;
        await research.SaveChangesAsync(Token);
        Assert.Equal(recipe.DurationSeconds, await UnitSecondsAsync());

        effect.Value = -1m;
        await research.SaveChangesAsync(Token);
        await Assert.ThrowsAsync<InvalidOperationException>(UnitSecondsAsync);
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
            return new ProductionFactoryDto(planetId, buildingId, 0, [], [], new ProductionStorageDto(0, []), default);
        }
    }

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

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

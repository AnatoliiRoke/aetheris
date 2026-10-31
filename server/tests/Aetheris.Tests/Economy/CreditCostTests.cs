using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Economy.Services;
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
using Aetheris.Research;
using Aetheris.Research.Api;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.Production;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of a cost with credits in the real queues of the construction, the research and the
/// production: the credits come from the balance of the civilization and the other resources
/// from the body. The rows of the cost with credits are written straight into the tables, as the
/// admin panel writes them, so no code of a queue is changed for a currency (task 00015,
/// criterion 4).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CreditCostTests
{
    private const long CreditsCost = 300;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreditCostTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CreditCostTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A building whose level costs iron and credits takes the iron from the body and the credits
    /// from the civilization, and the cancel gives both back to the same balances.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_building_pays_its_credits_from_the_civilization_and_the_cancel_returns_them()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        (int ironId, int creditsId) = await IdsAsync(services);

        int buildingTypeId = await AddBuildingAsync(services, ironId, creditsId);
        DateTimeOffset now = PlanetsTestData.GameNow(services);

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [ironId] = 100 }, now);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 1000, now);

        PlanetOverviewDto queued = await InScopeAsync(call => call.GetRequiredService<ConstructionService>().EnqueueAsync(player.PlanetId, buildingTypeId, now, Token));

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Dictionary<int, long> planet = await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId);

            // The iron is taken from the body, the credits from the civilization, and the body
            // gets no row of the credits.
            Assert.Equal(95, planet[ironId]);
            Assert.False(planet.ContainsKey(creditsId));
            Assert.Equal(1000 - CreditsCost, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
        }

        await InScopeAsync(call => call.GetRequiredService<ConstructionService>().CancelAsync(player.PlanetId, queued.Queue.Items.Single().Id, now.AddSeconds(1), Token));

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Dictionary<int, long> planet = await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId);

            // The iron returns to the body and the credits to the balance of the civilization.
            Assert.Equal(100, planet[ironId]);
            Assert.False(planet.ContainsKey(creditsId));
            Assert.Equal(1000, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
        }
    }

    /// <summary>
    /// A building is refused when the civilization has too few credits, and nothing is taken
    /// from the body or from the balance.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_building_is_refused_when_the_civilization_lacks_the_credits()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        (int ironId, int creditsId) = await IdsAsync(services);

        int buildingTypeId = await AddBuildingAsync(services, ironId, creditsId);
        DateTimeOffset now = PlanetsTestData.GameNow(services);

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [ironId] = 100 }, now);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, CreditsCost - 1, now);

        GameException refused = await Assert.ThrowsAsync<GameException>(
            () => InScopeAsync(call => call.GetRequiredService<ConstructionService>().EnqueueAsync(player.PlanetId, buildingTypeId, now, Token)));

        // The shortage of the credits is a shortage either way: of the balance of the civilization or
        // of the resources of the cost.
        Assert.Contains(refused.Code, new[] { EconomyErrorCodes.CurrencyShort, PlanetsErrorCodes.ResourcesShort });

        using IServiceScope check = _factory.Services.CreateScope();

        Assert.Equal(100, (await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId))[ironId]);
        Assert.Equal(CreditsCost - 1, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
        Assert.Empty(await check.ServiceProvider.GetRequiredService<PlanetsDbContext>().QueueItems.AsNoTracking().ToListAsync(Token));
    }

    /// <summary>
    /// A research whose level costs iron and credits takes the iron from the body and the credits
    /// from the civilization; a refusal for the credits takes nothing, and the cancel gives both
    /// back.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_research_pays_its_credits_from_the_civilization()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        (int ironId, int creditsId) = await IdsAsync(services);

        int technologyId = await AddTechnologyAsync(services, ironId, creditsId);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);

        DateTimeOffset now = PlanetsTestData.GameNow(services);

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [ironId] = 100 }, now);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 100, now);

        // The civilization has 100 credits and the level costs 300: the refusal takes nothing.
        GameException refused = await Assert.ThrowsAsync<GameException>(
            () => InScopeAsync(call => call.GetRequiredService<IResearchModule>().EnqueueAsync(player.Account.UserId, player.PlanetId, technologyId, 1, Token)));

        Assert.Equal(EconomyErrorCodes.CurrencyShort, refused.Code);

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Assert.Equal(100, (await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId))[ironId]);
            Assert.Equal(100, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
            Assert.Empty((await check.ServiceProvider.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, player.PlanetId, Token)).Queue);
        }

        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 1000, now);

        ResearchTreeDto queued = await InScopeAsync(call => call.GetRequiredService<IResearchModule>().EnqueueAsync(player.Account.UserId, player.PlanetId, technologyId, 1, Token));

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Dictionary<int, long> planet = await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId);

            Assert.Equal(90, planet[ironId]);
            Assert.False(planet.ContainsKey(creditsId));
            Assert.Equal(1000 - CreditsCost, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
        }

        await InScopeAsync(call => call.GetRequiredService<IResearchModule>().CancelAsync(player.Account.UserId, queued.Queue.Single().Id, Token));

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Dictionary<int, long> planet = await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId);

            Assert.Equal(100, planet[ironId]);
            Assert.False(planet.ContainsKey(creditsId));
            Assert.Equal(1000, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
        }
    }

    /// <summary>
    /// A batch of a recipe with credits among its ingredients takes the credits of every unit
    /// from the civilization and the other ingredients from the body; a refusal for the credits
    /// takes nothing, and the cancel gives everything back.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_batch_pays_its_credits_from_the_civilization()
    {
        const long creditsPerUnit = 60;
        const long quantity = 3;

        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        int creditsId = await EconomyTestData.CreditsIdAsync(services);

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);

        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(services);
        int ingredientId = recipe.IngredientIds[0].ResourceTypeId!.Value;

        await AddCreditsIngredientAsync(services, recipe.Id, creditsId, creditsPerUnit);

        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;
        long planetCredits = await ProductionTestData.BalanceAsync(player, services, creditsId);

        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, creditsPerUnit, now);

        long ingredientBefore = await ProductionTestData.BalanceAsync(player, services, ingredientId);

        // The civilization has the credits for one unit and the batch needs three: refused.
        GameException refused = await Assert.ThrowsAsync<GameException>(
            () => InScopeAsync(call => call.GetRequiredService<IProductionModule>().EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, quantity, Token)));

        Assert.Equal(EconomyErrorCodes.CurrencyShort, refused.Code);

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Assert.Equal(ingredientBefore, await ProductionTestData.BalanceAsync(player, check.ServiceProvider, ingredientId));
            Assert.Equal(creditsPerUnit, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
            Assert.Empty(await check.ServiceProvider.GetRequiredService<ProductionDbContext>().ProductionOrders.AsNoTracking().ToListAsync(Token));
        }

        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 1000, now);

        ProductionFactoryDto queued = await InScopeAsync(call => call.GetRequiredService<IProductionModule>().EnqueueAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id, quantity, Token));

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            // The credits of three units are taken from the civilization; the credits the body
            // had are untouched, and the ingredient of the body is taken as before.
            Assert.Equal(1000 - (creditsPerUnit * quantity), await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
            Assert.Equal(planetCredits, await ProductionTestData.BalanceAsync(player, check.ServiceProvider, creditsId));
            Assert.Equal(ingredientBefore - (recipe.IngredientIds[0].Amount * quantity), await ProductionTestData.BalanceAsync(player, check.ServiceProvider, ingredientId));
        }

        await InScopeAsync(call => call.GetRequiredService<IProductionModule>().CancelAsync(player.Account.UserId, player.PlanetId, buildingId, queued.Queue.Single().OrderId, Token));

        using (IServiceScope check = _factory.Services.CreateScope())
        {
            Assert.Equal(1000, await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId));
            Assert.Equal(planetCredits, await ProductionTestData.BalanceAsync(player, check.ServiceProvider, creditsId));
            Assert.Equal(ingredientBefore, await ProductionTestData.BalanceAsync(player, check.ServiceProvider, ingredientId));
        }
    }

    /// <summary>
    /// Twenty parallel batches that cost credits never take the balance of the civilization below
    /// zero and never take the same credits twice: exactly as many batches as the credits pay for
    /// are made, and the rest are refused for the credits.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_batches_never_take_the_balance_below_zero()
    {
        const int parallelCount = 20;
        const long creditsPerUnit = 10;
        const long balance = 100;

        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        int creditsId = await EconomyTestData.CreditsIdAsync(services);

        // The queue of the factory has room for every batch, so only the credits limit them.
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.QueueBaseSlots, "50");

        long buildingId = await ProductionTestData.BuildFactoryAsync(player, services);
        await ProductionTestData.GiveEveryResourceAsync(player, services);

        RecipeInfo recipe = await ProductionTestData.FirstRecipeAsync(services);

        await AddCreditsIngredientAsync(services, recipe.Id, creditsId, creditsPerUnit);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, balance, services.GetRequiredService<IGameClock>().GameNow);

        string?[] outcomes = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(_ => EnqueueOneAsync(player.Account.UserId, player.PlanetId, buildingId, recipe.Id)));

        int made = outcomes.Count(outcome => outcome is null);

        using IServiceScope check = _factory.Services.CreateScope();

        long left = await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId);
        int orders = await check.ServiceProvider.GetRequiredService<ProductionDbContext>().ProductionOrders.AsNoTracking().CountAsync(Token);

        // The hundred credits pay for ten batches of ten, and the balance is empty, never negative.
        Assert.Equal(balance / creditsPerUnit, made);
        Assert.Equal(made, orders);
        Assert.Equal(0, left);
        Assert.All(outcomes.Where(outcome => outcome is not null), outcome => Assert.Equal(EconomyErrorCodes.CurrencyShort, outcome));
    }

    private static async Task<(int IronId, int CreditsId)> IdsAsync(IServiceProvider services) =>
        (await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron), await EconomyTestData.CreditsIdAsync(services));

    /// <summary>
    /// Writes a building of one level into the tables, as the admin panel does: its cost is 5 iron
    /// and 300 credits, and it has no requirements.
    /// </summary>
    private static async Task<int> AddBuildingAsync(IServiceProvider services, int ironId, int creditsId)
    {
        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        BuildingTypeEntity building = new()
        {
            Code = "building_900",
            NameKey = "planets.building.building_900.name",
            DescriptionKey = "planets.building.building_900.description",
            SortOrder = 900,
            CategoryId = await planets.BuildingCategories.AsNoTracking().OrderBy(category => category.Id).Select(category => category.Id).FirstAsync(Token),
            LevelIds =
            [
                new BuildingLevelEntity
                {
                    Level = 1,
                    DurationSeconds = 60,
                    CostIds =
                    [
                        new BuildingLevelCostEntity { ResourceTypeId = ironId, Amount = 5 },
                        new BuildingLevelCostEntity { ResourceTypeId = creditsId, Amount = CreditsCost },
                    ],
                },
            ],
        };

        planets.BuildingTypes.Add(building);
        await planets.SaveChangesAsync(Token);

        return building.Id;
    }

    /// <summary>
    /// Writes a technology of one level into the tables, as the admin panel does: its cost is 10
    /// iron and 300 credits, and it needs the first level of the science centre.
    /// </summary>
    private static async Task<int> AddTechnologyAsync(IServiceProvider services, int ironId, int creditsId)
    {
        await AetherisTestFactory.SetParameterAsync(services, Aetheris.Research.Configuration.ResearchConfigKeys.CatalogCacheSeconds, "0");
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);

        ResearchDbContext research = services.GetRequiredService<ResearchDbContext>();
        int branchId = await research.TechnologyBranches.AsNoTracking().OrderBy(branch => branch.Id).Select(branch => branch.Id).FirstAsync(Token);

        TechnologyEntity technology = new()
        {
            Code = "tech_900",
            NameKey = "research.technology.tech_900.name",
            DescriptionKey = "research.technology.tech_900.description",
            BranchId = branchId,
            SortOrder = 900,
            MaxLevel = 1,
            ResearchCenterLevel = 1,
            LevelIds =
            [
                new TechnologyLevelEntity
                {
                    Level = 1,
                    DurationSeconds = 60,
                    CostIds =
                    [
                        new TechnologyLevelCostEntity { ResourceTypeId = ironId, Amount = 10 },
                        new TechnologyLevelCostEntity { ResourceTypeId = creditsId, Amount = CreditsCost },
                    ],
                },
            ],
        };

        research.Technologies.Add(technology);
        await research.SaveChangesAsync(Token);

        return technology.Id;
    }

    private static async Task AddCreditsIngredientAsync(IServiceProvider services, int recipeId, int creditsId, long amount)
    {
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();

        // The seed recipe already spends credits. The test names the amount itself.
        await production.Set<RecipeIngredientEntity>()
            .Where(ingredient => ingredient.RecipeId == recipeId && ingredient.ResourceTypeId == creditsId)
            .ExecuteDeleteAsync(Token);
        production.Set<RecipeIngredientEntity>().Add(new RecipeIngredientEntity { RecipeId = recipeId, ResourceTypeId = creditsId, Amount = amount });
        await production.SaveChangesAsync(Token);
        services.GetRequiredService<ProductionCatalogCache>().Invalidate();
    }

    /// <summary>
    /// Runs one command in its own scope, like a request of the game: a refused command leaves
    /// nothing tracked for the next one.
    /// </summary>
    private async Task<TResult> InScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> command)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await command(scope.ServiceProvider);
    }

    private async Task<string?> EnqueueOneAsync(string playerId, long planetId, long buildingId, int recipeId)
    {
        // Every parallel request has its own scope, like the requests of the game.
        using IServiceScope scope = _factory.Services.CreateScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<IProductionModule>()
                .EnqueueAsync(playerId, planetId, buildingId, recipeId, 1, Token);

            return null;
        }
        catch (GameException exception)
        {
            return exception.Code;
        }
    }

    private async Task ResetAsync()
    {
        await EconomyTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

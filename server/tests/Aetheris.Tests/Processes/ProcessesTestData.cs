using Aetheris.Core.Time;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Research;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Helpers of the tests of the processes: a building that runs a process on a body, its
/// balance, the storage of the parts, the technologies and the recipes of the catalog (task
/// 00014).
/// </summary>
internal static class ProcessesTestData
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Clears the database and prepares the modules the processes stand on, as a fresh server
    /// does at start: the bodies, the research, the production and the processes.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <returns>A task that completes when the starting data is written.</returns>
    internal static async Task ResetAsync(AetherisTestFactory factory)
    {
        await PlanetsTestData.ResetAsync(factory);

        using IServiceScope scope = factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ResearchConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.CatalogCacheSeconds, "0");
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IProcessesModule>().EnsureAsync(Token);
        services.GetRequiredService<ProcessCatalogCache>().Invalidate();
    }

    /// <summary>
    /// Puts the building of a class on the body at a level.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="level">The level of the building.</param>
    /// <returns>The building.</returns>
    internal static async Task<long> BuildAsync(
        PlanetsTestPlayer player,
        IServiceProvider services,
        string buildingClass,
        int level = 1)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        int buildingTypeId = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>()
            .FindBuildingTypeIdByClassAsync(buildingClass, Token)
            ?? throw new InvalidOperationException($"The building of the class '{buildingClass}' does not exist.");

        PlanetBuildingEntity? building = await dbContext.PlanetBuildings
            .Where(entry => entry.PlanetId == player.PlanetId && entry.BuildingTypeId == buildingTypeId)
            .FirstOrDefaultAsync(Token);

        if (building is null)
        {
            building = new PlanetBuildingEntity
            {
                PlanetId = player.PlanetId,
                SectorId = 0,
                BuildingTypeId = buildingTypeId,
                Level = level,
            };

            dbContext.PlanetBuildings.Add(building);
        }
        else
        {
            building.Level = level;
        }

        await dbContext.SaveChangesAsync(Token);

        return building.Id;
    }

    /// <summary>
    /// Completes every order of a building that is due, at the moment its last unit is ready.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingId">The building.</param>
    /// <returns>The number of the units that were made.</returns>
    internal static async Task<int> CompleteAsync(PlanetsTestPlayer player, IServiceProvider services, long buildingId)
    {
        int made = 0;

        // Every order starts when the one before it ends, so the queue is completed order by
        // order up to the end of its last one.
        while (await services.GetRequiredService<ProcessesDbContext>().ProcessOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId && entry.PlanetBuildingId == buildingId)
            .MaxAsync(entry => entry.FinishesAt, Token) is { } finishesAt)
        {
            made += await services.GetRequiredService<ProcessQueueService>()
                .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, finishesAt, Token);
        }

        return made;
    }

    /// <summary>
    /// Gives the body a large amount of every resource of the world. The amounts are fixed a
    /// day ahead of the game clock, so the mines of the body add nothing while the test runs
    /// and every change of the balance is the change of a process.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <returns>A task that completes when the balance is filled.</returns>
    internal static async Task GiveEveryResourceAsync(PlanetsTestPlayer player, IServiceProvider services)
    {
        Aetheris.World.Data.WorldDbContext world = services.GetRequiredService<Aetheris.World.Data.WorldDbContext>();

        Dictionary<int, long> amounts = await world.ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Id, _ => 1_000_000L, Token);

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, amounts, services.GetRequiredService<IGameClock>().GameNow.AddDays(1));
    }

    /// <summary>
    /// The balance of a resource of the body.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="resourceTypeId">The resource.</param>
    /// <returns>The balance.</returns>
    internal static async Task<long> BalanceAsync(
        PlanetsTestPlayer player,
        IServiceProvider services,
        int resourceTypeId)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        PlanetResourceEntity? row = await dbContext.PlanetResources.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlanetId == player.PlanetId && entry.ResourceTypeId == resourceTypeId, Token);

        return row?.Amount ?? 0;
    }

    /// <summary>
    /// Puts parts of a kind and of a condition into the storage of the body.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="partTypeId">The kind of the part.</param>
    /// <param name="conditionPercent">The condition of the parts, in per cent.</param>
    /// <param name="quantity">How many parts.</param>
    /// <returns>A task that completes when the parts are in the storage.</returns>
    internal static async Task PutPartsAsync(
        PlanetsTestPlayer player,
        IServiceProvider services,
        int partTypeId,
        int conditionPercent,
        long quantity)
    {
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        PartStorageEntity? storage = await dbContext.PartStorages.SingleOrDefaultAsync(entry => entry.PlanetId == player.PlanetId, Token);

        if (storage is null)
        {
            storage = new PartStorageEntity { PlanetId = player.PlanetId, SectorId = 0, Capacity = 0 };
            dbContext.PartStorages.Add(storage);
        }

        dbContext.PartStocks.Add(new PartStockEntity
        {
            PlanetId = player.PlanetId,
            SectorId = 0,
            PartTypeId = partTypeId,
            MarkLevel = PartMarkLevels.First,
            HasRaceBonus = false,
            ConditionPercent = conditionPercent,
            Quantity = quantity,
        });

        await dbContext.SaveChangesAsync(Token);
    }

    /// <summary>
    /// How many parts of a kind and of a condition the storage of the body holds.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="partTypeId">The kind of the part.</param>
    /// <param name="conditionPercent">The condition of the parts, in per cent.</param>
    /// <returns>The number of the parts.</returns>
    internal static Task<long> PartsAsync(
        PlanetsTestPlayer player,
        IServiceProvider services,
        int partTypeId,
        int conditionPercent) =>
        services.GetRequiredService<ProductionDbContext>().PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == player.PlanetId && stack.PartTypeId == partTypeId && stack.ConditionPercent == conditionPercent)
            .SumAsync(stack => stack.Quantity, Token);

    /// <summary>
    /// The facts of a part by its neutral code.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="code">The neutral code of the part.</param>
    /// <returns>The facts of the part.</returns>
    internal static async Task<PartFacts> PartAsync(IServiceProvider services, string code) =>
        (await services.GetRequiredService<PartStockService>().GetPartFactsAsync(Token)).Values
            .Single(part => string.Equals(part.Code, code, StringComparison.Ordinal));

    /// <summary>
    /// Gives the player a level of a technology.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="code">The neutral code of the technology.</param>
    /// <param name="level">The level.</param>
    /// <returns>The technology.</returns>
    internal static async Task<int> LearnAsync(PlanetsTestPlayer player, IServiceProvider services, string code, int level)
    {
        ResearchDbContext research = services.GetRequiredService<ResearchDbContext>();
        int technologyId = await research.Technologies.AsNoTracking()
            .Where(technology => technology.Code == code)
            .Select(technology => technology.Id)
            .SingleAsync(Token);

        PlayerTechnologyEntity? learned = await research.PlayerTechnologies
            .SingleOrDefaultAsync(entry => entry.PlayerId == player.Account.UserId && entry.TechnologyId == technologyId, Token);

        if (learned is null)
        {
            research.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = player.Account.UserId, TechnologyId = technologyId, Level = level });
        }
        else
        {
            learned.Level = level;
        }

        await research.SaveChangesAsync(Token);

        return technologyId;
    }

    /// <summary>
    /// The recipes of the kind of a process, by the logical code of the handler of the kind.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="handlerCode">The logical code of the handler.</param>
    /// <returns>The first recipe of the kind.</returns>
    /// <remarks>
    /// The kinds of the repair and the replacement are stubs the seed archives (author decision
    /// 31.10.2026). Their logic is still tested, so a stub kind that is archived is offered again
    /// here, as the administrator does in the admin panel.
    /// </remarks>
    internal static async Task<ProcessRecipeInfo> RecipeOfAsync(IServiceProvider services, string handlerCode)
    {
        ProcessCatalog catalog = await CatalogAsync(services);

        ProcessTypeInfo processType = catalog.Types.FirstOrDefault(type => string.Equals(type.HandlerCode, handlerCode, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"The kind of the process '{handlerCode}' does not exist.");
        ProcessRecipeInfo recipe = catalog.Recipes.First(entry => entry.ProcessTypeId == processType.Id);

        if (!processType.IsArchived && !recipe.IsArchived)
        {
            return recipe;
        }

        ProcessesDbContext dbContext = services.GetRequiredService<ProcessesDbContext>();
        await dbContext.ProcessTypes.Where(entry => entry.Id == processType.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.IsArchived, false), Token);
        await dbContext.ProcessRecipes.Where(entry => entry.Id == recipe.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.IsArchived, false), Token);
        services.GetRequiredService<ProcessCatalogCache>().Invalidate();

        return (await CatalogAsync(services)).Recipes.First(entry => entry.Id == recipe.Id);
    }

    /// <summary>
    /// The recipe of a kind of a process that takes a group of the resources.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="handlerCode">The logical code of the handler.</param>
    /// <param name="groupCode">The neutral code of the group of the resources.</param>
    /// <returns>The recipe.</returns>
    internal static async Task<ProcessRecipeInfo> RecipeOfGroupAsync(IServiceProvider services, string handlerCode, string groupCode)
    {
        ProcessCatalog catalog = await CatalogAsync(services);
        int groupId = await services.GetRequiredService<Aetheris.World.Data.WorldDbContext>().ResourceGroups.AsNoTracking()
            .Where(group => group.Code == groupCode)
            .Select(group => group.Id)
            .SingleAsync(Token);
        ProcessTypeInfo processType = catalog.Types.First(type => string.Equals(type.HandlerCode, handlerCode, StringComparison.Ordinal));

        return catalog.Recipes.First(recipe => recipe.ProcessTypeId == processType.Id && recipe.SourceGroupId == groupId);
    }

    /// <summary>
    /// The catalog of the processes as the tables hold it now.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <returns>The catalog.</returns>
    internal static Task<ProcessCatalog> CatalogAsync(IServiceProvider services) =>
        services.GetRequiredService<ProcessCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProcessesDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

    /// <summary>
    /// The resource of the world by its code.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="code">The neutral code of the resource.</param>
    /// <returns>The identifier of the resource.</returns>
    internal static Task<int> ResourceIdAsync(IServiceProvider services, string code) =>
        PlanetsTestData.ResourceIdAsync(services, code);

    /// <summary>
    /// The orders of the queue of a building, in their order.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingId">The building.</param>
    /// <returns>The orders.</returns>
    internal static Task<List<ProcessOrderEntity>> OrdersAsync(PlanetsTestPlayer player, IServiceProvider services, long buildingId) =>
        services.GetRequiredService<ProcessesDbContext>().ProcessOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId && entry.PlanetBuildingId == buildingId)
            .OrderBy(entry => entry.Position)
            .ToListAsync(Token);
}

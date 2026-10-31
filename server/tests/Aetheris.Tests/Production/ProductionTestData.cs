using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Helpers of the tests of the production: a factory and a storage on a body, its balance and
/// the recipes of the catalog (task 00013).
/// </summary>
internal static class ProductionTestData
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Puts the first factory of the starting data on the body, at level 1.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="recipe">The recipe the factory must be able to run, or <see langword="null"/> for the first recipe of a resource.</param>
    /// <returns>The building of the factory.</returns>
    internal static async Task<long> BuildFactoryAsync(PlanetsTestPlayer player, IServiceProvider services, RecipeInfo? recipe = null)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        int? requestedTypeId = recipe?.BuildingTypeId ?? (await FirstRecipeAsync(services)).BuildingTypeId;

        if (requestedTypeId is not int factoryTypeId)
        {
            throw new InvalidOperationException("The factory type does not exist.");
        }

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

    /// <summary>
    /// Completes every batch of a factory that is due, at the moment its last unit is ready.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingId">The factory.</param>
    /// <returns>A task that completes when the batches are made.</returns>
    internal static async Task CompleteAsync(PlanetsTestPlayer player, IServiceProvider services, long buildingId)
    {
        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();

        // The batch is run until its last unit is made, so the whole batch is done.
        DateTimeOffset? finishesAt = await dbContext.ProductionOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId && entry.PlanetBuildingId == buildingId)
            .MaxAsync(entry => (DateTimeOffset?)entry.FinishesAt, Token);

        await services.GetRequiredService<ProductionQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, finishesAt ?? DateTimeOffset.MaxValue, Token);
    }

    /// <summary>
    /// Gives the body every resource of the world, so a recipe whose ingredients are the
    /// produced resources can be ordered too.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <returns>A task that completes when the balance is filled.</returns>
    internal static async Task GiveEveryResourceAsync(PlanetsTestPlayer player, IServiceProvider services)
    {
        Aetheris.World.Data.WorldDbContext world = services.GetRequiredService<Aetheris.World.Data.WorldDbContext>();

        Dictionary<int, long> amounts = await world.ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Id, _ => 1_000_000L, Token);

        // A currency belongs to the civilization. The moment is ahead of the clock, so a mine adds nothing while the test runs.
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, amounts, PlanetsTestData.GameNow(services).AddDays(1));
    }

    /// <summary>
    /// Builds the storage of the parts on the body, so its room comes from the data of the
    /// building, and fills it until only the given room is free.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="freeRoom">How many units of the kind still fit, or <see langword="null"/> for an empty storage.</param>
    /// <param name="filledPartTypeId">The kind of the part that is filled until only <paramref name="freeRoom"/> is free; the room is the room of one kind.</param>
    /// <returns>The room of the storage for every kind.</returns>
    internal static async Task<long> BuildStorageAsync(PlanetsTestPlayer player, IServiceProvider services, long? freeRoom = null, int? filledPartTypeId = null)
    {
        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        Aetheris.Planets.IPlanetsModule planetsModule = services.GetRequiredService<Aetheris.Planets.IPlanetsModule>();

        int storageTypeId = await planetsModule.FindBuildingTypeIdAsync(SeedCodes.MetalStorage, Token)
            ?? throw new InvalidOperationException("The storage type does not exist.");

        if (!await planets.PlanetBuildings.AnyAsync(entry => entry.PlanetId == player.PlanetId && entry.BuildingTypeId == storageTypeId, Token))
        {
            planets.PlanetBuildings.Add(new PlanetBuildingEntity
            {
                PlanetId = player.PlanetId,
                SectorId = 0,
                BuildingTypeId = storageTypeId,
                Level = 1,
            });
            await planets.SaveChangesAsync(Token);
        }

        long capacity = await planetsModule.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.PartStorageCapacity, Token);
        Assert.True(capacity > 0);

        ProductionDbContext dbContext = services.GetRequiredService<ProductionDbContext>();
        PartStorageEntity? storage = await dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == player.PlanetId, Token);

        if (storage is null)
        {
            storage = new PartStorageEntity { PlanetId = player.PlanetId, SectorId = 0 };
            dbContext.PartStorages.Add(storage);
        }

        storage.Capacity = capacity;

        // The room is the room of one kind, so the storage is filled with the parts of one kind.
        if (freeRoom is not null)
        {
            dbContext.PartStocks.Add(new PartStockEntity
            {
                PlanetId = player.PlanetId,
                SectorId = 0,
                PartTypeId = filledPartTypeId ?? throw new ArgumentException("The kind to fill is needed.", nameof(filledPartTypeId)),
                Quantity = Math.Max(0, capacity - freeRoom.Value),
            });
        }

        await dbContext.SaveChangesAsync(Token);

        return capacity;
    }

    /// <summary>
    /// Reads the stored amount of one resource of the body.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="resourceTypeId">The resource.</param>
    /// <returns>The amount.</returns>
    internal static async Task<long> BalanceAsync(PlanetsTestPlayer player, IServiceProvider services, int resourceTypeId)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        PlanetResourceEntity? row = await dbContext.PlanetResources
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlanetId == player.PlanetId && entry.ResourceTypeId == resourceTypeId, Token);

        return row?.Amount ?? 0;
    }

    /// <summary>
    /// Finds the first recipe of the catalog that makes a resource.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <returns>The recipe.</returns>
    internal static async Task<RecipeInfo> FirstRecipeAsync(IServiceProvider services)
    {
        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

        return catalog.Recipes.First(recipe => recipe.PartTypeId is null);
    }

    /// <summary>
    /// Finds the first recipe of the catalog that makes a part.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <returns>The recipe.</returns>
    internal static async Task<RecipeInfo> FirstPartRecipeAsync(IServiceProvider services)
    {
        ProductionCatalog catalog = await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

        return catalog.Recipes.First(recipe => recipe.PartTypeId is not null);
    }
}

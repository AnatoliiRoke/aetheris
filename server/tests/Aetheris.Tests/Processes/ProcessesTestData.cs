using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Processes.Data;
using Aetheris.Processes.Services;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Helpers of the tests of the processes: a building that runs a process on a body, its
/// balance and the recipes of the catalog (task 00014).
/// </summary>
internal static class ProcessesTestData
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Puts the building of a class on the body, at level 1.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingClass">The class of the building, one of <see cref="Aetheris.Planets.Services.BuildingClasses"/>.</param>
    /// <returns>The building.</returns>
    internal static async Task<long> BuildAsync(
        PlanetsTestPlayer player,
        IServiceProvider services,
        string buildingClass)
    {
        PlanetsDbContext dbContext = services.GetRequiredService<PlanetsDbContext>();

        int buildingTypeId = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>()
            .FindBuildingTypeIdByClassAsync(buildingClass, Token)
            ?? throw new InvalidOperationException($"The building of the class '{buildingClass}' does not exist.");

        PlanetBuildingEntity? building = await dbContext.PlanetBuildings
            .Where(entry => entry.PlanetId == player.PlanetId && entry.BuildingTypeId == buildingTypeId)
            .FirstOrDefaultAsync(Token);

        if (building is not null)
        {
            return building.Id;
        }

        PlanetBuildingEntity entity = new()
        {
            PlanetId = player.PlanetId,
            SectorId = 0,
            BuildingTypeId = buildingTypeId,
            Level = 1,
        };

        dbContext.PlanetBuildings.Add(entity);
        await dbContext.SaveChangesAsync(Token);

        return entity.Id;
    }

    /// <summary>
    /// Completes every order of a building that is due, at the moment its last unit is ready.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <param name="buildingId">The building.</param>
    /// <returns>A task that completes when the orders are made.</returns>
    internal static async Task CompleteAsync(PlanetsTestPlayer player, IServiceProvider services, long buildingId)
    {
        ProcessesDbContext dbContext = services.GetRequiredService<ProcessesDbContext>();

        DateTimeOffset? finishesAt = await dbContext.ProcessOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId && entry.PlanetBuildingId == buildingId)
            .MaxAsync(entry => (DateTimeOffset?)entry.FinishesAt, Token);

        await services.GetRequiredService<ProcessQueueService>()
            .CompleteDueAsync(player.Account.UserId, player.PlanetId, buildingId, finishesAt ?? DateTimeOffset.MaxValue, Token);
    }

    /// <summary>
    /// Gives the body every resource of the world, so a recipe whose ingredients are any
    /// resource can be ordered too.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The services of the request.</param>
    /// <returns>A task that completes when the balance is filled.</returns>
    internal static async Task GiveEveryResourceAsync(PlanetsTestPlayer player, IServiceProvider services)
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
    /// The recipe of the kind of a process, by the logical code of the handler of the kind.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="handlerCode">The logical code of the handler.</param>
    /// <returns>The recipe of the kind.</returns>
    internal static async Task<ProcessRecipeInfo> RecipeOfAsync(IServiceProvider services, string handlerCode)
    {
        ProcessCatalog catalog = await services.GetRequiredService<ProcessCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProcessesDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

        ProcessTypeInfo? processType = catalog.Types.FirstOrDefault(type =>
            string.Equals(type.HandlerCode, handlerCode, StringComparison.Ordinal));

        if (processType is null)
        {
            throw new InvalidOperationException($"The kind of the process '{handlerCode}' does not exist.");
        }

        return catalog.Recipes.First(recipe => recipe.ProcessTypeId == processType.Id);
    }

    /// <summary>
    /// The resource of a group of the world, by its code.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="code">The neutral code of the resource.</param>
    /// <returns>The identifier of the resource.</returns>
    internal static async Task<int> ResourceIdAsync(IServiceProvider services, string code)
    {
        Aetheris.World.Api.ResourceCatalog catalog = await services.GetRequiredService<Aetheris.World.IWorldModule>()
            .GetResourceCatalogAsync(Token);

        return catalog.Resources.First(resource => string.Equals(resource.Code, code, StringComparison.Ordinal)).Id;
    }
}

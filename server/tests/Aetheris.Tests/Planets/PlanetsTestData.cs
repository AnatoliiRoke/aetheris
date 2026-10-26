using System.Net;
using System.Text.Json;
using Aetheris.Core.Time;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// A player with a civilization, ready for the tests of the bodies module.
/// </summary>
/// <param name="Client">The signed-in client.</param>
/// <param name="Account">The account.</param>
/// <param name="PlanetId">The home body.</param>
public sealed record PlanetsTestPlayer(HttpClient Client, TestAccount Account, long PlanetId) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Client.Dispose();
}

/// <summary>
/// Builds the state of the bodies module for the tests (task 00011).
/// </summary>
public static class PlanetsTestData
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Clears the database, creates the world and prepares the bodies module, as a fresh
    /// server does at start. The catalog is read again on every call.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <returns>A task that completes when the world and the building tables exist.</returns>
    public static async Task ResetAsync(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await WorldTestData.ResetWorldAsync(factory);

        using IServiceScope scope = factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, PlanetsConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);
    }

    /// <summary>
    /// Registers a player and creates a civilization of the first race.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <returns>The player and the home body.</returns>
    public static async Task<PlanetsTestPlayer> FoundAsync(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        HttpClient client = factory.CreateBrowserClient();
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(factory);
        await TestAccounts.SignInAsync(client, account);

        int raceId = await CivilizationTestData.FirstRaceIdAsync(client);
        HttpResponseMessage response = await CivilizationTestData.FoundAsync(client, raceId, "Дім", "found-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return new PlanetsTestPlayer(client, account, body.RootElement.GetProperty("planet_id").GetInt64());
    }

    /// <summary>
    /// Gets a building type by its code.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="code">The code.</param>
    /// <returns>The type.</returns>
    public static Task<BuildingTypeEntity> TypeAsync(IServiceProvider services, string code) =>
        services.GetRequiredService<PlanetsDbContext>().BuildingTypes.AsNoTracking()
            .Include(type => type.LevelIds).ThenInclude(level => level.CostIds)
            .Include(type => type.LevelIds).ThenInclude(level => level.EffectIds)
            .SingleAsync(type => type.Code == code, Token);

    /// <summary>
    /// Gets a resource by its code.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="code">The code.</param>
    /// <returns>The resource.</returns>
    public static Task<int> ResourceIdAsync(IServiceProvider services, string code) =>
        services.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .Where(resource => resource.Code == code)
            .Select(resource => resource.Id)
            .SingleAsync(Token);

    /// <summary>
    /// Gives a body a deposit of a resource with a coefficient, or changes the coefficient.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="resourceCode">The code of the resource.</param>
    /// <param name="coefficient">The coefficient.</param>
    /// <returns>The resource.</returns>
    public static async Task<int> SetDepositAsync(IServiceProvider services, long planetId, string resourceCode, decimal coefficient)
    {
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        int resourceId = await ResourceIdAsync(services, resourceCode);
        PlanetEntity planet = await world.Planets.AsNoTracking().SingleAsync(entry => entry.Id == planetId, Token);
        DepositEntity? deposit = await world.Deposits.SingleOrDefaultAsync(entry => entry.PlanetId == planetId && entry.ResourceId == resourceId, Token);

        if (deposit is null)
        {
            world.Deposits.Add(new DepositEntity { PlanetId = planetId, SectorId = planet.SectorId, ResourceId = resourceId, Coefficient = coefficient });
        }
        else
        {
            deposit.Coefficient = coefficient;
        }

        await world.SaveChangesAsync(Token);

        return resourceId;
    }

    /// <summary>
    /// Removes the deposit of a resource from a body.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="resourceCode">The code of the resource.</param>
    /// <returns>A task that completes when the deposit is gone.</returns>
    public static async Task RemoveDepositAsync(IServiceProvider services, long planetId, string resourceCode)
    {
        int resourceId = await ResourceIdAsync(services, resourceCode);

        await services.GetRequiredService<WorldDbContext>().Deposits
            .Where(entry => entry.PlanetId == planetId && entry.ResourceId == resourceId)
            .ExecuteDeleteAsync(Token);
    }

    /// <summary>
    /// Writes the balance of a body as of a moment, replacing what it had.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="amounts">The amounts by resource.</param>
    /// <param name="at">The moment of the amounts.</param>
    /// <returns>A task that completes when the balance is written.</returns>
    public static async Task SetBalanceAsync(IServiceProvider services, long planetId, IReadOnlyDictionary<int, long> amounts, DateTimeOffset at)
    {
        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        PlanetStateEntity state = await planets.PlanetStates.SingleAsync(entry => entry.PlanetId == planetId, Token);

        await planets.PlanetResources.Where(entry => entry.PlanetId == planetId).ExecuteDeleteAsync(Token);
        planets.PlanetResources.AddRange(amounts.Select(entry => new PlanetResourceEntity
        {
            PlanetId = planetId,
            SectorId = state.SectorId,
            ResourceTypeId = entry.Key,
            Amount = entry.Value,
        }));
        state.ResourcesUpdatedAt = at;

        await planets.SaveChangesAsync(Token);
    }

    /// <summary>
    /// Sets the level of a building on a body directly, as if it had been built.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="code">The code of the building type.</param>
    /// <param name="level">The level.</param>
    /// <returns>A task that completes when the level is written.</returns>
    public static async Task SetLevelAsync(IServiceProvider services, long planetId, string code, int level)
    {
        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        BuildingTypeEntity type = await TypeAsync(services, code);
        PlanetStateEntity state = await planets.PlanetStates.AsNoTracking().SingleAsync(entry => entry.PlanetId == planetId, Token);
        PlanetBuildingEntity? building = await planets.PlanetBuildings.SingleOrDefaultAsync(entry => entry.PlanetId == planetId && entry.BuildingTypeId == type.Id, Token);

        if (building is null)
        {
            planets.PlanetBuildings.Add(new PlanetBuildingEntity { PlanetId = planetId, SectorId = state.SectorId, BuildingTypeId = type.Id, Level = level });
        }
        else
        {
            building.Level = level;
        }

        await planets.SaveChangesAsync(Token);
    }

    /// <summary>
    /// Gets the balance of a body as it is stored.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <returns>The amounts by resource.</returns>
    public static Task<Dictionary<int, long>> StoredBalanceAsync(IServiceProvider services, long planetId) =>
        services.GetRequiredService<PlanetsDbContext>().PlanetResources.AsNoTracking()
            .Where(entry => entry.PlanetId == planetId)
            .ToDictionaryAsync(entry => entry.ResourceTypeId, entry => entry.Amount, Token);

    /// <summary>
    /// Reads a body at a moment of game time, the way the client sees it.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="now">The moment.</param>
    /// <returns>The body.</returns>
    public static async Task<PlanetOverviewDto> ReadAsync(IServiceProvider services, long planetId, DateTimeOffset now)
    {
        ConstructionService construction = services.GetRequiredService<ConstructionService>();
        await construction.CompleteDueAsync(planetId, now, Token);

        return await construction.ReadAsync(planetId, now, Token) ?? throw new InvalidOperationException("The body has no state.");
    }

    /// <summary>
    /// Gets the current game time of the server.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The game time.</returns>
    public static DateTimeOffset GameNow(IServiceProvider services) =>
        ConstructionService.Normalize(services.GetRequiredService<IGameClock>().GameNow);

    /// <summary>
    /// Gets the building of a type from an answer.
    /// </summary>
    /// <param name="overview">The answer.</param>
    /// <param name="code">The code of the building type.</param>
    /// <returns>The building.</returns>
    public static BuildingDto Building(PlanetOverviewDto overview, string code) =>
        overview.Buildings.Single(building => building.Code == code);
}

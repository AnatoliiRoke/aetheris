using Aetheris.Core.Errors;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Requirements;
using Aetheris.Requirements.Data;
using Aetheris.Requirements.Data.Entities;
using Aetheris.Requirements.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the start of a civilization, the requirements and the level limit (task 00011,
/// criteria 1, 2 and 4).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BuildingsTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildingsTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public BuildingsTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A new civilization has the main building of level 1 and its start resources in the
    /// balance of the body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_civilization_has_the_main_building_and_the_start_resources()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        RaceEntity race = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Races.AsNoTracking()
            .Include(entry => entry.StartResourceIds)
            .OrderBy(entry => entry.SortOrder)
            .FirstAsync(Token);
        BuildingTypeEntity main = await PlanetsTestData.TypeAsync(scope.ServiceProvider, SeedCodes.AdminCenter);

        Assert.Equal(main.Id, race.StartBuildingTypeId);

        PlanetBuildingEntity building = Assert.Single(await scope.ServiceProvider.GetRequiredService<PlanetsDbContext>().PlanetBuildings.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId)
            .ToListAsync(Token));
        Assert.Equal((main.Id, 1), (building.BuildingTypeId, building.Level));

        Assert.Equal(
            race.StartResourceIds.ToDictionary(entry => entry.ResourceTypeId, entry => entry.Amount),
            await PlanetsTestData.StoredBalanceAsync(scope.ServiceProvider, player.PlanetId));

        // The client reads the same through the API.
        PlanetOverviewDto? overview = await scope.ServiceProvider.GetRequiredService<IPlanetsModule>()
            .GetPlanetAsync(player.Account.UserId, player.PlanetId, Token);
        Assert.NotNull(overview);
        Assert.Equal(1, PlanetsTestData.Building(overview, SeedCodes.AdminCenter).Level);

        // Another player does not see the body.
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IPlanetsModule>().GetPlanetAsync("someone-else", player.PlanetId, Token));
    }

    /// <summary>
    /// Without technologies the base set is open: the main building, the science centre behind
    /// the main building of level 2, the housing, the power plant, the mines of the deposits of
    /// the body and the storages. A changed requirement in the database changes the
    /// availability without a release; a technology requirement stays unmet.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_base_set_is_open_and_a_changed_requirement_changes_it()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Iron, 1m);
        await PlanetsTestData.RemoveDepositAsync(services, player.PlanetId, SeedCodes.Copper);

        DateTimeOffset now = PlanetsTestData.GameNow(services);
        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);

        string[] open = [SeedCodes.AdminCenter, SeedCodes.ResidentialComplex, SeedCodes.PowerPlant, SeedCodes.IronMine, SeedCodes.MetalStorage, SeedCodes.MineralStorage, SeedCodes.FuelStorage];

        Assert.All(open, code => Assert.DoesNotContain(PlanetsErrorCodes.RequirementUnmet, PlanetsTestData.Building(overview, code).Next.ReasonCodes));

        RequirementDto science = Assert.Single(PlanetsTestData.Building(overview, SeedCodes.ScienceCenter).Next.UnmetRequirements);
        Assert.Equal(PlanetsErrorCodes.RequirementBuildingLevel, science.ReasonCode);
        Assert.Equal(2, science.TargetLevel);
        Assert.Equal(PlanetsErrorCodes.RequirementDeposit, Assert.Single(PlanetsTestData.Building(overview, SeedCodes.CopperMine).Next.UnmetRequirements).ReasonCode);

        // A designer lowers the requirement of the science centre in the database. The
        // requirements live in the common requirements module (task 00012, decision 3).
        RequirementsDbContext requirements = services.GetRequiredService<RequirementsDbContext>();
        await requirements.Requirements.Where(entry => entry.Id == science.RequirementId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.TargetLevel, 1), Token);

        // A technology that does not exist yet closes the housing.
        BuildingTypeEntity housing = await PlanetsTestData.TypeAsync(services, SeedCodes.ResidentialComplex);
        requirements.Requirements.Add(new RequirementEntity
        {
            SubjectType = RequirementSubjectTypes.BuildingType,
            SubjectId = housing.Id,
            SubjectLevel = 1,
            RequirementType = RequirementTypes.TechnologyLevel,
            TargetId = 1,
            TargetLevel = 1,
        });
        await requirements.SaveChangesAsync(Token);

        PlanetOverviewDto changed = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);

        Assert.Empty(PlanetsTestData.Building(changed, SeedCodes.ScienceCenter).Next.UnmetRequirements);
        Assert.Equal(PlanetsErrorCodes.RequirementTechnology, Assert.Single(PlanetsTestData.Building(changed, SeedCodes.ResidentialComplex).Next.UnmetRequirements).ReasonCode);

        // The common check answers the same for any subject.
        AvailabilityDto availability = await services.GetRequiredService<IPlanetsModule>().CheckRequirementsAsync(
            RequirementSubjectTypes.BuildingType,
            housing.Id,
            1,
            player.PlanetId,
            player.Account.UserId,
            Token);
        Assert.False(availability.IsAvailable);

        // The server refuses the closed building whatever the client shows.
        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            services.GetRequiredService<ConstructionService>().EnqueueAsync(player.PlanetId, housing.Id, now, Token));
        Assert.Equal(PlanetsErrorCodes.RequirementUnmet, refused.Code);
    }

    /// <summary>
    /// No building rises above the main building; the science centre opens with the main
    /// building of level 2.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_main_building_limits_the_levels_and_opens_the_science_centre()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        DateTimeOffset now = PlanetsTestData.GameNow(services);

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.PowerPlant, 1);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), now);

        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);
        BuildingTypeEntity science = await PlanetsTestData.TypeAsync(services, SeedCodes.ScienceCenter);
        ConstructionService construction = services.GetRequiredService<ConstructionService>();

        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, now);
        Assert.Contains(PlanetsErrorCodes.LevelCapped, PlanetsTestData.Building(overview, SeedCodes.PowerPlant).Next.ReasonCodes);
        Assert.Contains(PlanetsErrorCodes.RequirementUnmet, PlanetsTestData.Building(overview, SeedCodes.ScienceCenter).Next.ReasonCodes);

        GameException capped = await Assert.ThrowsAsync<GameException>(() => construction.EnqueueAsync(player.PlanetId, plant.Id, now, Token));
        Assert.Equal(PlanetsErrorCodes.LevelCapped, capped.Code);

        GameException closed = await Assert.ThrowsAsync<GameException>(() => construction.EnqueueAsync(player.PlanetId, science.Id, now, Token));
        Assert.Equal(PlanetsErrorCodes.RequirementUnmet, closed.Code);

        // The main building grows by the ordinary rules, and the others follow it.
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.AdminCenter, 2);

        PlanetOverviewDto grown = await construction.EnqueueAsync(player.PlanetId, plant.Id, now, Token);
        Assert.Equal(2, PlanetsTestData.Building(grown, SeedCodes.PowerPlant).QueuedLevel);

        PlanetOverviewDto opened = await construction.EnqueueAsync(player.PlanetId, science.Id, now, Token);
        Assert.Equal(1, PlanetsTestData.Building(opened, SeedCodes.ScienceCenter).QueuedLevel);
    }

    /// <summary>
    /// A civilization created before the module gets a state at the start of the server: the
    /// start building of its race and the amounts of the old storage of the world.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_older_civilization_is_adopted_at_start()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        int iron = await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron);

        // The state of task 00006: nothing in the module, the amounts in the world storage.
        await services.GetRequiredService<PlanetsDbContext>().PlanetStates.Where(entry => entry.PlanetId == player.PlanetId).ExecuteDeleteAsync(Token);
        int sectorId = await world.Planets.Where(entry => entry.Id == player.PlanetId).Select(entry => entry.SectorId).SingleAsync(Token);
        world.Storage.Add(new PlanetStorageEntity { PlanetId = player.PlanetId, SectorId = sectorId, ResourceTypeId = iron, Amount = 777 });
        await world.SaveChangesAsync(Token);

        PlanetsBootstrapResult first = await services.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);
        PlanetsBootstrapResult second = await services.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);

        Assert.Equal(1, first.AdoptedPlanets);
        Assert.Equal(0, second.AdoptedPlanets);
        Assert.Equal(new Dictionary<int, long> { [iron] = 777 }, await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId));
        Assert.False(await world.Storage.AnyAsync(entry => entry.PlanetId == player.PlanetId, Token));

        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, PlanetsTestData.GameNow(services));
        Assert.Equal(1, PlanetsTestData.Building(overview, SeedCodes.AdminCenter).Level);
    }

    /// <summary>
    /// A building type says what it is for by its class, not by its neutral code: the catalog
    /// finds the science centre by the class, and the code of the row may be any (task 00012,
    /// decision 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_building_type_is_found_by_its_class_and_not_by_its_code()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        BuildingCatalogCache cache = services.GetRequiredService<BuildingCatalogCache>();
        BuildingCatalog catalog = await cache.GetAsync(
            planets,
            services.GetRequiredService<IWorldModule>(),
            services.GetRequiredService<IRequirementsModule>(),
            Token);

        BuildingTypeEntity science = await PlanetsTestData.TypeAsync(services, SeedCodes.ScienceCenter);
        Assert.Equal(BuildingClasses.ResearchCenter, science.BuildingClass);

        BuildingTypeInfo? found = catalog.FindByClass(BuildingClasses.ResearchCenter);
        Assert.NotNull(found);
        Assert.Equal(science.Id, found.Id);

        // A building without a role has no class, and an unknown class finds nothing.
        BuildingTypeInfo main = catalog.FindType(await PlanetsTestData.TypeAsync(services, SeedCodes.AdminCenter) is { } mainType ? mainType.Id : 0)!;
        Assert.Equal(BuildingClasses.None, main.BuildingClass);
        Assert.Null(catalog.FindByClass(SeedCodes.ScienceCenter));
        Assert.Null(catalog.FindByClass(BuildingClasses.Shipyard));

        // The class is a logical code of the program, so every value of it is known.
        Assert.All(BuildingClassValues.All, value => Assert.True(BuildingClassValues.IsKnown(value)));
        Assert.False(BuildingClassValues.IsKnown("research"));
    }

    /// <summary>
    /// The deletion of an account removes everything the module kept for its bodies.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Releasing_a_player_removes_the_state_of_the_bodies()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<Aetheris.World.IWorldModule>().ReleasePlayerAsync(player.Account.UserId, Token);

        PlanetsDbContext planets = scope.ServiceProvider.GetRequiredService<PlanetsDbContext>();
        Assert.False(await planets.PlanetStates.AnyAsync(entry => entry.PlanetId == player.PlanetId, Token));
        Assert.False(await planets.PlanetBuildings.AnyAsync(entry => entry.PlanetId == player.PlanetId, Token));
        Assert.False(await planets.PlanetResources.AnyAsync(entry => entry.PlanetId == player.PlanetId, Token));
    }

    /// <summary>
    /// A balance that pays for anything in these tests.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <returns>The amounts by resource.</returns>
    public static async Task<Dictionary<int, long>> RichBalanceAsync(IServiceProvider services) =>
        await services.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Id, _ => 1_000_000L, Token);
}

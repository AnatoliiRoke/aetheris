using System.Net;
using System.Text.Json;
using Aetheris.Research;
using Aetheris.Research.Api;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Tests.Planets;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the levels of the technologies of a new colony (decision of 04.11.2026, game design 04): the
/// level of a technology belongs to the body that researched it, so a new colony starts from nothing.
/// </summary>
public sealed partial class ColonizationFlowTests
{
    /// <summary>
    /// The tree of the technologies of a new colony shows the levels of the colony, not the levels of the
    /// home body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_tree_of_a_new_colony_shows_its_own_levels()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long colonyId = (await SuitablePlanetsAsync(player, 1))[0];
        await PlaceAsync(fleetId, colonyId);

        using (IServiceScope setup = _factory.Services.CreateScope())
        {
            await SetColonialAdministrationAsync(setup.ServiceProvider, player, 25);
        }

        await using DeployTimeScope deploy = await DeployTimeScope.ZeroAsync(_factory.Services, Token);
        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = new object[] { new { type_code = "colonize" } } });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        using IServiceScope scope = _factory.Services.CreateScope();
        IResearchModule research = scope.ServiceProvider.GetRequiredService<IResearchModule>();
        ResearchTreeDto home = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        ResearchTreeDto colony = await research.GetTreeAsync(player.Account.UserId, colonyId, Token);

        Assert.Contains(home.Technologies, technology => technology.LearnedLevel == 25);
        Assert.Equal(colonyId, colony.SelectedPlanetId);
        Assert.All(colony.Technologies, technology => Assert.Equal(0, technology.LearnedLevel));

        // The screen asks the server with the query the client sends: the body is named planet_id.
        using HttpResponseMessage response = await player.Client.GetAsync($"/api/v1/research?planet_id={colonyId}", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        JsonElement tree = JsonDocument.Parse(text).RootElement;

        Assert.Equal(colonyId, tree.GetProperty("selected_planet_id").GetInt64());
        Assert.All(tree.GetProperty("technologies").EnumerateArray(), technology => Assert.Equal(0, technology.GetProperty("learned_level").GetInt32()));
    }

    /// <summary>
    /// The research of a new colony goes at the speed of the levels of the colony: «Розвиток науки» learned
    /// on the home body shortens the researches of the home body only (decision of 04.11.2026).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_science_of_the_home_body_does_not_speed_up_the_research_of_a_colony()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        long colonyId = await ColonyAsync(player);

        using (IServiceScope setup = _factory.Services.CreateScope())
        {
            IServiceProvider services = setup.ServiceProvider;
            await PlanetsTestData.SetLevelAsync(services, colonyId, ScienceCenterCode, 1);
            await PlanetsTestData.SetBalanceAsync(services, colonyId, await RichBalanceAsync(services), PlanetsTestData.GameNow(services));
        }

        (int technologyId, long before) = await FirstAvailableAsync(player, colonyId);

        using (IServiceScope setup = _factory.Services.CreateScope())
        {
            await SetTechnologyAsync(setup.ServiceProvider, player, ScienceDevelopmentCode, 25);
        }

        long home = await DurationOfFirstLevelAsync(player, player.PlanetId, technologyId);
        long after = await DurationOfFirstLevelAsync(player, colonyId, technologyId);

        Assert.True(home < before, $"The science of the home body does not shorten its own researches ({home} s against {before} s).");
        Assert.Equal(before, after);

        // The research that starts on the colony takes the time of the colony.
        using IServiceScope scope = _factory.Services.CreateScope();
        ResearchTreeDto queued = await scope.ServiceProvider.GetRequiredService<IResearchModule>()
            .EnqueueAsync(player.Account.UserId, colonyId, technologyId, 1, Token);
        ResearchQueueItemDto item = Assert.Single(queued.Queue);

        Assert.Equal(colonyId, item.PlanetId);
        Assert.Equal(before, (long)(item.FinishesAt!.Value - item.StartsAt).TotalSeconds);
    }

    private const string ScienceCenterCode = "building_9";
    private const string ScienceDevelopmentCode = "tech_21";

    /// <summary>
    /// Colonizes a free planet with a colony module that deploys at once and gives the planet.
    /// </summary>
    private async Task<long> ColonyAsync(PlanetsTestPlayer player)
    {
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long colonyId = (await SuitablePlanetsAsync(player, 1))[0];
        await PlaceAsync(fleetId, colonyId);

        await using DeployTimeScope deploy = await DeployTimeScope.ZeroAsync(_factory.Services, Token);
        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = new object[] { new { type_code = "colonize" } } });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        return colonyId;
    }

    private async Task<(int TechnologyId, long DurationSeconds)> FirstAvailableAsync(PlanetsTestPlayer player, long planetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ResearchTreeDto tree = await scope.ServiceProvider.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, planetId, Token);
        TechnologyDto technology = tree.Technologies.First(entry => entry.Status == ResearchTechnologyStatuses.Available);

        return (technology.Id, technology.Levels.Single(level => level.Level == 1).DurationSeconds);
    }

    private async Task<long> DurationOfFirstLevelAsync(PlanetsTestPlayer player, long planetId, int technologyId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ResearchTreeDto tree = await scope.ServiceProvider.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, planetId, Token);

        return tree.Technologies.Single(entry => entry.Id == technologyId).Levels.Single(level => level.Level == 1).DurationSeconds;
    }

    private static async Task SetTechnologyAsync(IServiceProvider services, PlanetsTestPlayer player, string code, int level)
    {
        ResearchDbContext database = services.GetRequiredService<ResearchDbContext>();
        int technologyId = await database.Technologies.AsNoTracking().Where(technology => technology.Code == code).Select(technology => technology.Id).SingleAsync(Token);

        database.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = player.Account.UserId, PlanetId = player.PlanetId, TechnologyId = technologyId, Level = level });
        await database.SaveChangesAsync(Token);
    }

    private static async Task<Dictionary<int, long>> RichBalanceAsync(IServiceProvider services) =>
        await services.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Id, _ => 1_000_000L, Token);
}

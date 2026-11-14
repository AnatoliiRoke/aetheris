using System.Net;
using System.Text.Json;
using Aetheris.Research;
using Aetheris.Research.Api;
using Aetheris.Tests.Planets;
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
}

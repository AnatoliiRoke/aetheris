using System.Net;
using System.Text.Json;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the facts the answer of a body carries for the screens of the game (task 00020):
/// the name, the type, the place, the population and the richness of the deposits.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PlanetOverviewTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetOverviewTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public PlanetOverviewTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The answer names the body, its type and its place from the world, and the richness of its
    /// deposits is the very coefficient the production of the body is counted with.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_answer_carries_the_name_the_type_the_place_and_the_deposits_of_the_world()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        const decimal changedCoefficient = 1.7m;
        int resourceId = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Iron, changedCoefficient);

        PlanetFacts facts = (await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(player.PlanetId, Token))!;
        string typeCode = await services.GetRequiredService<WorldDbContext>().ObjectTypes.AsNoTracking()
            .Where(type => type.Id == facts.ObjectTypeId)
            .Select(type => type.Code)
            .SingleAsync(Token);

        PlanetOverviewDto overview = (await services.GetRequiredService<IPlanetsModule>()
            .GetPlanetAsync(player.Account.UserId, player.PlanetId, Token))!;

        Assert.Equal(facts.Name, overview.Name);
        Assert.Equal(typeCode, overview.TypeCode);
        Assert.NotNull(overview.Coordinates);
        Assert.Equal((facts.SectorId, facts.SystemId, facts.Orbit), (overview.Coordinates.SectorId, overview.Coordinates.SystemId, overview.Coordinates.Orbit));

        // The coefficients are the ones of the world, so the richness shown is the richness
        // the production is counted with.
        Assert.NotNull(overview.DepositRichness);
        Assert.Equal(
            facts.Deposits.OrderBy(deposit => deposit.ResourceTypeId).Select(deposit => (deposit.ResourceTypeId, deposit.Coefficient)),
            overview.DepositRichness.OrderBy(deposit => deposit.ResourceTypeId).Select(deposit => (deposit.ResourceTypeId, deposit.Coefficient)));
        Assert.Equal(changedCoefficient, overview.DepositRichness.Single(deposit => deposit.ResourceTypeId == resourceId).Coefficient);

        // A building carries the category the data gives it, and the answer lists the categories
        // with their order, so the client groups the buildings by them and guesses nothing.
        string categoryCode = PlanetsReferenceSeed.Load().BuildingTypes.Single(type => type.Code == SeedCodes.ScienceCenter).Category;
        BuildingCategoryDto category = overview.BuildingCategories!.Single(entry => entry.Code == categoryCode);
        Assert.Equal(category.Id, PlanetsTestData.Building(overview, SeedCodes.ScienceCenter).CategoryId);
        Assert.Equal(PlanetsTextKeys.CategoryName(categoryCode), category.NameKey);
    }

    /// <summary>
    /// The population of the answer is the population of the economy, and the HTTP answer
    /// carries the new fields in the snake case of the API.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_http_answer_carries_the_population_and_the_new_fields_in_the_snake_case()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        HttpResponseMessage response = await player.Client.GetAsync($"/api/v1/planets/{player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement root = body.RootElement;

        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("type_code", out JsonElement typeCode));
        Assert.False(string.IsNullOrEmpty(typeCode.GetString()));
        Assert.True(root.GetProperty("coordinates").TryGetProperty("system_id", out _));
        Assert.True(root.TryGetProperty("population", out _));
        Assert.True(root.TryGetProperty("population_growth_per_hour", out _));
        Assert.True(root.TryGetProperty("time_to_maximum", out _));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("deposit_richness").ValueKind);

        using IServiceScope scope = _factory.Services.CreateScope();
        PopulationFacts? population = await scope.ServiceProvider.GetRequiredService<IEconomyModule>()
            .GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(population?.Amount ?? 0, root.GetProperty("population").GetInt64());
    }

    /// <summary>
    /// A player reads only its own body: another player gets no answer for it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Another_player_gets_no_answer_for_the_body()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);

        HttpResponseMessage own = await owner.Client.GetAsync($"/api/v1/planets/{owner.PlanetId}", Token);
        HttpResponseMessage foreign = await stranger.Client.GetAsync($"/api/v1/planets/{owner.PlanetId}", Token);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.DoesNotContain("coordinates", await foreign.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    /// <summary>
    /// The answer of a command on a body takes the place of the body on the screen, so it
    /// carries the same name, type and place as a read.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_answer_of_a_command_carries_the_same_facts_as_a_read()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Dictionary<int, long> balance = await BuildingsTests.RichBalanceAsync(services);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, balance, PlanetsTestData.GameNow(services));
        Aetheris.Planets.Data.Entities.BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);

        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            EnqueueConstructionCommand.RoutePath,
            new Dictionary<string, object?> { ["planet_id"] = player.PlanetId, ["building_type_id"] = plant.Id },
            "enriched-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        PlanetOverviewDto read = (await services.GetRequiredService<IPlanetsModule>().GetPlanetAsync(player.Account.UserId, player.PlanetId, Token))!;

        Assert.Equal(read.Name, answer.RootElement.GetProperty("name").GetString());
        Assert.Equal(read.TypeCode, answer.RootElement.GetProperty("type_code").GetString());
        Assert.Equal(read.Coordinates!.SystemId, answer.RootElement.GetProperty("coordinates").GetProperty("system_id").GetInt64());
        Assert.Equal(read.DepositRichness!.Count, answer.RootElement.GetProperty("deposit_richness").GetArrayLength());
    }
}

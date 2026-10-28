using System.Net;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Planets.Data;
using Aetheris.Tests.Accounts;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the civilization step and of the start package (task 00006, acceptance criteria
/// 8, 8a and 9).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CivilizationTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CivilizationTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CivilizationTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Until the step is done only the step is open; afterwards the player has a home body of
    /// the type the race prefers, and the race never changes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_step_gives_a_home_body_of_the_preferred_type_and_the_race_never_changes()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        HttpResponseMessage before = await TestAccounts.SendCommandAsync(client, TestGameActionCommand.RoutePath, new { }, "game-1");
        Assert.Equal(AccountGateErrorCodes.OnboardingRequired, await TestAccounts.ReadErrorAsync(before));

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        RaceEntity race = await world.Races.AsNoTracking().SingleAsync(entry => entry.Code == SeedCodes.SecondRace, Token);
        bool preferredIsFree = await world.Planets.AnyAsync(
            planet => planet.OwnerPlayerId == null && planet.CelestialObject!.ObjectTypeId == race.PreferredObjectTypeId,
            Token);

        HttpResponseMessage founded = await CivilizationTestData.FoundAsync(client, race.Id, "Нова Земля", "found-1");
        Assert.Equal(HttpStatusCode.OK, founded.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await founded.Content.ReadAsStringAsync(Token));
        long planetId = body.RootElement.GetProperty("planet_id").GetInt64();

        Assert.Equal(SeedCodes.SecondRace, body.RootElement.GetProperty("race_code").GetString());
        Assert.Equal("Нова Земля", body.RootElement.GetProperty("planet_name").GetString());

        int typeId = await world.Planets.AsNoTracking()
            .Where(planet => planet.Id == planetId)
            .Select(planet => planet.CelestialObject!.ObjectTypeId)
            .SingleAsync(Token);

        if (preferredIsFree)
        {
            Assert.Equal(race.PreferredObjectTypeId, typeId);
        }

        HttpResponseMessage after = await TestAccounts.SendCommandAsync(client, TestGameActionCommand.RoutePath, new { }, "game-2");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);

        int otherRaceId = await world.Races.AsNoTracking().Where(entry => entry.Id != race.Id).Select(entry => entry.Id).FirstAsync(Token);
        HttpResponseMessage again = await CivilizationTestData.FoundAsync(client, otherRaceId, "Друга", "found-2");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal(WorldErrorCodes.CivilizationExists, await TestAccounts.ReadErrorAsync(again));

        using JsonDocument me = await AccountTestSupport.MeAsync(client);
        Assert.Equal(race.Id, me.RootElement.GetProperty("civilization").GetProperty("race_id").GetInt32());
    }

    /// <summary>
    /// The start package comes from the tables of the race: a changed row changes the
    /// result without a release, the storage limit caps it, and it is never given twice. Since
    /// task 00011 the amounts land in the balance of the body in the Planets module.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_start_package_comes_from_the_race_tables_and_is_given_once()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        RaceEntity race = await world.Races.AsNoTracking().Include(entry => entry.StartResourceIds).OrderBy(entry => entry.SortOrder).FirstAsync(Token);
        RaceStartResourceEntity changed = race.StartResourceIds[0];

        (HttpClient first, long firstPlanet) = await FoundForNewPlayerAsync(race.Id, "Перша");

        Dictionary<int, long> firstStorage = await StorageAsync(scope, firstPlanet);
        Assert.Equal(race.StartResourceIds.ToDictionary(entry => entry.ResourceTypeId, entry => entry.Amount), firstStorage);

        // A designer changes one row and lowers the storage limit.
        await world.RaceStartResources.Where(entry => entry.Id == changed.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Amount, 77), Token);
        long capacity = race.StartResourceIds.Where(entry => entry.Id != changed.Id).Min(entry => entry.Amount) - 1;
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.StartStorageCapacity, capacity.ToString(System.Globalization.CultureInfo.InvariantCulture));

        (HttpClient second, long secondPlanet) = await FoundForNewPlayerAsync(race.Id, "Друга");
        Dictionary<int, long> secondStorage = await StorageAsync(scope, secondPlanet);

        Assert.Equal(77, secondStorage[changed.ResourceTypeId]);
        Assert.All(secondStorage.Where(entry => entry.Key != changed.ResourceTypeId), entry => Assert.Equal(capacity, entry.Value));

        // A second step is refused and gives nothing.
        HttpResponseMessage again = await CivilizationTestData.FoundAsync(first, race.Id, "Знову", "again-1");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.StatusCode);
        Assert.Equal(firstStorage, await StorageAsync(scope, firstPlanet));

        first.Dispose();
        second.Dispose();
    }

    /// <summary>
    /// The start building of the race goes to the extension point of the buildings module:
    /// a stub placer receives the type and the level of the race. Since task 00011 the placer
    /// of the host is the Planets module, so the old grant table stays empty.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_start_building_of_the_race_goes_to_the_extension_point()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        const int buildingTypeId = 42;
        const int level = 1;

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        RaceEntity race = await world.Races.AsNoTracking().OrderBy(entry => entry.SortOrder).FirstAsync(Token);

        Assert.Null(race.StartBuildingTypeId);

        await world.Races.Where(entry => entry.Id == race.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.StartBuildingTypeId, buildingTypeId), Token);

        // The stub of the future buildings module.
        CapturingBuildingPlacer stub = new();
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);
        CivilizationService service = new(
            world,
            scope.ServiceProvider.GetRequiredService<WorldUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<StartPlanetAllocator>(),
            scope.ServiceProvider.GetRequiredService<WorldReferenceRepository>(),
            stub,
            scope.ServiceProvider.GetRequiredService<IHomePlanetStore>(),
            scope.ServiceProvider.GetRequiredService<ITextModeration>(),
            TimeProvider.System);

        CivilizationSummary summary = await service.FoundAsync(player.UserId, race.Id, "Заглушка", Token);

        (long PlanetId, int BuildingTypeId, int Level) placed = Assert.Single(stub.Placed);
        Assert.Equal((summary.PlanetId, buildingTypeId, level), placed);

        // The placer of the host is the Planets module: no grant is kept for later.
        (HttpClient client, long planetId) = await FoundForNewPlayerAsync(race.Id, "Типова");
        client.Dispose();

        Assert.False(await world.StartBuildingGrants.AsNoTracking().AnyAsync(entry => entry.PlanetId == planetId, Token));
    }

    /// <summary>
    /// Many newcomers at once never share a home body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_newcomers_never_share_a_home_body()
    {
        const int players = 20;

        await WorldTestData.ResetWorldAsync(_factory);

        int raceId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            raceId = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Races.Select(race => race.Id).FirstAsync(Token);
        }

        List<HttpClient> clients = [];

        for (int index = 0; index < players; index++)
        {
            HttpClient client = _factory.CreateBrowserClient();
            await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));
            clients.Add(client);
        }

        HttpResponseMessage[] responses = await Task.WhenAll(clients.Select((client, index) =>
            CivilizationTestData.FoundAsync(client, raceId, $"Колонія {index}", $"parallel-{index}")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        List<long> planetIds = [];

        foreach (HttpResponseMessage response in responses)
        {
            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
            planetIds.Add(body.RootElement.GetProperty("planet_id").GetInt64());
        }

        Assert.Equal(players, planetIds.Distinct().Count());

        clients.ForEach(client => client.Dispose());
    }

    /// <summary>
    /// The name of the home body follows the rules of a visible name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="expectedCode">The refused rule.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("Ab", WorldErrorCodes.HomeNameTooShort)]
    [InlineData("Planet<script>", WorldErrorCodes.HomeNameInvalidCharacters)]
    [InlineData("Shitty Rock", WorldErrorCodes.HomeNameRestricted)]
    [InlineData("Sh'itty Rock", WorldErrorCodes.HomeNameRestricted)]
    [InlineData("Кам\"яна", WorldErrorCodes.HomeNameInvalidCharacters)]
    public async Task The_home_name_follows_the_rules(string name, string expectedCode)
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        HttpResponseMessage response = await CivilizationTestData.FoundAsync(client, await CivilizationTestData.FirstRaceIdAsync(client), name, "name-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GameErrorCodes.ValidationFailed, await TestAccounts.ReadErrorAsync(response));
        Assert.Contains(expectedCode, await AccountTestSupport.FieldErrorsAsync(response, "planet_name"));
    }

    /// <summary>
    /// The name of the home body may contain the apostrophe, the plain one and the typographic
    /// one; the name is stored and returned exactly as it was typed, also when it looks like
    /// a piece of SQL.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("Кам'яна")]
    [InlineData("Кам’яна")]
    [InlineData("O'Brien' --")]
    public async Task The_home_name_may_contain_an_apostrophe(string name)
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        HttpResponseMessage response = await CivilizationTestData.FoundAsync(client, await CivilizationTestData.FirstRaceIdAsync(client), name, "apostrophe-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        long planetId = body.RootElement.GetProperty("planet_id").GetInt64();
        Assert.Equal(name, body.RootElement.GetProperty("planet_name").GetString());

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        Assert.Equal(name, await world.Planets.AsNoTracking().Where(entry => entry.Id == planetId).Select(entry => entry.Name).SingleAsync(Token));
    }

    /// <summary>
    /// The allowed characters of the name come from the database: without the apostrophe in
    /// the definition the same name is refused at once, without a rebuild.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_home_name_characters_come_from_the_database()
    {
        await WorldTestData.ResetWorldAsync(_factory, new Dictionary<string, string>
        {
            [WorldConfigKeys.HomeNameCharacters] = "A-Za-zЀ-ӿ0-9 _-",
        });

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));
        int raceId = await CivilizationTestData.FirstRaceIdAsync(client);

        HttpResponseMessage refused = await CivilizationTestData.FoundAsync(client, raceId, "Кам'яна", "chars-1");
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(WorldErrorCodes.HomeNameInvalidCharacters, await AccountTestSupport.FieldErrorsAsync(refused, "planet_name"));

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.HomeNameCharacters, "A-Za-zЀ-ӿ0-9 _'-");

        HttpResponseMessage accepted = await CivilizationTestData.FoundAsync(client, raceId, "Кам'яна", "chars-2");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// <summary>
    /// An account whose address stays unconfirmed for the configured 7 days is deleted with
    /// its home body, which becomes free again; a confirmed account stays.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_unconfirmed_account_is_deleted_after_its_lifetime_and_frees_its_home_body()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        TestAccount unconfirmed = await TestAccounts.RegisterAsync(_factory);
        TestAccount confirmed = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, unconfirmed);

        // The step is a setting of the account and the body, allowed before the confirmation.
        HttpResponseMessage founded = await CivilizationTestData.FoundAsync(client, await CivilizationTestData.FirstRaceIdAsync(client), "Тимчасова", "expire-1");
        Assert.Equal(HttpStatusCode.OK, founded.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await founded.Content.ReadAsStringAsync(Token));
        long planetId = body.RootElement.GetProperty("planet_id").GetInt64();

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext accounts = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        // A day before the end of the lifetime nothing happens.
        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(6));
        Assert.True(await accounts.Users.AnyAsync(user => user.Id == unconfirmed.UserId, Token));

        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        Assert.False(await accounts.Users.AnyAsync(user => user.Id == unconfirmed.UserId, Token));
        Assert.True(await accounts.Users.AnyAsync(user => user.Id == confirmed.UserId, Token));

        PlanetEntity planet = await world.Planets.AsNoTracking().SingleAsync(entry => entry.Id == planetId, Token);
        Assert.Null(planet.OwnerPlayerId);
        Assert.Null(planet.Name);
        Assert.False(await world.Civilizations.AnyAsync(entry => entry.PlayerId == unconfirmed.UserId, Token));
        Assert.False(await world.Storage.AnyAsync(entry => entry.PlanetId == planetId, Token));
        Assert.False(await world.StartAllocations.AnyAsync(entry => entry.PlayerId == unconfirmed.UserId, Token));
    }

    /// <summary>
    /// A requested deletion frees the home body at the end of its delay.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_deleted_account_frees_its_home_body()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, account);

        HttpResponseMessage founded = await CivilizationTestData.FoundAsync(client, await CivilizationTestData.FirstRaceIdAsync(client), "Прощавай", "delete-world-1");
        using JsonDocument body = JsonDocument.Parse(await founded.Content.ReadAsStringAsync(Token));
        long planetId = body.RootElement.GetProperty("planet_id").GetInt64();

        HttpResponseMessage deleted = await TestAccounts.SendCommandAsync(client, AccountActions.Prefix + "/delete", new { password = account.Password }, "delete-world-2");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);

        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        Assert.Null(await world.Planets.AsNoTracking().Where(entry => entry.Id == planetId).Select(entry => entry.OwnerPlayerId).SingleAsync(Token));
    }

    private static async Task<Dictionary<int, long>> StorageAsync(IServiceScope scope, long planetId) =>
        await scope.ServiceProvider.GetRequiredService<PlanetsDbContext>().PlanetResources.AsNoTracking()
            .Where(entry => entry.PlanetId == planetId)
            .ToDictionaryAsync(entry => entry.ResourceTypeId, entry => entry.Amount, Token);

    private async Task<(HttpClient Client, long PlanetId)> FoundForNewPlayerAsync(int raceId, string name)
    {
        HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        HttpResponseMessage response = await CivilizationTestData.FoundAsync(client, raceId, name, "found-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return (client, body.RootElement.GetProperty("planet_id").GetInt64());
    }

    private sealed class CapturingBuildingPlacer : IStartBuildingPlacer
    {
        public List<(long PlanetId, int BuildingTypeId, int Level)> Placed { get; } = [];

        public Task PlaceAsync(long planetId, int sectorId, int buildingTypeId, int level, CancellationToken cancellationToken)
        {
            Placed.Add((planetId, buildingTypeId, level));

            return Task.CompletedTask;
        }
    }
}

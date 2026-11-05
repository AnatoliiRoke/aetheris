using System.Net;
using System.Text.Json;
using Aetheris.Fleet.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the constructor of the ships: the projects of a player and the rules of the
/// constructor (task 00038).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ShipDesignTests
{
    private const string Designs = "/api/v1/fleet/designs";
    private const string Catalog = "/api/v1/fleet/ship-catalog";

    /// <summary>
    /// The code of the design bureau: its level gives the number of the projects of the player.
    /// </summary>
    private const string DesignBureau = "building_10";

    /// <summary>
    /// The token that stops a test when it is over.
    /// </summary>
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipDesignTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public ShipDesignTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A project is created, renamed, saved and archived, and it is read from the database.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_project_is_created_renamed_saved_and_archived()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 3);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");
        int[] slots = [.. catalog.ComponentsFor(hull, "drive").Take(1)
            .Concat(catalog.ComponentsFor(hull, "reactor").Take(1))
            .Concat(catalog.ComponentsFor(hull, "life_support").Take(1))
            .Select(module => module.Id)];

        HttpResponseMessage created = await CreateAsync(player, "Перехоплювач", hull.Id);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token));
        long designId = body.RootElement.GetProperty("id").GetInt64();
        Assert.Equal("Перехоплювач", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("slots").GetArrayLength());

        HttpResponseMessage saved = await SaveAsync(player, designId, hull.Id, slots);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using JsonDocument savedBody = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Token));
        Assert.Equal(slots.Length, savedBody.RootElement.GetProperty("slots").GetArrayLength());
        Assert.True(savedBody.RootElement.GetProperty("summary").GetProperty("has_drive").GetBoolean());
        Assert.True(savedBody.RootElement.GetProperty("summary").GetProperty("has_reactor").GetBoolean());
        Assert.True(savedBody.RootElement.GetProperty("summary").GetProperty("has_life_support").GetBoolean());

        HttpResponseMessage renamed = await SendAsync(player, Designs + "/rename", new { design_id = designId, name = "Новий" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        using JsonDocument renamedBody = JsonDocument.Parse(await renamed.Content.ReadAsStringAsync(Token));
        Assert.Equal("Новий", renamedBody.RootElement.GetProperty("name").GetString());

        HttpResponseMessage archived = await SendAsync(player, Designs + "/archive", new { design_id = designId });
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        using JsonDocument archivedBody = JsonDocument.Parse(await archived.Content.ReadAsStringAsync(Token));
        Assert.True(archivedBody.RootElement.GetProperty("is_archived").GetBoolean());

        // The archived project is kept in the database, so a ship already built of it does not
        // lose its link (task 00023).
        using IServiceScope scope = _factory.Services.CreateScope();
        long stored = await scope.ServiceProvider.GetRequiredService<Aetheris.Fleet.Data.FleetDbContext>().ShipDesigns
            .AsNoTracking()
            .Where(design => design.Id == designId)
            .Select(design => design.Id)
            .SingleAsync(Token);

        Assert.Equal(designId, stored);
    }

    /// <summary>
    /// The player keeps no more projects than the design bureau allows.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_player_keeps_no_more_projects_than_the_bureau_allows()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 2);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");

        // The level 2 of the design bureau gives two projects.
        Assert.Equal(2, catalog.DesignLimit);

        HttpResponseMessage first = await CreateAsync(player, "Перший", hull.Id);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        HttpResponseMessage second = await CreateAsync(player, "Другий", hull.Id);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        HttpResponseMessage third = await CreateAsync(player, "Третій", hull.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, third.StatusCode);
        Assert.Equal("fleet_design_limit_reached", await TestAccounts.ReadErrorAsync(third));
    }

    /// <summary>
    /// The ship takes no more modules than the hull has slots.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_ship_takes_no_more_modules_than_the_hull_has_slots()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");

        HttpResponseMessage created = await CreateAsync(player, "Багато", hull.Id);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token));
        long designId = body.RootElement.GetProperty("id").GetInt64();

        // The hull has no free slot left, so the module does not go into the ship.
        int[] slots = [.. Enumerable.Range(0, hull.Slots + 1).Select(index => catalog.ComponentsFor(hull, "drive").First().Id)];

        HttpResponseMessage response = await SaveAsync(player, designId, hull.Id, slots);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fleet_slots_full", await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// The ship needs a drive, a reactor and the life support.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_ship_needs_a_drive_a_reactor_and_the_life_support()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");

        int drive = catalog.ComponentsFor(hull, "drive").First().Id;
        int reactor = catalog.ComponentsFor(hull, "reactor").First().Id;
        int lifeSupport = catalog.ComponentsFor(hull, "life_support").First().Id;

        HttpResponseMessage noDrive = await SaveAsync(player, await CreateIdAsync(player, "Без руху", hull.Id), hull.Id, [reactor, lifeSupport]);
        Assert.Equal("fleet_drive_missing", await TestAccounts.ReadErrorAsync(noDrive));

        HttpResponseMessage noReactor = await SaveAsync(player, await CreateIdAsync(player, "Без енергії", hull.Id), hull.Id, [drive, lifeSupport]);
        Assert.Equal("fleet_reactor_missing", await TestAccounts.ReadErrorAsync(noReactor));

        HttpResponseMessage noLifeSupport = await SaveAsync(player, await CreateIdAsync(player, "Без екіпажу", hull.Id), hull.Id, [drive, reactor]);
        Assert.Equal("fleet_life_support_missing", await TestAccounts.ReadErrorAsync(noLifeSupport));

        HttpResponseMessage complete = await SaveAsync(player, await CreateIdAsync(player, "Повний", hull.Id), hull.Id, [drive, reactor, lifeSupport]);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    /// <summary>
    /// The modules of the ship take no more energy than the reactors give.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_modules_take_no_more_energy_than_the_reactors_give()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);

        // The hull and the reactor are picked together with the module that takes more energy
        // than the reactor gives, so the case has a pair that breaks the rule.
        (ShipHull hull, ShipModule reactor, ShipModule hungry) = catalog.Hulls
            .Where(hull => hull.Role == "combat")
            .SelectMany(hull => catalog.ComponentsFor(hull, "reactor")
                .OrderBy(module => module.Power)
                .Take(1)
                .Select(reactor => (hull, reactor, Hungry(catalog, hull, reactor))))
            .Where(pair => pair.Item3 is not null)
            .Select(pair => (pair.hull, pair.reactor, pair.Item3!))
            .First();

        int drive = catalog.ComponentsFor(hull, "drive").First().Id;
        int lifeSupport = catalog.ComponentsFor(hull, "life_support").First().Id;

        HttpResponseMessage response = await SaveAsync(
            player,
            await CreateIdAsync(player, "Без енергії", hull.Id),
            hull.Id,
            [drive, lifeSupport, reactor.Id, hungry.Id]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fleet_energy_short", await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// A combat module goes only on a combat hull, and a colonial module only on a transport
    /// hull.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_module_goes_only_on_the_hull_of_its_role()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull combat = catalog.HullWithComponents("combat");
        ShipHull transport = catalog.HullWithComponents("transport");

        int[] combatKit = [.. catalog.ComponentsFor(combat, "drive").Take(1)
            .Concat(catalog.ComponentsFor(combat, "reactor").Take(1))
            .Concat(catalog.ComponentsFor(combat, "life_support").Take(1))
            .Select(module => module.Id)];

        ShipModule weapon = catalog.Modules.First(module => module.Role == "combat");
        ShipModule colonial = catalog.Modules.First(module => module.Role == "transport");

        // The weapon does not go on the peaceful hull.
        HttpResponseMessage weaponOnTransport = await SaveAsync(
            player,
            await CreateIdAsync(player, "Зброя на транспорті", transport.Id),
            transport.Id,
            [.. combatKit, weapon.Id]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, weaponOnTransport.StatusCode);
        Assert.Equal("fleet_role_mismatch", await TestAccounts.ReadErrorAsync(weaponOnTransport));

        // The colonial module does not go on the combat hull.
        HttpResponseMessage colonialOnCombat = await SaveAsync(
            player,
            await CreateIdAsync(player, "Колонія на бойовому", combat.Id),
            combat.Id,
            [.. combatKit, colonial.Id]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, colonialOnCombat.StatusCode);
        Assert.Equal("fleet_role_mismatch", await TestAccounts.ReadErrorAsync(colonialOnCombat));
    }

    /// <summary>
    /// A module that is not fit for the hull cannot be put into it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_module_that_is_not_fit_for_the_hull_cannot_be_put_into_it()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");

        int[] kit = [.. catalog.ComponentsFor(hull, "drive").Take(1)
            .Concat(catalog.ComponentsFor(hull, "reactor").Take(1))
            .Concat(catalog.ComponentsFor(hull, "life_support").Take(1))
            .Select(module => module.Id)];

        // The modules of the seed name the hulls they fit, so the test picks the module that
        // does not fit the hull of the test.
        ShipModule alien = catalog.Modules.First(module =>
            module.Role == "combat"
            && module.CompatibleHullIds.Count > 0
            && !module.CompatibleHullIds.Contains(hull.Id));

        HttpResponseMessage response = await SaveAsync(player, await CreateIdAsync(player, "Не той корпус", hull.Id), hull.Id, [.. kit, alien.Id]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fleet_not_compatible", await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// The race gives one more slot to the heavy hulls and to the super hulls, and no more slot
    /// to the light ones.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_race_gives_one_more_slot_to_the_heavy_hulls()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);

        ShipHull heavy = catalog.Hulls.First(entry => entry.AllowsRaceBonusSlot);
        ShipHull light = catalog.Hulls.First(entry => !entry.AllowsRaceBonusSlot);

        Assert.True(heavy.AllowsRaceBonusSlot);
        Assert.False(light.AllowsRaceBonusSlot);

        // The light hull takes no extra slot, so the project that asks for one is refused.
        HttpResponseMessage refused = await CreateAsync(player, "Легкий з бонусом", light.Id, true);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal("fleet_race_slot_unavailable", await TestAccounts.ReadErrorAsync(refused));

        // The heavy hull takes one more slot, so the summary of the project shows it.
        HttpResponseMessage accepted = await CreateAsync(player, "Важкий з бонусом", heavy.Id, true);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(Token));
        int total = body.RootElement.GetProperty("summary").GetProperty("total_slots").GetInt32();
        Assert.Equal(heavy.Slots + 1, total);
    }

    /// <summary>
    /// The basic hold of the hull takes the least set of ammunition.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_basic_hold_of_the_hull_takes_the_least_ammunition()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, FleetConfigKeys.MinimumAmmunition, "1000");
        }

        HttpResponseMessage response = await CreateAsync(player, "Малий трюм", hull.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("fleet_hold_too_small", await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// The change of the hull clears the slots that do not fit the new hull.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_change_of_the_hull_clears_the_slots_that_do_not_fit_it()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 5);

        ShipCatalog catalog = await ReadCatalogAsync(player);

        // The heavy hull takes one more slot when the race gives it, so the project has one
        // slot more than the peaceful hull has. The two hulls are picked with the components
        // that go into both of them, so the change of the hull is the only thing that differs.
        (ShipHull heavy, ShipHull light, int[] kit) = catalog.Hulls
            .Where(hull => hull.AllowsRaceBonusSlot)
            .SelectMany(heavy => catalog.Hulls
                .Where(light => !light.AllowsRaceBonusSlot)
                .Select(light => (heavy, light, kit: CommonKit(catalog, heavy, light))))
            .Where(pair => pair.kit.Length == 3)
            .First();

        // The ammunition fits every hull, so it fills the rest of the slots.
        ShipModule filler = catalog.Modules.First(module => module.CompatibleHullIds.Count == 0);

        int[] slots = [.. kit.Concat(Enumerable.Repeat(filler.Id, heavy.Slots + 1 - kit.Length))];

        HttpResponseMessage created = await CreateAsync(player, "Великий", heavy.Id, true);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using JsonDocument createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token));
        long designId = createdBody.RootElement.GetProperty("id").GetInt64();

        HttpResponseMessage saved = await SaveAsync(player, designId, heavy.Id, slots, true);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        using JsonDocument savedBody = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Token));
        Assert.Equal(heavy.Slots + 1, savedBody.RootElement.GetProperty("slots").GetArrayLength());

        // The hull changes and the new hull has fewer slots, so the slots that do not fit are
        // cleared (task 00038, section 6, question 7).
        HttpResponseMessage changed = await SaveAsync(player, designId, light.Id, slots);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        using JsonDocument changedBody = JsonDocument.Parse(await changed.Content.ReadAsStringAsync(Token));
        Assert.Equal(light.Slots, changedBody.RootElement.GetProperty("slots").GetArrayLength());
    }

    /// <summary>
    /// Twenty parallel commands do not create more projects than the bureau allows.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_parallel_commands_do_not_break_the_limit()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 3);

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.HullWithComponents("combat");

        Task<HttpResponseMessage>[] requests = [.. Enumerable.Range(0, 20)
            .Select(index => CreateAsync(player, "Проєкт " + index, hull.Id))];

        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        int accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        int refused = responses.Count(response => response.StatusCode == HttpStatusCode.UnprocessableEntity);

        // The level 3 of the design bureau gives four projects.
        Assert.Equal(4, accepted);
        Assert.Equal(16, refused);

        using IServiceScope scope = _factory.Services.CreateScope();
        int stored = await scope.ServiceProvider.GetRequiredService<Aetheris.Fleet.Data.FleetDbContext>().ShipDesigns
            .AsNoTracking()
            .CountAsync(design => design.OwnerPlayerId == player.Account.UserId, Token);

        Assert.Equal(4, stored);
    }

    /// <summary>
    /// The catalog of the ships tells the hulls and the modules, and the number of the projects
    /// the player may keep.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_catalog_of_the_ships_tells_the_hulls_and_the_modules()
    {
        await ResetAsync();
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 3);

        ShipCatalog catalog = await ReadCatalogAsync(player);

        Assert.NotEmpty(catalog.Hulls);
        Assert.NotEmpty(catalog.Modules);
        Assert.Equal(4, catalog.DesignLimit);
        Assert.Equal(0, catalog.DesignCount);

        // A drone is carried in the hold and a structure is not a ship, so neither of them is
        // in the catalog of the constructor (task 00042, decision 3).
        Assert.DoesNotContain(catalog.Hulls, hull => hull.Role == "drone");
        Assert.DoesNotContain(catalog.Hulls, hull => hull.Role == "civil");

        // Every hull tells the number of its slots, and every module tells the hulls it fits.
        Assert.All(catalog.Hulls, hull => Assert.True(hull.Slots > 0));
        Assert.Contains(catalog.Modules, module => module.Component == "drive");
        Assert.Contains(catalog.Modules, module => module.Component == "reactor");
        Assert.Contains(catalog.Modules, module => module.Component == "life_support");
    }

    /// <summary>
    /// Prepares the world, the bodies and the production, as a fresh server does at start.
    /// </summary>
    /// <returns>A task that completes when the modules are ready.</returns>
    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, Aetheris.Production.Configuration.ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<Aetheris.Production.IProductionModule>().EnsureAsync(Token);
    }

    /// <summary>
    /// Sets the level of the design bureau on the body of the player.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="level">The level of the bureau.</param>
    /// <returns>A task that completes when the level is written.</returns>
    private async Task SetBureauLevelAsync(long planetId, int level)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, planetId, DesignBureau, level);
    }

    /// <summary>
    /// Reads the catalog of the ships of the player.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The catalog.</returns>
    private async Task<ShipCatalog> ReadCatalogAsync(PlanetsTestPlayer player)
    {
        HttpResponseMessage response = await player.Client.GetAsync($"{Catalog}?planet_id={player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return new ShipCatalog(
            [.. body.RootElement.GetProperty("hulls").EnumerateArray().Select(ReadHull)],
            [.. body.RootElement.GetProperty("modules").EnumerateArray().Select(ReadModule)],
            body.RootElement.GetProperty("design_limit").GetInt32(),
            body.RootElement.GetProperty("design_count").GetInt32());
    }

    /// <summary>
    /// Creates a project of a ship and returns the answer.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="name">The name of the project.</param>
    /// <param name="hullId">The hull of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> CreateAsync(
        PlanetsTestPlayer player,
        string name,
        int hullId,
        bool hasRaceBonusSlot = false) =>
        SendAsync(player, Designs, new
        {
            planet_id = player.PlanetId,
            name,
            hull_part_type_id = hullId,
            has_race_bonus_slot = hasRaceBonusSlot,
        });

    /// <summary>
    /// Creates a project of a ship and returns its identifier.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="name">The name of the project.</param>
    /// <param name="hullId">The hull of the project.</param>
    /// <returns>The identifier of the project.</returns>
    private async Task<long> CreateIdAsync(PlanetsTestPlayer player, string name, int hullId)
    {
        HttpResponseMessage response = await CreateAsync(player, name, hullId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return body.RootElement.GetProperty("id").GetInt64();
    }

    /// <summary>
    /// Saves the slots of a project of a ship.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="designId">The project.</param>
    /// <param name="hullId">The hull of the project.</param>
    /// <param name="parts">The modules of the project.</param>
    /// <param name="hasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> SaveAsync(
        PlanetsTestPlayer player,
        long designId,
        int hullId,
        IReadOnlyCollection<int> parts,
        bool hasRaceBonusSlot = false) =>
        SendAsync(player, Designs + "/save", new
        {
            design_id = designId,
            hull_part_type_id = hullId,
            has_race_bonus_slot = hasRaceBonusSlot,
            slots = parts.Select((id, index) => new { slot_index = index, part_type_id = id, mark_level = 1, has_race_bonus = false }).ToArray(),
        });

    /// <summary>
    /// Sends a command of the module.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="route">The route of the command.</param>
    /// <param name="body">The body of the command.</param>
    /// <returns>The response.</returns>
    private Task<HttpResponseMessage> SendAsync(PlanetsTestPlayer player, string route, object body) =>
        TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));

    private static ShipHull ReadHull(JsonElement element) => new(
        element.GetProperty("id").GetInt32(),
        element.GetProperty("role").GetString() ?? string.Empty,
        element.GetProperty("slots").GetInt32(),
        element.GetProperty("allows_race_bonus_slot").GetBoolean());

    private static ShipModule ReadModule(JsonElement element) => new(
        element.GetProperty("id").GetInt32(),
        element.GetProperty("role").GetString() ?? string.Empty,
        element.GetProperty("component").GetString(),
        element.GetProperty("energy_use").GetDecimal(),
        element.GetProperty("power").GetDecimal(),
        [.. element.GetProperty("compatible_hull_ids").EnumerateArray().Select(id => id.GetInt32())]);

    /// <summary>
    /// The catalog of the ships as the test reads it.
    /// </summary>
    /// <param name="Hulls">The hulls.</param>
    /// <param name="Modules">The modules.</param>
    /// <param name="DesignLimit">How many projects the player may keep.</param>
    /// <param name="DesignCount">How many projects the player has.</param>
    private sealed record ShipCatalog(
        IReadOnlyList<ShipHull> Hulls,
        IReadOnlyList<ShipModule> Modules,
        int DesignLimit,
        int DesignCount)
    {
        /// <summary>
        /// Finds the hull of the role that takes a drive, a reactor and the life support, so
        /// the test may build a ship of it.
        /// </summary>
        /// <param name="role">The role of the hull.</param>
        /// <returns>The hull.</returns>
        public ShipHull HullWithComponents(string role) =>
            Hulls.First(hull => hull.Role == role
                && ComponentsFor(hull, "drive").Count > 0
                && ComponentsFor(hull, "reactor").Count > 0
                && ComponentsFor(hull, "life_support").Count > 0);

        /// <summary>
        /// Finds the modules of the kind that go into the hull.
        /// </summary>
        /// <param name="hull">The hull.</param>
        /// <param name="component">The kind of the component.</param>
        /// <returns>The modules.</returns>
        public IReadOnlyList<ShipModule> ComponentsFor(ShipHull hull, string component) =>
            [.. Modules.Where(module => module.Component == component && module.Fits(hull))];
    }

    /// <summary>
    /// Finds the drive, the reactor and the life support that go into both hulls.
    /// </summary>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="heavy">The heavy hull.</param>
    /// <param name="light">The peaceful hull.</param>
    /// <returns>The identifiers of the components, or an empty list when the hulls share none.</returns>
    private static int[] CommonKit(ShipCatalog catalog, ShipHull heavy, ShipHull light)
    {
        ShipModule? drive = catalog.ComponentsFor(heavy, "drive").FirstOrDefault(module => module.Fits(light));
        ShipModule? reactor = catalog.ComponentsFor(heavy, "reactor").FirstOrDefault(module => module.Fits(light));
        ShipModule? lifeSupport = catalog.ComponentsFor(heavy, "life_support").FirstOrDefault(module => module.Fits(light));

        if (drive is null || reactor is null || lifeSupport is null)
        {
            return [];
        }

        return [drive.Id, reactor.Id, lifeSupport.Id];
    }

    /// <summary>
    /// Finds the module that takes more energy than the reactor gives and that goes into the
    /// hull.
    /// </summary>
    /// <param name="catalog">The catalog of the ships.</param>
    /// <param name="hull">The hull.</param>
    /// <param name="reactor">The reactor of the hull.</param>
    /// <returns>The module, or <see langword="null"/> when the hull has none.</returns>
    private static ShipModule? Hungry(ShipCatalog catalog, ShipHull hull, ShipModule reactor) =>
        catalog.Modules
            .Where(module => module.Role == "combat" && module.EnergyUse > reactor.Power && module.Fits(hull))
            .OrderBy(module => module.EnergyUse)
            .FirstOrDefault();

    /// <summary>
    /// A hull of the catalog as the test reads it.
    /// </summary>
    /// <param name="Id">The hull.</param>
    /// <param name="Role">The role of the hull.</param>
    /// <param name="Slots">How many slots the hull has.</param>
    /// <param name="AllowsRaceBonusSlot">Whether the race gives the hull one more slot.</param>
    private sealed record ShipHull(int Id, string Role, int Slots, bool AllowsRaceBonusSlot);

    /// <summary>
    /// A module of the catalog as the test reads it.
    /// </summary>
    /// <param name="Id">The module.</param>
    /// <param name="Role">The role of the module.</param>
    /// <param name="Component">The kind of the component the module is.</param>
    /// <param name="EnergyUse">How much energy the module takes.</param>
    /// <param name="Power">How much power the module gives.</param>
    /// <param name="CompatibleHullIds">The hulls the module fits.</param>
    private sealed record ShipModule(
        int Id,
        string Role,
        string? Component,
        decimal EnergyUse,
        decimal Power,
        IReadOnlyList<int> CompatibleHullIds)
    {
        /// <summary>
        /// Tells whether the module goes into the hull: a module with no hulls of its own
        /// goes into any hull.
        /// </summary>
        /// <param name="hull">The hull.</param>
        /// <returns><see langword="true"/> when the module fits the hull.</returns>
        public bool Fits(ShipHull hull) =>
            CompatibleHullIds.Count == 0 || CompatibleHullIds.Contains(hull.Id);
    }
}

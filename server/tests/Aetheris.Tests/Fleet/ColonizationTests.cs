using System.Net;
using System.Text.Json;
using Aetheris.Fleet;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Planets.Configuration;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Research;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the colonization of a planet with the colony module (task 00073): the module is a
/// cargo and is not fitted to a hull, the command of the colonization is offered only with the
/// module in the hold, the planet belongs to the player after the deployment, the limit of the
/// planets is kept and the module and the cargo commands work as the orders of the plan do.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ColonizationTests
{
    private const string HullCode = "part_h_21";
    private const string ContainerCode = "part_m_66";
    private const string ColonyModuleCode = "part_m_70";
    private const string Spaceport = "building_6";
    private const string DesignBureau = "building_10";
    private const string Designs = "/api/v1/fleet/designs";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const string FlightPlans = "/api/v1/fleet/flight-plans";
    private const string CargoLoad = "/api/v1/fleet/fleets/cargo-load";
    private const string CargoUnload = "/api/v1/fleet/fleets/cargo-unload";
    private const int ContainerCount = 2;
    private const long ShipCrew = 7;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColonizationTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public ColonizationTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The colony module is only a cargo: it is never fitted to a hull, so the project that names
    /// it in a slot is refused with the code of a cargo that is not a module of a ship.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_colony_module_is_not_fitted_to_a_slot()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await partIdsAsync(scope.ServiceProvider, HullCode, ColonyModuleCode);

        using HttpResponseMessage created = await TestAccounts.SendCommandAsync(
            player.Client,
            Designs,
            new { planet_id = player.PlanetId, name = "Проєкт", hull_part_type_id = partIds[HullCode] },
            Guid.NewGuid().ToString("N"));
        string createdText = await created.Content.ReadAsStringAsync(Token);

        Assert.True(created.StatusCode == HttpStatusCode.OK, createdText);

        long designId = JsonDocument.Parse(createdText).RootElement.GetProperty("id").GetInt64();

        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            Designs + "/save",
            new
            {
                design_id = designId,
                hull_part_type_id = partIds[HullCode],
                has_race_bonus_slot = false,
                slots = new[] { new { slot_index = 0, part_type_id = partIds[ColonyModuleCode], mark_level = 1, has_race_bonus = false } },
            },
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, text);
        Assert.Equal(FleetErrorCodes.DesignPartCargoOnly, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// The catalog says that the colony module is a cargo and that it opens the order of the
    /// colonization, so the client knows it without naming it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_catalog_marks_the_colony_module_as_a_cargo()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await partIdsAsync(scope.ServiceProvider, ColonyModuleCode);
        ShipCatalogDto catalog = await scope.ServiceProvider.GetRequiredService<IProductionModule>().GetShipCatalogAsync(Token);

        ShipModuleDto module = Assert.Single(catalog.Modules, entry => entry.Id == partIds[ColonyModuleCode]);

        Assert.True(module.CargoOnly);
        Assert.Equal(FlightOrderTypes.Colonize, module.CargoOrder);
    }

    /// <summary>
    /// The hold of a fleet with the colony module in it offers the order of the colonization, and
    /// the hold of a fleet without it does not. The number of the queries does not depend on the
    /// number of the ships of the fleet.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_hold_offers_the_colonization_only_with_the_module()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(player);
        long fleetId = await FormAsync(player, shipIds);

        JsonElement without = await ReadHoldAsync(player, fleetId);

        Assert.DoesNotContain(FlightOrderTypes.Colonize, OrdersOf(without));

        await PutColonyModuleAsync(scope: _factory.Services.CreateScope(), shipIds[0], fleetId);

        JsonElement with = await ReadHoldAsync(player, fleetId);

        Assert.Contains(FlightOrderTypes.Colonize, OrdersOf(with));
    }

    /// <summary>
    /// The command of the colonization is refused without the colony module in the hold of the
    /// fleet, and the fleet is refused with the code of a fleet that carries no colony module.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_colonization_is_refused_without_the_module()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(player);
        long fleetId = await FormAsync(player, shipIds);
        long planetId = await FreeBodyAsync(player);

        using HttpResponseMessage response = await SavePlanAsync(player, fleetId, player.PlanetId, planetId);

        Assert.Equal(FleetErrorCodes.ColonizeNoModule, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// The limit of the planets comes from the technology of the colonial administration: a
    /// player without it keeps the limit of the configuration, and the levels give the limits of
    /// the table of the task.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_limit_of_the_planets_comes_from_the_technology()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IResearchModule research = scope.ServiceProvider.GetRequiredService<IResearchModule>();

        int baseLimit = Aetheris.Research.Configuration.ResearchSettings.From(scope.ServiceProvider.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>()).ColonyBaseLimit;

        Assert.Equal(baseLimit, await research.GetPlanetLimitAsync(player.Account.UserId, Token));

        await SetTechnologyLevelAsync(scope.ServiceProvider, player, 3);

        Assert.Equal(2, await research.GetPlanetLimitAsync(player.Account.UserId, Token));

        await SetTechnologyLevelAsync(scope.ServiceProvider, player, 25);

        Assert.Equal(12, await research.GetPlanetLimitAsync(player.Account.UserId, Token));
    }

    /// <summary>
    /// The effect of the limit of the planets is in the seed of the colonial administration: the
    /// value of a level is absolute, and the levels grow and never fall.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_of_the_limit_of_the_planets_grows_and_never_falls()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        ResearchDbContext database = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();

        int technologyId = await database.Technologies.AsNoTracking()
            .Where(technology => technology.Code == "tech_17")
            .Select(technology => technology.Id)
            .SingleAsync(Token);

        var rows = await (
                from effect in database.TechnologyLevelEffects.AsNoTracking()
                join level in database.TechnologyLevels.AsNoTracking() on effect.TechnologyLevelId equals level.Id
                where level.TechnologyId == technologyId && effect.EffectType == TechnologyEffectTypes.PlanetLimit
                orderby level.Level
                select new { Level = level.Level, Value = effect.Value })
            .ToListAsync(Token);

        List<(int Level, decimal Value)> effects = [.. rows.Select(row => (Level: row.Level, Value: row.Value))];

        Assert.Equal(25, effects.Count);
        Assert.Equal(1m, effects[0].Value);
        Assert.Equal(2m, effects[2].Value);
        Assert.Equal(12m, effects[24].Value);

        for (int place = 1; place < effects.Count; place += 1)
        {
            Assert.True(effects[place].Value >= effects[place - 1].Value, $"The limit falls at the level {effects[place].Level}.");
        }
    }

    /// <summary>
    /// The types of the bodies that suit a race are the data of the table of the world: one type a
    /// race, and the oceanic type suits no race.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_of_the_types_of_the_bodies_suits_one_type_a_race()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        var types = await (
                from entry in world.RaceColonizableTypes.AsNoTracking()
                join race in world.Races.AsNoTracking() on entry.RaceId equals race.Id
                join type in world.ObjectTypes.AsNoTracking() on entry.ObjectTypeId equals type.Id
                orderby race.Code
                select new { Race = race.Code, Type = type.Code })
            .ToListAsync(Token);

        List<(string Race, string Type)> rows = [.. types.Select(row => (Race: row.Race, Type: row.Type))];

        Assert.Equal(
        [
            ("race_1", "object_type_5"),
            ("race_2", "object_type_7"),
            ("race_3", "object_type_6"),
            ("race_4", "object_type_8"),
        ], rows);
    }

    /// <summary>
    /// The commands of the hold work as the orders of the plan do: a load takes what the body has
    /// and an unload puts it back, and a fleet that is not on a body of its own is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_commands_of_the_hold_work_as_the_orders_of_the_plan()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(player);
        long fleetId = await FormAsync(player, shipIds);
        int[] resourceIds = await ResourceIdsAsync(2);

        using HttpResponseMessage loaded = await TestAccounts.SendCommandAsync(
            player.Client,
            CargoLoad,
            new { fleet_id = fleetId, items = new[] { new { kind = "resource", id = resourceIds[0], amount = 25 } } },
            Guid.NewGuid().ToString("N"));
        string loadText = await loaded.Content.ReadAsStringAsync(Token);

        Assert.True(loaded.StatusCode == HttpStatusCode.OK, loadText);

        JsonElement hold = JsonDocument.Parse(loadText).RootElement.Clone();

        Assert.Equal(25m, hold.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("resource_id").GetInt32() == resourceIds[0]).GetProperty("amount").GetInt64());

        using HttpResponseMessage unloaded = await TestAccounts.SendCommandAsync(
            player.Client,
            CargoUnload,
            new { fleet_id = fleetId, items = new[] { new { kind = "resource", id = resourceIds[0], amount = 25 } } },
            Guid.NewGuid().ToString("N"));
        string unloadText = await unloaded.Content.ReadAsStringAsync(Token);

        Assert.True(unloaded.StatusCode == HttpStatusCode.OK, unloadText);

        JsonElement emptied = JsonDocument.Parse(unloadText).RootElement.Clone();

        Assert.DoesNotContain(emptied.GetProperty("items").EnumerateArray(), item => item.GetProperty("resource_id").GetInt32() == resourceIds[0]);
    }

    /// <summary>
    /// A command of the hold that names nothing to carry is refused: the list of the items is
    /// obligatory, as it is in the orders of the plan.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_command_of_the_hold_needs_the_items()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(player);
        long fleetId = await FormAsync(player, shipIds);

        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            CargoLoad,
            new { fleet_id = fleetId, items = Array.Empty<object>() },
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.UnprocessableEntity, text);
    }

    /// <summary>
    /// The fleet is refused with the code of a fleet it does not know when the command of the hold
    /// names another fleet.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_command_of_the_hold_refuses_a_fleet_of_another_player()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using PlanetsTestPlayer other = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(other);
        long fleetId = await FormAsync(other, shipIds);
        int[] resourceIds = await ResourceIdsAsync(1);

        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            CargoLoad,
            new { fleet_id = fleetId, items = new[] { new { kind = "resource", id = resourceIds[0], amount = 1 } } },
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity, text);
        Assert.Equal(FleetErrorCodes.FleetUnknown, await TestAccounts.ReadErrorAsync(response));
    }

    private static string[] OrdersOf(JsonElement hold) =>
        [.. hold.GetProperty("available_orders").EnumerateArray().Select(order => order.GetString()!)];

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);
        await services.GetRequiredService<IFleetModule>().EnsureAsync(Token);
    }

    private async Task<PlanetsTestPlayer> FoundAsync()
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, 1);
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, DesignBureau, 3);

        return player;
    }

    private async Task<IReadOnlyDictionary<string, int>> partIdsAsync(IServiceProvider services, params string[] codes) =>
        await services.GetRequiredService<IProductionModule>().FindPartIdsByFileCodeAsync(codes, Token);

    /// <summary>
    /// Writes a project and two finished ships of it, as the shipyard leaves them.
    /// </summary>
    private async Task<List<long>> SeedShipsAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await partIdsAsync(scope.ServiceProvider, HullCode, ContainerCode);
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ShipDesignEntity design = new()
        {
            OwnerPlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            Name = "Проєкт " + Guid.NewGuid().ToString("N")[..6],
            HullPartTypeId = partIds[HullCode],
            CreatedAt = now,
            UpdatedAt = now,
        };
        database.ShipDesigns.Add(design);
        await database.SaveChangesAsync(Token);

        List<ShipEntity> ships =
        [
            NewShip(player, design, partIds, now, ContainerCount),
            NewShip(player, design, partIds, now, 0),
        ];
        database.Ships.AddRange(ships);
        await database.SaveChangesAsync(Token);

        return [.. ships.Select(ship => ship.Id)];
    }

    private static ShipEntity NewShip(
        PlanetsTestPlayer player,
        ShipDesignEntity design,
        IReadOnlyDictionary<string, int> partIds,
        DateTimeOffset now,
        int containers)
    {
        List<ShipModuleEntity> parts = [new ShipModuleEntity { PartTypeId = partIds[HullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true }];

        if (containers > 0)
        {
            parts.Add(new ShipModuleEntity { PartTypeId = partIds[ContainerCode], MarkLevel = 1, ConditionPercent = 100, ConditionCode = ShipModuleConditions.Intact, Quantity = containers });
        }

        return new ShipEntity
        {
            PlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            ShipDesignId = design.Id,
            Crew = ShipCrew,
            StateCode = ShipStates.Ready,
            CreatedAt = now,
            ModuleIds = parts,
        };
    }

    private async Task<long> FormAsync(PlanetsTestPlayer player, List<long> shipIds)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            Fleets,
            new { planet_id = player.PlanetId, name = "Флот", ship_ids = shipIds },
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64();
    }

    /// <summary>
    /// Puts the colony module into the hold of a ship of the fleet, as the loading does.
    /// </summary>
    private static async Task PutColonyModuleAsync(IServiceScope scope, long shipId, long fleetId)
    {
        IServiceProvider services = scope.ServiceProvider;
        IReadOnlyDictionary<string, int> partIds = await services.GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([ColonyModuleCode], Token);
        FleetDbContext database = services.GetRequiredService<FleetDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        database.ShipPartCargos.Add(new ShipPartCargoEntity
        {
            ShipId = shipId,
            PartTypeId = partIds[ColonyModuleCode],
            MarkLevel = 1,
            HasRaceBonus = false,
            Quantity = 1,
            UpdatedAt = now,
        });
        await database.SaveChangesAsync(Token);
    }

    /// <summary>
    /// Writes a plan with an order of the colonization at the end of it.
    /// </summary>
    private static async Task<HttpResponseMessage> SavePlanAsync(PlanetsTestPlayer player, long fleetId, long planetId, long targetId)
    {
        return await TestAccounts.SendCommandAsync(
            player.Client,
            FlightPlans,
            new
            {
                fleet_id = fleetId,
                orders = new[]
                {
                    new
                    {
                        type_code = "colonize",
                        target_kind = "planet",
                        target_sector_id = 0,
                        target_system_id = 0,
                        target_object_id = targetId,
                        target_x = 0,
                        target_y = 0,
                        target_z = 0,
                        duration_seconds = 0,
                        parameters = "{}",
                    },
                },
            },
            Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Sets the level of the colonial administration of a player, as the research does.
    /// </summary>
    private static async Task SetTechnologyLevelAsync(IServiceProvider services, PlanetsTestPlayer player, int level)
    {
        ResearchDbContext database = services.GetRequiredService<ResearchDbContext>();

        int technologyId = await database.Technologies.AsNoTracking()
            .Where(technology => technology.Code == "tech_17")
            .Select(technology => technology.Id)
            .SingleAsync(Token);

        await database.PlayerTechnologies
            .Where(entry => entry.PlayerId == player.Account.UserId)
            .ExecuteDeleteAsync(Token);

        database.PlayerTechnologies.Add(new PlayerTechnologyEntity
        {
            PlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            TechnologyId = technologyId,
            Level = level,
        });
        await database.SaveChangesAsync(Token);
    }

    /// <summary>
    /// Finds a free body of the sector of the home body of the player.
    /// </summary>
    private async Task<long> FreeBodyAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        int sectorId = await world.Planets.AsNoTracking()
            .Where(entry => entry.Id == player.PlanetId)
            .Select(entry => entry.SectorId)
            .SingleAsync(Token);

        return await world.Planets.AsNoTracking()
            .Where(planet => planet.SectorId == sectorId && planet.OwnerPlayerId == null)
            .Join(world.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet, entry })
            .Join(world.ObjectTypes, pair => pair.entry.ObjectTypeId, type => type.Id, (pair, type) => new { pair.planet, type })
            .Where(row => row.type.IsColonizable)
            .OrderBy(row => row.planet.Id)
            .Select(row => row.planet.Id)
            .FirstAsync(Token);
    }

    private async Task<int[]> ResourceIdsAsync(int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        return [.. catalog.Resources.OrderBy(resource => resource.Id).Take(count).Select(resource => resource.Id)];
    }

    private static async Task<JsonElement> ReadHoldAsync(PlanetsTestPlayer player, long fleetId)
    {
        using HttpResponseMessage response = await player.Client.GetAsync($"{Fleets}/{fleetId}/hold", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }
}

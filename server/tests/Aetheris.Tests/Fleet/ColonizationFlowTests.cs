using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Core.Time;
using Aetheris.Fleet;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Host.Observability;
using Aetheris.Planets.Data;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Research;
using Aetheris.Research.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the way of a colonization (task 00073): the plan the button «Колонізувати» of the map
/// writes, the colonization of the planet the fleet stands on, the planet taken on the way, the
/// planet that does not suit the race, the limit of the planets with two fleets, the module that
/// fits no hold, the colonization without a time of deployment and the cost of the hold in queries.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class ColonizationFlowTests : IAsyncLifetime
{
    private const string HullCode = "part_h_21";
    private const string ContainerCode = "part_m_66";
    private const string NormalDrive = "part_m_1";
    private const string HyperDrive = "part_m_5";
    private const string ColonyModuleCode = "part_m_70";
    private const string Spaceport = "building_6";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const string Plans = "/api/v1/fleet/flight-plans";
    private const string Execute = "/api/v1/fleet/flight-plans/execute";
    private const string Colonize = "/api/v1/fleet/flight-plans/colonize";
    private const string CargoLoad = "/api/v1/fleet/fleets/cargo-load";
    private const string FuelResource = "resource_12";
    private const int MaxSteps = 12;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    private DriveConsumptionScope? _consumption;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColonizationFlowTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public ColonizationFlowTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Prepares a test of the class; the consumption of the drives is set by the reset of the test.
    /// </summary>
    /// <returns>A task that completes at once.</returns>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Puts the consumption of the drives back after the test.
    /// </summary>
    /// <returns>A task that completes when the drives are put back.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_consumption is not null)
        {
            await _consumption.DisposeAsync();
        }
    }

    /// <summary>
    /// The plan of a colonization for a fleet without a hyperdrive is an ordinary flight to the planet
    /// and the colonization, which names no target (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_plan_of_a_fleet_without_a_hyperdrive_is_a_flight_and_the_colonization()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long planetId = await SuitablePlanetAsync(player, otherSystem: true);

        JsonElement plan = await CommandAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId });
        List<JsonElement> orders = [.. plan.GetProperty("orders").EnumerateArray()];

        Assert.Equal("draft", plan.GetProperty("state_code").GetString());
        Assert.Equal(["fly", "colonize"], orders.Select(order => order.GetProperty("type_code").GetString()));
        Assert.Equal(await ObjectOfAsync(planetId), orders[0].GetProperty("target_object_id").GetInt64());
        Assert.True(orders[0].GetProperty("duration_seconds").GetInt64() > 0);
        Assert.Equal("none", orders[1].GetProperty("target_kind").GetString());
    }

    /// <summary>
    /// The plan of a colonization for a fleet with a hyperdrive on every ship is the hyperflight spread
    /// over its real commands, each with its own end and time, and the colonization at the end
    /// (task 00073, task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_plan_of_a_fleet_with_a_hyperdrive_is_the_hyperflight_and_the_colonization()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, HyperDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long planetId = await SuitablePlanetAsync(player, otherSystem: true);

        JsonElement plan = await CommandAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId });
        List<JsonElement> orders = [.. plan.GetProperty("orders").EnumerateArray()];

        Assert.Equal("colonize", orders[^1].GetProperty("type_code").GetString());

        JsonElement ftl = Assert.Single(orders, order => order.GetProperty("leg").ValueKind == JsonValueKind.String && order.GetProperty("leg").GetString() == "ftl");

        Assert.Equal("hyperfly", ftl.GetProperty("type_code").GetString());
        Assert.All(orders.SkipLast(1), order => Assert.True(order.GetProperty("duration_seconds").GetInt64() > 0, order.GetRawText()));
        long objectId = await ObjectOfAsync(planetId);

        Assert.Contains(orders, order => order.GetProperty("target_object_id").GetInt64() == objectId);
    }

    /// <summary>
    /// A fleet flies to the planet and colonizes it (task 00073): the planet belongs to the player,
    /// it has the start buildings of the registration, the module is gone and the command burned no fuel.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_flies_to_the_planet_and_colonizes_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        await FuelAsync(fleetId, shipId, 1000);
        long planetId = await SuitablePlanetAsync(player, otherSystem: true);

        await CommandAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
        await RunToTheEndAsync(fleetId);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        PlanetsDbContext planets = scope.ServiceProvider.GetRequiredService<PlanetsDbContext>();
        FleetDbContext fleets = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        Assert.Equal(player.Account.UserId, await world.Planets.Where(entry => entry.Id == planetId).Select(entry => entry.OwnerPlayerId).SingleAsync(Token));

        // The start buildings of the registration: every type with the flag and the building of the race.
        int startTypes = await planets.BuildingTypes.CountAsync(type => type.IsStartBuilding && !type.IsArchived, Token);
        int colonyBuildings = await planets.PlanetBuildings.CountAsync(entry => entry.PlanetId == planetId, Token);

        Assert.InRange(colonyBuildings, startTypes, startTypes + 1);
        Assert.False(await fleets.ShipPartCargos.AnyAsync(entry => entry.ShipId == shipId && entry.Quantity > 0, Token));
        Assert.Equal(1000L, await fleets.FleetCargos.Where(entry => entry.FleetId == fleetId).SumAsync(entry => entry.Amount, Token));
    }

    /// <summary>
    /// A planet another player takes while the fleet flies to it stops the plan with the code of a taken
    /// planet, and the fleet keeps its module (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_planet_taken_on_the_way_stops_the_plan_and_the_module_stays()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using PlanetsTestPlayer other = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long planetId = await SuitablePlanetAsync(player, otherSystem: true);

        await CommandAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

            await world.Planets.Where(entry => entry.Id == planetId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.OwnerPlayerId, other.Account.UserId), Token);
        }

        await RunToTheEndAsync(fleetId);

        using IServiceScope after = _factory.Services.CreateScope();
        FleetDbContext fleets = after.ServiceProvider.GetRequiredService<FleetDbContext>();
        FlightPlanEntity plan = await fleets.FlightPlans.AsNoTracking().SingleAsync(entry => entry.FleetId == fleetId, Token);

        Assert.Equal(FlightPlanStates.Paused, plan.StateCode);
        Assert.Equal(FleetErrorCodes.ColonizeTaken, plan.PauseCode);
        Assert.True(await fleets.ShipPartCargos.AnyAsync(entry => entry.ShipId == shipId && entry.Quantity > 0, Token));
    }

    /// <summary>
    /// A planet whose type does not suit the race of the player is refused (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_planet_that_does_not_suit_the_race_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long planetId = await UnsuitablePlanetAsync(player);

        Assert.Equal(FleetErrorCodes.ColonizeUnsuitable, await RefusalAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId }));
    }

    /// <summary>
    /// A fleet without the colony module and a player at the limit of the planets are refused
    /// (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_without_the_module_and_a_player_at_the_limit_are_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        long planetId = await SuitablePlanetAsync(player, otherSystem: true);

        Assert.Equal(FleetErrorCodes.ColonizeNoModule, await RefusalAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId }));

        await PutModuleAsync(shipId);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ResearchConfigKeys.ColonyBaseLimit, "1");
        }

        Assert.Equal(FleetErrorCodes.ColonizeLimit, await RefusalAsync(player, Colonize, new { fleet_id = fleetId, planet_id = planetId }));
    }

    /// <summary>
    /// Two fleets of one player that colonize at once cannot pass the limit of the planets: the
    /// limit is checked again under the lock of the player when the deployment ends (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Two_fleets_colonizing_at_once_do_not_pass_the_limit()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long firstFleet, long firstShip) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        (long secondFleet, long secondShip) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(firstShip);
        await PutModuleAsync(secondShip);
        long[] planetIds = await SuitablePlanetsAsync(player, 2);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ResearchConfigKeys.ColonyBaseLimit, "2");
        }

        // Both fleets stand on their planets and colonize them: the home planet and one colony fit the limit.
        await PlaceAsync(firstFleet, planetIds[0]);
        await PlaceAsync(secondFleet, planetIds[1]);

        foreach (long fleetId in new[] { firstFleet, secondFleet })
        {
            await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = new object[] { new { type_code = "colonize" } } });
            await CommandAsync(player, Execute, new { fleet_id = fleetId });
        }

        await RunToTheEndAsync(firstFleet);
        await RunToTheEndAsync(secondFleet);

        using IServiceScope after = _factory.Services.CreateScope();
        WorldDbContext world = after.ServiceProvider.GetRequiredService<WorldDbContext>();
        FleetDbContext fleets = after.ServiceProvider.GetRequiredService<FleetDbContext>();

        Assert.Equal(1, await world.Planets.CountAsync(entry => planetIds.Contains(entry.Id) && entry.OwnerPlayerId == player.Account.UserId, Token));
        Assert.Single(await fleets.FlightPlans.AsNoTracking().Where(entry => entry.PauseCode == FleetErrorCodes.ColonizeLimit).ToListAsync(Token));
    }

    /// <summary>
    /// A colony module heavier than the room of any one hold of the fleet is not loaded (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_module_heavier_than_every_hold_is_not_loaded()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, _) = await FleetAsync(player, [NormalDrive]);
        int moduleId = await StockModuleAsync(player);

        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            CargoLoad,
            new { fleet_id = fleetId, items = new[] { new { kind = "part", id = moduleId, mark_level = 1, has_race_bonus = false, amount = 1 } } },
            Guid.NewGuid().ToString("N"));

        Assert.Equal(FleetErrorCodes.CargoOverflow, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// A colony module without a time of deployment colonizes the planet at once, in the step of the
    /// plan (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_module_without_a_time_of_deployment_colonizes_at_once()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        await PutModuleAsync(shipId);
        long planetId = (await SuitablePlanetsAsync(player, 1))[0];
        await PlaceAsync(fleetId, planetId);

        await using DeployTimeScope deploy = await DeployTimeScope.ZeroAsync(_factory.Services, Token);
        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = new object[] { new { type_code = "colonize" } } });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        Assert.Equal(player.Account.UserId, await world.Planets.Where(entry => entry.Id == planetId).Select(entry => entry.OwnerPlayerId).SingleAsync(Token));
    }

    /// <summary>
    /// The hold of a fleet tells the orders its cargo opens with as many queries for three ships as
    /// for one (task 00073).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_orders_of_the_hold_ask_the_database_the_same_for_one_ship_and_for_three()
    {
        await ResetAsync();
        using PlanetsTestPlayer small = await FoundAsync();
        using PlanetsTestPlayer large = await FoundAsync();
        (long smallFleet, long smallShip) = await FleetAsync(small, [NormalDrive, ContainerCode]);
        (long largeFleet, long largeShip) = await FleetOfShipsAsync(large, 3);
        await PutModuleAsync(smallShip);
        await PutModuleAsync(largeShip);

        int one = await HoldCommandsAsync(small, smallFleet);
        int three = await HoldCommandsAsync(large, largeFleet);

        Assert.Equal(one, three);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, ResearchConfigKeys.ColonyBaseLimit, "5");
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);
        await services.GetRequiredService<IFleetModule>().EnsureAsync(Token);

        // The flights of these tests are not about their cost: the drives burn nothing (task 00072).
        if (_consumption is null)
        {
            _consumption = await DriveConsumptionScope.SetAsync(_factory.Services, 0m, Token);
        }
        else
        {
            await _consumption.ApplyAsync(Token);
        }
    }

    private async Task<PlanetsTestPlayer> FoundAsync()
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, 3);

        return player;
    }

    /// <summary>
    /// Writes a fleet of one ship that carries the given modules, one of each.
    /// </summary>
    private async Task<(long FleetId, long ShipId)> FleetAsync(PlanetsTestPlayer player, string[] moduleCodes)
    {
        long shipId = await ShipAsync(player, moduleCodes);
        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот " + Guid.NewGuid().ToString("N")[..4], ship_ids = new[] { shipId } });
        long fleetId = view.GetProperty("fleets").EnumerateArray().Max(fleet => fleet.GetProperty("id").GetInt64());

        return (fleetId, shipId);
    }

    private async Task<(long FleetId, long FirstShipId)> FleetOfShipsAsync(PlanetsTestPlayer player, int count)
    {
        List<long> shipIds = [];

        for (int index = 0; index < count; index++)
        {
            shipIds.Add(await ShipAsync(player, [NormalDrive, ContainerCode]));
        }

        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот", ship_ids = shipIds });

        return (view.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64(), shipIds[0]);
    }

    private async Task<long> ShipAsync(PlanetsTestPlayer player, string[] moduleCodes)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([HullCode, .. moduleCodes], Token);
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

        for (int index = 0; index < moduleCodes.Length; index++)
        {
            database.ShipDesignSlots.Add(new ShipDesignSlotEntity { DesignId = design.Id, SlotIndex = index, PartTypeId = partIds[moduleCodes[index]], MarkLevel = 1 });
        }

        ShipEntity ship = new()
        {
            PlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            ShipDesignId = design.Id,
            Crew = 7,
            StateCode = ShipStates.Ready,
            CreatedAt = now,
            ModuleIds =
            [
                new ShipModuleEntity { PartTypeId = partIds[HullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true },
                .. moduleCodes.Select(code => new ShipModuleEntity { PartTypeId = partIds[code], MarkLevel = 1, ConditionPercent = 100, Quantity = 1 }),
            ],
        };
        database.Ships.Add(ship);
        await database.SaveChangesAsync(Token);

        return ship.Id;
    }

    private async Task PutModuleAsync(long shipId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider.GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([ColonyModuleCode], Token);
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        database.ShipPartCargos.Add(new ShipPartCargoEntity
        {
            ShipId = shipId,
            PartTypeId = partIds[ColonyModuleCode],
            MarkLevel = 1,
            HasRaceBonus = false,
            Quantity = 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await database.SaveChangesAsync(Token);
    }

    private async Task FuelAsync(long fleetId, long shipId, long amount)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        int resourceId = catalog.Resources.Single(resource => resource.Code == FuelResource).Id;
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        // The cargo lies in the hold of a ship, and the hold of the fleet is the sum of its ships.
        database.ShipCargos.Add(new ShipCargoEntity { ShipId = shipId, ResourceId = resourceId, Amount = amount, UpdatedAt = DateTimeOffset.UtcNow });
        database.FleetCargos.Add(new FleetCargoEntity { FleetId = fleetId, ResourceId = resourceId, Amount = amount, UpdatedAt = DateTimeOffset.UtcNow });
        await database.SaveChangesAsync(Token);
    }

    private async Task<int> StockModuleAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider.GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([ColonyModuleCode], Token);
        ProductionDbContext production = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
        int sectorId = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Planets
            .Where(entry => entry.Id == player.PlanetId)
            .Select(entry => entry.SectorId)
            .SingleAsync(Token);

        production.PartStocks.Add(new PartStockEntity { PlanetId = player.PlanetId, SectorId = sectorId, PartTypeId = partIds[ColonyModuleCode], MarkLevel = 1, Quantity = 1 });
        await production.SaveChangesAsync(Token);

        return partIds[ColonyModuleCode];
    }

    /// <summary>
    /// Puts a fleet on a planet, as an arrival does.
    /// </summary>
    private async Task PlaceAsync(long fleetId, long planetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldPlaceDto place = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetPlanetPlaceAsync(planetId, Token)
            ?? throw new InvalidOperationException("The planet has no place.");
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        FleetEntity fleet = await database.Fleets.SingleAsync(entry => entry.Id == fleetId, Token);

        FleetFlightService.Place(fleet, place);
        await database.SaveChangesAsync(Token);
    }

    private async Task<long> ObjectOfAsync(long planetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Planets
            .Where(entry => entry.Id == planetId)
            .Select(entry => entry.CelestialObjectId)
            .SingleAsync(Token);
    }

    private async Task<long> SuitablePlanetAsync(PlanetsTestPlayer player, bool otherSystem) =>
        (await PlanetsOfRaceAsync(player, suitable: true, otherSystem, 1))[0];

    private Task<long[]> SuitablePlanetsAsync(PlanetsTestPlayer player, int count) =>
        PlanetsOfRaceAsync(player, suitable: true, otherSystem: true, count);

    private async Task<long> UnsuitablePlanetAsync(PlanetsTestPlayer player) =>
        (await PlanetsOfRaceAsync(player, suitable: false, otherSystem: true, 1))[0];

    /// <summary>
    /// Finds free colonizable planets whose type suits, or does not suit, the race of the player.
    /// </summary>
    private async Task<long[]> PlanetsOfRaceAsync(PlanetsTestPlayer player, bool suitable, bool otherSystem, int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        int raceId = await world.Civilizations.Where(entry => entry.PlayerId == player.Account.UserId).Select(entry => entry.RaceId).SingleAsync(Token);
        long homeSystem = await world.Planets.Where(entry => entry.Id == player.PlanetId).Select(entry => entry.SystemId).SingleAsync(Token);
        int[] raceTypes = [.. await world.RaceColonizableTypes.Where(entry => entry.RaceId == raceId).Select(entry => entry.ObjectTypeId).ToListAsync(Token)];

        long[] found =
        [
            .. await world.Planets.AsNoTracking()
                .Where(planet => planet.OwnerPlayerId == null && (!otherSystem || planet.SystemId != homeSystem))
                .Join(world.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet.Id, entry.ObjectTypeId })
                .Join(world.ObjectTypes, row => row.ObjectTypeId, type => type.Id, (row, type) => new { row.Id, row.ObjectTypeId, type.IsColonizable })
                .Where(row => row.IsColonizable && raceTypes.Contains(row.ObjectTypeId) == suitable)
                .OrderBy(row => row.Id)
                .Select(row => row.Id)
                .Take(count)
                .ToListAsync(Token),
        ];

        Assert.True(found.Length == count, $"The world has fewer than {count} free planets of the kind the test needs.");

        return found;
    }

    /// <summary>
    /// Runs the scheduler event after event until the plan of the fleet is over or stopped.
    /// </summary>
    private async Task RunToTheEndAsync(long fleetId)
    {
        for (int step = 0; step < MaxSteps; step++)
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            FlightPlanEntity? plan = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FlightPlans.AsNoTracking()
                .SingleOrDefaultAsync(entry => entry.FleetId == fleetId, Token);

            if (plan is null || plan.StateCode != FlightPlanStates.Active)
            {
                return;
            }

            await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(3));
        }

        throw new InvalidOperationException("The plan of the fleet did not end.");
    }

    private async Task<int> HoldCommandsAsync(PlanetsTestPlayer player, long fleetId)
    {
        string path = $"{Fleets}/{fleetId}/hold";

        using (HttpResponseMessage warm = await player.Client.GetAsync(path, Token))
        {
            Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        }

        using HttpResponseMessage response = await player.Client.GetAsync(path, Token);
        Match count = CommandCountPattern().Match(response.Headers.GetValues(RequestTraceMiddleware.ServerTimingHeader).Single());

        Assert.True(count.Success);

        return int.Parse(count.Groups["count"].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("desc=\"(?<count>\\d+) ")]
    private static partial Regex CommandCountPattern();

    private async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.IsSuccessStatusCode, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string?> RefusalAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));

        return await TestAccounts.ReadErrorAsync(response);
    }

    /// <summary>
    /// Sets the time of deployment of every module to zero for a test and puts the old values back.
    /// </summary>
    private sealed class DeployTimeScope : IAsyncDisposable
    {
        private readonly IServiceProvider _services;
        private readonly Dictionary<(int PartTypeId, int CharacteristicId), decimal> _saved;

        private DeployTimeScope(IServiceProvider services, Dictionary<(int PartTypeId, int CharacteristicId), decimal> saved)
        {
            _services = services;
            _saved = saved;
        }

        public static async Task<DeployTimeScope> ZeroAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            using IServiceScope scope = services.CreateScope();
            ProductionDbContext database = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
            List<PartCharacteristicValueEntity> rows = await RowsAsync(database, cancellationToken);
            Dictionary<(int, int), decimal> saved = rows.ToDictionary(row => (row.PartTypeId, row.CharacteristicId), row => row.NumberValue);

            foreach (PartCharacteristicValueEntity row in rows)
            {
                row.NumberValue = 0m;
            }

            await database.SaveChangesAsync(cancellationToken);
            scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>().Invalidate();

            return new DeployTimeScope(services, saved);
        }

        public async ValueTask DisposeAsync()
        {
            using IServiceScope scope = _services.CreateScope();
            ProductionDbContext database = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

            foreach (PartCharacteristicValueEntity row in await RowsAsync(database, CancellationToken.None))
            {
                if (_saved.TryGetValue((row.PartTypeId, row.CharacteristicId), out decimal amount))
                {
                    row.NumberValue = amount;
                }
            }

            await database.SaveChangesAsync(CancellationToken.None);
            scope.ServiceProvider.GetRequiredService<ProductionCatalogCache>().Invalidate();
        }

        private static Task<List<PartCharacteristicValueEntity>> RowsAsync(ProductionDbContext database, CancellationToken cancellationToken) =>
            database.PartCharacteristicValues
                .Where(row => database.Characteristics.Any(characteristic => characteristic.Id == row.CharacteristicId
                    && characteristic.Kind == CharacteristicKinds.DeployTime))
                .ToListAsync(cancellationToken);
    }
}

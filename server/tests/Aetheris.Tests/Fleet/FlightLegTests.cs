using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Fleet;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Host.Observability;
using Aetheris.Planets;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the hyperflight, of the jump and of the fuel of the fleet (task 00072): the three legs
/// of a command, the speed of the hyperdrives, the preparation of the jump, the formula of the fuel
/// and the check of the fuel before the fleet leaves.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class FlightLegTests
{
    private const string HullCode = "part_h_21";
    private const string NormalDrive = "part_m_1";
    private const string HyperDrive = "part_m_5";
    private const string JumpDrive = "part_m_6";
    private const string Spaceport = "building_6";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const string Plans = "/api/v1/fleet/flight-plans";
    private const string Save = "/api/v1/fleet/flight-plans/save";
    private const string Execute = "/api/v1/fleet/flight-plans/execute";
    private const int MaxLegs = 8;
    private const int OpenSpaceTries = 64;
    private const double OpenSpaceStep = 37d;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightLegTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public FlightLegTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The seed of the production holds the fuel of the drives, and the shares of a drive add up to
    /// one (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_fuel_of_every_drive_adds_up_to_one()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext database = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();

        List<PartFuelEntity> rows = await database.PartFuels.AsNoTracking().ToListAsync(Token);

        Assert.NotEmpty(rows);

        foreach (IGrouping<int, PartFuelEntity> drive in rows.GroupBy(row => row.PartTypeId))
        {
            double sum = drive.Sum(row => row.Share);

            Assert.True(Math.Abs(sum - 1d) < 1e-9, $"The shares of the drive {drive.Key} add up to {sum}.");
        }
    }

    /// <summary>
    /// Every drive of the seed carries the class of the drives and the fuel of its own (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_drive_carries_the_class_and_the_fuel_of_its_own()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();
        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        ShipCatalogDto catalog = await production.GetShipCatalogAsync(Token);

        ShipModuleDto? hyper = catalog.Modules.FirstOrDefault(module => module.Code == HyperDrive);
        ShipModuleDto? jump = catalog.Modules.FirstOrDefault(module => module.Code == JumpDrive);

        Assert.NotNull(hyper);
        Assert.NotNull(jump);
        Assert.Equal("hyper", hyper!.EngineKind);
        Assert.Equal("jump", jump!.EngineKind);

        // The hyperdrive burns the dark matter and the ether, the jump drive the other way round.
        Assert.Equal(2, hyper!.Fuel?.Count);
        Assert.Equal(2, jump!.Fuel?.Count);
        Assert.Equal(1d, hyper!.Fuel!.Sum(row => row.Share), 6);
        Assert.Equal(1d, jump!.Fuel!.Sum(row => row.Share), 6);
    }

    /// <summary>
    /// The speed of a hyperflight comes from the hyperdrives alone and is multiplied by the
    /// multiplier of the configuration (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_speed_of_a_hyperflight_comes_from_the_hyperdrives_alone()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, HyperDrive]);

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetSpeedService speeds = scope.ServiceProvider.GetRequiredService<FleetSpeedService>();
        FleetSpeeds read = await speeds.ReadAsync(fleetId, Token);

        // The ordinary drives and the hyperdrives are counted apart, so the speed of the
        // hyperdrives is not the speed of the ordinary drives.
        Assert.True(read.Hyper.Current > 0m);
        Assert.True(read.Normal.Current > 0m);

        // A fleet without a hyperdrive has no speed for a hyperflight. The fleet of another player
        // is used, because a player has as many fleets as the spaceports allow.
        using PlanetsTestPlayer other = await FoundAsync(spaceportLevel: 3);
        long plainFleetId = await FleetAsync(other, [NormalDrive]);
        FleetSpeeds plain = await speeds.ReadAsync(plainFleetId, Token);
        Assert.Equal(0m, plain.Hyper.Current);
    }

    /// <summary>
    /// The preparation of a jump lasts as long as the preparation of the drives of the ships and
    /// grows past the reference distance (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_preparation_of_a_jump_comes_from_the_drive_of_the_ship()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, JumpDrive]);

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetSpeedService speeds = scope.ServiceProvider.GetRequiredService<FleetSpeedService>();
        FleetSpeeds read = await speeds.ReadAsync(fleetId, Token);

        // The seed gives the jump drive 900 seconds of preparation (task 00072).
        Assert.Equal(900m, read.JumpPrepSeconds);
    }

    /// <summary>
    /// The fuel of a leg is the sum of the drives of the class of the leg: every drive burns its
    /// own amount in a second of its work, split by the shares of its fuel (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_fuel_of_a_leg_is_the_sum_of_the_drives_of_its_class()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive]);

        using IServiceScope scope = _factory.Services.CreateScope();
        EngineKindsReader engines = scope.ServiceProvider.GetRequiredService<EngineKindsReader>();
        FleetEngines read = await engines.ReadAsync(fleetId, Token);

        FleetDrive drive = Assert.Single(read.Of("normal"));

        // The seed gives the jet engine 100 units of fuel in an hour of its work, and it burns the
        // deuterium alone, so an hour of its work costs 100 units of the deuterium.
        Assert.Equal(100m, drive.FuelUse);
        Assert.Single(drive.Fuel);
        Assert.Equal(1m, drive.Fuel[0].Share);

        Dictionary<int, decimal> fuel = FuelPlanner.BurnOf(read, "normal", 3600);

        Assert.Equal(100m, fuel.Single().Value);
    }

    /// <summary>
    /// A drive of the class that the command does not need burns nothing (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_fuel_of_a_leg_comes_only_from_the_drives_of_its_class()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, HyperDrive]);

        using IServiceScope scope = _factory.Services.CreateScope();
        EngineKindsReader engines = scope.ServiceProvider.GetRequiredService<EngineKindsReader>();
        FleetEngines read = await engines.ReadAsync(fleetId, Token);

        Dictionary<int, decimal> normal = FuelPlanner.BurnOf(read, "normal", 10);
        Dictionary<int, decimal> hyper = FuelPlanner.BurnOf(read, "hyper", 10);

        // The ordinary drives burn one resource and the hyperdrives two: the classes never share.
        Assert.Single(normal);
        Assert.Equal(2, hyper.Count);
    }

    /// <summary>
    /// A command of the player is spread over its legs by the server (task 00072): the legs share
    /// one key and the command that made them.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_command_is_spread_over_its_legs_by_the_server()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, HyperDrive]);

        JsonElement plan = await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "hyperfly", target_kind = "object", target_object_id = await OtherBodyAsync(player) } },
        });

        JsonElement orders = plan.GetProperty("orders");

        Assert.True(orders.GetArrayLength() >= 2, $"The plan has {orders.GetArrayLength()} orders: {plan.GetRawText()}");

        // Every leg of the command shares one key, names the command it comes from and keeps the
        // target of the command (task 00072, 2.3).
        string? groupKey = orders[0].GetProperty("group_key").GetString();
        long target = orders[0].GetProperty("target_object_id").GetInt64();

        Assert.NotNull(groupKey);
        Assert.NotEqual(0L, target);

        List<string?> legs = [];

        foreach (JsonElement order in orders.EnumerateArray())
        {
            Assert.Equal(groupKey, order.GetProperty("group_key").GetString());
            Assert.Equal("hyperfly", order.GetProperty("command").GetString());
            Assert.Equal(target, order.GetProperty("target_object_id").GetInt64());

            string? leg = order.GetProperty("leg").GetString();

            legs.Add(leg);

            // Only the leg between the systems is a hyperflight; the legs inside the systems are
            // ordinary flights.
            Assert.Equal(leg == "ftl" ? "hyperfly" : "fly", order.GetProperty("type_code").GetString());
        }

        // The target is not on the edge of its system, so the last leg flies from the edge to it.
        Assert.Equal("from_edge", legs[^1]);
        Assert.Equal("ftl", legs[^2]);

        if (legs.Count == 3)
        {
            Assert.Equal("to_edge", legs[0]);
        }
    }

    /// <summary>
    /// A command of the player that names a target in the system the fleet stands in is refused
    /// (task 00072): inside a system only the ordinary drives fly.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_command_in_the_system_of_the_fleet_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, HyperDrive]);

        WorldPlaceDto home = await HomeAsync(player);

        string text = await RefusalAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[]
            {
                new { type_code = "hyperfly", target_kind = "object", target_object_id = home.ObjectId ?? 0 },
            },
        });

        Assert.Contains(FleetErrorCodes.FtlSameSystem, text);
    }

    /// <summary>
    /// The fleet does not leave when its hold holds less fuel than the route needs (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_fleet_does_not_leave_without_the_fuel_of_its_route()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive]);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "fly", target_kind = "system", target_system_id = 12 } },
        });

        // The hold of the fleet holds no fuel at all, so the fleet may not leave.
        string text = await RefusalAsync(player, Execute, new { fleet_id = fleetId });

        Assert.Contains(FleetErrorCodes.FuelShort, text);
    }

    /// <summary>
    /// The estimate of the fuel names the need of every resource and the shortage (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_estimate_of_the_fuel_names_the_need_and_the_shortage()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive]);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "fly", target_kind = "system", target_system_id = 12 } },
        });

        using HttpResponseMessage response = await player.Client.GetAsync(
            $"/api/v1/fleet/flight-plans/plan-estimate?fleet_id={fleetId}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement estimate = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;

        Assert.True(estimate.GetProperty("fuel_need").GetArrayLength() > 0);
        Assert.True(estimate.GetProperty("shortages").GetArrayLength() > 0);
        Assert.True(estimate.GetProperty("legs").GetArrayLength() > 0);
    }

    /// <summary>
    /// The fuel is burned at the start of the flight, and the flight of the fleet says how much of
    /// every resource it burned (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_fuel_is_burned_at_the_start_of_the_flight()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive]);

        // The drives burn one unit a second for this test, so the hold of the fleet holds enough
        // for the flight and for the way home (task 00072).
        await using DriveConsumptionScope consumption = await DriveConsumptionScope.SetAsync(_factory.Services, 1m, Token);
        await FuelAsync(player, fleetId, 12, 100000);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "fly", target_kind = "system", target_system_id = 12 } },
        });

        JsonElement plan = await CommandAsync(player, Execute, new { fleet_id = fleetId });

        Assert.Equal("active", plan.GetProperty("state_code").GetString());

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        FleetFlightEntity? flight = await database.FleetFlights.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.FleetId == fleetId, Token);

        Assert.NotNull(flight);
        Assert.Equal("normal", flight!.LegKind);
        Assert.NotEqual("{}", flight.FuelJson);

        FleetCargoEntity? cargo = await database.FleetCargos.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.FleetId == fleetId && entry.ResourceId == 12, Token);

        Assert.NotNull(cargo);
        Assert.True(cargo!.Amount < 100000);
    }

    /// <summary>
    /// The project of a ship with a hyperdrive and without an ordinary drive is saved and says what
    /// is wrong with it (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_project_without_an_ordinary_drive_says_what_is_wrong_with_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);

        using IServiceScope scope = _factory.Services.CreateScope();
        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        IReadOnlyDictionary<string, int> partIds = await production.FindPartIdsByFileCodeAsync([HullCode, HyperDrive], Token);
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

        database.ShipDesignSlots.Add(new ShipDesignSlotEntity { DesignId = design.Id, SlotIndex = 0, PartTypeId = partIds[HyperDrive], MarkLevel = 1 });
        await database.SaveChangesAsync(Token);

        using HttpResponseMessage response = await player.Client.GetAsync(
            $"/api/v1/fleet/designs/{design.Id}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement view = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;

        // The project is saved, but it says what is wrong with it (task 00072).
        JsonElement warnings = view.GetProperty("warnings");

        Assert.Equal(1, warnings.GetArrayLength());
        Assert.Equal("fleet_ftl_needs_normal_drive", warnings[0].GetProperty("code").GetString());
        Assert.Equal(0, warnings[0].GetProperty("slot_indexes")[0].GetInt32());
    }

    /// <summary>
    /// A project with an ordinary drive says nothing about the drives (task 00072).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_project_with_an_ordinary_drive_has_no_warning()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);

        using IServiceScope scope = _factory.Services.CreateScope();
        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        IReadOnlyDictionary<string, int> partIds = await production.FindPartIdsByFileCodeAsync([HullCode, NormalDrive, HyperDrive], Token);
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

        database.ShipDesignSlots.Add(new ShipDesignSlotEntity { DesignId = design.Id, SlotIndex = 0, PartTypeId = partIds[NormalDrive], MarkLevel = 1 });
        database.ShipDesignSlots.Add(new ShipDesignSlotEntity { DesignId = design.Id, SlotIndex = 1, PartTypeId = partIds[HyperDrive], MarkLevel = 1 });
        await database.SaveChangesAsync(Token);

        using HttpResponseMessage response = await player.Client.GetAsync(
            $"/api/v1/fleet/designs/{design.Id}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement view = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;

        Assert.Empty(view.GetProperty("warnings").EnumerateArray());
    }

    /// <summary>
    /// A hyperflight is flown leg after leg (task 00072, criterion 1): the ordinary drives fly to the
    /// edge of the system, the hyperdrives fly between the systems, the ordinary drives fly from the
    /// edge to the target, and the fleet stands at the target at the end.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_hyperflight_is_flown_leg_after_leg_to_its_target()
    {
        await ResetAsync();
        await using DriveConsumptionScope consumption = await DriveConsumptionScope.SetAsync(_factory.Services, 0m, Token);
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, HyperDrive]);
        long target = await OtherBodyAsync(player);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "hyperfly", target_kind = "object", target_object_id = target } },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        List<FleetFlightEntity> flights = await FlyToTheEndAsync(fleetId);

        // The legs inside the systems are ordinary flights and the leg between them a hyperflight.
        Assert.Equal("hyper", flights[^2].LegKind);
        Assert.Equal("normal", flights[^1].LegKind);
        Assert.All(flights.Where(flight => flight.LegKind == "normal"), flight => Assert.Equal(flight.FromSystemId, flight.ToSystemId));

        FleetEntity fleet = await FleetRowAsync(fleetId);
        WorldPlaceDto place = await ObjectPlaceAsync(target);

        Assert.Equal(FleetStates.Stationed, fleet.StateCode);
        Assert.Equal(target, fleet.ObjectId);
        Assert.Equal(place.SystemId, fleet.SystemId);
    }

    /// <summary>
    /// A jump waits for its preparation and then takes no time (task 00072, criterion 1): the fleet
    /// stands at the edge of its system while the drives charge, the preparation grows with the
    /// distance past the reference one, and the fleet ends at the target.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_jump_waits_for_its_preparation_and_then_takes_no_time()
    {
        await ResetAsync();
        await using DriveConsumptionScope consumption = await DriveConsumptionScope.SetAsync(_factory.Services, 0m, Token);
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive, JumpDrive]);
        long target = await OtherBodyAsync(player);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "jump", target_kind = "object", target_object_id = target } },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        List<FleetFlightEntity> flights = await FlyToTheEndAsync(fleetId);
        FleetFlightEntity jump = Assert.Single(flights, flight => flight.LegKind == "jump");
        FleetSettings settings = FleetSettings.From(_factory.Services.GetRequiredService<IGameConfig>());

        // The preparation is the preparation of the drive, grown past the reference distance.
        long expected = (long)Math.Ceiling(900d * Math.Max(1d, jump.Distance / settings.JumpReferenceDistance));

        Assert.Equal(jump.ArrivesAt, jump.PrepEndsAt);
        Assert.Equal(expected, (long)(jump.ArrivesAt - jump.StartedAt).TotalSeconds);

        // While the drives charge, the fleet stands where the jump began; the jump itself takes no time.
        WorldPlaceDto during = FleetFlightService.PlaceOnRoute(jump, jump.StartedAt + TimeSpan.FromSeconds(1));

        Assert.Equal(jump.FromSystemId, during.SystemId);
        Assert.Equal(jump.FromOrbit, during.Orbit);
        Assert.Equal("normal", flights[^1].LegKind);

        FleetEntity fleet = await FleetRowAsync(fleetId);

        Assert.Equal(FleetStates.Stationed, fleet.StateCode);
        Assert.Equal(target, fleet.ObjectId);
    }

    /// <summary>
    /// The preparation of a jump is the same up to the reference distance and grows in proportion
    /// to the distance past it (task 00072, 2.2.4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_preparation_of_a_jump_grows_past_the_reference_distance()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        WorldPlaceDto home = await HomeAsync(player);
        FleetSettings settings = FleetSettings.From(_factory.Services.GetRequiredService<IGameConfig>());
        FleetSpeedSet none = new(0m, 0m, 0m);
        FleetSpeeds speeds = new(none, none, none, 900m);
        double reference = settings.JumpReferenceDistance;

        long near = FuelPlanner.SecondsOf(new FlightLeg("ftl", "jump", home, home, reference / 2d), speeds, settings, set => set.Full);
        long atReference = FuelPlanner.SecondsOf(new FlightLeg("ftl", "jump", home, home, reference), speeds, settings, set => set.Full);
        long far = FuelPlanner.SecondsOf(new FlightLeg("ftl", "jump", home, home, reference * 3d), speeds, settings, set => set.Full);

        Assert.Equal(900L, near);
        Assert.Equal(900L, atReference);
        Assert.Equal(2700L, far);
    }

    /// <summary>
    /// A fleet in open space has no leg to the edge, and a target in open space has no leg from the
    /// edge (task 00072, 2.2.1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Open_space_has_no_leg_to_or_from_an_edge()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        WorldPlaceDto home = await HomeAsync(player);
        WorldPlaceDto target = await ObjectPlaceAsync(await OtherBodyAsync(player));
        WorldPlaceDto space = await OpenSpaceAsync(home.SectorId);

        using IServiceScope scope = _factory.Services.CreateScope();
        FlightLegPlanner planner = scope.ServiceProvider.GetRequiredService<FlightLegPlanner>();

        IReadOnlyList<FlightLeg> fromSpace = await planner.PlanAsync(space, target, "hyper", Token);
        IReadOnlyList<FlightLeg> toSpace = await planner.PlanAsync(home, space, "hyper", Token);

        Assert.Equal(["ftl", "from_edge"], fromSpace.Select(leg => leg.Leg));
        Assert.Equal(space.X, fromSpace[0].From.X);

        Assert.DoesNotContain(toSpace, leg => leg.Leg == "from_edge");
        Assert.Equal("ftl", toSpace[^1].Leg);
        Assert.Equal(space.X, toSpace[^1].To.X);
    }

    /// <summary>
    /// A fleet with a ship that has no drive of the class of the command is refused with the list
    /// of the ships (task 00072, 2.2.5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_with_a_ship_without_the_drive_of_the_command_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        (long fleetId, IReadOnlyList<long> shipIds) = await FleetOfShipsAsync(player, [NormalDrive, HyperDrive], [NormalDrive]);

        string text = await RefusalAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "hyperfly", target_kind = "object", target_object_id = await OtherBodyAsync(player) } },
        });

        Assert.Contains(FleetErrorCodes.FtlShipMissing, text);

        JsonElement listed = JsonDocument.Parse(text).RootElement.GetProperty("details").GetProperty(FlightPlanRunner.ShipIdsDetail);

        Assert.Equal([shipIds[1]], listed.EnumerateArray().Select(entry => entry.GetInt64()));
    }

    /// <summary>
    /// A ship with a hyperdrive alone has no ordinary push, so it does not fly an ordinary flight
    /// (task 00072, 2.1.3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_ship_with_a_hyperdrive_alone_has_no_ordinary_speed()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [HyperDrive]);

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetSpeeds speeds = await scope.ServiceProvider.GetRequiredService<FleetSpeedService>().ReadAsync(fleetId, Token);

        Assert.Equal(0m, speeds.Normal.Empty);
        Assert.True(speeds.Hyper.Empty > 0m);
    }

    /// <summary>
    /// The check before the fleet leaves counts the legs with full holds (task 00072, 2.4.6): the
    /// slowest and the dearest case.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_check_of_the_fuel_counts_with_full_holds()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        WorldPlaceDto home = await HomeAsync(player);
        FleetSettings settings = FleetSettings.From(_factory.Services.GetRequiredService<IGameConfig>());
        FleetSpeedSet normal = new(10m, 5m, 2m);
        FleetSpeeds speeds = new(normal, normal, new FleetSpeedSet(0m, 0m, 0m), 0m);
        FleetEngines engines = new([new FleetDrive(1, "normal", 100m, 1m, 0m, 1, [new FleetFuelShare(12, 1m)])], [1]);

        FuelEstimate estimate = FuelPlanner.Estimate(engines, speeds, [new FlightLeg("normal", "normal", home, home, 100d)], settings);
        long full = FleetSpeedService.FlightSeconds(100d, 2m);

        Assert.Equal(full, estimate.Legs.Single().Seconds);
        Assert.Equal(decimal.Ceiling(full / 3600m), estimate.NeedByResource[12]);
        Assert.True(full > FleetSpeedService.FlightSeconds(100d, 5m));
    }

    /// <summary>
    /// The gravitational drive works only near a star (task 00072, 2.4.7): in open space it gives no
    /// push and burns nothing.
    /// </summary>
    [Fact]
    public void The_gravitational_drive_does_nothing_in_open_space()
    {
        FleetEngines engines = new([new FleetDrive(1, "gravity", 500m, 2m, 0m, 1, [new FleetFuelShare(13, 1m)])], [1]);

        Assert.Equal(500m, engines.PushOfShip(1, "normal"));
        Assert.Equal(0m, engines.PushOfShip(1, "normal", inSystem: false));
        Assert.Equal(2m, FuelPlanner.BurnOf(engines, "normal", 3600).Single().Value);
        Assert.Empty(FuelPlanner.BurnOf(engines, "normal", 3600, inSystem: false));
        Assert.Empty(engines.ShipsWithout("normal"));
    }

    /// <summary>
    /// The consumption of a drive is counted in units of fuel per hour of its work (task 00072,
    /// decision of the author of 13.11.2026): a drive of 3600 units an hour burns one unit in a
    /// second, and a part of a unit is rounded up once for the leg.
    /// </summary>
    [Fact]
    public void The_consumption_of_a_drive_is_counted_per_hour_of_work()
    {
        FleetEngines engines = new([new FleetDrive(1, "normal", 100m, 3600m, 0m, 1, [new FleetFuelShare(12, 1m)])], [1]);
        FleetEngines slow = new([new FleetDrive(1, "normal", 100m, 1m, 0m, 1, [new FleetFuelShare(12, 1m)])], [1]);

        Assert.Equal(1m, FuelPlanner.BurnOf(engines, "normal", 1).Single().Value);
        Assert.Equal(3600m, FuelPlanner.BurnOf(engines, "normal", 3600).Single().Value);
        Assert.Equal(1m, FuelPlanner.BurnOf(slow, "normal", 1).Single().Value);
    }

    /// <summary>
    /// The example of the author (task 00072, 13.11.2026): a fleet of 100 ships with a drive of 200
    /// units an hour flies a leg of 9:42:07 there and the same back, so it burns about
    /// 200 × 100 × 19.4 hours of the deuterium, rounded up once for every leg.
    /// </summary>
    [Fact]
    public void A_fleet_of_a_hundred_ships_burns_its_hours_of_work()
    {
        const long LegSeconds = (9 * 3600) + (42 * 60) + 7;
        const int Ships = 100;
        FleetEngines engines = new(
            [.. Enumerable.Range(1, Ships).Select(ship => new FleetDrive(ship, "normal", 100m, 200m, 0m, 1, [new FleetFuelShare(12, 1m)]))],
            [.. Enumerable.Range(1, Ships).Select(ship => (long)ship)]);

        decimal there = FuelPlanner.BurnOf(engines, "normal", LegSeconds).Single().Value;
        decimal back = FuelPlanner.BurnOf(engines, "normal", LegSeconds).Single().Value;
        decimal exact = 200m * Ships * LegSeconds * 2 / 3600m;

        Assert.Equal(decimal.Ceiling(200m * Ships * LegSeconds / 3600m) * 2, there + back);
        Assert.InRange(there + back, exact, exact + 2m);
        Assert.Equal(388078m, there + back);
    }

    /// <summary>
    /// A jump burns the fuel of the hours of its preparation (task 00072, 2.4.3): a jump drive of
    /// 3600 units an hour that prepares for 900 seconds burns 900 units, split by its shares.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_jump_burns_the_fuel_of_its_preparation()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        WorldPlaceDto home = await HomeAsync(player);
        FleetSettings settings = FleetSettings.From(_factory.Services.GetRequiredService<IGameConfig>());
        FleetSpeedSet none = new(0m, 0m, 0m);
        FleetEngines engines = new([new FleetDrive(1, "jump", 100m, 3600m, 900m, 1, [new FleetFuelShare(14, 0.7m), new FleetFuelShare(15, 0.3m)])], [1]);

        FuelEstimate estimate = FuelPlanner.Estimate(
            engines,
            new FleetSpeeds(none, none, none, engines.JumpPrepSeconds()),
            [new FlightLeg("ftl", "jump", home, home, settings.JumpReferenceDistance / 2d)],
            settings);

        Assert.Equal(900L, estimate.Legs.Single().Seconds);
        Assert.Equal(630m, estimate.NeedByResource[14]);
        Assert.Equal(270m, estimate.NeedByResource[15]);
    }

    /// <summary>
    /// The orders that do not fly burn nothing (task 00072, 2.4.4): a plan of a wait and of an
    /// extraction needs no fuel, and the fleet leaves with an empty hold.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_orders_that_do_not_fly_burn_nothing()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive]);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[]
            {
                new { type_code = "wait", duration_seconds = 60 },
                new { type_code = "mine", duration_seconds = 60 },
            },
        });

        using HttpResponseMessage response = await player.Client.GetAsync($"/api/v1/fleet/flight-plans/plan-estimate?fleet_id={fleetId}", Token);
        JsonElement estimate = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(estimate.GetProperty("legs").EnumerateArray());
        Assert.Empty(estimate.GetProperty("fuel_need").EnumerateArray());

        // The hold holds nothing, and the fleet still leaves.
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
    }

    /// <summary>
    /// The fuel of a leg is taken from the hold at the start of the leg, and a hold that holds less
    /// refuses the leg and keeps what it holds (task 00072, 2.4.5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_leg_without_its_fuel_is_refused_and_the_hold_keeps_its_cargo()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player, [NormalDrive]);

        await FuelAsync(player, fleetId, 12, 50);

        using IServiceScope scope = _factory.Services.CreateScope();
        FuelService service = scope.ServiceProvider.GetRequiredService<FuelService>();

        GameException refused = await Assert.ThrowsAsync<GameException>(
            () => service.ConsumeAsync(fleetId, new Dictionary<int, decimal> { [12] = 80m }, DateTimeOffset.UtcNow, Token));

        Assert.Equal(FleetErrorCodes.FuelShort, refused.Code);
        Assert.Equal(50m, (await service.ReadAsync(fleetId, Token))[12]);

        await service.ConsumeAsync(fleetId, new Dictionary<int, decimal> { [12] = 30m }, DateTimeOffset.UtcNow, Token);

        using IServiceScope after = _factory.Services.CreateScope();
        IReadOnlyDictionary<int, decimal> left = await after.ServiceProvider.GetRequiredService<FuelService>().ReadAsync(fleetId, Token);

        Assert.Equal(20m, left[12]);
    }

    /// <summary>
    /// The estimate of a hyperflight asks the database as many times for a fleet of one ship as for
    /// a fleet of three (task 00072): the drives of all the ships are read at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_estimate_asks_the_database_the_same_for_one_ship_and_for_three()
    {
        await ResetAsync();
        using PlanetsTestPlayer small = await FoundAsync(spaceportLevel: 1);
        using PlanetsTestPlayer large = await FoundAsync(spaceportLevel: 1);
        long smallFleet = await FleetAsync(small, [NormalDrive, HyperDrive]);
        (long largeFleet, _) = await FleetOfShipsAsync(large, [NormalDrive, HyperDrive], [NormalDrive, HyperDrive], [NormalDrive, HyperDrive]);
        long target = await OtherBodyAsync(small);

        // The first read warms the caches of the catalogs, so the second one is measured.
        int one = await EstimateCommandsAsync(small, smallFleet, target);
        int three = await EstimateCommandsAsync(large, largeFleet, target);

        Assert.Equal(one, three);
    }

    private async Task ResetAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        await database.FleetFlights.ExecuteDeleteAsync(Token);
        await database.FlightPlanOrders.ExecuteDeleteAsync(Token);
        await database.FlightPlans.ExecuteDeleteAsync(Token);
        await database.FleetCargos.ExecuteDeleteAsync(Token);
        await database.FleetShips.ExecuteDeleteAsync(Token);
        await database.Fleets.ExecuteDeleteAsync(Token);
        await database.ShipModules.ExecuteDeleteAsync(Token);
        await database.Ships.ExecuteDeleteAsync(Token);
        await database.ShipDesignSlots.ExecuteDeleteAsync(Token);
        await database.ShipDesigns.ExecuteDeleteAsync(Token);

        // The starting data of the production is written by the module itself, so the fuel of the
        // drives is there after the step (task 00072). The world and the planets are seeded before
        // it, because the starting data of the production names the resources of the world and
        // the factories of the planets.
        await scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);
        await scope.ServiceProvider.GetRequiredService<IPlanetsModule>().EnsureAsync(Token);
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private async Task<PlanetsTestPlayer> FoundAsync(int spaceportLevel)
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, spaceportLevel);

        return player;
    }

    /// <summary>
    /// Writes a fleet of the player of one ship that carries the given drives.
    /// </summary>
    private async Task<long> FleetAsync(PlanetsTestPlayer player, string[] driveCodes) =>
        (await FleetOfShipsAsync(player, driveCodes)).FleetId;

    /// <summary>
    /// Writes a fleet of the player with a ship for every list of drives.
    /// </summary>
    private async Task<(long FleetId, IReadOnlyList<long> ShipIds)> FleetOfShipsAsync(PlanetsTestPlayer player, params string[][] shipDrives) =>
        await FleetOfHullAsync(player, HullCode, shipDrives);

    /// <summary>
    /// Writes a fleet of the player with a ship on a hull for every list of modules.
    /// </summary>
    private async Task<(long FleetId, IReadOnlyList<long> ShipIds)> FleetOfHullAsync(PlanetsTestPlayer player, string hullCode, params string[][] shipDrives)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([hullCode, .. shipDrives.SelectMany(drives => drives).Distinct()], Token);
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<ShipEntity> ships = [];

        foreach (string[] driveCodes in shipDrives)
        {
            ShipDesignEntity design = new()
            {
                OwnerPlayerId = player.Account.UserId,
                PlanetId = player.PlanetId,
                Name = "Проєкт " + Guid.NewGuid().ToString("N")[..6],
                HullPartTypeId = partIds[hullCode],
                CreatedAt = now,
                UpdatedAt = now,
            };
            database.ShipDesigns.Add(design);
            await database.SaveChangesAsync(Token);

            for (int index = 0; index < driveCodes.Length; index++)
            {
                database.ShipDesignSlots.Add(new ShipDesignSlotEntity
                {
                    DesignId = design.Id,
                    SlotIndex = index,
                    PartTypeId = partIds[driveCodes[index]],
                    MarkLevel = 1,
                });
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
                    new ShipModuleEntity { PartTypeId = partIds[hullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true },
                    .. driveCodes.Select(code => new ShipModuleEntity { PartTypeId = partIds[code], MarkLevel = 1, ConditionPercent = 100, Quantity = 1 }),
                ],
            };
            database.Ships.Add(ship);
            await database.SaveChangesAsync(Token);
            ships.Add(ship);
        }

        long[] shipIds = [.. ships.Select(ship => ship.Id)];
        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот", ship_ids = shipIds });

        return (view.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64(), shipIds);
    }

    /// <summary>
    /// Puts a resource into the hold of the fleet.
    /// </summary>
    private async Task FuelAsync(PlanetsTestPlayer player, long fleetId, int resourceTypeId, long amount)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        database.FleetCargos.Add(new FleetCargoEntity
        {
            FleetId = fleetId,
            ResourceId = resourceTypeId,
            Amount = amount,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        await database.SaveChangesAsync(Token);
    }

    private async Task<WorldPlaceDto> HomeAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetPlanetPlaceAsync(player.PlanetId, Token)
            ?? throw new InvalidOperationException("The body has no place.");
    }

    /// <summary>
    /// Finds a body that is in another system and is not on the last orbit of it (task 00072): a
    /// hyperflight to it is made of three legs, because the fleet flies to the edge of its system
    /// and from the edge of the system of the body.
    /// </summary>
    private async Task<long> OtherBodyAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldPlaceDto home = await HomeAsync(player);
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        // The last orbit of every system is the edge of it, and a body on the edge has no leg to
        // it, so the bodies on the edge are left out.
        HashSet<long> bodiesOfPlanets = await world.Planets.AsNoTracking()
            .Select(planet => planet.CelestialObjectId)
            .ToHashSetAsync(Token);

        List<BodyOrbit> bodies =
        [
            .. (await world.Objects.AsNoTracking()
                    .Where(entry => entry.SystemId != null && entry.Orbit != null)
                    .Select(entry => new BodyOrbit(entry.Id, entry.SystemId!.Value, entry.Orbit!.Value))
                    .ToListAsync(Token))
                .Where(entry => bodiesOfPlanets.Contains(entry.ObjectId)),
        ];

        foreach (BodyOrbit body in bodies.Where(body => home.SystemId == null || body.SystemId != home.SystemId))
        {
            int lastOrbit = await world.Objects.AsNoTracking()
                .Where(entry => entry.SystemId == body.SystemId && entry.Orbit != null)
                .MaxAsync(entry => entry.Orbit!.Value, Token);

            if (body.Orbit != lastOrbit)
            {
                return body.ObjectId;
            }
        }

        throw new InvalidOperationException("The world has no body that is not on the edge of its system.");
    }

    /// <summary>
    /// One body of the world with the orbit it stands on (task 00072).
    /// </summary>
    /// <param name="ObjectId">The body of the map.</param>
    /// <param name="SystemId">The system it is in.</param>
    /// <param name="Orbit">The orbit it stands on.</param>
    private sealed record BodyOrbit(long ObjectId, long SystemId, int Orbit);

    /// <summary>
    /// Finds a star system that is not the system of the body of the player (task 00072): a
    /// hyperflight or a jump goes between two systems.
    /// </summary>
    private async Task<long> OtherSystemAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldPlaceDto home = await HomeAsync(player);

        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        long? system = await world.Systems.AsNoTracking()
            .Where(entry => home.SystemId == null || entry.Id != home.SystemId)
            .OrderBy(entry => entry.Id)
            .Select(entry => (long?)entry.Id)
            .FirstOrDefaultAsync(Token);

        return system ?? throw new InvalidOperationException("The world has no other system.");
    }

    /// <summary>
    /// Runs the scheduler arrival after arrival until the fleet has no flight left, and returns the
    /// flights it flew, in their order.
    /// </summary>
    private async Task<List<FleetFlightEntity>> FlyToTheEndAsync(long fleetId)
    {
        List<FleetFlightEntity> flights = [];

        for (int step = 0; step < MaxLegs; step++)
        {
            FleetFlightEntity? flight = await CurrentFlightAsync(fleetId);

            if (flight is null)
            {
                return flights;
            }

            flights.Add(flight);

            DateTimeOffset now = _factory.Services.GetRequiredService<IGameClock>().GameNow;

            await ShiftedGameClock.RunDueEventsAsync(_factory, flight.ArrivesAt - now + TimeSpan.FromSeconds(1));
        }

        throw new InvalidOperationException("The fleet did not arrive in the number of legs a command has.");
    }

    private async Task<FleetFlightEntity?> CurrentFlightAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FleetFlights.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.FleetId == fleetId, Token);
    }

    private async Task<FleetEntity> FleetRowAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Fleets.AsNoTracking()
            .SingleAsync(entry => entry.Id == fleetId, Token);
    }

    private async Task<WorldPlaceDto> ObjectPlaceAsync(long objectId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetObjectPlaceAsync(objectId, Token)
            ?? throw new InvalidOperationException("The object has no place.");
    }

    /// <summary>
    /// Finds a point of open space in a sector: a point that is in no system.
    /// </summary>
    private async Task<WorldPlaceDto> OpenSpaceAsync(int sectorId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();

        for (int step = 0; step < OpenSpaceTries; step++)
        {
            double offset = step * OpenSpaceStep;
            WorldPlaceDto? place = await world.GetPointPlaceAsync(sectorId, offset, offset, 0d, Token);

            if (place is not null && place.SystemId is null)
            {
                return place;
            }
        }

        throw new InvalidOperationException("The sector has no point of open space.");
    }

    /// <summary>
    /// Reads the estimate of a hyperflight twice and returns how many commands the second read sent
    /// to the database, from the header of the timing of the answer.
    /// </summary>
    private async Task<int> EstimateCommandsAsync(PlanetsTestPlayer player, long fleetId, long target)
    {
        string path = $"/api/v1/fleet/flight-plans/estimate?fleet_id={fleetId}&type_code=hyperfly&target_kind=object&target_object_id={target}";

        using (HttpResponseMessage warm = await player.Client.GetAsync(path, Token))
        {
            Assert.Equal(HttpStatusCode.OK, warm.StatusCode);
        }

        using HttpResponseMessage response = await player.Client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string timing = response.Headers.GetValues(RequestTraceMiddleware.ServerTimingHeader).Single();
        Match count = CommandCountPattern().Match(timing);

        Assert.True(count.Success, timing);

        return int.Parse(count.Groups["count"].Value, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("desc=\"(?<count>\\d+) ")]
    private static partial Regex CommandCountPattern();

    private async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.IsSuccessStatusCode, text);

        return JsonDocument.Parse(text).RootElement;
    }

    private async Task<string> RefusalAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));

        // A refusal of the game is not a bad request of the protocol: it is a refusal with a code,
        // and the server answers 422 for it.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        return await response.Content.ReadAsStringAsync(Token);
    }
}

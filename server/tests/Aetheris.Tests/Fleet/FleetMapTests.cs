using System.Net;
using System.Text.Json;
using Aetheris.Core.Time;
using Aetheris.Fleet;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the fleets that are under way on the map (task 00059): the server counts the place of a
/// fleet on its route, the map reads the fleets that fly in one request, and the answer carries the
/// route of every flight, so the client may draw it and put the mark of the fleet on it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FleetMapTests : IAsyncLifetime
{
    private const string Spaceport = "building_6";
    private const string HullCode = "part_h_21";
    private const string DriveCode = "part_m_1";
    private const string Plans = "/api/v1/fleet/flight-plans";
    private const string Execute = "/api/v1/fleet/flight-plans/execute";
    private const string InFlight = "/api/v1/fleet/fleets/in-flight";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    private DriveConsumptionScope? _consumption;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetMapTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public FleetMapTests(AetherisTestFactory factory)
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
    /// The place of a fleet on its route: the point of the route the time has reached, counted by the
    /// server. The client counts the same place from the same times, so the two agree.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public void The_place_of_a_fleet_on_its_route_is_the_point_the_time_has_reached()
    {
        DateTimeOffset started = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
        FleetFlightEntity flight = new()
        {
            StartedAt = started,
            ArrivesAt = started.AddMinutes(10),
            FromSectorId = 3,
            ToSectorId = 7,
            FromX = 0,
            FromY = 0,
            FromZ = 0,
            ToX = 100,
            ToY = 200,
            ToZ = 0,
        };

        WorldPlaceDto atStart = FleetFlightService.PlaceOnRoute(flight, started);
        WorldPlaceDto atHalf = FleetFlightService.PlaceOnRoute(flight, started.AddMinutes(5));
        WorldPlaceDto atEnd = FleetFlightService.PlaceOnRoute(flight, started.AddMinutes(10));

        Assert.Equal(0d, atStart.X, 6);
        Assert.Equal(0d, atStart.Y, 6);
        Assert.Equal(50d, atHalf.X, 6);
        Assert.Equal(100d, atHalf.Y, 6);
        Assert.Equal(100d, atEnd.X, 6);
        Assert.Equal(200d, atEnd.Y, 6);

        // The fleet belongs to the sector it left until the middle of the route, and to the sector it
        // flies to from the middle on.
        Assert.Equal(3, atStart.SectorId);
        Assert.Equal(3, FleetFlightService.PlaceOnRoute(flight, started.AddMinutes(4)).SectorId);
        Assert.Equal(7, atHalf.SectorId);
        Assert.Equal(7, atEnd.SectorId);
    }

    /// <summary>
    /// The place of a fleet inside one system: it keeps the system and takes the orbit it has reached,
    /// because the distance inside a system is counted by the orbits.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public void The_place_of_a_fleet_inside_a_system_is_the_orbit_it_has_reached()
    {
        DateTimeOffset started = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
        FleetFlightEntity flight = new()
        {
            StartedAt = started,
            ArrivesAt = started.AddMinutes(10),
            FromSectorId = 3,
            ToSectorId = 3,
            FromSystemId = 4,
            ToSystemId = 4,
            FromOrbit = 1,
            ToOrbit = 5,
            FromX = 12,
            FromY = 34,
            FromZ = 0,
        };

        Assert.Equal(1, FleetFlightService.PlaceOnRoute(flight, started).Orbit);
        Assert.Equal(3, FleetFlightService.PlaceOnRoute(flight, started.AddMinutes(5)).Orbit);
        Assert.Equal(5, FleetFlightService.PlaceOnRoute(flight, started.AddMinutes(10)).Orbit);
        Assert.Equal(4, FleetFlightService.PlaceOnRoute(flight, started.AddMinutes(5)).SystemId);
    }

    /// <summary>
    /// The map reads the fleets that fly: the answer carries the route of the flight of every fleet,
    /// the place of the sectors it joins and the moment the fleet arrives.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_map_reads_the_fleets_that_fly_with_their_routes()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long FleetId, long ShipId) ship = await FleetAsync(player);
        WorldPlaceDto home = await HomeAsync(player);
        WorldPlaceDto point = await PointAsync(home.SectorId, 120.5, 80.25, 0);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 120.5, 80.25, 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        using HttpResponseMessage answer = await player.Client.GetAsync(InFlight, Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        using JsonDocument body = JsonDocument.Parse(text);
        JsonElement root = body.RootElement;

        Assert.Equal(1, root.GetProperty("fleets").GetArrayLength());

        JsonElement fleet = root.GetProperty("fleets").EnumerateArray().Single();

        Assert.Equal(ship.FleetId, fleet.GetProperty("fleet_id").GetInt64());
        Assert.Equal("Флот", fleet.GetProperty("name").GetString());

        JsonElement flight = fleet.GetProperty("flight");

        // The route of the flight: where the fleet left and where it flies to.
        Assert.Equal(home.SectorId, flight.GetProperty("from_sector_id").GetInt32());
        Assert.Equal(home.X, flight.GetProperty("from_x").GetDouble(), 6);
        Assert.Equal(home.Y, flight.GetProperty("from_y").GetDouble(), 6);
        Assert.Equal(point.X, flight.GetProperty("to_x").GetDouble(), 6);
        Assert.Equal(point.Y, flight.GetProperty("to_y").GetDouble(), 6);
        Assert.True(flight.GetProperty("started_at").GetDateTimeOffset() < flight.GetProperty("arrives_at").GetDateTimeOffset());

        // The places of the sectors of the route, so the map of the galaxy may draw it.
        IReadOnlyDictionary<int, SectorPlaceDto> places = await _factory.Services.CreateScope()
            .ServiceProvider.GetRequiredService<IWorldModule>()
            .GetSectorPlacesAsync([home.SectorId], Token);
        SectorPlaceDto sectorPlace = places[home.SectorId];

        Assert.Equal(home.SectorId, fleet.GetProperty("from_sector").GetProperty("sector_id").GetInt32());
        Assert.Equal(home.SectorId, fleet.GetProperty("to_sector").GetProperty("sector_id").GetInt32());
        Assert.Equal(sectorPlace.X, fleet.GetProperty("from_sector").GetProperty("x").GetDouble(), 6);
        Assert.Equal(sectorPlace.Y, fleet.GetProperty("from_sector").GetProperty("y").GetDouble(), 6);
        Assert.Equal(sectorPlace.X, fleet.GetProperty("to_sector").GetProperty("x").GetDouble(), 6);
        Assert.Equal(sectorPlace.Y, fleet.GetProperty("to_sector").GetProperty("y").GetDouble(), 6);

        // The game time of the answer, so the client counts the time left of the flight.
        Assert.True(root.GetProperty("game_now").GetDateTimeOffset() > DateTimeOffset.UnixEpoch);
        Assert.True(root.GetProperty("time_multiplier").GetDouble() > 0d);
    }

    /// <summary>
    /// The map of a player without a fleet that flies carries no fleet: the answer is empty and the
    /// screen draws nothing.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_map_of_a_player_without_a_fleet_that_flies_is_empty()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        await FleetAsync(player);

        using HttpResponseMessage answer = await player.Client.GetAsync(InFlight, Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        using JsonDocument body = JsonDocument.Parse(text);

        Assert.Empty(body.RootElement.GetProperty("fleets").EnumerateArray());

        // The fleet that stands at a place is in the answer too, so the map shows where it is.
        Assert.Equal(1, body.RootElement.GetProperty("stationed").GetArrayLength());
    }

    /// <summary>
    /// A fleet that arrived stands in the place it flew to: the map shows it there and not on a
    /// route.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_map_shows_a_fleet_that_arrived_where_it_stands()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long FleetId, long ShipId) ship = await FleetAsync(player);
        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 120.5, 80.25, 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        // The flight runs out: the fleet arrives and stands in the place it flew to.
        FleetFlightEntity flight = await FlightAsync(ship.FleetId);
        await RunUntilAsync(flight.ArrivesAt);

        using HttpResponseMessage answer = await player.Client.GetAsync(InFlight, Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        using JsonDocument body = JsonDocument.Parse(text);

        // The fleet is not under way any more.
        Assert.Empty(body.RootElement.GetProperty("fleets").EnumerateArray());

        JsonElement stationed = body.RootElement.GetProperty("stationed").EnumerateArray().Single();

        Assert.Equal(ship.FleetId, stationed.GetProperty("fleet_id").GetInt64());
        Assert.Equal("Флот", stationed.GetProperty("name").GetString());
        Assert.Equal(home.SectorId, stationed.GetProperty("sector_id").GetInt32());
    }

    /// <summary>
    /// The fleets that fly of one player are not the fleets of another: the answer carries only the
    /// fleets of the player who asks.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_map_shows_the_fleets_of_the_player_who_asks_and_no_other()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using PlanetsTestPlayer other = await FoundAsync();
        (long FleetId, long ShipId) mine = await FleetAsync(player);
        await FleetAsync(other);

        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = mine.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 120.5, 80.25, 0) } });
        await CommandAsync(player, Execute, new { fleet_id = mine.FleetId });

        using HttpResponseMessage answer = await other.Client.GetAsync(InFlight, Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        using JsonDocument body = JsonDocument.Parse(text);

        Assert.Empty(body.RootElement.GetProperty("fleets").EnumerateArray());
    }

    /// <summary>
    /// The flight of a fleet is carried by the view of the fleets as well: the card of the fleet on the
    /// screen of the fleets tells where it flies to, and now also where it left.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_view_of_the_fleets_carries_the_way_the_fleet_left()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long FleetId, long ShipId) ship = await FleetAsync(player);
        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 120.5, 80.25, 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        using HttpResponseMessage answer = await player.Client.GetAsync("/api/v1/fleet/fleets", Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        using JsonDocument body = JsonDocument.Parse(text);
        JsonElement flight = body.RootElement.GetProperty("fleets").EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetInt64() == ship.FleetId)
            .GetProperty("flight");

        Assert.Equal(home.SectorId, flight.GetProperty("from_sector_id").GetInt32());
        Assert.Equal(home.X, flight.GetProperty("from_x").GetDouble(), 6);
        Assert.Equal(home.Y, flight.GetProperty("from_y").GetDouble(), 6);
    }

    private async Task<FleetFlightEntity> FlightAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FleetFlights.AsNoTracking()
            .SingleAsync(entry => entry.FleetId == fleetId, Token);
    }

    /// <summary>
    /// Runs the scheduler up to a moment of game time, so the fleet arrives.
    /// </summary>
    private async Task RunUntilAsync(DateTimeOffset moment)
    {
        DateTimeOffset now = _factory.Services.GetRequiredService<IGameClock>().GameNow;

        await ShiftedGameClock.RunDueEventsAsync(_factory, moment - now + TimeSpan.FromSeconds(1));
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.FleetNameMaxLength, "64");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.FlightPlanMaxOrders, "32");
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IFleetModule>().EnsureAsync(Token);

        // These tests check the flight and not its cost (task 00072): the drives burn nothing, so
        // the fleets need no propellant in their holds and the times stay the times of the formula.
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
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, 1);

        return player;
    }

    /// <summary>
    /// Writes a ship of the player with a drive and a fleet of that ship.
    /// </summary>
    private async Task<(long FleetId, long ShipId)> FleetAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([HullCode, DriveCode], Token);
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

        database.ShipDesignSlots.Add(new ShipDesignSlotEntity { DesignId = design.Id, SlotIndex = 0, PartTypeId = partIds[DriveCode], MarkLevel = 1 });
        database.Ships.Add(new ShipEntity
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
                new ShipModuleEntity { PartTypeId = partIds[DriveCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1 },
            ],
        });
        await database.SaveChangesAsync(Token);

        long shipId = database.Ships.AsNoTracking().OrderBy(ship => ship.Id).Last().Id;
        JsonElement view = await CommandAsync(player, "/api/v1/fleet/fleets", new { planet_id = player.PlanetId, name = "Флот", ship_ids = new[] { shipId } });

        return (view.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64(), shipId);
    }

    private async Task<WorldPlaceDto> HomeAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetPlanetPlaceAsync(player.PlanetId, Token)
            ?? throw new InvalidOperationException("The body has no place.");
    }

    private async Task<WorldPlaceDto> PointAsync(int sectorId, double x, double y, double z)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetPointPlaceAsync(sectorId, x, y, z, Token)
            ?? throw new InvalidOperationException("The point is outside the sector.");
    }

    private static object PointOrder(string typeCode, int sectorId, double x, double y, double z) => new
    {
        type_code = typeCode,
        target_kind = "point",
        target_sector_id = sectorId,
        target_x = x,
        target_y = y,
        target_z = z,
    };

    private async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }
}

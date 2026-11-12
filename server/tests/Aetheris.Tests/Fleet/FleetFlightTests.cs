using System.Globalization;
using System.Net;
using System.Text.Json;
using Aetheris.Core.Time;
using Aetheris.Fleet;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the orders «fly», «return» and «mine» of a flight plan (task 00053): the fleet moves, the
/// server counts the time by the formula of the speed, the orders go one after another, and the
/// extraction (task 00054: it names no target, the place is where the fleet stands) works in cycles of
/// the configuration.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FleetFlightTests
{
    private const string Spaceport = "building_6";
    private const string HullCode = "part_h_21";
    private const string DriveCode = "part_m_1";
    private const string DrillCode = "part_m_54";
    private const string HeliumCode = "part_m_53";
    private const string EtherCode = "part_m_56";
    private const string MatterCode = "part_m_57";
    private const string ContainerCode = "part_m_66";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const string Dissolve = "/api/v1/fleet/fleets/dissolve";
    private const string RemoveShips = "/api/v1/fleet/fleets/remove-ships";
    private const string Plans = "/api/v1/fleet/flight-plans";
    private const string Save = "/api/v1/fleet/flight-plans/save";
    private const string Stop = "/api/v1/fleet/flight-plans/stop";
    private const string Execute = "/api/v1/fleet/flight-plans/execute";
    private const string Estimate = "/api/v1/fleet/flight-plans/estimate";
    private const string HeliumResource = "resource_13";
    private const double ReferencePush = 100d;
    private const double ReferenceMass = 1000d;
    private const double ReferenceSpeed = 45d;
    private const int SecondsPerHour = 3600;
    private const int Day = 24;
    private const int ParallelCount = 20;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetFlightTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public FleetFlightTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The flight to coordinates: the fleet leaves, the time is the distance over the speed of the
    /// formula, and at the end of it the fleet stands at the point with its three axes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_flies_to_coordinates_in_the_time_of_the_formula()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 2));
        WorldPlaceDto home = await HomeAsync(player);
        WorldPlaceDto point = await PointAsync(home.SectorId, 120.5, 80.25, 0);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 120.5, 80.25, 0) } });
        JsonElement plan = await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        Assert.Equal("active", plan.GetProperty("state_code").GetString());
        Assert.Equal(1, plan.GetProperty("current_position").GetInt32());

        double expectedSeconds = await ExpectedSecondsAsync(ship, home, point, carried: 0);
        FleetFlightEntity flight = await FlightAsync(ship.FleetId);

        Assert.Equal(expectedSeconds, (flight.ArrivesAt - flight.StartedAt).TotalSeconds, 1);
        Assert.Equal(point.X, flight.ToX, 6);
        Assert.Equal(point.Y, flight.ToY, 6);
        Assert.Equal(point.Z, flight.ToZ, 6);

        FleetEntity flying = await FleetRowAsync(ship.FleetId);

        Assert.Equal(FleetStates.Flying, flying.StateCode);
        Assert.Equal(0L, flying.PlanetId);

        // The fleet view carries the flight: where it goes and when it arrives.
        JsonElement view = await ReadFleetAsync(player, ship.FleetId);

        Assert.Equal("flying", view.GetProperty("state_code").GetString());
        Assert.Equal(flight.ArrivesAt, view.GetProperty("flight").GetProperty("arrives_at").GetDateTimeOffset());

        await RunUntilAsync(flight.ArrivesAt);

        FleetEntity arrived = await FleetRowAsync(ship.FleetId);

        Assert.Equal(FleetStates.Stationed, arrived.StateCode);
        Assert.Equal(point.X, arrived.PositionX!.Value, 6);
        Assert.Equal(point.Y, arrived.PositionY!.Value, 6);
        Assert.Equal(point.Z, arrived.PositionZ!.Value, 6);
        Assert.Null(arrived.ObjectId);
        Assert.Equal(0L, arrived.PlanetId);
        Assert.Equal(home.SectorId, arrived.SectorId);
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await PendingEventsAsync(FlightPlanRunner.ArrivalEventType));
    }

    /// <summary>
    /// The flight to an object of the map: the fleet stands at the object when it arrives and keeps the
    /// reference to it with the coordinates of the object.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_flies_to_an_object_and_gets_its_reference()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        WorldPlaceDto field = await ObjectPlaceAsync(fieldObjectId);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity flight = await FlightAsync(ship.FleetId);

        Assert.Equal(await ExpectedSecondsAsync(ship, home, field, carried: 0), (flight.ArrivesAt - flight.StartedAt).TotalSeconds, 1);

        await RunUntilAsync(flight.ArrivesAt);

        FleetEntity arrived = await FleetRowAsync(ship.FleetId);

        Assert.Equal(FleetStates.Stationed, arrived.StateCode);
        Assert.Equal(fieldObjectId, arrived.ObjectId);
        Assert.Equal(0L, arrived.PlanetId);
        Assert.Null(arrived.SystemId);
        Assert.Equal(field.X, arrived.PositionX!.Value, 6);
        Assert.Equal(field.SectorId, arrived.SectorId);
    }

    /// <summary>
    /// The flight to a planet inside the system of the fleet: the distance is counted by the orbits, and
    /// the fleet stands on the orbit of the planet when it arrives. A planet of another body does not
    /// take the ships of the fleet.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_flies_to_a_planet_of_its_system_by_the_orbits()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        PlanetEntity neighbour = await NeighbourAsync(home);
        WorldPlaceDto place = await PlanetPlaceAsync(neighbour.Id);

        Assert.Equal(home.SystemId, place.SystemId);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PlanetOrder("fly", neighbour.CelestialObjectId) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity flight = await FlightAsync(ship.FleetId);
        double orbitStep = (double)decimal.Parse(
            await ParameterAsync("world.map.orbit_distance_step"),
            CultureInfo.InvariantCulture);
        double orbits = Math.Abs((place.Orbit ?? 0) - (home.Orbit ?? 0));

        Assert.Equal(orbits * orbitStep, flight.Distance, 6);
        Assert.Equal(await ExpectedSecondsAsync(ship, home, place, carried: 0), (flight.ArrivesAt - flight.StartedAt).TotalSeconds, 1);

        await RunUntilAsync(flight.ArrivesAt);

        FleetEntity arrived = await FleetRowAsync(ship.FleetId);

        Assert.Equal(neighbour.Id, arrived.PlanetId);
        Assert.Equal(neighbour.CelestialObjectId, arrived.ObjectId);
        Assert.Equal(home.SystemId, arrived.SystemId);
        Assert.Equal(place.Orbit, arrived.Orbit);

        // The body is not the body of the player: the ship stays on its own.
        Assert.Equal(player.PlanetId, (await ShipRowAsync(ship.ShipId)).PlanetId);
    }

    /// <summary>
    /// The flight to a planet of another system is the straight distance between the systems.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_flies_between_systems_by_the_straight_distance()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        PlanetEntity far = await world.Planets.AsNoTracking()
            .Where(planet => planet.SystemId != home.SystemId && planet.OwnerPlayerId == null)
            .OrderBy(planet => planet.Id)
            .FirstAsync(Token);
        WorldPlaceDto place = await PlanetPlaceAsync(far.Id);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PlanetOrder("fly", far.CelestialObjectId) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity flight = await FlightAsync(ship.FleetId);
        double straight = Math.Sqrt(Math.Pow(home.X - place.X, 2) + Math.Pow(home.Y - place.Y, 2) + Math.Pow(home.Z - place.Z, 2));

        Assert.Equal(straight, flight.Distance, 6);
        Assert.Equal(await ExpectedSecondsAsync(ship, home, place, carried: 0), (flight.ArrivesAt - flight.StartedAt).TotalSeconds, 1);
    }

    /// <summary>
    /// The return brings the fleet to the body it belongs to with the same time counting, and the ships
    /// stand on that body again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_return_brings_the_fleet_home_with_the_same_time_counting()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        WorldPlaceDto field = await ObjectPlaceAsync(fieldObjectId);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId), new { type_code = "return" } } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity there = await FlightAsync(ship.FleetId);
        await RunUntilAsync(there.ArrivesAt);

        // The second order began at the moment the first ended: the return is a flight of its own.
        FleetFlightEntity back = await FlightAsync(ship.FleetId);

        Assert.Equal(there.ArrivesAt, back.StartedAt);
        Assert.Equal(home.X, back.ToX, 6);
        Assert.Equal(await ExpectedSecondsAsync(ship, field, home, carried: 0), (back.ArrivesAt - back.StartedAt).TotalSeconds, 1);

        // The estimate of the server for the return is the same time.
        using HttpResponseMessage answer = await player.Client.GetAsync($"{Estimate}?fleet_id={ship.FleetId}&type_code=return", Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        // The fleet is under way to the field and back, so the estimate counts from where it is now.
        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        await RunUntilAsync(back.ArrivesAt);

        FleetEntity arrived = await FleetRowAsync(ship.FleetId);

        Assert.Equal(player.PlanetId, arrived.PlanetId);
        Assert.Equal(home.ObjectId, arrived.ObjectId);
        Assert.Equal(player.PlanetId, (await ShipRowAsync(ship.ShipId)).PlanetId);
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>
    /// The estimate of the server for a flight is the time the fleet takes: the numbers of the screen are
    /// the numbers of the flight, with an empty hold, with the cargo and with a full hold.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_estimate_of_the_server_is_the_time_of_the_flight()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        WorldPlaceDto field = await ObjectPlaceAsync(fieldObjectId);

        using HttpResponseMessage answer = await player.Client.GetAsync(
            $"{Estimate}?fleet_id={ship.FleetId}&type_code=fly&target_kind=object&target_object_id={fieldObjectId}",
            Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        JsonElement body = JsonDocument.Parse(text).RootElement;
        DesignFacts facts = await FactsAsync(ship);
        double distance = await DistanceAsync(home, field);

        Assert.Equal(distance, body.GetProperty("distance").GetDouble(), 6);
        Assert.True(body.GetProperty("can_fly").GetBoolean());
        Assert.Equal(Math.Ceiling(distance / Speed(facts.Push, facts.Weight) * SecondsPerHour), body.GetProperty("empty_seconds").GetDouble(), 1);
        Assert.Equal(Math.Ceiling(distance / Speed(facts.Push, facts.Weight + facts.Hold) * SecondsPerHour), body.GetProperty("full_seconds").GetDouble(), 1);

        // The full hold makes the flight longer; nothing is carried yet, so the present time is the empty one.
        Assert.True(body.GetProperty("full_seconds").GetInt64() > body.GetProperty("empty_seconds").GetInt64());
        Assert.Equal(body.GetProperty("empty_seconds").GetInt64(), body.GetProperty("current_seconds").GetInt64());

        // A fleet of another player is not read.
        using PlanetsTestPlayer stranger = await FoundAsync();
        using HttpResponseMessage refused = await stranger.Client.GetAsync($"{Estimate}?fleet_id={ship.FleetId}&type_code=return", Token);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    /// <summary>
    /// The cargo makes the flight longer: the same flight with a loaded hold takes the time of the
    /// formula with the mass of the cargo.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_cargo_makes_the_flight_longer()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        WorldPlaceDto point = await PointAsync(home.SectorId, 200, 200, 0);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            database.ShipCargos.Add(new ShipCargoEntity { ShipId = ship.ShipId, ResourceId = 1, Amount = 5000, UpdatedAt = DateTimeOffset.UtcNow });
            await database.SaveChangesAsync(Token);
        }

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 200, 200, 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity flight = await FlightAsync(ship.FleetId);

        Assert.Equal(await ExpectedSecondsAsync(ship, home, point, carried: 5000), (flight.ArrivesAt - flight.StartedAt).TotalSeconds, 1);
        Assert.True((flight.ArrivesAt - flight.StartedAt).TotalSeconds > await ExpectedSecondsAsync(ship, home, point, carried: 0));
    }

    /// <summary>
    /// The extraction begins only when the fleet is at the place: the order waits for the arrival of the
    /// flight before it, and the plan goes on to the return when the extraction is over.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_orders_are_carried_out_one_after_another_and_the_extraction_waits_for_the_arrival()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 2), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        long fieldMassBefore = await FieldMassAsync(fieldObjectId);

        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[]
            {
                ObjectOrder("fly", fieldObjectId),
                MineOrder(durationSeconds: 3600),
                new { type_code = "return" },
            },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        // Nothing is taken before the fleet is at the field: no cycle is planned yet.
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));
        Assert.Equal(1, (await PlanAsync(ship.FleetId)).CurrentPosition);

        FleetFlightEntity there = await FlightAsync(ship.FleetId);
        await RunUntilAsync(there.ArrivesAt);

        // The fleet is at the field, the second order has begun, and the first cycle is planned for a
        // quarter of an hour after the arrival.
        FlightPlanEntity plan = await PlanAsync(ship.FleetId);
        List<ScheduledEventEntity> cycles = await PendingEventsAsync(FleetExtractionCycleService.CycleEventType);

        Assert.Equal(2, plan.CurrentPosition);
        Assert.Equal(fieldObjectId, (await FleetRowAsync(ship.FleetId)).ObjectId);
        Assert.Equal(there.ArrivesAt + TimeSpan.FromSeconds(900), Assert.Single(cycles).RunAt);
        Assert.Equal(0L, await CargoAsync(ship.ShipId));

        // The order named no target; the place the fleet stands at is written into it when it begins.
        FlightPlanOrderEntity mine = await OrderAsync(ship.FleetId, 2);

        Assert.Equal(FlightTargetKinds.Object, mine.TargetKind);
        Assert.Equal(fieldObjectId, mine.TargetObjectId);

        // The extraction lasts an hour: four cycles of a quarter of an hour, then the return.
        await RunUntilAsync(there.ArrivesAt + TimeSpan.FromSeconds(3600));

        Assert.Equal(3, (await PlanAsync(ship.FleetId)).CurrentPosition);

        FleetFlightEntity back = await FlightAsync(ship.FleetId);

        Assert.Equal(there.ArrivesAt + TimeSpan.FromSeconds(3600), back.StartedAt);

        await RunUntilAsync(back.ArrivesAt);

        // Two drill rigs process 700 t per cycle; four cycles take 2800 t from the field, the waste included.
        long fieldMassAfter = await FieldMassAsync(fieldObjectId);
        long carried = await CargoAsync(ship.ShipId);

        Assert.Equal(fieldMassBefore - (4 * 700), fieldMassAfter);
        Assert.InRange(carried, 1, 4 * 700);
        Assert.Equal(carried, await FleetCargoAsync(ship.FleetId));

        FleetEntity home2 = await FleetRowAsync(ship.FleetId);

        Assert.Equal(player.PlanetId, home2.PlanetId);
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));
    }

    /// <summary>
    /// The extraction without a time stops by itself when the holds are full: the waste stays in the place
    /// and the field loses only the mass whose yield fitted.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_extraction_ends_when_the_holds_are_full()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        long fieldMassBefore = await FieldMassAsync(fieldObjectId);

        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { ObjectOrder("fly", fieldObjectId), MineOrder(durationSeconds: 0), new { type_code = "return" } },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        // The hull holds 100 tonnes: the first cycle fills it, the order is over, the fleet is home.
        long carried = await CargoAsync(ship.ShipId);
        long fieldMassAfter = await FieldMassAsync(fieldObjectId);

        Assert.InRange(carried, 1, 100);
        Assert.InRange(fieldMassBefore - fieldMassAfter, carried, 350);
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Equal(player.PlanetId, (await FleetRowAsync(ship.FleetId)).PlanetId);
    }

    /// <summary>
    /// The extraction without a time goes on while the field has mass and the holds have room, and ends
    /// when the field is gone: the field disappears at zero mass and the plan goes on.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_extraction_without_a_time_ends_when_the_field_is_gone()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1), (ContainerCode, 8));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);

        // A field of 500 t: the drill rig processes 350 t a cycle, so the second cycle empties it.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Fields
                .Where(entry => entry.CelestialObjectId == fieldObjectId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Mass, 500L).SetProperty(entry => entry.InitialMass, 500L), Token);
        }

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId), MineOrder(durationSeconds: 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        using IServiceScope after = _factory.Services.CreateScope();
        AsteroidFieldEntity gone = await after.ServiceProvider.GetRequiredService<WorldDbContext>().Fields.AsNoTracking()
            .SingleAsync(entry => entry.CelestialObjectId == fieldObjectId, Token);

        Assert.Equal(0L, gone.Mass);
        Assert.NotNull(gone.DepletedAt);
        Assert.InRange(await CargoAsync(ship.ShipId), 1, 500);
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));
    }

    /// <summary>
    /// A gas giant never runs out, so the extraction without a time at it ends only when the holds are
    /// full.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_extraction_without_a_time_at_a_gas_giant_ends_when_the_holds_are_full()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (HeliumCode, 1));
        long giantObjectId = await GasGiantAsync();

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PlanetOrder("fly", giantObjectId), MineOrder(durationSeconds: 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        // The hull holds 100 tonnes and a cycle gives 350: the first cycle fills the hold and the order is over.
        Assert.Equal(100L, await CargoAsync(ship.ShipId));
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));
    }

    /// <summary>
    /// The cycle of the extraction is a parameter of the configuration, and the last cycle of an order
    /// with a time is shortened to what is left and takes the matching part of the yield.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_cycle_comes_from_the_configuration_and_the_last_one_is_shortened()
    {
        await ResetAsync();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, FleetConfigKeys.ExtractionCycleSeconds, "600");
        }

        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        long fieldMassBefore = await FieldMassAsync(fieldObjectId);

        // 25 minutes with a cycle of 10 minutes: cycles of 600, 600 and 300 seconds.
        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId), MineOrder(durationSeconds: 1500) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity there = await FlightAsync(ship.FleetId);
        await RunUntilAsync(there.ArrivesAt);

        Assert.Equal(there.ArrivesAt + TimeSpan.FromSeconds(600), Assert.Single(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType)).RunAt);

        await RunAllAsync();

        // One drill rig: 1400 t an hour, so 233 t in 600 s twice and 116 t in the last 300 s.
        Assert.Equal(fieldMassBefore - (233 + 233 + 116), await FieldMassAsync(fieldObjectId));
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>
    /// A fleet with the helium extractor takes the helium of a gas giant as it is, the giant loses
    /// nothing, and several extractors multiply the yield.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_gas_giant_gives_the_helium()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (HeliumCode, 2), (ContainerCode, 1));
        long giantObjectId = await GasGiantAsync();

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PlanetOrder("fly", giantObjectId), MineOrder(durationSeconds: 3600) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        // Two extractors of 1400 t an hour take 2800 t in an hour: all of it is the helium.
        Dictionary<string, long> cargo = await CargoByCodeAsync(ship.ShipId);

        Assert.Equal([HeliumResource], cargo.Keys);
        Assert.Equal(2800L, cargo[HeliumResource]);
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>
    /// A fleet with the extractor of ether takes the ether and the deuterium of a nebula (the rows of the
    /// module, task 00055), each by its share in the composition, and nothing else; the nebula stays what
    /// it was.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_nebula_gives_the_ether_to_the_extractor_of_ether_and_is_never_emptied()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (EtherCode, 1), (ContainerCode, 1));
        (NebulaEntity nebula, List<NebulaCompositionEntity> shares) = await NebulaAsync();
        ResourceCatalog catalog;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        }

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", nebula.CelestialObjectId), MineOrder(durationSeconds: 3600) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        // The extractor of ether takes 1300 t an hour: four cycles of 325 t, and what goes to the hold is the
        // share of the ether and the share of the deuterium; the dark matter and the helium stay in the nebula.
        decimal etherShare = ShareOf(catalog, shares, SeedCodes.Ether);
        decimal deuteriumShare = ShareOf(catalog, shares, SeedCodes.Deuterium);
        Dictionary<string, long> cargo = await CargoByCodeAsync(ship.ShipId);

        Assert.Equal([SeedCodes.Deuterium, SeedCodes.Ether], cargo.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(4 * (long)decimal.Floor(325 * etherShare), cargo[SeedCodes.Ether]);
        Assert.Equal(4 * (long)decimal.Floor(325 * deuteriumShare), cargo[SeedCodes.Deuterium]);

        using IServiceScope after = _factory.Services.CreateScope();
        WorldDbContext check = after.ServiceProvider.GetRequiredService<WorldDbContext>();

        Assert.Equal(1, await check.Nebulae.CountAsync(entry => entry.Id == nebula.Id, Token));
        Assert.Equal(shares.Count, await check.NebulaCompositions.CountAsync(share => share.NebulaId == nebula.Id, Token));
    }

    /// <summary>
    /// The extractor of matter takes the dark matter and the deuterium of a nebula, not the ether
    /// (task 00055).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_nebula_gives_the_dark_matter_to_the_extractor_of_matter()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (MatterCode, 1), (ContainerCode, 1));
        (NebulaEntity nebula, _) = await NebulaAsync();
        long nebulaObjectId = nebula.CelestialObjectId;

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", nebulaObjectId), MineOrder(durationSeconds: 3600) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Assert.Equal([SeedCodes.Deuterium, SeedCodes.DarkMatter], (await CargoByCodeAsync(ship.ShipId)).Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The extractor of ether and the extractor of matter together take the deuterium of a nebula twice:
    /// the deuterium of the two modules adds up, and each of them still takes its own resource (task 00055).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_extractors_of_ether_and_of_matter_together_take_the_deuterium_of_both()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (EtherCode, 1), (MatterCode, 1), (ContainerCode, 1));
        (NebulaEntity nebula, List<NebulaCompositionEntity> shares) = await NebulaAsync();
        ResourceCatalog catalog = await ResourceCatalogAsync();

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", nebula.CelestialObjectId), MineOrder(durationSeconds: 3600) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        // Four cycles; in each of them the extractor of ether takes 325 t and the extractor of matter 275 t,
        // and each takes the share of the deuterium of its own part, rounded down.
        decimal deuteriumShare = ShareOf(catalog, shares, SeedCodes.Deuterium);
        Dictionary<string, long> cargo = await CargoByCodeAsync(ship.ShipId);

        Assert.Equal([SeedCodes.Deuterium, SeedCodes.DarkMatter, SeedCodes.Ether], cargo.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(4 * ((long)decimal.Floor(325 * deuteriumShare) + (long)decimal.Floor(275 * deuteriumShare)), cargo[SeedCodes.Deuterium]);
        Assert.Equal(4 * (long)decimal.Floor(325 * ShareOf(catalog, shares, SeedCodes.Ether)), cargo[SeedCodes.Ether]);
        Assert.Equal(4 * (long)decimal.Floor(275 * ShareOf(catalog, shares, SeedCodes.DarkMatter)), cargo[SeedCodes.DarkMatter]);
    }

    /// <summary>
    /// A new row of the data changes what a module takes without a change of the code: a row added to the
    /// tables only gives the extractor of ether one more resource of a nebula (task 00055).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_row_added_to_the_tables_only_changes_what_the_extractor_takes()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (EtherCode, 1), (ContainerCode, 1));
        (NebulaEntity nebula, List<NebulaCompositionEntity> shares) = await NebulaAsync();
        ResourceCatalog catalog = await ResourceCatalogAsync();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ProductionDbContext production = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
            int etherExtractorId = await production.PartTypes.AsNoTracking().Where(part => part.Code == EtherCode).Select(part => part.Id).SingleAsync(Token);

            production.PartExtractions.Add(new PartExtractionEntity
            {
                PartTypeId = etherExtractorId,
                PlaceKind = ExtractionPlaceKinds.Nebula,
                Mode = ExtractionModes.Listed,
                ResourceId = catalog.Resources.Single(resource => resource.Code == SeedCodes.DarkMatter).Id,
            });
            await production.SaveChangesAsync(Token);
        }

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", nebula.CelestialObjectId), MineOrder(durationSeconds: 3600) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Dictionary<string, long> cargo = await CargoByCodeAsync(ship.ShipId);

        Assert.Equal([SeedCodes.Deuterium, SeedCodes.DarkMatter, SeedCodes.Ether], cargo.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(4 * (long)decimal.Floor(325 * ShareOf(catalog, shares, SeedCodes.DarkMatter)), cargo[SeedCodes.DarkMatter]);
    }

    /// <summary>
    /// A nebula that has none of the resources the extractors of the fleet take (the extractor of ether
    /// takes only the ether) gives nothing: the order is skipped at its start and no cycle is planned.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_extraction_at_a_nebula_without_the_resource_of_the_extractor_is_skipped()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (EtherCode, 1), (ContainerCode, 1));
        (NebulaEntity nebula, List<NebulaCompositionEntity> shares) = await NebulaAsync();

        // The nebula keeps only the resource that no extractor of a nebula takes.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
            int otherId = catalog.Resources.Single(resource => resource.Code == SeedCodes.Helium).Id;

            await scope.ServiceProvider.GetRequiredService<WorldDbContext>().NebulaCompositions
                .Where(share => share.NebulaId == nebula.Id && share.ResourceId != otherId)
                .ExecuteDeleteAsync(Token);
        }

        Assert.Equal(4, shares.Count);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", nebula.CelestialObjectId), MineOrder(durationSeconds: 3600) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Assert.Equal(0L, await CargoAsync(ship.ShipId));
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));
    }

    /// <summary>
    /// A fleet that stands where there is nothing to take from skips the order of extraction: the plan is
    /// not refused and does not stop, and the next order begins.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_extraction_where_the_fleet_has_nothing_to_take_is_skipped()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        (double x, double y) = await EmptyPointAsync(home.SectorId);

        await EnsureNothingHereAsync(home);

        // A plan that is the extraction alone is carried out at once: the order is skipped, the plan ends.
        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { MineOrder(durationSeconds: 3600) } });
        JsonElement answer = await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        Assert.Equal("completed", answer.GetProperty("state_code").GetString());
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));

        // After a flight to an empty point the extraction is skipped too, and the next order is the return.
        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { PointOrder("fly", home.SectorId, x, y, 0), MineOrder(durationSeconds: 3600), new { type_code = "return" } },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity there = await FlightAsync(ship.FleetId);
        await RunUntilAsync(there.ArrivesAt);

        Assert.Equal(3, (await PlanAsync(ship.FleetId)).CurrentPosition);
        Assert.Equal(FlightPlanStates.Active, (await PlanAsync(ship.FleetId)).StateCode);
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));

        await RunAllAsync();

        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Equal(player.PlanetId, (await FleetRowAsync(ship.FleetId)).PlanetId);
    }

    /// <summary>
    /// A plan that holds only a flight leaves the fleet hanging where it arrived, and a new plan that
    /// begins with the extraction takes from that place: the extraction needs no flight before it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_that_begins_with_the_extraction_takes_from_the_place_the_fleet_hangs_at()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        long fieldMassBefore = await FieldMassAsync(fieldObjectId);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Equal(fieldObjectId, (await FleetRowAsync(ship.FleetId)).ObjectId);

        // The new plan starts with the extraction: it begins where the fleet hangs.
        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { MineOrder(durationSeconds: 3600) } });
        JsonElement answer = await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        Assert.Equal("active", answer.GetProperty("state_code").GetString());
        Assert.Single(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));

        await RunAllAsync();

        Assert.True(await FieldMassAsync(fieldObjectId) < fieldMassBefore);
        Assert.InRange(await CargoAsync(ship.ShipId), 1, long.MaxValue);
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>
    /// The time of the extraction is optional: an order without one (or with zero) is written, and the
    /// target the request names is dropped.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_extraction_needs_no_time_and_keeps_no_target()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { new { type_code = "mine" } } });

        FlightPlanOrderEntity withoutTime = await OrderAsync(ship.FleetId, 1);

        Assert.Equal(0L, withoutTime.DurationSeconds);

        await CommandAsync(player, Save, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { new { type_code = "mine", target_kind = "object", target_object_id = fieldObjectId, duration_seconds = 600 } },
        });

        FlightPlanOrderEntity order = await OrderAsync(ship.FleetId, 1);

        Assert.Equal(FlightTargetKinds.None, order.TargetKind);
        Assert.Equal(0L, order.TargetObjectId);
        Assert.Equal(600L, order.DurationSeconds);
    }

    /// <summary>
    /// A fleet that stands at the point of a free asteroid field, within the tolerance of the
    /// configuration, takes from that field, and the order gets the field as its place.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_at_the_point_of_a_free_field_takes_from_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        long fieldMassBefore = await FieldMassAsync(fieldObjectId);
        CelestialObjectEntity field = await ObjectRowAsync(fieldObjectId);

        // The point is next to the point of the field, closer than the tolerance (one unit).
        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { PointOrder("fly", home.SectorId, field.X + 0.4, field.Y, field.Z), MineOrder(durationSeconds: 3600) },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity there = await FlightAsync(ship.FleetId);
        await RunUntilAsync(there.ArrivesAt);

        FleetEntity arrived = await FleetRowAsync(ship.FleetId);
        FlightPlanOrderEntity mine = await OrderAsync(ship.FleetId, 2);

        Assert.Null(arrived.ObjectId);
        Assert.Equal(fieldObjectId, mine.TargetObjectId);
        Assert.Equal(home.SectorId, mine.TargetSectorId);
        Assert.Single(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));

        await RunAllAsync();

        Assert.True(await FieldMassAsync(fieldObjectId) < fieldMassBefore);
        Assert.InRange(await CargoAsync(ship.ShipId), 1, long.MaxValue);
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>
    /// A fleet that stands inside the sphere of a nebula, with no object of its own, takes from the
    /// nebula.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_inside_a_nebula_takes_from_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (EtherCode, 1), (ContainerCode, 1));
        (NebulaEntity nebula, _) = await NebulaAsync();
        CelestialObjectEntity center = await ObjectRowAsync(nebula.CelestialObjectId);

        // A point inside the sphere that is not its centre: half a radius along the first axis, the side
        // that stays inside the sector.
        double sectorSize = await SectorSizeAsync();
        double shifted = center.X + (nebula.Radius / 2) <= sectorSize ? center.X + (nebula.Radius / 2) : center.X - (nebula.Radius / 2);

        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { PointOrder("fly", nebula.SectorId, shifted, center.Y, center.Z), MineOrder(durationSeconds: 3600) },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        FleetFlightEntity there = await FlightAsync(ship.FleetId);
        await RunUntilAsync(there.ArrivesAt);

        Assert.Null((await FleetRowAsync(ship.FleetId)).ObjectId);
        Assert.Equal(nebula.CelestialObjectId, (await OrderAsync(ship.FleetId, 2)).TargetObjectId);

        await RunAllAsync();

        Assert.InRange(await CargoAsync(ship.ShipId), 1, long.MaxValue);
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>
    /// The place is looked up again when the order begins: a field that is gone while the fleet flies to
    /// its point makes the extraction skip, and the plan goes on to the next order.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_extraction_is_skipped_when_the_place_is_gone_at_the_turn_of_the_order()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);
        CelestialObjectEntity field = await ObjectRowAsync(fieldObjectId);

        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { PointOrder("fly", home.SectorId, field.X, field.Y, field.Z), MineOrder(durationSeconds: 3600), new { type_code = "return" } },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Fields
                .Where(entry => entry.CelestialObjectId == fieldObjectId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.DepletedAt, DateTimeOffset.UtcNow), Token);
        }

        await RunAllAsync();

        // The plan did not stop: the extraction was skipped, the fleet returned and the plan is carried out.
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Equal(player.PlanetId, (await FleetRowAsync(ship.FleetId)).PlanetId);
        Assert.Empty(await PendingEventsAsync(FleetExtractionCycleService.CycleEventType));
    }

    /// <summary>
    /// A target that a saved plan still carries on an order of extraction is ignored and reset when the
    /// order begins: the fleet takes from the place where it stands, not from the old target.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_old_target_of_an_extraction_is_ignored()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1), (ContainerCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long first = await FreeFieldAsync(home.SectorId);
        long second = await FreeFieldAsync(home.SectorId, skip: 1);
        long secondMassBefore = await FieldMassAsync(second);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", first), MineOrder(durationSeconds: 3600) } });

        // A plan saved before the task 00054 carries the old target of the extraction.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FlightPlanOrders
                .Where(order => order.TypeCode == FlightOrderTypes.Mine)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(order => order.TargetKind, FlightTargetKinds.Object)
                        .SetProperty(order => order.TargetObjectId, second),
                    Token);
        }

        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Assert.Equal(secondMassBefore, await FieldMassAsync(second));
        Assert.True(await FieldMassAsync(first) < await InitialFieldMassAsync(first));
    }

    /// <summary>
    /// The nearest place wins when several fit the position: a free field next to the centre of a nebula
    /// is the place at its own point, and the nebula is the place at its centre.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_nearest_place_wins_when_several_fit()
    {
        await ResetAsync();
        NebulaEntity nebula;
        CelestialObjectEntity center;
        long fieldObjectId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            nebula = await world.Nebulae.AsNoTracking().OrderBy(entry => entry.Id).FirstAsync(Token);
            center = await world.Objects.AsNoTracking().SingleAsync(entry => entry.Id == nebula.CelestialObjectId, Token);
            fieldObjectId = await world.Fields.AsNoTracking()
                .Where(field => field.SectorId == nebula.SectorId && field.SystemId == null && field.DepletedAt == null)
                .OrderBy(field => field.Id)
                .Select(field => field.CelestialObjectId)
                .FirstAsync(Token);

            // The field is moved to three units from the centre of the nebula.
            await world.Objects
                .Where(entry => entry.Id == fieldObjectId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(entry => entry.X, center.X + 3)
                        .SetProperty(entry => entry.Y, center.Y)
                        .SetProperty(entry => entry.Z, center.Z),
                    Token);
        }

        using IServiceScope check = _factory.Services.CreateScope();
        IWorldModule worldModule = check.ServiceProvider.GetRequiredService<IWorldModule>();
        WorldPlaceDto origin = await PointAsync(nebula.SectorId, 0, 0, 0);
        double x = origin.X + center.X;
        double y = origin.Y + center.Y;
        double z = origin.Z + center.Z;

        // At the point of the field (tolerance of one unit) the field is the nearest; at the centre of the
        // nebula the field is too far to match, so the nebula is the place.
        WorldPlaceDto? atField = await worldModule.FindExtractionPlaceAsync(x + 3, y, z, 1, Token);
        WorldPlaceDto? atCenter = await worldModule.FindExtractionPlaceAsync(x, y, z, 1, Token);

        Assert.Equal(fieldObjectId, atField?.ObjectId);
        Assert.Equal(WorldObjectClasses.AsteroidField, atField?.ObjectClass);
        Assert.Equal(nebula.CelestialObjectId, atCenter?.ObjectId);
        Assert.Equal(WorldObjectClasses.Nebula, atCenter?.ObjectClass);

        // With a tolerance that holds both, the nearer one still wins: the field at its own point.
        Assert.Equal(fieldObjectId, (await worldModule.FindExtractionPlaceAsync(x + 3, y, z, 5, Token))?.ObjectId);
        Assert.Equal(nebula.CelestialObjectId, (await worldModule.FindExtractionPlaceAsync(x + 1, y, z, 5, Token))?.ObjectId);

        // Open space far from every field and every nebula holds nothing.
        Assert.Null(await worldModule.FindExtractionPlaceAsync(x + 100000, y, z, 5, Token));
    }

    /// <summary>
    /// A fleet without a module that suits the place skips the order of extraction: the drill rig takes
    /// nothing from a gas giant, so the plan goes on and nothing is taken.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_extraction_without_the_right_module_is_skipped()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1), (DrillCode, 1));
        long giantObjectId = await GasGiantAsync();

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PlanetOrder("fly", giantObjectId), MineOrder(durationSeconds: 3600), new { type_code = "return" } } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Assert.Equal(0L, await CargoAsync(ship.ShipId));
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Equal(player.PlanetId, (await FleetRowAsync(ship.FleetId)).PlanetId);
    }

    /// <summary>
    /// A plan without orders, a plan with an order the fleet cannot carry out yet and a fleet without a
    /// drive are refused with short codes, and nothing starts.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_that_cannot_be_carried_out_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship withDrive = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = withDrive.FleetId, orders = Array.Empty<object>() });
        Assert.Contains(FleetErrorCodes.FlightPlanEmpty, await RefusalAsync(player, Execute, new { fleet_id = withDrive.FleetId }));

        await CommandAsync(player, Save, new { fleet_id = withDrive.FleetId, orders = new object[] { new { type_code = "scan", target_kind = "object", target_object_id = 1 } } });
        Assert.Contains(FleetErrorCodes.ScanNoModule, await RefusalAsync(player, Execute, new { fleet_id = withDrive.FleetId }));

        await CommandAsync(player, Save, new { fleet_id = withDrive.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 5000000, 1, 0) } });
        Assert.Contains(FleetErrorCodes.FlightTargetUnknown, await RefusalAsync(player, Execute, new { fleet_id = withDrive.FleetId }));

        using PlanetsTestPlayer other = await FoundAsync();
        Ship withoutDrive = await FleetAsync(other);
        WorldPlaceDto otherHome = await HomeAsync(other);

        await CommandAsync(other, Plans, new { fleet_id = withoutDrive.FleetId, orders = new object[] { PointOrder("fly", otherHome.SectorId, 10, 10, 0) } });
        Assert.Contains(FleetErrorCodes.FlightNoDrive, await RefusalAsync(other, Execute, new { fleet_id = withoutDrive.FleetId }));
    }

    /// <summary>
    /// The plan that is stopped in flight leaves the fleet at the point of its route that the time has
    /// reached: the fleet waits there, the flight is gone, and the arrival that was planned changes
    /// nothing.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_stopped_in_flight_leaves_the_fleet_on_the_route()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        WorldPlaceDto point = await PointAsync(home.SectorId, 300, 300, 0);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 300, 300, 0) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        // The flight is moved so that the fleet is in the middle of its route now.
        IGameClock clock = _factory.Services.GetRequiredService<IGameClock>();
        DateTimeOffset now = clock.GameNow;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            FleetFlightEntity flight = await database.FleetFlights.SingleAsync(entry => entry.FleetId == ship.FleetId, Token);
            flight.StartedAt = now - TimeSpan.FromMinutes(30);
            flight.ArrivesAt = now + TimeSpan.FromMinutes(30);
            await database.SaveChangesAsync(Token);
        }

        JsonElement stopped = await CommandAsync(player, Stop, new { fleet_id = ship.FleetId });

        Assert.Equal("paused", stopped.GetProperty("state_code").GetString());

        FleetEntity waiting = await FleetRowAsync(ship.FleetId);

        Assert.Equal(FleetStates.Stationed, waiting.StateCode);
        Assert.Equal(0L, waiting.PlanetId);
        Assert.Null(waiting.ObjectId);
        Assert.Equal((home.X + point.X) / 2d, waiting.PositionX!.Value, 1);
        Assert.Equal((home.Y + point.Y) / 2d, waiting.PositionY!.Value, 1);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FleetFlights.ToListAsync(Token));
        }

        // The arrival that was planned finds no flight and changes nothing.
        await RunAllAsync();

        FleetEntity still = await FleetRowAsync(ship.FleetId);

        Assert.Equal(waiting.PositionX, still.PositionX);
        Assert.Equal("paused", (await PlanAsync(ship.FleetId)).StateCode);
    }

    /// <summary>
    /// A plan that was stopped goes on with the order it was stopped at, and the events of the run that
    /// was stopped do not run again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stopped_plan_goes_on_with_the_order_it_stopped_at()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new
        {
            fleet_id = ship.FleetId,
            orders = new object[] { new { type_code = "wait", duration_seconds = 600 }, PointOrder("fly", home.SectorId, 50, 50, 0) },
        });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await CommandAsync(player, Stop, new { fleet_id = ship.FleetId });

        Assert.Equal(1, (await PlanAsync(ship.FleetId)).CurrentPosition);

        JsonElement again = await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        Assert.Equal("active", again.GetProperty("state_code").GetString());
        Assert.Equal(1, again.GetProperty("current_position").GetInt32());

        // Two waits are planned: the one of the first run and the one of the second.
        Assert.Equal(2, (await PendingEventsAsync(FlightPlanRunner.WaitEventType)).Count);

        DateTimeOffset now = _factory.Services.GetRequiredService<IGameClock>().GameNow;
        await RunUntilAsync(now + TimeSpan.FromSeconds(601));

        // The wait of the second run ended and the flight began; the wait of the first run did nothing.
        Assert.Equal(2, (await PlanAsync(ship.FleetId)).CurrentPosition);
        Assert.Single(await FlightsAsync());
    }

    /// <summary>
    /// A fleet that is under way, or that stands at a place that is not the body of its ships, does not
    /// give its ships back: the fleet is neither disbanded nor emptied.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_that_is_away_keeps_its_ships()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship first = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);

        await CommandAsync(player, Plans, new { fleet_id = first.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId) } });
        await CommandAsync(player, Execute, new { fleet_id = first.FleetId });

        // Under way.
        Assert.Contains(FleetErrorCodes.FleetAway, await RefusalAsync(player, Dissolve, new { fleet_id = first.FleetId }));
        Assert.Contains(FleetErrorCodes.FleetAway, await RefusalAsync(player, RemoveShips, new { fleet_id = first.FleetId, ship_ids = new[] { first.ShipId } }));

        // Arrived at a field: the field is not the body of the ships.
        FleetFlightEntity flight = await FlightAsync(first.FleetId);
        await RunUntilAsync(flight.ArrivesAt);

        Assert.Contains(FleetErrorCodes.FleetAway, await RefusalAsync(player, Dissolve, new { fleet_id = first.FleetId }));
        Assert.Contains(FleetErrorCodes.FleetAway, await RefusalAsync(player, RemoveShips, new { fleet_id = first.FleetId, ship_ids = new[] { first.ShipId } }));
    }

    /// <summary>
    /// A fleet that comes home is docked again and may be disbanded, and its ships stand on the body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_that_came_home_is_docked_again()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long fieldObjectId = await FreeFieldAsync(home.SectorId);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", fieldObjectId), new { type_code = "return" } } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        await RunAllAsync();

        Assert.Equal(player.PlanetId, (await FleetRowAsync(ship.FleetId)).PlanetId);

        JsonElement view = await CommandAsync(player, Dissolve, new { fleet_id = ship.FleetId });

        Assert.Empty(view.GetProperty("fleets").EnumerateArray());
        Assert.Equal(player.PlanetId, (await ShipRowAsync(ship.ShipId)).PlanetId);
    }

    /// <summary>
    /// A flight to the place the fleet is at takes no time: the fleet arrives at once, the plan is carried
    /// out in the command that started it, and the answer says so.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_flight_to_the_place_the_fleet_is_at_takes_no_time()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PlanetOrder("fly", home.ObjectId!.Value) } });
        JsonElement answer = await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        Assert.Equal("completed", answer.GetProperty("state_code").GetString());
        Assert.False(await PlanExistsAsync(ship.FleetId));
        Assert.Empty(await FlightsAsync());

        // The fleet has the position of the body now, with its three axes.
        FleetEntity fleet = await FleetRowAsync(ship.FleetId);

        Assert.Equal(player.PlanetId, fleet.PlanetId);
        Assert.Equal(home.ObjectId, fleet.ObjectId);
        Assert.Equal(home.X, fleet.PositionX!.Value, 6);
        Assert.Equal(home.Z, fleet.PositionZ!.Value, 6);
    }

    /// <summary>
    /// An order that cannot begin at its turn because its target is gone stops the plan with the reason,
    /// and the fleet waits where it is.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_stops_with_the_reason_when_a_target_is_gone_at_the_turn_of_its_order()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);
        long first = await FreeFieldAsync(home.SectorId);
        long second = await FreeFieldAsync(home.SectorId, skip: 1);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { ObjectOrder("fly", first), ObjectOrder("fly", second) } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        // The second field disappears while the fleet flies to the first one.
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Fields
                .Where(field => field.CelestialObjectId == second)
                .ExecuteUpdateAsync(setters => setters.SetProperty(field => field.DepletedAt, DateTimeOffset.UtcNow), Token);
        }

        await RunAllAsync();

        FlightPlanEntity plan = await PlanAsync(ship.FleetId);

        Assert.Equal(FlightPlanStates.Paused, plan.StateCode);
        Assert.Equal(FleetErrorCodes.FlightTargetUnknown, plan.PauseCode);
        Assert.Equal(2, plan.CurrentPosition);

        FleetEntity fleet = await FleetRowAsync(ship.FleetId);

        Assert.Equal(first, fleet.ObjectId);
        Assert.Equal(FleetStates.Stationed, fleet.StateCode);
    }

    /// <summary>
    /// Twenty commands at once start the plan of a fleet once: the others find it under way, and one
    /// flight is written.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_commands_at_once_start_a_plan_once()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));
        WorldPlaceDto home = await HomeAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { PointOrder("fly", home.SectorId, 40, 40, 0) } });

        HttpResponseMessage[] answers = await Task.WhenAll(
            Enumerable.Range(0, ParallelCount).Select(_ => SendAsync(player, Execute, new { fleet_id = ship.FleetId })));

        Assert.Equal(1, answers.Count(answer => answer.StatusCode == HttpStatusCode.OK));
        Assert.Single(await FlightsAsync());
        Assert.Single(await PendingEventsAsync(FlightPlanRunner.ArrivalEventType));
    }

    /// <summary>
    /// The plan of the fleet of another player is neither started nor stopped.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_plan_of_another_player_is_neither_started_nor_stopped()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        using PlanetsTestPlayer stranger = await FoundAsync();
        Ship ship = await FleetAsync(player, (DriveCode, 1));

        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { new { type_code = "wait", duration_seconds = 600 } } });

        Assert.Contains(FleetErrorCodes.FlightPlanUnknown, await RefusalAsync(stranger, Execute, new { fleet_id = ship.FleetId }));

        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });

        Assert.Contains(FleetErrorCodes.FlightPlanUnknown, await RefusalAsync(stranger, Stop, new { fleet_id = ship.FleetId }));
        Assert.Equal("active", (await PlanAsync(ship.FleetId)).StateCode);
    }

    /// <summary>A scanner completes once and writes a snapshot and a report.</summary>
    /// <returns>A task that completes when the test has finished.</returns>
    [Fact]
    public async Task A_system_scanner_writes_a_snapshot_and_report_after_its_duration()
    {
        await ResetAsync();
        using (IServiceScope setup = _factory.Services.CreateScope())
        {
            await setup.ServiceProvider.GetRequiredService<Aetheris.Mail.Services.MailBootstrapService>().EnsureAsync(Token);
        }
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, ("part_m_58", 1));
        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { new { type_code = "scan", target_kind = "none" } } });
        JsonElement plan = await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        Assert.Equal("active", plan.GetProperty("state_code").GetString());
        ScheduledEventEntity scheduled = Assert.Single(await PendingEventsAsync(FlightPlanRunner.ScanEventType));
        await RunUntilAsync(scheduled.RunAt);
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        ScanSnapshotEntity snapshot = await world.ScanSnapshots.AsNoTracking().Include(entry => entry.SystemIds).SingleAsync(entry => entry.PlayerId == player.Account.UserId, Token);
        Assert.Equal(25, snapshot.Radius);
        Assert.Equal(TimeSpan.FromHours(48), snapshot.ExpiresAt - snapshot.ScannedAt);
        Assert.NotEmpty(snapshot.SystemIds);
        var mail = scope.ServiceProvider.GetRequiredService<Aetheris.Mail.Data.MailDbContext>();
        Assert.True(await mail.MailMessages.AnyAsync(entry => entry.ReceiverPlayerId == player.Account.UserId && entry.SystemKindCode == "scan_report", Token));
        Assert.False(await PlanExistsAsync(ship.FleetId));
    }

    /// <summary>Stopping a scan invalidates its completion event.</summary>
    /// <returns>A task that completes when the test has finished.</returns>
    [Fact]
    public async Task A_stopped_scan_does_not_write_a_snapshot()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, ("part_m_58", 1));
        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { new { type_code = "scan", target_kind = "none" } } });
        await CommandAsync(player, Execute, new { fleet_id = ship.FleetId });
        ScheduledEventEntity scheduled = Assert.Single(await PendingEventsAsync(FlightPlanRunner.ScanEventType));
        await CommandAsync(player, Stop, new { fleet_id = ship.FleetId });
        await RunUntilAsync(scheduled.RunAt);
        using IServiceScope scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ScanSnapshots.AnyAsync(entry => entry.PlayerId == player.Account.UserId, Token));
    }

    /// <summary>A ship radar alone does not provide system scanning.</summary>
    /// <returns>A task that completes when the test has finished.</returns>
    [Fact]
    public async Task A_ship_radar_cannot_scan_systems()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        Ship ship = await FleetAsync(player, ("part_m_59", 1));
        await CommandAsync(player, Plans, new { fleet_id = ship.FleetId, orders = new object[] { new { type_code = "scan", target_kind = "none" } } });
        Assert.Contains(FleetErrorCodes.FlightOrderNotSupported, await RefusalAsync(player, Execute, new { fleet_id = ship.FleetId }));
    }

    /// <summary>A snapshot remains unchanged until a later scan replaces the covered system.</summary>
    /// <returns>A task that completes when the test has finished.</returns>
    [Fact]
    public async Task A_scan_keeps_the_recorded_state_and_replaces_it_on_a_new_scan()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        WorldPlaceDto home = await HomeAsync(player);
        DateTimeOffset now = _factory.Services.GetRequiredService<IGameClock>().GameNow;
        long snapshotId;
        long systemId;
        string original;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ScanSnapshotResult result = await scope.ServiceProvider.GetRequiredService<IWorldModule>().TakeScanSnapshotAsync(player.Account.UserId, home.SectorId, home.X, home.Y, home.Z, 25, now, now.AddHours(48), Token);
            snapshotId = result.Id;
            systemId = result.Systems.First().Id;
            original = result.Systems.First().Name;
        }
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            StarSystemEntity system = await db.Systems.SingleAsync(entry => entry.Id == systemId, Token);
            system.Name = "Changed after scan";
            await db.SaveChangesAsync(Token);
            Assert.Equal(original, (await db.ScanSnapshotSystems.AsNoTracking().SingleAsync(entry => entry.ScanSnapshotId == snapshotId && entry.SystemId == systemId, Token)).Name);
        }
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ScanSnapshotResult next = await scope.ServiceProvider.GetRequiredService<IWorldModule>().TakeScanSnapshotAsync(player.Account.UserId, home.SectorId, home.X, home.Y, home.Z, 25, now.AddMinutes(1), now.AddHours(49), Token);
            Assert.Equal("Changed after scan", next.Systems.Single(entry => entry.Id == systemId).Name);
            WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            Assert.False(await db.ScanSnapshotSystems.AnyAsync(entry => entry.ScanSnapshotId == snapshotId && entry.SystemId == systemId, Token));
        }
    }

    /// <summary>A free field is selected in three dimensions and removed at expiration.</summary>
    /// <returns>A task that completes when the test has finished.</returns>
    [Fact]
    public async Task A_scan_finds_free_fields_in_three_dimensions_and_expires()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        WorldPlaceDto home = await HomeAsync(player);
        long fieldId = await FreeFieldAsync(home.SectorId);
        WorldPlaceDto field = await ObjectPlaceAsync(fieldId);
        DateTimeOffset now = _factory.Services.GetRequiredService<IGameClock>().GameNow;
        long snapshotId;
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();
            ScanSnapshotResult high = await world.TakeScanSnapshotAsync(player.Account.UserId, field.SectorId, field.X, field.Y, field.Z + 5, 1, now, now.AddHours(48), Token);
            Assert.DoesNotContain(high.Objects, entry => entry.Id == fieldId);
        }
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ScanSnapshotResult found = await scope.ServiceProvider.GetRequiredService<IWorldModule>().TakeScanSnapshotAsync(player.Account.UserId, field.SectorId, field.X, field.Y, field.Z, 1, now, now.AddHours(48), Token);
            Assert.Contains(found.Objects, entry => entry.Id == fieldId && entry.SystemId == null);
            snapshotId = found.Id;
        }
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            AsteroidFieldEntity liveField = await db.Fields.SingleAsync(entry => entry.CelestialObjectId == fieldId, Token);
            liveField.DepletedAt = now.AddMinutes(1);
            await db.SaveChangesAsync(Token);
            Assert.True(await db.ScanSnapshotObjects.AnyAsync(entry => entry.ScanSnapshotId == snapshotId && entry.CelestialObjectId == fieldId, Token));
        }
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ScanSnapshotResult rescanned = await scope.ServiceProvider.GetRequiredService<IWorldModule>().TakeScanSnapshotAsync(player.Account.UserId, field.SectorId, field.X, field.Y, field.Z, 1, now.AddMinutes(1), now.AddHours(48), Token);
            Assert.DoesNotContain(rescanned.Objects, entry => entry.Id == fieldId);
            Assert.False(await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ScanSnapshotObjects.AnyAsync(entry => entry.ScanSnapshotId == snapshotId && entry.CelestialObjectId == fieldId, Token));
            snapshotId = rescanned.Id;
        }
        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            ScanSnapshotService service = scope.ServiceProvider.GetRequiredService<ScanSnapshotService>();
            Assert.Equal(0, await service.ExpireAsync(snapshotId, now.AddHours(47), Token));
            var handler = scope.ServiceProvider.GetServices<Aetheris.Core.Scheduling.IScheduledEventHandler>()
                .Single(entry => entry.EventType == ScanSnapshotService.ExpireEventType);
            await handler.HandleAsync(new Aetheris.Core.Scheduling.ScheduledEvent(1, field.SectorId, ScanSnapshotService.ExpireEventType,
                now.AddHours(48), JsonSerializer.Serialize(snapshotId)), Token);
            Assert.False(await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ScanSnapshots.AnyAsync(entry => entry.Id == snapshotId, Token));
        }
    }

    private static object PointOrder(string type, int sectorId, double x, double y, double z) =>
        new { type_code = type, target_kind = "point", target_sector_id = sectorId, target_x = x, target_y = y, target_z = z };

    private static object ObjectOrder(string type, long objectId) =>
        new { type_code = type, target_kind = "object", target_object_id = objectId };

    private static object PlanetOrder(string type, long objectId) =>
        new { type_code = type, target_kind = "planet", target_object_id = objectId };

    private static object MineOrder(long durationSeconds) =>
        new { type_code = "mine", target_kind = "none", duration_seconds = durationSeconds };

    private static double Speed(double push, double mass) =>
        ReferenceSpeed * Math.Sqrt((push / mass) / (ReferencePush / ReferenceMass));

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.FleetNameMaxLength, "64");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.FlightPlanMaxOrders, "32");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.ExtractionCycleSeconds, "900");
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IFleetModule>().EnsureAsync(Token);
    }

    private async Task<PlanetsTestPlayer> FoundAsync()
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, 1);

        return player;
    }

    /// <summary>
    /// Writes a ship of the player with a project that has the given modules, and a fleet of that ship.
    /// A module is written once for every slot, and the ship holds them as a stack of that many parts.
    /// </summary>
    private async Task<Ship> FleetAsync(PlanetsTestPlayer player, params (string Code, int Count)[] modules)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        string[] codes = [HullCode, .. modules.Select(module => module.Code)];
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync(codes, Token);
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

        int slotIndex = 0;

        foreach ((string code, int count) in modules)
        {
            for (int i = 0; i < count; i++)
            {
                database.ShipDesignSlots.Add(new ShipDesignSlotEntity { DesignId = design.Id, SlotIndex = slotIndex++, PartTypeId = partIds[code], MarkLevel = 1 });
            }
        }

        List<ShipModuleEntity> parts = [new ShipModuleEntity { PartTypeId = partIds[HullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true }];
        parts.AddRange(modules.Select(module => new ShipModuleEntity
        {
            PartTypeId = partIds[module.Code],
            MarkLevel = 1,
            ConditionPercent = 100,
            ConditionCode = ShipModuleConditions.Intact,
            Quantity = module.Count,
        }));

        ShipEntity ship = new()
        {
            PlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            ShipDesignId = design.Id,
            Crew = 7,
            StateCode = ShipStates.Ready,
            CreatedAt = now,
            ModuleIds = parts,
        };
        database.Ships.Add(ship);
        await database.SaveChangesAsync(Token);

        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот", ship_ids = new[] { ship.Id } });
        long fleetId = view.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64();

        return new Ship(fleetId, ship.Id, design.Id, player);
    }

    private async Task<WorldPlaceDto> HomeAsync(PlanetsTestPlayer player) => await PlanetPlaceAsync(player.PlanetId);

    private async Task<WorldPlaceDto> PlanetPlaceAsync(long planetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetPlanetPlaceAsync(planetId, Token)
            ?? throw new InvalidOperationException("The body has no place.");
    }

    private async Task<WorldPlaceDto> ObjectPlaceAsync(long objectId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetObjectPlaceAsync(objectId, Token)
            ?? throw new InvalidOperationException("The object has no place.");
    }

    private async Task<WorldPlaceDto> PointAsync(int sectorId, double x, double y, double z)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetPointPlaceAsync(sectorId, x, y, z, Token)
            ?? throw new InvalidOperationException("The point is outside the sector.");
    }

    private Task<double> DistanceAsync(WorldPlaceDto from, WorldPlaceDto to)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return Task.FromResult(scope.ServiceProvider.GetRequiredService<IWorldModule>().GetTravelDistance(from, to));
    }

    private async Task<long> FreeFieldAsync(int sectorId, int skip = 0)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        return await world.Fields.AsNoTracking()
            .Where(field => field.SectorId == sectorId && field.SystemId == null && field.DepletedAt == null)
            .OrderBy(field => field.Id)
            .Skip(skip)
            .Select(field => field.CelestialObjectId)
            .FirstAsync(Token);
    }

    /// <summary>
    /// Gives the first nebula the composition the extractors of a nebula work with: the ether, the dark
    /// matter, the deuterium that both extractors take and a resource that no extractor takes (the
    /// generated composition is random).
    /// </summary>
    private async Task<(NebulaEntity Nebula, List<NebulaCompositionEntity> Shares)> NebulaAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        NebulaEntity nebula = await world.Nebulae.AsNoTracking().OrderBy(entry => entry.Id).FirstAsync(Token);

        await world.NebulaCompositions.Where(share => share.NebulaId == nebula.Id).ExecuteDeleteAsync(Token);

        (string Code, decimal Coefficient)[] wanted =
        [
            (SeedCodes.Ether, 0.4m),
            (SeedCodes.DarkMatter, 0.3m),
            (SeedCodes.Helium, 0.2m),
            (SeedCodes.Deuterium, 0.08m),
        ];

        foreach ((string code, decimal coefficient) in wanted)
        {
            world.NebulaCompositions.Add(new NebulaCompositionEntity
            {
                SectorId = nebula.SectorId,
                NebulaId = nebula.Id,
                ResourceId = catalog.Resources.Single(resource => resource.Code == code).Id,
                Coefficient = coefficient,
            });
        }

        await world.SaveChangesAsync(Token);

        return (nebula, await world.NebulaCompositions.AsNoTracking().Where(share => share.NebulaId == nebula.Id).OrderBy(share => share.Id).ToListAsync(Token));
    }

    private async Task<ResourceCatalog> ResourceCatalogAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
    }

    private static decimal ShareOf(ResourceCatalog catalog, List<NebulaCompositionEntity> shares, string resourceCode)
    {
        int resourceId = catalog.Resources.Single(resource => resource.Code == resourceCode).Id;

        return shares.Single(share => share.ResourceId == resourceId).Coefficient;
    }

    private async Task<CelestialObjectEntity> ObjectRowAsync(long objectId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Objects.AsNoTracking()
            .SingleAsync(entry => entry.Id == objectId, Token);
    }

    private async Task<long> InitialFieldMassAsync(long objectId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Fields.AsNoTracking()
            .Where(field => field.CelestialObjectId == objectId)
            .Select(field => field.InitialMass)
            .SingleAsync(Token);
    }

    private async Task<double> SectorSizeAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return double.Parse(
            await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().ConfigParameters.AsNoTracking()
                .Where(parameter => parameter.Key == WorldConfigKeys.SectorSize)
                .Select(parameter => parameter.Value)
                .SingleAsync(Token),
            CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Finds a point of a sector that holds nothing to take from: no free field within the tolerance and
    /// no nebula, so a fleet that stands there has nothing to extract.
    /// </summary>
    private async Task<(double X, double Y)> EmptyPointAsync(int sectorId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();
        double size = await SectorSizeAsync();

        for (double x = 1; x < size; x += 37)
        {
            for (double y = 1; y < size; y += 41)
            {
                WorldPlaceDto point = await PointAsync(sectorId, x, y, 0);

                if (await world.FindExtractionPlaceAsync(point.X, point.Y, point.Z, 5, Token) is null)
                {
                    return (x, y);
                }
            }
        }

        throw new InvalidOperationException("The sector has no empty point.");
    }

    /// <summary>
    /// Checks that nothing is taken from the position of a place, so a test that relies on it fails loudly
    /// if the generated world puts a nebula over the home of the player.
    /// </summary>
    private async Task EnsureNothingHereAsync(WorldPlaceDto home)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IWorldModule>().FindExtractionPlaceAsync(home.X, home.Y, home.Z, 5, Token));
    }

    private async Task<FlightPlanOrderEntity> OrderAsync(long fleetId, int position)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        return await (
                from order in database.FlightPlanOrders.AsNoTracking()
                join plan in database.FlightPlans.AsNoTracking() on order.FlightPlanId equals plan.Id
                where plan.FleetId == fleetId && order.Position == position
                select order)
            .SingleAsync(Token);
    }

    private async Task<long> GasGiantAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        return await world.Objects.AsNoTracking()
            .Join(world.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => new { entry.Id, type.ObjectClass })
            .Where(row => row.ObjectClass == WorldObjectClasses.GasGiant)
            .OrderBy(row => row.Id)
            .Select(row => row.Id)
            .FirstAsync(Token);
    }

    private async Task<PlanetEntity> NeighbourAsync(WorldPlaceDto home)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        return await world.Planets.AsNoTracking()
            .Where(planet => planet.SystemId == home.SystemId && planet.Id != home.PlanetId && planet.OwnerPlayerId == null)
            .OrderBy(planet => planet.Id)
            .FirstAsync(Token);
    }

    private async Task<long> FieldMassAsync(long objectId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Fields.AsNoTracking()
            .Where(field => field.CelestialObjectId == objectId)
            .Select(field => field.Mass)
            .SingleAsync(Token);
    }

    private async Task<string> ParameterAsync(string key)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().ConfigParameters.AsNoTracking()
            .Where(parameter => parameter.Key == key)
            .Select(parameter => parameter.Value)
            .SingleAsync(Token);
    }

    private static async Task<DesignFacts> FactsAsync(Ship ship)
    {
        using HttpResponseMessage design = await ship.Owner.Client.GetAsync($"/api/v1/fleet/designs/{ship.DesignId}", Token);
        using JsonDocument body = JsonDocument.Parse(await design.Content.ReadAsStringAsync(Token));
        JsonElement summary = body.RootElement.GetProperty("summary");

        return new DesignFacts(
            summary.GetProperty("drive_push").GetDouble(),
            summary.GetProperty("weight").GetDouble(),
            summary.GetProperty("hold_capacity").GetDouble());
    }

    /// <summary>
    /// The time of a flight by the formula, from the mass and the push the server reads out of the
    /// project of the ship, with an independent calculation.
    /// </summary>
    private async Task<double> ExpectedSecondsAsync(Ship ship, WorldPlaceDto from, WorldPlaceDto to, double carried)
    {
        DesignFacts facts = await FactsAsync(ship);
        double distance = await DistanceAsync(from, to);

        return Math.Ceiling(distance / Speed(facts.Push, facts.Weight + carried) * SecondsPerHour);
    }

    private async Task<FleetFlightEntity> FlightAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FleetFlights.AsNoTracking()
            .SingleAsync(entry => entry.FleetId == fleetId, Token);
    }

    private async Task<List<FleetFlightEntity>> FlightsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FleetFlights.AsNoTracking().ToListAsync(Token);
    }

    private async Task<FleetEntity> FleetRowAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Fleets.AsNoTracking()
            .SingleAsync(entry => entry.Id == fleetId, Token);
    }

    private async Task<ShipEntity> ShipRowAsync(long shipId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Ships.AsNoTracking()
            .SingleAsync(entry => entry.Id == shipId, Token);
    }

    private async Task<FlightPlanEntity> PlanAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FlightPlans.AsNoTracking()
            .SingleAsync(entry => entry.FleetId == fleetId, Token);
    }

    private async Task<bool> PlanExistsAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FlightPlans.AsNoTracking()
            .AnyAsync(entry => entry.FleetId == fleetId, Token);
    }

    private async Task<long> CargoAsync(long shipId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipCargos.AsNoTracking()
            .Where(cargo => cargo.ShipId == shipId)
            .SumAsync(cargo => (long?)cargo.Amount, Token) ?? 0L;
    }

    private async Task<long> FleetCargoAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FleetCargos.AsNoTracking()
            .Where(cargo => cargo.FleetId == fleetId)
            .SumAsync(cargo => (long?)cargo.Amount, Token) ?? 0L;
    }

    private async Task<Dictionary<string, long>> CargoByCodeAsync(long shipId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        Dictionary<int, long> byId = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipCargos.AsNoTracking()
            .Where(cargo => cargo.ShipId == shipId)
            .ToDictionaryAsync(cargo => cargo.ResourceId, cargo => cargo.Amount, Token);
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        return byId.ToDictionary(entry => catalog.Resources.Single(resource => resource.Id == entry.Key).Code, entry => entry.Value);
    }

    private async Task<List<ScheduledEventEntity>> PendingEventsAsync(string type)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AetherisDbContext>().ScheduledEvents.AsNoTracking()
            .Where(entry => entry.Type == type && entry.Status == "pending")
            .OrderBy(entry => entry.RunAt)
            .ToListAsync(Token);
    }

    private async Task<JsonElement> ReadFleetAsync(PlanetsTestPlayer player, long fleetId)
    {
        using HttpResponseMessage answer = await player.Client.GetAsync(Fleets, Token);
        string text = await answer.Content.ReadAsStringAsync(Token);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.GetProperty("fleets").EnumerateArray()
            .Single(entry => entry.GetProperty("id").GetInt64() == fleetId)
            .Clone();
    }

    /// <summary>
    /// Runs the scheduler up to a moment of game time: every event that is due then is handled, the
    /// events that those plan for the same moment too.
    /// </summary>
    private async Task RunUntilAsync(DateTimeOffset moment)
    {
        DateTimeOffset now = _factory.Services.GetRequiredService<IGameClock>().GameNow;

        await ShiftedGameClock.RunDueEventsAsync(_factory, moment - now + TimeSpan.FromSeconds(1));
    }

    private Task RunAllAsync() => ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromHours(Day * 7));

    private async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await SendAsync(player, route, body);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<string> RefusalAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await SendAsync(player, route, body);

        return await response.Content.ReadAsStringAsync(Token);
    }

    private static Task<HttpResponseMessage> SendAsync(PlanetsTestPlayer player, string route, object body) =>
        TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));

    private sealed record Ship(long FleetId, long ShipId, long DesignId, PlanetsTestPlayer Owner);

    private sealed record DesignFacts(double Push, double Weight, double Hold);
}

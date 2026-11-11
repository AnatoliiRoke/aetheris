using System.Net;
using System.Text.Json;
using Aetheris.Fleet;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the flight plans of the fleets (task 00050): the plan belongs to the player and to
/// the fleet, a fleet has one plan at most, the orders are kept in the order they are written and
/// the plan under way is neither changed nor deleted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FlightPlanTests
{
    private const string Spaceport = "building_6";
    private const string HullCode = "part_h_21";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const string Plans = "/api/v1/fleet/flight-plans";
    private const string Save = "/api/v1/fleet/flight-plans/save";
    private const string Delete = "/api/v1/fleet/flight-plans/delete";
    private const string Stop = "/api/v1/fleet/flight-plans/stop";
    private const string Execute = "/api/v1/fleet/flight-plans/execute";
    private const int ParallelCount = 20;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlanTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public FlightPlanTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A plan is written for a fleet of the player: it belongs to that fleet, its orders are kept
    /// in the order they were written, and a read finds it again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_is_written_for_a_fleet_and_keeps_the_orders_in_their_order()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        JsonElement plan = await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[]
            {
                new { type_code = "fly", target_kind = "system", target_system_id = 12 },
                new { type_code = "wait", duration_seconds = 600 },
                new { type_code = "return" },
            },
        });

        Assert.Equal(fleetId, plan.GetProperty("fleet_id").GetInt64());
        Assert.Equal("draft", plan.GetProperty("state_code").GetString());
        Assert.Equal(3, plan.GetProperty("orders").GetArrayLength());

        JsonElement orders = plan.GetProperty("orders");
        Assert.Equal("fly", orders[0].GetProperty("type_code").GetString());
        Assert.Equal("system", orders[0].GetProperty("target_kind").GetString());
        Assert.Equal(12, orders[0].GetProperty("target_system_id").GetInt64());
        Assert.Equal(1, orders[0].GetProperty("position").GetInt32());
        Assert.Equal("wait", orders[1].GetProperty("type_code").GetString());
        Assert.Equal(600, orders[1].GetProperty("duration_seconds").GetInt64());
        Assert.Equal("return", orders[2].GetProperty("type_code").GetString());
        Assert.Equal(3, orders[2].GetProperty("position").GetInt32());

        // A read in a new scope finds the stored rows.
        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        List<FlightPlanEntity> stored = await database.FlightPlans
            .Include(plan => plan.OrderIds)
            .Where(plan => plan.PlayerId == player.Account.UserId)
            .ToListAsync(Token);

        Assert.Single(stored);
        Assert.Equal(fleetId, stored[0].FleetId);
        Assert.Equal(3, stored[0].OrderIds.Count);
    }

    /// <summary>
    /// The plan belongs to the player and to the fleet: the fleet of another player is not its
    /// fleet, so the plan is not written for it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_is_not_written_for_the_fleet_of_another_player()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        using PlanetsTestPlayer stranger = await FoundAsync(spaceportLevel: 1);
        long strangerFleetId = await FleetAsync(stranger);

        // The fleet of the stranger is not the fleet of the player, so no plan is written for it.
        string text = await RefusalAsync(player, Plans, new { fleet_id = strangerFleetId, orders = Array.Empty<object>() });

        Assert.Contains(FleetErrorCodes.FleetUnknown, text);

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        Assert.Empty(await database.FlightPlans.Where(plan => plan.FleetId == strangerFleetId).ToListAsync(Token));
        Assert.Empty(await database.FlightPlans.Where(plan => plan.PlayerId == player.Account.UserId).ToListAsync(Token));
    }

    /// <summary>
    /// A fleet has one plan at most: a second plan for the same fleet is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_has_one_plan_at_most()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = Array.Empty<object>() });
        string text = await RefusalAsync(player, Plans, new { fleet_id = fleetId, orders = Array.Empty<object>() });

        Assert.Contains(FleetErrorCodes.FlightPlanExists, text);
    }

    /// <summary>
    /// The orders of a plan are changed by one command: the whole list is replaced, and the
    /// positions follow the new order.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_orders_of_a_plan_are_replaced_whole()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[]
            {
                new { type_code = "fly", target_kind = "system", target_system_id = 12 },
                new { type_code = "scan", target_kind = "object", target_object_id = 40 },
            },
        });

        JsonElement plan = await CommandAsync(player, Save, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "mine", duration_seconds = 600 } },
        });

        JsonElement orders = plan.GetProperty("orders");
        Assert.Single(orders.EnumerateArray());
        Assert.Equal("mine", orders[0].GetProperty("type_code").GetString());

        // The order of extraction names no target (task 00054): it has the time only.
        Assert.Equal("none", orders[0].GetProperty("target_kind").GetString());
        Assert.Equal(0, orders[0].GetProperty("target_object_id").GetInt64());
        Assert.Equal(600, orders[0].GetProperty("duration_seconds").GetInt64());
        Assert.Equal(1, orders[0].GetProperty("position").GetInt32());

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        Assert.Single(await database.FlightPlanOrders.Where(order => order.FlightPlanId == plan.GetProperty("id").GetInt64()).ToListAsync(Token));
    }

    /// <summary>
    /// The plan is deleted before it is carried out, and the fleet has none again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_is_deleted_before_it_is_carried_out()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = Array.Empty<object>() });
        await CommandAsync(player, Delete, new { fleet_id = fleetId });

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        Assert.Empty(await database.FlightPlans.Where(plan => plan.FleetId == fleetId).ToListAsync(Token));
    }

    /// <summary>
    /// The plan under way is neither changed nor deleted: the fleet is flying and the player waits
    /// for it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_under_way_is_neither_changed_nor_deleted()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        // The plan waits for ten minutes, so it is under way when the command answers (task 00053).
        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = new object[] { new { type_code = "wait", duration_seconds = 600 } } });
        JsonElement plan = await CommandAsync(player, Execute, new { fleet_id = fleetId });

        Assert.Equal("active", plan.GetProperty("state_code").GetString());

        Assert.Contains(FleetErrorCodes.FlightPlanUnderWay, await RefusalAsync(player, Delete, new { fleet_id = fleetId }));
        Assert.Contains(FleetErrorCodes.FlightPlanUnderWay, await RefusalAsync(player, Save, new { fleet_id = fleetId, orders = Array.Empty<object>() }));
    }

    /// <summary>
    /// The plan under way is stopped: the fleet waits where it is, and the plan may be carried out
    /// again or written anew.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_under_way_is_stopped_and_the_fleet_waits()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        await CommandAsync(player, Plans, new { fleet_id = fleetId, orders = new object[] { new { type_code = "wait", duration_seconds = 600 } } });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });

        JsonElement stopped = await CommandAsync(player, Stop, new { fleet_id = fleetId });
        Assert.Equal("paused", stopped.GetProperty("state_code").GetString());

        // A plan that is not under way cannot be stopped again.
        Assert.Contains(FleetErrorCodes.FlightPlanNotUnderWay, await RefusalAsync(player, Stop, new { fleet_id = fleetId }));

        // A stopped plan is changed and deleted like any other.
        await CommandAsync(player, Save, new { fleet_id = fleetId, orders = Array.Empty<object>() });
        await CommandAsync(player, Delete, new { fleet_id = fleetId });
    }

    /// <summary>
    /// An order that names a type that does not exist is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_order_that_names_an_unknown_type_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        string text = await RefusalAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "teleport", target_kind = "none" } },
        });

        Assert.Contains(FleetErrorCodes.FlightOrderTypeUnknown, text);
    }

    /// <summary>
    /// An order that names a system but does not say which one is refused: the order would
    /// point at nothing.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_order_that_names_a_system_without_one_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        string text = await RefusalAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "fly", target_kind = "system", target_system_id = 0 } },
        });

        Assert.Contains(FleetErrorCodes.FlightOrderTargetUnknown, text);
    }

    /// <summary>
    /// An order that names an object but does not say which one is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_order_that_names_an_object_without_one_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        string text = await RefusalAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { new { type_code = "mine", target_kind = "object", target_object_id = 0 } },
        });

        Assert.Contains(FleetErrorCodes.FlightOrderTargetUnknown, text);
    }

    /// <summary>
    /// Twenty commands at once write one plan for a fleet: the fleet has one plan at most, so the
    /// others are refused and the plan is not written twice.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_plans_at_once_write_one_plan_for_a_fleet()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        HttpResponseMessage[] answers = await Task.WhenAll(
            Enumerable.Range(0, ParallelCount).Select(_ => SendAsync(player, Plans, new { fleet_id = fleetId, orders = Array.Empty<object>() })));

        Assert.Equal(1, answers.Count(answer => answer.StatusCode == HttpStatusCode.OK));

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        Assert.Single(await database.FlightPlans.Where(plan => plan.FleetId == fleetId).ToListAsync(Token));
    }

    /// <summary>
    /// A plan holds as many orders as the parameter allows and no more.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plan_holds_no_more_orders_than_the_parameter_allows()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        long fleetId = await FleetAsync(player);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, FleetConfigKeys.FlightPlanMaxOrders, "2");

        string text = await RefusalAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[]
            {
                new { type_code = "fly", target_kind = "none" },
                new { type_code = "fly", target_kind = "none" },
                new { type_code = "fly", target_kind = "none" },
            },
        });

        Assert.Contains(FleetErrorCodes.FlightPlanTooManyOrders, text);
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
    }

    private async Task<PlanetsTestPlayer> FoundAsync(int spaceportLevel)
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, spaceportLevel);

        return player;
    }

    /// <summary>
    /// Writes a fleet of the player, so a plan may be made for it.
    /// </summary>
    /// <param name="player">The owner of the fleet.</param>
    /// <returns>The fleet.</returns>
    private async Task<long> FleetAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([HullCode], Token);
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

        ShipEntity ship = new()
        {
            PlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            ShipDesignId = design.Id,
            Crew = 7,
            UpkeepPerHour = 2.5m,
            StateCode = ShipStates.Ready,
            CreatedAt = now,
            ModuleIds = [new ShipModuleEntity { PartTypeId = partIds[HullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true }],
        };
        database.Ships.Add(ship);
        await database.SaveChangesAsync(Token);

        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот", ship_ids = new[] { ship.Id } });

        return view.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64();
    }

    private static async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await SendAsync(player, route, body);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>
    /// Sends a command and reads the answer as a text, so a refusal carries its code.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="route">The route of the command.</param>
    /// <param name="body">The body of the command.</param>
    /// <returns>The text of the answer.</returns>
    private static async Task<string> RefusalAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await SendAsync(player, route, body);

        return await response.Content.ReadAsStringAsync(Token);
    }

    private static Task<HttpResponseMessage> SendAsync(PlanetsTestPlayer player, string route, object body) =>
        TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));
}

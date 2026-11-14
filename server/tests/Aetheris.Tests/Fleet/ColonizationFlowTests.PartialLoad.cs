using System.Net;
using System.Text.Json;
using Aetheris.Core.Time;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Services;
using Aetheris.Mail.Data;
using Aetheris.Mail.Data.Entities;
using Aetheris.Mail.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the load that never stops a plan (author's rule, 14.11.2026): a load takes what the stock
/// has and what the holds take, the plan goes on to its next order, no module of the ships is needed,
/// and a report tells the player what was not loaded.
/// </summary>
public sealed partial class ColonizationFlowTests
{
    private const string FirstShortResource = "resource_13";
    private const string SecondShortResource = "resource_14";

    /// <summary>
    /// A load that asks more of a resource than the stock has loads what the stock has, and the plan
    /// goes on to the next load, which is carried out whole.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_load_short_of_a_resource_loads_what_the_stock_has_and_the_plan_goes_on()
    {
        await ResetWithMailAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        (int firstId, int secondId) = await StockResourcesAsync(player, 40, 30);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[]
            {
                LoadOrder(new { kind = "resource", id = firstId, amount = 100 }),
                LoadOrder(new { kind = "resource", id = secondId, amount = 30 }),
            },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
        await RunToTheEndAsync(fleetId);

        Assert.Equal(40, await CarriedResourceAsync(shipId, firstId));
        Assert.Equal(30, await CarriedResourceAsync(shipId, secondId));
        Assert.Null(await PlanOfAsync(fleetId));

        JsonElement item = Assert.Single(await LoadShortItemsAsync(player));
        Assert.Equal(firstId, item.GetProperty("id").GetInt32());
        Assert.Equal(100, item.GetProperty("requested").GetInt64());
        Assert.Equal(40, item.GetProperty("loaded").GetInt64());
        Assert.Equal(CargoShortfallReasons.Stock, item.GetProperty("reason_code").GetString());
        Assert.False(string.IsNullOrEmpty(item.GetProperty("name_key").GetString()));
    }

    /// <summary>
    /// A load that asks more modules than the stock has loads the modules the stock has.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_load_short_of_modules_loads_the_modules_the_stock_has()
    {
        await ResetWithMailAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        int moduleId = await StockModuleAsync(player);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { LoadOrder(new { kind = "part", id = moduleId, mark_level = 1, has_race_bonus = false, amount = 3 }) },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
        await RunToTheEndAsync(fleetId);

        Assert.Equal(1, await CarriedPartAsync(shipId, moduleId));
        Assert.Null(await PlanOfAsync(fleetId));

        JsonElement item = Assert.Single(await LoadShortItemsAsync(player));
        Assert.Equal("part", item.GetProperty("kind").GetString());
        Assert.Equal(3, item.GetProperty("requested").GetInt64());
        Assert.Equal(1, item.GetProperty("loaded").GetInt64());
    }

    /// <summary>
    /// A load of which the stock has nothing at all loads nothing, does not stop the plan and reports
    /// that nothing was loaded.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_load_of_which_the_stock_has_nothing_completes_and_reports_it()
    {
        await ResetWithMailAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        (int firstId, _) = await StockResourcesAsync(player, 0, 0);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { LoadOrder(new { kind = "resource", id = firstId, amount = 1000 }) },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
        await RunToTheEndAsync(fleetId);

        Assert.Equal(0, await CarriedResourceAsync(shipId, firstId));
        Assert.Null(await PlanOfAsync(fleetId));

        JsonElement item = Assert.Single(await LoadShortItemsAsync(player));
        Assert.Equal(1000, item.GetProperty("requested").GetInt64());
        Assert.Equal(0, item.GetProperty("loaded").GetInt64());
        Assert.Equal(CargoShortfallReasons.Stock, item.GetProperty("reason_code").GetString());
    }

    /// <summary>
    /// A ship without any module of cargo loads into the hold of its hull: no special module is needed.
    /// A load heavier than the room loads as much as the room takes and reports the rest as a want of room.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_load_needs_no_module_and_stops_at_the_room_of_the_holds()
    {
        await ResetWithMailAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive]);
        long free = await FreeRoomAsync(fleetId);

        Assert.True(free > 0, "The hull of the test ship has no hold of its own.");

        (int firstId, _) = await StockResourcesAsync(player, free * 2, 0);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { LoadOrder(new { kind = "resource", id = firstId, amount = free * 2 }) },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
        await RunToTheEndAsync(fleetId);

        Assert.Equal(free, await CarriedResourceAsync(shipId, firstId));
        Assert.Null(await PlanOfAsync(fleetId));

        JsonElement item = Assert.Single(await LoadShortItemsAsync(player));
        Assert.Equal(free * 2, item.GetProperty("requested").GetInt64());
        Assert.Equal(free, item.GetProperty("loaded").GetInt64());
        Assert.Equal(CargoShortfallReasons.Room, item.GetProperty("reason_code").GetString());
    }

    /// <summary>
    /// The command of the hold loads what the stock has as the order does and reports the rest; a command
    /// that can load nothing at all is refused with the reason.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_command_of_the_hold_loads_what_the_stock_has_and_refuses_when_there_is_nothing()
    {
        await ResetWithMailAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive, ContainerCode]);
        (int firstId, int secondId) = await StockResourcesAsync(player, 25, 0);

        await CommandAsync(player, CargoLoad, new { fleet_id = fleetId, items = new[] { new { kind = "resource", id = firstId, amount = 60 } } });

        Assert.Equal(25, await CarriedResourceAsync(shipId, firstId));
        Assert.Equal(25, Assert.Single(await LoadShortItemsAsync(player)).GetProperty("loaded").GetInt64());

        using HttpResponseMessage refused = await TestAccounts.SendCommandAsync(
            player.Client,
            CargoLoad,
            new { fleet_id = fleetId, items = new[] { new { kind = "resource", id = secondId, amount = 5 } } },
            Guid.NewGuid().ToString("N"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(FleetErrorCodes.CargoShort, await TestAccounts.ReadErrorAsync(refused));
    }

    /// <summary>
    /// Resets the data and prepares the mail, so the reports of the loads have their category.
    /// </summary>
    private async Task ResetWithMailAsync()
    {
        await ResetAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MailBootstrapService>().EnsureAsync(Token);
    }

    private static object LoadOrder(params object[] items) =>
        new { type_code = "load", target_kind = "none", parameters = JsonSerializer.Serialize(new { items }) };

    /// <summary>
    /// Writes the stock of two resources the home planet does not produce, so the amounts stay as written.
    /// </summary>
    private async Task<(int FirstId, int SecondId)> StockResourcesAsync(PlanetsTestPlayer player, long first, long second)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        int firstId = await PlanetsTestData.ResourceIdAsync(services, FirstShortResource);
        int secondId = await PlanetsTestData.ResourceIdAsync(services, SecondShortResource);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [firstId] = first, [secondId] = second }, now);

        return (firstId, secondId);
    }

    private async Task<long> CarriedResourceAsync(long shipId, int resourceId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipCargos.AsNoTracking()
            .Where(cargo => cargo.ShipId == shipId && cargo.ResourceId == resourceId)
            .SumAsync(cargo => cargo.Amount, Token);
    }

    private async Task<long> CarriedPartAsync(long shipId, int partTypeId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipPartCargos.AsNoTracking()
            .Where(cargo => cargo.ShipId == shipId && cargo.PartTypeId == partTypeId)
            .SumAsync(cargo => cargo.Quantity, Token);
    }

    private async Task<long> FreeRoomAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyList<ShipHold> holds = await scope.ServiceProvider.GetRequiredService<FleetHoldService>().ReadAsync(fleetId, Token);

        return holds.Sum(hold => hold.Free);
    }

    private async Task<string?> PlanOfAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<FleetDbContext>().FlightPlans.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Select(entry => entry.StateCode + ":" + (entry.PauseCode ?? string.Empty))
            .SingleOrDefaultAsync(Token);
    }

    /// <summary>
    /// Reads the items of the reports of short loads written to the player.
    /// </summary>
    private async Task<List<JsonElement>> LoadShortItemsAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        List<MailMessageEntity> letters = await scope.ServiceProvider.GetRequiredService<MailDbContext>().MailMessages.AsNoTracking()
            .Where(entry => entry.ReceiverPlayerId == player.Account.UserId && entry.SystemKindCode == FleetReportService.LoadShortReportKindCode)
            .ToListAsync(Token);

        Assert.All(letters, letter => Assert.Equal(FleetReportService.LoadShortReportTextKey, letter.TextKey));

        return [.. letters.SelectMany(letter => JsonDocument.Parse(letter.TextParameters!).RootElement.Clone().GetProperty("items").EnumerateArray())];
    }
}

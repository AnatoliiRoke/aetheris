using System.Net;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Planets.Services;
using Aetheris.Processes;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Tests of the read of every order of every building of a body that runs a process, which the
/// queue of the processes on the screen of the body shows (task 00020, section 7.5).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProcessActiveOrdersTests
{
    private const string Fuel = "resource_group_3";

    private const string Deuterium = "resource_9";

    private const string Plasma = "resource_10";

    private const string ConversionTechnology = "tech_18";

    private const string Hull = "part_1";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessActiveOrdersTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ProcessActiveOrdersTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The orders of the converter and of the recycling building stand in one list in the order
    /// of the queues, the places of the two queues add up, and every order is the very row of the
    /// database.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_orders_of_several_buildings_stand_in_one_list_and_match_the_database()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        (long converterId, long recyclingId, ProcessRecipeInfo conversion, ProcessRecipeInfo recycling) = await PrepareTwoBuildingsAsync(player, services);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        string playerId = player.Account.UserId;
        int deuteriumId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int plasmaId = await ProcessesTestData.ResourceIdAsync(services, Plasma);
        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);

        await processes.EnqueueAsync(playerId, player.PlanetId, converterId, conversion.Id, new ProcessOrderRequest(10, deuteriumId, plasmaId), Token);
        await processes.EnqueueAsync(playerId, player.PlanetId, converterId, conversion.Id, new ProcessOrderRequest(5, deuteriumId, plasmaId), Token);
        await processes.EnqueueAsync(playerId, player.PlanetId, recyclingId, recycling.Id, new ProcessOrderRequest(2, PartTypeId: hull.PartTypeId), Token);

        HttpResponseMessage response = await player.Client.GetAsync($"/api/v1/processes/orders?planet_id={player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement root = body.RootElement;
        JsonElement[] orders = [.. root.GetProperty("orders").EnumerateArray()];

        Assert.Equal(player.PlanetId, root.GetProperty("planet_id").GetInt64());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("game_now").GetString()));
        Assert.Equal(3, orders.Length);
        Assert.Equal(3, root.GetProperty("queue_used_slots").GetInt32());

        // Every building has its own queue, so the places of the body are their sum.
        int baseSlots = ProcessesSettings.From(services.GetRequiredService<IGameConfig>()).QueueBaseSlots;
        Assert.Equal(baseSlots * 2, root.GetProperty("queue_slots").GetInt32());

        // The orders follow the order of the queues: the one under way of every building first.
        Assert.Equal([1, 1, 2], orders.Select(order => order.GetProperty("position").GetInt32()));
        Assert.Equal(
            new[] { converterId, recyclingId }.Order(),
            orders.Take(2).Select(order => order.GetProperty("planet_building_id").GetInt64()).Order());

        // The orders are the rows of the database and the orders of the buildings one by one.
        List<ProcessOrderEntity> rows = await services.GetRequiredService<ProcessesDbContext>().ProcessOrders.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId)
            .ToListAsync(Token);

        Assert.Equal(
            rows.OrderBy(row => row.Id).Select(row => (row.Id, row.PlanetBuildingId, row.ProcessRecipeId, row.Quantity, row.ProducedQuantity, row.Position, row.UnitSeconds)),
            orders.OrderBy(order => order.GetProperty("order_id").GetInt64()).Select(order => (
                order.GetProperty("order_id").GetInt64(),
                order.GetProperty("planet_building_id").GetInt64(),
                order.GetProperty("recipe_id").GetInt32(),
                order.GetProperty("quantity").GetInt64(),
                order.GetProperty("produced_quantity").GetInt64(),
                order.GetProperty("position").GetInt32(),
                order.GetProperty("unit_seconds").GetInt64())));

        ProcessBuildingDto converter = await processes.GetBuildingAsync(playerId, player.PlanetId, converterId, Token);
        ProcessBuildingDto recyclingBuilding = await processes.GetBuildingAsync(playerId, player.PlanetId, recyclingId, Token);
        Assert.Equal(
            converter.Queue.Concat(recyclingBuilding.Queue).Select(order => order.OrderId).Order(),
            orders.Select(order => order.GetProperty("order_id").GetInt64()).Order());

        // The names are the keys of the recipe, of its kind and of the building, never texts of the code.
        JsonElement onRecycling = orders.Single(order => order.GetProperty("planet_building_id").GetInt64() == recyclingId);
        Assert.Equal(recycling.NameKey, onRecycling.GetProperty("name_key").GetString());
        Assert.False(string.IsNullOrEmpty(onRecycling.GetProperty("process_type_name_key").GetString()));
        Assert.False(string.IsNullOrEmpty(onRecycling.GetProperty("building_name_key").GetString()));
        Assert.NotEqual(JsonValueKind.Null, onRecycling.GetProperty("next_unit_at").ValueKind);
    }

    /// <summary>
    /// A cancelled order leaves the list at once and frees its place, and a body with no order
    /// gives an empty list with the places it has.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_cancelled_order_leaves_the_list_and_frees_its_place()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long converterId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);
        ProcessRecipeInfo conversion = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        int deuteriumId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int plasmaId = await ProcessesTestData.ResourceIdAsync(services, Plasma);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        string playerId = player.Account.UserId;

        ProcessActiveOrdersDto empty = await processes.GetActiveOrdersAsync(playerId, player.PlanetId, Token);
        Assert.Empty(empty.Orders);
        Assert.Equal(0, empty.QueueUsedSlots);
        Assert.True(empty.QueueSlots > 0);

        ProcessBuildingDto queued = await processes.EnqueueAsync(playerId, player.PlanetId, converterId, conversion.Id, new ProcessOrderRequest(10, deuteriumId, plasmaId), Token);
        ProcessActiveOrdersDto one = await processes.GetActiveOrdersAsync(playerId, player.PlanetId, Token);

        Assert.Equal(queued.Queue[0].OrderId, Assert.Single(one.Orders).OrderId);
        Assert.Equal(1, one.QueueUsedSlots);
        Assert.Equal(empty.QueueSlots, one.QueueSlots);

        await processes.CancelAsync(playerId, player.PlanetId, converterId, queued.Queue[0].OrderId, Token);
        ProcessActiveOrdersDto after = await processes.GetActiveOrdersAsync(playerId, player.PlanetId, Token);

        Assert.Empty(after.Orders);
        Assert.Equal(0, after.QueueUsedSlots);
    }

    /// <summary>
    /// A stranger and a client without a session see no order of the body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_stranger_and_a_guest_read_no_order()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using HttpClient guest = _factory.CreateBrowserClient();
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long converterId = await ProcessesTestData.BuildAsync(owner, services, BuildingClasses.Converter);
        await ProcessesTestData.GiveEveryResourceAsync(owner, services);
        await ProcessesTestData.LearnAsync(owner, services, ConversionTechnology, 1);
        ProcessRecipeInfo conversion = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        int deuteriumId = await ProcessesTestData.ResourceIdAsync(services, Deuterium);
        int plasmaId = await ProcessesTestData.ResourceIdAsync(services, Plasma);

        await services.GetRequiredService<IProcessesModule>()
            .EnqueueAsync(owner.Account.UserId, owner.PlanetId, converterId, conversion.Id, new ProcessOrderRequest(10, deuteriumId, plasmaId), Token);

        string url = $"/api/v1/processes/orders?planet_id={owner.PlanetId}";
        HttpResponseMessage own = await owner.Client.GetAsync(url, Token);
        HttpResponseMessage foreign = await stranger.Client.GetAsync(url, Token);
        HttpResponseMessage anonymous = await guest.GetAsync(url, Token);

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        // The body of another player is refused as the other reads of the module refuse it.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        string foreignBody = await foreign.Content.ReadAsStringAsync(Token);
        Assert.Contains(ProcessErrorCodes.PlanetUnknown, foreignBody, StringComparison.Ordinal);
        Assert.DoesNotContain("order_id", foreignBody, StringComparison.Ordinal);
        Assert.DoesNotContain("name_key", foreignBody, StringComparison.Ordinal);
    }

    private static async Task<(long ConverterId, long RecyclingId, ProcessRecipeInfo Conversion, ProcessRecipeInfo Recycling)> PrepareTwoBuildingsAsync(
        PlanetsTestPlayer player,
        IServiceProvider services)
    {
        long converterId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Converter);
        long recyclingId = await ProcessesTestData.BuildAsync(player, services, BuildingClasses.Recycling);
        await ProcessesTestData.GiveEveryResourceAsync(player, services);
        await ProcessesTestData.LearnAsync(player, services, ConversionTechnology, 1);

        PartFacts hull = await ProcessesTestData.PartAsync(services, Hull);
        await ProcessesTestData.PutPartsAsync(player, services, hull.PartTypeId, PartCondition.Full, 4);

        ProcessRecipeInfo conversion = await ProcessesTestData.RecipeOfGroupAsync(services, ProcessHandlerCodes.MatterConversion, Fuel);
        ProcessRecipeInfo recycling = await ProcessesTestData.RecipeOfAsync(services, ProcessHandlerCodes.Recycling);

        return (converterId, recyclingId, conversion, recycling);
    }
}

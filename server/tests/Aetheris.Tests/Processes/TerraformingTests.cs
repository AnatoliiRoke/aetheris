using System.Net;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Processes;
using Aetheris.Processes.Api;
using Aetheris.Processes.Configuration;
using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Processes;

/// <summary>
/// Tests of the terraforming started by a ship (task 00014, section 9): a one-shot device of
/// the storage of a colony makes the deposits of a free body rich, once, and the body stays
/// free for whoever colonizes it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TerraformingTests
{
    private const string TerraformingDevice = "part_34";

    private const string Iron = "resource_1";

    private const string Copper = "resource_3";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerraformingTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public TerraformingTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A device of the storage of a colony makes the deposits of a free body rich in the range
    /// of the data without lowering a richer deposit; the device is spent at the start and
    /// never comes back, the body is marked and keeps its type, its size and its free state.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_device_makes_a_free_body_rich_and_is_spent()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        int poor = await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);
        int rich = await PlanetsTestData.SetDepositAsync(services, targetId, Copper, 1.85m);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 2);

        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        PlanetEntity before = await world.Planets.AsNoTracking().Include(entry => entry.CelestialObject).SingleAsync(entry => entry.Id == targetId, Token);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // The colony offers its devices and the free body before the start.
        await WithMapOpenAsync(services, async () =>
        {
            TerraformingDto offered = await processes.GetTerraformingAsync(player.Account.UserId, player.PlanetId, Token);
            TerraformingDeviceDto stored = Assert.Single(offered.Devices);
            Assert.Equal(device.PartTypeId, stored.PartTypeId);
            Assert.Equal(2, stored.Quantity);
            Assert.False(Assert.Single(offered.Targets, target => target.PlanetId == targetId).IsTerraformed);
        });

        TerraformingResultDto result = await processes.TerraformAsync(player.Account.UserId, player.PlanetId, targetId, device.PartTypeId, Token);

        Dictionary<int, decimal> deposits = await world.Deposits.AsNoTracking()
            .Where(entry => entry.PlanetId == targetId)
            .ToDictionaryAsync(entry => entry.ResourceId, entry => entry.Coefficient, Token);

        // The poor deposit becomes rich in the range, the richer one keeps its richness.
        Assert.Equal(device.TerraformingMultiplier, result.Multiplier);
        Assert.True(deposits[poor] > 1.4m && deposits[poor] <= 1.9m);
        Assert.Equal(1.85m, deposits[rich]);
        Assert.NotEmpty(await world.DepositChanges.AsNoTracking().Where(change => change.PlanetId == targetId).ToListAsync(Token));

        // One device is spent at the start: nothing is in a queue, so nothing gives it back.
        Assert.Equal(1, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
        Assert.Equal(1, Assert.Single(result.Terraforming.Devices).Quantity);
        Assert.Empty(await services.GetRequiredService<Aetheris.Processes.Data.ProcessesDbContext>().ProcessOrders.AsNoTracking().ToListAsync(Token));

        // The body is marked, stays free and keeps its type and its size.
        PlanetEntity after = await world.Planets.AsNoTracking().Include(entry => entry.CelestialObject).SingleAsync(entry => entry.Id == targetId, Token);
        Assert.True(after.IsTerraformed);
        Assert.Null(after.OwnerPlayerId);
        Assert.Equal(before.CelestialObject!.ObjectTypeId, after.CelestialObject!.ObjectTypeId);
        Assert.Equal(before.SizeClassId, after.SizeClassId);
        Assert.Equal(before.Size, after.Size);
    }

    /// <summary>
    /// A colony is no target: the device stays in the storage and the deposits stay as they
    /// were.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_colony_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer neighbour = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 1);
        int poor = await PlanetsTestData.SetDepositAsync(services, neighbour.PlanetId, Iron, 1.0m);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // Another player's colony and the own colony are both colonies.
        Assert.Equal(ProcessErrorCodes.TargetColonized, await RefusedAsync(processes, player, neighbour.PlanetId, device.PartTypeId));
        Assert.Equal(ProcessErrorCodes.TargetColonized, await RefusedAsync(processes, player, player.PlanetId, device.PartTypeId));

        Assert.Equal(1, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        Assert.Equal(1.0m, (await world.Deposits.AsNoTracking().SingleAsync(entry => entry.PlanetId == neighbour.PlanetId && entry.ResourceId == poor, Token)).Coefficient);
        Assert.False((await world.Planets.AsNoTracking().SingleAsync(entry => entry.Id == neighbour.PlanetId, Token)).IsTerraformed);
    }

    /// <summary>
    /// A body is terraformed once: a second device is refused and stays in the storage.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_second_device_on_one_body_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 2);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        int poor = await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);
        TerraformingResultDto first = await processes.TerraformAsync(player.Account.UserId, player.PlanetId, targetId, device.PartTypeId, Token);
        await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);

        Assert.Equal(ProcessErrorCodes.TargetTerraformed, await RefusedAsync(processes, player, targetId, device.PartTypeId));

        // The second device stays, and the deposits are not raised again.
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        Assert.Equal(1, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
        Assert.Equal(first.ChangedDeposits, await world.DepositChanges.AsNoTracking().CountAsync(change => change.PlanetId == targetId, Token));
        Assert.Equal(1.0m, (await world.Deposits.AsNoTracking().SingleAsync(entry => entry.PlanetId == targetId && entry.ResourceId == poor, Token)).Coefficient);
    }

    /// <summary>
    /// The terraformed body stays free: it is offered to another player as a body to colonize,
    /// and the one who terraformed it has no claim on it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_terraformed_body_stays_free_for_another_player()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer other = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        int sectorId = (await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(targetId, Token))!.SectorId;
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 1);
        await ProcessesTestData.PutPartsAsync(other, services, device.PartTypeId, PartCondition.Full, 1);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        await processes.TerraformAsync(player.Account.UserId, player.PlanetId, targetId, device.PartTypeId, Token);

        // The other player finds the body among the free bodies to colonize, marked as terraformed.
        await WithMapOpenAsync(services, async () =>
        {
            IReadOnlyList<FreePlanetDto> free = await services.GetRequiredService<IWorldModule>().GetFreePlanetsAsync(other.Account.UserId, sectorId, Token);
            Assert.True(Assert.Single(free, body => body.PlanetId == targetId).IsTerraformed);
        });

        PlanetFacts facts = (await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(targetId, Token))!;
        Assert.Null(facts.OwnerPlayerId);
        Assert.True(facts.IsTerraformed);

        // The mark belongs to the body and not to the player: the other player cannot terraform
        // it again, and nobody owns it.
        GameException again = await Assert.ThrowsAsync<GameException>(() =>
            processes.TerraformAsync(other.Account.UserId, other.PlanetId, targetId, device.PartTypeId, Token));
        Assert.Equal(ProcessErrorCodes.TargetTerraformed, again.Code);
        Assert.Equal(1, await ProcessesTestData.PartsAsync(other, services, device.PartTypeId, PartCondition.Full));
    }

    /// <summary>
    /// The device of another player is never spent: the colony that holds it is checked from
    /// the session, by the module and by the pipeline of the command; a ship that is not the
    /// player's own does not turn a device on; a part that is no device is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Another_owner_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, owner);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        PartFacts hull = await ProcessesTestData.PartAsync(services, "part_1");
        await ProcessesTestData.PutPartsAsync(owner, services, device.PartTypeId, PartCondition.Full, 1);
        await ProcessesTestData.PutPartsAsync(stranger, services, device.PartTypeId, PartCondition.Full, 1);
        await ProcessesTestData.PutPartsAsync(stranger, services, hull.PartTypeId, PartCondition.Full, 1);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // The colony of another player.
        GameException module = await Assert.ThrowsAsync<GameException>(() =>
            processes.TerraformAsync(stranger.Account.UserId, owner.PlanetId, targetId, device.PartTypeId, Token));
        Assert.Equal(ProcessErrorCodes.PlanetUnknown, module.Code);
        GameException reading = await Assert.ThrowsAsync<GameException>(() =>
            processes.GetTerraformingAsync(stranger.Account.UserId, owner.PlanetId, Token));
        Assert.Equal(ProcessErrorCodes.PlanetUnknown, reading.Code);

        using HttpResponseMessage foreign = await TestAccounts.SendCommandAsync(
            stranger.Client,
            TerraformCommand.RoutePath,
            new Dictionary<string, object?> { ["planet_id"] = owner.PlanetId, ["target_planet_id"] = targetId, ["part_type_id"] = device.PartTypeId },
            "stranger-terraform");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(GameErrorCodes.NotYourObject, await TestAccounts.ReadErrorAsync(foreign));
        Assert.Equal(1, await ProcessesTestData.PartsAsync(owner, services, device.PartTypeId, PartCondition.Full));

        // Only the own ship turns a device on: the stranger has none by the body.
        ShipsOnOrbitStub ships = services.GetRequiredService<ShipsOnOrbitStub>();

        try
        {
            ships.Place(stranger.Account.UserId, targetId, null);
            Assert.Equal(ProcessErrorCodes.NoShipOnOrbit, await RefusedAsync(processes, stranger, targetId, device.PartTypeId));
        }
        finally
        {
            ships.Place(stranger.Account.UserId, targetId, new ShipOnOrbit([]));
        }

        // A part that is no device turns nothing on.
        Assert.Equal(ProcessErrorCodes.DeviceMissing, await RefusedAsync(processes, stranger, targetId, hull.PartTypeId));
        Assert.Equal(1, await ProcessesTestData.PartsAsync(stranger, services, device.PartTypeId, PartCondition.Full));
        Assert.Equal(1, await ProcessesTestData.PartsAsync(stranger, services, hull.PartTypeId, PartCondition.Full));

        // The owner reaches the same command with the own colony.
        using HttpResponseMessage own = await TestAccounts.SendCommandAsync(
            owner.Client,
            TerraformCommand.RoutePath,
            new Dictionary<string, object?> { ["planet_id"] = owner.PlanetId, ["target_planet_id"] = targetId, ["part_type_id"] = device.PartTypeId },
            "owner-terraform");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await own.Content.ReadAsStringAsync(Token));
        Assert.Equal(targetId, body.RootElement.GetProperty("target_planet_id").GetInt64());
        Assert.Equal(0, await ProcessesTestData.PartsAsync(owner, services, device.PartTypeId, PartCondition.Full));
    }

    /// <summary>
    /// The multiplier of a device must lie in the range of the configuration; a device out of
    /// it is refused and stays.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_device_out_of_the_range_of_the_data_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 1);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        string highest = ProcessesConfigKeys.Definitions.Single(definition => definition.Key == ProcessesConfigKeys.TerraformingMultiplierMax).DefaultValue;

        try
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMax, "1.5");
            Assert.Equal(ProcessErrorCodes.ParamMissing, await RefusedAsync(processes, player, targetId, device.PartTypeId));
            Assert.Equal(1, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
            Assert.False((await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(targetId, Token))!.IsTerraformed);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMax, highest);
        }
    }

    /// <summary>
    /// Twenty starts on one body at once, by two players with devices of their own, give
    /// exactly one terraforming and spend exactly one device.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_starts_on_one_body_give_one_success()
    {
        const int parallelCount = 20;

        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer first = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer second = await PlanetsTestData.FoundAsync(_factory);
        PlanetsTestPlayer[] players = [first, second];
        long targetId;
        int deviceId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            IServiceProvider services = scope.ServiceProvider;
            targetId = await FreeBodyAsync(services, first);
            deviceId = (await ProcessesTestData.PartAsync(services, TerraformingDevice)).PartTypeId;

            foreach (PlanetsTestPlayer player in players)
            {
                await ProcessesTestData.PutPartsAsync(player, services, deviceId, PartCondition.Full, parallelCount / players.Length);
            }
        }

        bool[] outcomes = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => TryTerraformAsync(players[index % players.Length], targetId, deviceId)));

        Assert.Equal(1, outcomes.Count(outcome => outcome));

        using IServiceScope check = _factory.Services.CreateScope();
        long left = 0;

        foreach (PlanetsTestPlayer player in players)
        {
            left += await ProcessesTestData.PartsAsync(player, check.ServiceProvider, deviceId, PartCondition.Full);
        }

        Assert.Equal(parallelCount - 1, left);
        Assert.True((await check.ServiceProvider.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(targetId, Token))!.IsTerraformed);
    }

    /// <summary>
    /// The terraforming is no process of the queue of a building: no kind, no recipe and no
    /// handler of the processes names it, and the queue of the factory offers nothing that takes
    /// a device.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_terraforming_is_not_in_the_queue_of_a_building()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Dictionary<int, PartFacts> devices = (await services.GetRequiredService<PartStockService>().GetPartFactsAsync(Token)).Values
            .Where(part => part.TerraformingMultiplier is not null)
            .ToDictionary(part => part.PartTypeId);
        Assert.NotEmpty(devices);

        ProcessCatalog catalog = await ProcessesTestData.CatalogAsync(services);
        List<string> handlers = [.. services.GetServices<IProcessHandler>().Select(handler => handler.HandlerCode)];

        // Every kind of the starting data has a handler of the queue, and no recipe takes a device.
        Assert.All(catalog.Types, type => Assert.Contains(type.HandlerCode, handlers));
        Assert.All(ProcessHandlerCodes.All, handlerCode => Assert.Contains(handlerCode, handlers));
        Assert.DoesNotContain(handlers, handlerCode => handlerCode.Contains("terraform", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.Recipes.SelectMany(recipe => recipe.IngredientIds), ingredient => ingredient.PartTypeId is { } partTypeId && devices.ContainsKey(partTypeId));

        // Every building of the body that runs a process offers no recipe with a device.
        long factoryId = await ProcessesTestData.BuildAsync(player, services, Aetheris.Planets.Services.BuildingClasses.Production);
        await ProcessesTestData.PutPartsAsync(player, services, devices.Keys.First(), PartCondition.Full, 1);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        foreach (ProcessBuildingSummaryDto summary in await processes.GetBuildingsAsync(player.Account.UserId, player.PlanetId, Token))
        {
            ProcessBuildingDto building = await processes.GetBuildingAsync(player.Account.UserId, player.PlanetId, summary.PlanetBuildingId, Token);
            Assert.DoesNotContain(building.Recipes.SelectMany(recipe => recipe.Ingredients), ingredient => ingredient.PartTypeId is { } partTypeId && devices.ContainsKey(partTypeId));
        }

        Assert.DoesNotContain(await processes.GetBuildingsAsync(player.Account.UserId, player.PlanetId, Token), summary => summary.PlanetBuildingId == factoryId);
    }

    private static async Task<string> RefusedAsync(IProcessesModule processes, PlanetsTestPlayer player, long targetId, int partTypeId)
    {
        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            processes.TerraformAsync(player.Account.UserId, player.PlanetId, targetId, partTypeId, Token));

        return refused.Code;
    }

    /// <summary>
    /// The first free body of the sector of the colony that may be colonized, the one the list
    /// of the free bodies shows first.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="player">The player and the colony.</param>
    /// <returns>The body.</returns>
    private static async Task<long> FreeBodyAsync(IServiceProvider services, PlanetsTestPlayer player)
    {
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        int sectorId = await world.Planets.AsNoTracking().Where(entry => entry.Id == player.PlanetId).Select(entry => entry.SectorId).SingleAsync(Token);

        return await world.Planets.AsNoTracking()
            .Where(planet => planet.SectorId == sectorId && planet.OwnerPlayerId == null)
            .Join(world.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet, entry })
            .Join(world.ObjectTypes, pair => pair.entry.ObjectTypeId, type => type.Id, (pair, type) => new { pair.planet, type })
            .Where(row => row.type.IsColonizable)
            .OrderBy(row => row.planet.Id)
            .Select(row => row.planet.Id)
            .FirstAsync(Token);
    }

    private static async Task WithMapOpenAsync(IServiceProvider services, Func<Task> check)
    {
        try
        {
            await AetherisTestFactory.SetParameterAsync(services, WorldConfigKeys.RevealAll, "true");
            await check();
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, WorldConfigKeys.RevealAll, "false");
        }
    }

    private async Task<bool> TryTerraformAsync(PlanetsTestPlayer player, long targetId, int deviceId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<IProcessesModule>()
                .TerraformAsync(player.Account.UserId, player.PlanetId, targetId, deviceId, Token);

            return true;
        }
        catch (GameException exception) when (exception.Code == ProcessErrorCodes.TargetTerraformed)
        {
            return false;
        }
    }
}

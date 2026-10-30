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
/// Tests of the terraforming started by a ship (task 00014, sections 9 and 11): a one-shot
/// device from the hold of a ship that stands by a free body that may be settled makes every
/// deposit of the body rich by a roll of its own, once, and the body stays free for whoever
/// colonizes it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TerraformingTests
{
    private const string TerraformingDevice = "part_34";

    private const string Iron = "resource_1";

    private const string Copper = "resource_3";

    private const string Deuterium = "resource_9";

    private const long ShipId = 1;

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

    private ShipsOnOrbitStub Ships => _factory.Services.GetRequiredService<ShipsOnOrbitStub>();

    private ScriptedRandomSource Rolls => _factory.Services.GetRequiredService<ScriptedRandomSource>();

    /// <summary>
    /// A device from the hold of a ship makes every poor deposit of a free body rich, each by a
    /// roll of its own in the range of the data, without lowering a richer deposit; the device
    /// leaves the hold at the start and never comes back, the storage of the colony is not
    /// touched, the body is marked and keeps its type, its size and its free state.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_device_from_the_hold_makes_every_deposit_rich_by_its_own_roll_and_is_spent()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        await world.Deposits.Where(entry => entry.PlanetId == targetId).ExecuteDeleteAsync(Token);
        int firstPoor = await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);
        int secondPoor = await PlanetsTestData.SetDepositAsync(services, targetId, Copper, 1.0m);
        int rich = await PlanetsTestData.SetDepositAsync(services, targetId, Deuterium, 1.85m);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);

        // The storage of the colony holds devices too, and none of them may be taken.
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 2);
        PlaceShip(services, player, targetId, ShipId, device.PartTypeId, 2);

        PlanetEntity before = await world.Planets.AsNoTracking().Include(entry => entry.CelestialObject).SingleAsync(entry => entry.Id == targetId, Token);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // The player is offered the free body a ship stands by, with the device in its hold; no
        // colony takes part, and the map needs no opening, because the ship sees its body.
        TerraformingDto offered = await processes.GetTerraformingAsync(player.Account.UserId, Token);
        TerraformingTargetDto target = Assert.Single(offered.Targets);
        Assert.Equal(targetId, target.PlanetId);
        TerraformingShipDto ship = Assert.Single(target.ShipIds);
        Assert.Equal(ShipId, ship.ShipId);
        TerraformingDeviceDto held = Assert.Single(ship.Devices);
        Assert.Equal(device.PartTypeId, held.PartTypeId);
        Assert.Equal(2, held.Quantity);
        Assert.False(target.IsTerraformed);

        // The three deposits are rolled in the order of their resources: the lowest, the middle
        // and a quarter of the way up the range of the configuration.
        List<int> ordered = [.. new[] { firstPoor, secondPoor, rich }.Order()];
        double[] rolls = [0.0, 0.5, 0.25];
        Rolls.Script(rolls);

        TerraformingResultDto result = await processes.TerraformAsync(player.Account.UserId, targetId, ShipId, device.PartTypeId, Token);

        Dictionary<int, decimal> deposits = await world.Deposits.AsNoTracking()
            .Where(entry => entry.PlanetId == targetId)
            .ToDictionaryAsync(entry => entry.ResourceId, entry => entry.Coefficient, Token);

        decimal low = 1.4m;
        decimal span = 0.5m;
        Dictionary<int, decimal> rolled = ordered.Select((resourceId, index) => (resourceId, multiplier: low + ((decimal)rolls[index] * span)))
            .ToDictionary(entry => entry.resourceId, entry => entry.multiplier);

        // Each poor deposit got the multiplier of its own roll, and the two differ.
        Assert.Equal(rolled[firstPoor], deposits[firstPoor]);
        Assert.Equal(rolled[secondPoor], deposits[secondPoor]);
        Assert.NotEqual(deposits[firstPoor], deposits[secondPoor]);
        Assert.All(new[] { firstPoor, secondPoor }, resourceId => Assert.InRange(deposits[resourceId], 1.4m, 1.9m));

        // The rich deposit was below its roll nowhere: it keeps its richness.
        Assert.Equal(1.85m, deposits[rich]);
        Assert.Equal(2, result.ChangedDeposits);
        Assert.Equal(2, result.DepositIds.Count);
        Assert.All(result.DepositIds, changed => Assert.Equal(deposits[changed.ResourceTypeId], changed.NewCoefficient));
        Assert.Equal(2, await world.DepositChanges.AsNoTracking().CountAsync(change => change.PlanetId == targetId, Token));

        // One device left the hold at the start; the storage of the colony and the queues are
        // untouched, so nothing gives the device back.
        Assert.Equal(1, Ships.CargoOf(player.Account.UserId, targetId, ShipId, device.PartTypeId));
        Assert.Equal(2, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
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
    /// The range of the multiplier comes from the two keys of the configuration, so a roll of
    /// zero gives the lowest multiplier and a roll near one gives a multiplier near the highest.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_rolls_follow_the_range_of_the_configuration()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        await world.Deposits.Where(entry => entry.PlanetId == targetId).ExecuteDeleteAsync(Token);
        int first = await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);
        int second = await PlanetsTestData.SetDepositAsync(services, targetId, Copper, 1.0m);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        PlaceShip(services, player, targetId, ShipId, device.PartTypeId, 1);
        string lowest = ProcessesConfigKeys.Definitions.Single(definition => definition.Key == ProcessesConfigKeys.TerraformingMultiplierMin).DefaultValue;
        string highest = ProcessesConfigKeys.Definitions.Single(definition => definition.Key == ProcessesConfigKeys.TerraformingMultiplierMax).DefaultValue;

        try
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMin, "2");
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMax, "3");
            Rolls.Script(0.0, 0.5);

            await services.GetRequiredService<IProcessesModule>()
                .TerraformAsync(player.Account.UserId, targetId, ShipId, device.PartTypeId, Token);

            Dictionary<int, decimal> deposits = await world.Deposits.AsNoTracking()
                .Where(entry => entry.PlanetId == targetId)
                .ToDictionaryAsync(entry => entry.ResourceId, entry => entry.Coefficient, Token);
            int lower = Math.Min(first, second);
            int upper = Math.Max(first, second);

            Assert.Equal(2.0m, deposits[lower]);
            Assert.Equal(2.5m, deposits[upper]);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMin, lowest);
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMax, highest);
        }
    }

    /// <summary>
    /// The device comes from the hold of the ship that stands by the target and from nowhere
    /// else: a device in the storage of the colony is not enough, a ship whose hold is empty
    /// refuses, and the ship that is named is the one that is charged.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_device_is_taken_from_the_named_hold_and_never_from_the_storage()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // The storage of the colony holds devices, and the only ship by the target has an empty hold.
        await ProcessesTestData.PutPartsAsync(player, services, device.PartTypeId, PartCondition.Full, 3);
        PlaceShip(services, player, targetId, ShipId, device.PartTypeId, 0);

        Assert.Equal(ProcessErrorCodes.DeviceNotInHold, await RefusedAsync(processes, player, targetId, ShipId, device.PartTypeId));
        Assert.Equal(3, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
        Assert.False((await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(targetId, Token))!.IsTerraformed);

        // A second ship carries the device: naming the first one still refuses, naming the second
        // one charges its hold and nothing else.
        Ships.Add(player.Account.UserId, targetId, new ShipOnOrbit([]) { ShipId = ShipId + 1, Cargo = [new ShipCargoPart(device.PartTypeId, 1)] });

        Assert.Equal(ProcessErrorCodes.DeviceNotInHold, await RefusedAsync(processes, player, targetId, ShipId, device.PartTypeId));
        Assert.Equal(ProcessErrorCodes.NoShipOnOrbit, await RefusedAsync(processes, player, targetId, ShipId + 100, device.PartTypeId));

        TerraformingResultDto result = await processes.TerraformAsync(player.Account.UserId, targetId, ShipId + 1, device.PartTypeId, Token);

        Assert.Equal(ShipId + 1, result.ShipId);
        Assert.Equal(0, Ships.CargoOf(player.Account.UserId, targetId, ShipId + 1, device.PartTypeId));
        Assert.Equal(0, Ships.CargoOf(player.Account.UserId, targetId, ShipId, device.PartTypeId));
        Assert.Equal(3, await ProcessesTestData.PartsAsync(player, services, device.PartTypeId, PartCondition.Full));
    }

    /// <summary>
    /// A colony is no target: the device stays in the hold and the deposits stay as they were.
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
        PlaceShip(services, player, neighbour.PlanetId, ShipId, device.PartTypeId, 1);
        PlaceShip(services, player, player.PlanetId, ShipId, device.PartTypeId, 1);
        int poor = await PlanetsTestData.SetDepositAsync(services, neighbour.PlanetId, Iron, 1.0m);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // Another player's colony and the own colony are both colonies.
        Assert.Equal(ProcessErrorCodes.TargetColonized, await RefusedAsync(processes, player, neighbour.PlanetId, ShipId, device.PartTypeId));
        Assert.Equal(ProcessErrorCodes.TargetColonized, await RefusedAsync(processes, player, player.PlanetId, ShipId, device.PartTypeId));

        Assert.Equal(1, Ships.CargoOf(player.Account.UserId, neighbour.PlanetId, ShipId, device.PartTypeId));
        Assert.Equal(1, Ships.CargoOf(player.Account.UserId, player.PlanetId, ShipId, device.PartTypeId));
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        Assert.Equal(1.0m, (await world.Deposits.AsNoTracking().SingleAsync(entry => entry.PlanetId == neighbour.PlanetId && entry.ResourceId == poor, Token)).Coefficient);
        Assert.False((await world.Planets.AsNoTracking().SingleAsync(entry => entry.Id == neighbour.PlanetId, Token)).IsTerraformed);
    }

    /// <summary>
    /// A gas giant is no target: its type may not be colonized, which the data of the type say
    /// and no code of the body, so the device stays in the hold and nothing changes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_gas_giant_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        long giantId = await world.Planets.AsNoTracking()
            .Where(planet => planet.OwnerPlayerId == null)
            .Join(world.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet, entry })
            .Join(world.ObjectTypes, pair => pair.entry.ObjectTypeId, type => type.Id, (pair, type) => new { pair.planet, type })
            .Where(row => !row.type.IsColonizable)
            .OrderBy(row => row.planet.Id)
            .Select(row => row.planet.Id)
            .FirstAsync(Token);

        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        PlaceShip(services, player, giantId, ShipId, device.PartTypeId, 1);
        List<decimal> deposits = await world.Deposits.AsNoTracking().Where(entry => entry.PlanetId == giantId).Select(entry => entry.Coefficient).ToListAsync(Token);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        Assert.False((await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(giantId, Token))!.IsColonizable);
        Assert.Equal(ProcessErrorCodes.TargetNotColonizable, await RefusedAsync(processes, player, giantId, ShipId, device.PartTypeId));

        // The device stays in the hold, the body is not marked and its deposits are the same.
        Assert.Equal(1, Ships.CargoOf(player.Account.UserId, giantId, ShipId, device.PartTypeId));
        Assert.False((await world.Planets.AsNoTracking().SingleAsync(entry => entry.Id == giantId, Token)).IsTerraformed);
        Assert.Equal(deposits, await world.Deposits.AsNoTracking().Where(entry => entry.PlanetId == giantId).Select(entry => entry.Coefficient).ToListAsync(Token));
        Assert.Empty(await world.DepositChanges.AsNoTracking().Where(change => change.PlanetId == giantId).ToListAsync(Token));
    }

    /// <summary>
    /// A body is terraformed once: a second device is refused and stays in the hold.
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
        PlaceShip(services, player, targetId, ShipId, device.PartTypeId, 2);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        int poor = await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);
        TerraformingResultDto first = await processes.TerraformAsync(player.Account.UserId, targetId, ShipId, device.PartTypeId, Token);
        await PlanetsTestData.SetDepositAsync(services, targetId, Iron, 1.0m);

        Assert.Equal(ProcessErrorCodes.TargetTerraformed, await RefusedAsync(processes, player, targetId, ShipId, device.PartTypeId));

        // The second device stays, and the deposits are not raised again.
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        Assert.Equal(1, Ships.CargoOf(player.Account.UserId, targetId, ShipId, device.PartTypeId));
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
        PlaceShip(services, player, targetId, ShipId, device.PartTypeId, 1);
        PlaceShip(services, other, targetId, ShipId, device.PartTypeId, 1);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        await processes.TerraformAsync(player.Account.UserId, targetId, ShipId, device.PartTypeId, Token);

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
            processes.TerraformAsync(other.Account.UserId, targetId, ShipId, device.PartTypeId, Token));
        Assert.Equal(ProcessErrorCodes.TargetTerraformed, again.Code);
        Assert.Equal(1, Ships.CargoOf(other.Account.UserId, targetId, ShipId, device.PartTypeId));
    }

    /// <summary>
    /// The device of another player is never spent: a ship that is not the player's own does not
    /// turn a device on, and the list of the player shows the ships of the player only; a part
    /// that is no device is refused. The terraforming names no colony at all.
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
        PlaceShip(services, owner, targetId, ShipId, device.PartTypeId, 1);
        Ships.Place(stranger.Account.UserId, targetId, new ShipOnOrbit([]) { ShipId = ShipId + 1, Cargo = [new ShipCargoPart(device.PartTypeId, 1), new ShipCargoPart(hull.PartTypeId, 1)] });
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();

        // The stranger sees the ships of the stranger only, so the body of the owner is not offered.
        TerraformingDto reading = await processes.GetTerraformingAsync(stranger.Account.UserId, Token);
        Assert.DoesNotContain(reading.Targets.SelectMany(entry => entry.ShipIds), entry => entry.ShipId == ShipId);

        using HttpResponseMessage foreign = await TestAccounts.SendCommandAsync(
            stranger.Client,
            TerraformCommand.RoutePath,
            new Dictionary<string, object?> { ["target_planet_id"] = targetId, ["ship_id"] = ShipId, ["part_type_id"] = device.PartTypeId },
            "stranger-terraform");
        Assert.NotEqual(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Equal(1, Ships.CargoOf(owner.Account.UserId, targetId, ShipId, device.PartTypeId));

        // Only the own ship turns a device on: the stranger names the ship of the owner and the
        // ships module knows no such ship of the stranger.
        Assert.Equal(ProcessErrorCodes.NoShipOnOrbit, await RefusedAsync(processes, stranger, targetId, ShipId, device.PartTypeId));
        Ships.Place(stranger.Account.UserId, targetId, null);
        Assert.Equal(ProcessErrorCodes.NoShipOnOrbit, await RefusedAsync(processes, stranger, targetId, ShipId + 1, device.PartTypeId));
        Ships.Place(stranger.Account.UserId, targetId, new ShipOnOrbit([]) { ShipId = ShipId + 1, Cargo = [new ShipCargoPart(device.PartTypeId, 1), new ShipCargoPart(hull.PartTypeId, 1)] });

        // A part that is no device turns nothing on, even when the hold carries it.
        Assert.Equal(ProcessErrorCodes.DeviceMissing, await RefusedAsync(processes, stranger, targetId, ShipId + 1, hull.PartTypeId));
        Assert.Equal(1, Ships.CargoOf(stranger.Account.UserId, targetId, ShipId + 1, device.PartTypeId));
        Assert.Equal(1, Ships.CargoOf(stranger.Account.UserId, targetId, ShipId + 1, hull.PartTypeId));

        // The owner reaches the same command with the own ship.
        using HttpResponseMessage own = await TestAccounts.SendCommandAsync(
            owner.Client,
            TerraformCommand.RoutePath,
            new Dictionary<string, object?> { ["target_planet_id"] = targetId, ["ship_id"] = ShipId, ["part_type_id"] = device.PartTypeId },
            "owner-terraform");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await own.Content.ReadAsStringAsync(Token));
        Assert.Equal(targetId, body.RootElement.GetProperty("target_planet_id").GetInt64());
        Assert.Equal(ShipId, body.RootElement.GetProperty("ship_id").GetInt64());
        Assert.Equal(0, Ships.CargoOf(owner.Account.UserId, targetId, ShipId, device.PartTypeId));
    }

    /// <summary>
    /// The range of the configuration must be a range: a highest multiplier below the lowest is
    /// refused and the device stays.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_range_that_is_no_range_is_refused()
    {
        await ProcessesTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        long targetId = await FreeBodyAsync(services, player);
        PartFacts device = await ProcessesTestData.PartAsync(services, TerraformingDevice);
        PlaceShip(services, player, targetId, ShipId, device.PartTypeId, 1);
        IProcessesModule processes = services.GetRequiredService<IProcessesModule>();
        string highest = ProcessesConfigKeys.Definitions.Single(definition => definition.Key == ProcessesConfigKeys.TerraformingMultiplierMax).DefaultValue;

        try
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMax, "1.0");
            Assert.Equal(ProcessErrorCodes.ParamMissing, await RefusedAsync(processes, player, targetId, ShipId, device.PartTypeId));
            Assert.Equal(1, Ships.CargoOf(player.Account.UserId, targetId, ShipId, device.PartTypeId));
            Assert.False((await services.GetRequiredService<IWorldModule>().GetPlanetFactsAsync(targetId, Token))!.IsTerraformed);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, ProcessesConfigKeys.TerraformingMultiplierMax, highest);
        }
    }

    /// <summary>
    /// Twenty starts on one body at once, by two players with ships of their own, give exactly
    /// one terraforming and spend exactly one device.
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
                PlaceShip(services, player, targetId, ShipId, deviceId, parallelCount / players.Length);
            }
        }

        bool[] outcomes = await Task.WhenAll(Enumerable.Range(0, parallelCount)
            .Select(index => TryTerraformAsync(players[index % players.Length], targetId, deviceId)));

        Assert.Equal(1, outcomes.Count(outcome => outcome));

        using IServiceScope check = _factory.Services.CreateScope();
        long left = players.Sum(player => Ships.CargoOf(player.Account.UserId, targetId, ShipId, deviceId));

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
            .Where(part => part.IsTerraformingDevice)
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

    private static async Task<string> RefusedAsync(IProcessesModule processes, PlanetsTestPlayer player, long targetId, long shipId, int partTypeId)
    {
        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            processes.TerraformAsync(player.Account.UserId, targetId, shipId, partTypeId, Token));

        return refused.Code;
    }

    /// <summary>
    /// Places a ship of the player by a body with a part in its hold.
    /// </summary>
    /// <param name="services">The services of the request.</param>
    /// <param name="player">The player.</param>
    /// <param name="planetId">The body the ship stands by.</param>
    /// <param name="shipId">The ship.</param>
    /// <param name="partTypeId">The part in the hold.</param>
    /// <param name="quantity">How many parts the hold carries.</param>
    private static void PlaceShip(IServiceProvider services, PlanetsTestPlayer player, long planetId, long shipId, int partTypeId, long quantity) =>
        services.GetRequiredService<ShipsOnOrbitStub>().Place(
            player.Account.UserId,
            planetId,
            new ShipOnOrbit([]) { ShipId = shipId, Cargo = quantity > 0 ? [new ShipCargoPart(partTypeId, quantity)] : [] });

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
                .TerraformAsync(player.Account.UserId, targetId, ShipId, deviceId, Token);

            return true;
        }
        catch (GameException exception) when (exception.Code == ProcessErrorCodes.TargetTerraformed)
        {
            return false;
        }
    }
}

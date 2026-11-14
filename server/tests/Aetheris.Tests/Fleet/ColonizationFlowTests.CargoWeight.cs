using Aetheris.Core.Time;
using Aetheris.Fleet.Services;
using Aetheris.Tests.Planets;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Extraction;
using Aetheris.World.Generation;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the weight of a resource in a hold (author's rule, 14.11.2026): a unit of a resource weighs
/// what the data of the resource says, and that weight is counted against the room of the holds of the
/// project of the ships, in the load, in the load of the holds and in the mass of the fleet for its speed.
/// </summary>
public sealed partial class ColonizationFlowTests
{
    private const string HeavyResource = "resource_13";
    private const string LightResource = "resource_14";
    private const string PlainResource = "resource_15";

    /// <summary>
    /// A resource of 2.5 t a unit fills the hold by its weight: the load takes as many units as the room
    /// divided by the weight, and the hold counts them by their weight.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_resource_of_two_and_a_half_tonnes_fills_the_hold_by_its_weight()
    {
        await ResetWithMailAsync();
        await using WeightScope weights = await WeightScope.SetAsync(_factory.Services, new Dictionary<string, decimal> { [HeavyResource] = 2.5m });
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive]);
        long free = await FreeRoomAsync(fleetId);
        int heavyId = await StockAsync(player, HeavyResource, free * 2);

        await CommandAsync(player, CargoLoad, new { fleet_id = fleetId, items = new[] { new { kind = "resource", id = heavyId, amount = free * 2 } } });

        long loaded = (long)decimal.Floor(free / 2.5m);
        Assert.Equal(loaded, await CarriedResourceAsync(shipId, heavyId));
        Assert.Equal((long)decimal.Ceiling(loaded * 2.5m), await CarriedMassAsync(fleetId));
        Assert.Equal(CargoShortfallReasons.Room, Assert.Single(await LoadShortItemsAsync(player)).GetProperty("reason_code").GetString());
    }

    /// <summary>
    /// A resource of 0.5 t a unit takes half the room: the hold takes twice as many units as it has tonnes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_resource_of_half_a_tonne_takes_half_the_room()
    {
        await ResetWithMailAsync();
        await using WeightScope weights = await WeightScope.SetAsync(_factory.Services, new Dictionary<string, decimal> { [LightResource] = 0.5m });
        using PlanetsTestPlayer player = await FoundAsync();
        (long fleetId, long shipId) = await FleetAsync(player, [NormalDrive]);
        long free = await FreeRoomAsync(fleetId);
        int lightId = await StockAsync(player, LightResource, free * 4);

        await CommandAsync(player, Plans, new
        {
            fleet_id = fleetId,
            orders = new object[] { LoadOrder(new { kind = "resource", id = lightId, amount = free * 4 }) },
        });
        await CommandAsync(player, Execute, new { fleet_id = fleetId });
        await RunToTheEndAsync(fleetId);

        Assert.Equal(free * 2, await CarriedResourceAsync(shipId, lightId));
        Assert.Equal(free, await CarriedMassAsync(fleetId));
        Assert.Equal(0, await FreeRoomAsync(fleetId));
    }

    /// <summary>
    /// The mass of the cargo for the speed of the fleet is counted by the weight of the resource: four units of
    /// 2.5 t slow a fleet down as much as ten units of 1 t.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_speed_of_a_fleet_counts_the_cargo_by_the_weight_of_the_resource()
    {
        await ResetWithMailAsync();
        await using WeightScope weights = await WeightScope.SetAsync(_factory.Services, new Dictionary<string, decimal> { [HeavyResource] = 2.5m, [PlainResource] = 1m });
        using PlanetsTestPlayer player = await FoundAsync();
        (long heavyFleet, _) = await FleetAsync(player, [NormalDrive]);
        (long plainFleet, _) = await FleetAsync(player, [NormalDrive]);
        long tonnes = await FreeRoomAsync(heavyFleet) / 5 * 5;

        Assert.True(tonnes > 0, "The hull of the test ship has too small a hold for the test.");

        (int heavyId, int plainId) = await StockPairAsync(player, HeavyResource, PlainResource, tonnes);

        await CommandAsync(player, CargoLoad, new { fleet_id = heavyFleet, items = new[] { new { kind = "resource", id = heavyId, amount = (long)(tonnes / 2.5m) } } });
        await CommandAsync(player, CargoLoad, new { fleet_id = plainFleet, items = new[] { new { kind = "resource", id = plainId, amount = tonnes } } });

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetSpeedService speeds = scope.ServiceProvider.GetRequiredService<FleetSpeedService>();
        FleetSpeeds heavy = await speeds.ReadAsync(heavyFleet, Token);
        FleetSpeeds plain = await speeds.ReadAsync(plainFleet, Token);

        Assert.True(plain.Normal.Current < plain.Normal.Empty, "The cargo does not change the speed of the test fleet.");
        Assert.Equal(plain.Normal.Current, heavy.Normal.Current);
    }

    /// <summary>
    /// The drilling of a field stops at the room of the hold by the weight of the yield: a share of 0.5 of a
    /// resource of 2.5 t a unit fills 100 t with 40 units, from 80 t of rock.
    /// </summary>
    [Fact]
    public void The_drilling_stops_at_the_room_of_the_hold_by_the_weight_of_the_yield()
    {
        ExtractionResult result = FieldExtraction.Extract(1000, [new ResourceShare(1, 0.5m)], 1000, 100, new Dictionary<int, decimal> { [1] = 2.5m });

        Assert.Equal(80, result.ProcessedMass);
        Assert.Equal(40, Assert.Single(result.YieldIds).Amount);
    }

    private async Task<int> StockAsync(PlanetsTestPlayer player, string code, long amount)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        int resourceId = await PlanetsTestData.ResourceIdAsync(services, code);

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [resourceId] = amount }, services.GetRequiredService<IGameClock>().GameNow);

        return resourceId;
    }

    private async Task<(int FirstId, int SecondId)> StockPairAsync(PlanetsTestPlayer player, string firstCode, string secondCode, long amount)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        int firstId = await PlanetsTestData.ResourceIdAsync(services, firstCode);
        int secondId = await PlanetsTestData.ResourceIdAsync(services, secondCode);

        await PlanetsTestData.SetBalanceAsync(
            services,
            player.PlanetId,
            new Dictionary<int, long> { [firstId] = amount, [secondId] = amount },
            services.GetRequiredService<IGameClock>().GameNow);

        return (firstId, secondId);
    }

    private async Task<long> CarriedMassAsync(long fleetId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyList<ShipHold> holds = await scope.ServiceProvider.GetRequiredService<FleetHoldService>().ReadAsync(fleetId, Token);

        return holds.Sum(hold => hold.Carried);
    }

    /// <summary>
    /// Sets the weight of resources for a test and puts the old weights back.
    /// </summary>
    private sealed class WeightScope : IAsyncDisposable
    {
        private readonly IServiceProvider _services;
        private readonly Dictionary<string, decimal> _saved;

        private WeightScope(IServiceProvider services, Dictionary<string, decimal> saved)
        {
            _services = services;
            _saved = saved;
        }

        public static async Task<WeightScope> SetAsync(IServiceProvider services, IReadOnlyDictionary<string, decimal> weights)
        {
            Dictionary<string, decimal> saved = await ApplyAsync(services, weights);

            return new WeightScope(services, saved);
        }

        public async ValueTask DisposeAsync() => await ApplyAsync(_services, _saved);

        private static async Task<Dictionary<string, decimal>> ApplyAsync(IServiceProvider services, IReadOnlyDictionary<string, decimal> weights)
        {
            using IServiceScope scope = services.CreateScope();
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            string[] codes = [.. weights.Keys];
            var rows = await world.ResourceTypes.Where(resource => codes.Contains(resource.Code)).ToListAsync(CancellationToken.None);
            Dictionary<string, decimal> before = rows.ToDictionary(resource => resource.Code, resource => resource.Weight);

            foreach (var row in rows)
            {
                row.Weight = weights[row.Code];
            }

            await world.SaveChangesAsync(CancellationToken.None);
            scope.ServiceProvider.GetRequiredService<ResourceCatalogCache>().Invalidate();

            return before;
        }
    }
}

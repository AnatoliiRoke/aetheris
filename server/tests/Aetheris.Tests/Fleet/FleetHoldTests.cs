using System.Net;
using System.Text.Json;
using Aetheris.Fleet;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the read of the hold of a fleet (task 00056): what the holds carry by resource with the room and
/// the load, a fleet of another player, an empty hold and a fleet without ships.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FleetHoldTests
{
    private const string Spaceport = "building_6";
    private const string HullCode = "part_h_21";
    private const string ContainerCode = "part_m_66";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const int ContainerCount = 2;
    private const long ShipCrew = 7;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetHoldTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public FleetHoldTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The hold of a fleet of two ships: the room is the sum of the holds of the ships, the load is the sum of
    /// what they carry, and the items go by amount, the greatest first; an item with nothing in it is not listed.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_hold_of_a_fleet_lists_what_the_ships_carry_by_amount()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(player, ContainerCount, 0);
        long fleetId = await FormAsync(player, shipIds);
        int[] resourceIds = await ResourceIdsAsync(4);

        await WriteCargoAsync(shipIds[0], fleetId, (resourceIds[0], 30), (resourceIds[1], 10));
        await WriteCargoAsync(shipIds[1], fleetId, (resourceIds[0], 5), (resourceIds[2], 40), (resourceIds[3], 0));

        JsonElement hold = await ReadHoldAsync(player, fleetId);

        Assert.Equal(fleetId, hold.GetProperty("fleet_id").GetInt64());
        Assert.Equal(await CapacityAsync(ContainerCount, 0), hold.GetProperty("capacity").GetInt64());
        Assert.Equal(85L, hold.GetProperty("carried").GetInt64());

        (int ResourceId, long Amount)[] items =
        [
            .. hold.GetProperty("items").EnumerateArray()
                .Select(item => (item.GetProperty("resource_id").GetInt32(), item.GetProperty("amount").GetInt64())),
        ];

        Assert.Equal([(resourceIds[2], 40L), (resourceIds[0], 35L), (resourceIds[1], 10L)], items);
    }

    /// <summary>
    /// A hold with nothing in it is read as it is: the room is there, the load is zero and the list is empty.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_empty_hold_has_room_and_no_items()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(player, ContainerCount);
        long fleetId = await FormAsync(player, shipIds);

        JsonElement hold = await ReadHoldAsync(player, fleetId);

        Assert.Equal(await CapacityAsync(ContainerCount), hold.GetProperty("capacity").GetInt64());
        Assert.True(hold.GetProperty("capacity").GetInt64() > 0);
        Assert.Equal(0L, hold.GetProperty("carried").GetInt64());
        Assert.Empty(hold.GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// A fleet without ships carries nothing, whatever rows of the sum it still has, and is read without a
    /// failure.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_without_ships_has_an_empty_hold()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync();
        int[] resourceIds = await ResourceIdsAsync(1);
        long fleetId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            FleetEntity fleet = new()
            {
                PlayerId = player.Account.UserId,
                Name = "Порожній",
                HomePlanetId = player.PlanetId,
                PlanetId = player.PlanetId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            database.Fleets.Add(fleet);
            await database.SaveChangesAsync(Token);
            database.FleetCargos.Add(new FleetCargoEntity { FleetId = fleet.Id, ResourceId = resourceIds[0], Amount = 50, UpdatedAt = DateTimeOffset.UtcNow });
            await database.SaveChangesAsync(Token);
            fleetId = fleet.Id;
        }

        JsonElement hold = await ReadHoldAsync(player, fleetId);

        Assert.Equal(0L, hold.GetProperty("capacity").GetInt64());
        Assert.Equal(0L, hold.GetProperty("carried").GetInt64());
        Assert.Empty(hold.GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// A player cannot read the hold of the fleet of another player, and a fleet that does not exist is refused
    /// the same way.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_hold_of_a_fleet_of_another_player_is_unknown()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await FoundAsync();
        using PlanetsTestPlayer stranger = await FoundAsync();
        List<long> shipIds = await SeedShipsAsync(owner, ContainerCount);
        long fleetId = await FormAsync(owner, shipIds);
        int[] resourceIds = await ResourceIdsAsync(1);

        await WriteCargoAsync(shipIds[0], fleetId, (resourceIds[0], 10));

        foreach (long unknownId in new[] { fleetId, fleetId + 1_000_000 })
        {
            using HttpResponseMessage response = await stranger.Client.GetAsync($"{Fleets}/{unknownId}/hold", Token);

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal("fleet_fleet_unknown", await TestAccounts.ReadErrorAsync(response));
        }

        using HttpClient anonymous = _factory.CreateBrowserClient();
        using HttpResponseMessage withoutSession = await anonymous.GetAsync($"{Fleets}/{fleetId}/hold", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.FleetNameMaxLength, "64");
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
    /// Writes a project and two finished ships of it, as the shipyard leaves them: the first has container
    /// modules and the second only the hold of its hull.
    /// </summary>
    /// <param name="player">The owner of the ships.</param>
    /// <param name="containers">How many container modules the first ship has.</param>
    /// <param name="secondContainers">How many container modules the second ship has.</param>
    /// <returns>The ships, in the order they were written.</returns>
    private async Task<List<long>> SeedShipsAsync(PlanetsTestPlayer player, int containers, int secondContainers = 0)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([HullCode, ContainerCode], Token);
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

        List<ShipEntity> ships =
        [
            NewShip(player, design, partIds, now, containers),
            NewShip(player, design, partIds, now, secondContainers),
        ];
        database.Ships.AddRange(ships);
        await database.SaveChangesAsync(Token);

        return [.. ships.Select(ship => ship.Id)];
    }

    private static ShipEntity NewShip(
        PlanetsTestPlayer player,
        ShipDesignEntity design,
        IReadOnlyDictionary<string, int> partIds,
        DateTimeOffset now,
        int containers)
    {
        List<ShipModuleEntity> parts = [new ShipModuleEntity { PartTypeId = partIds[HullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true }];

        if (containers > 0)
        {
            parts.Add(new ShipModuleEntity { PartTypeId = partIds[ContainerCode], MarkLevel = 1, ConditionPercent = 100, ConditionCode = ShipModuleConditions.Intact, Quantity = containers });
        }

        return new ShipEntity
        {
            PlayerId = player.Account.UserId,
            PlanetId = player.PlanetId,
            ShipDesignId = design.Id,
            Crew = ShipCrew,
            StateCode = ShipStates.Ready,
            CreatedAt = now,
            ModuleIds = parts,
        };
    }

    private async Task<long> FormAsync(PlanetsTestPlayer player, List<long> shipIds)
    {
        using HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            Fleets,
            new { planet_id = player.PlanetId, name = "Флот", ship_ids = shipIds },
            Guid.NewGuid().ToString("N"));
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64();
    }

    /// <summary>
    /// Writes what a ship carries and adds it to the sum of the fleet, as the extraction does.
    /// </summary>
    private async Task WriteCargoAsync(long shipId, long fleetId, params (int ResourceId, long Amount)[] rows)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((int resourceId, long amount) in rows)
        {
            database.ShipCargos.Add(new ShipCargoEntity { ShipId = shipId, ResourceId = resourceId, Amount = amount, UpdatedAt = now });

            FleetCargoEntity? sum = await database.FleetCargos
                .SingleOrDefaultAsync(cargo => cargo.FleetId == fleetId && cargo.ResourceId == resourceId, Token);

            if (sum is null)
            {
                database.FleetCargos.Add(new FleetCargoEntity { FleetId = fleetId, ResourceId = resourceId, Amount = amount, UpdatedAt = now });
            }
            else
            {
                sum.Amount += amount;
            }
        }

        await database.SaveChangesAsync(Token);
    }

    private async Task<int[]> ResourceIdsAsync(int count)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        return [.. catalog.Resources.OrderBy(resource => resource.Id).Take(count).Select(resource => resource.Id)];
    }

    /// <summary>
    /// The room of the holds of the two ships as the catalog gives it: the hull and the containers of each ship.
    /// </summary>
    private async Task<long> CapacityAsync(int firstContainers, int secondContainers = 0)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IProductionModule production = scope.ServiceProvider.GetRequiredService<IProductionModule>();
        IReadOnlyDictionary<string, int> partIds = await production.FindPartIdsByFileCodeAsync([HullCode, ContainerCode], Token);
        ShipCatalogDto catalog = await production.GetShipCatalogAsync(Token);
        decimal hull = catalog.Hulls.Single(entry => entry.Id == partIds[HullCode]).HoldCapacity;
        decimal container = catalog.Modules.Single(entry => entry.Id == partIds[ContainerCode]).CargoCapacity;

        return (long)Math.Floor(hull + (container * firstContainers)) + (long)Math.Floor(hull + (container * secondContainers));
    }

    private static async Task<JsonElement> ReadHoldAsync(PlanetsTestPlayer player, long fleetId)
    {
        using HttpResponseMessage response = await player.Client.GetAsync($"{Fleets}/{fleetId}/hold", Token);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }
}

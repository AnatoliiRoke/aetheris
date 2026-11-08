using System.Net;
using System.Text.Json;
using Aetheris.Fleet;
using Aetheris.Fleet.Api;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the fleets (task 00040): the limit of the spaceport, the composition, the transfer
/// between fleets, the disbanding and the loss of a fleet.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FleetFormationTests
{
    private const string Spaceport = "building_6";
    private const string HullCode = "part_h_21";
    private const string Fleets = "/api/v1/fleet/fleets";
    private const string AddShips = "/api/v1/fleet/fleets/add-ships";
    private const string RemoveShips = "/api/v1/fleet/fleets/remove-ships";
    private const string TransferShips = "/api/v1/fleet/fleets/transfer-ships";
    private const string Rename = "/api/v1/fleet/fleets/rename";
    private const string Dissolve = "/api/v1/fleet/fleets/dissolve";
    private const string Disassemble = "/api/v1/fleet/ship/disassemble";
    private const string Ships = "/api/v1/fleet/ships";
    private const int ParallelCount = 20;
    private const int BigFleetSize = 30;
    private const decimal ShipUpkeep = 2.5m;
    private const long ShipCrew = 7;
    private const long ElsewherePlanetId = 987_654_321;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetFormationTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public FleetFormationTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A fleet forms of ships that stand on one body: that body is its home and its place, the
    /// ships are listed in the order they joined, and a read in a new scope finds the stored rows.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_forms_on_the_body_of_its_ships_and_is_stored()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 3);

        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "  Перший флот  ", ship_ids = ships });
        JsonElement fleet = view.GetProperty("fleets").EnumerateArray().Single();

        Assert.Equal("Перший флот", fleet.GetProperty("name").GetString());
        Assert.Equal(player.PlanetId, fleet.GetProperty("home_planet_id").GetInt64());
        Assert.Equal(player.PlanetId, fleet.GetProperty("planet_id").GetInt64());
        Assert.Equal(ships, fleet.GetProperty("ships").EnumerateArray().Select(ship => ship.GetProperty("id").GetInt64()));
        Assert.Equal(3 * ShipCrew, fleet.GetProperty("crew").GetInt64());
        Assert.Equal(3 * ShipUpkeep, fleet.GetProperty("upkeep_per_hour").GetDecimal());
        Assert.Equal(1, view.GetProperty("limit").GetInt32());
        Assert.Equal(1, view.GetProperty("used").GetInt32());
        Assert.Empty(view.GetProperty("free_ships").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(fleet.GetProperty("ships")[0].GetProperty("hull_name_key").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(fleet.GetProperty("ships")[0].GetProperty("class_name_key").GetString()));
        Assert.Equal(100, fleet.GetProperty("ships")[0].GetProperty("condition_percent").GetInt32());

        // A new scope reads what is stored, as a restarted server does.
        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        FleetEntity stored = await database.Fleets.AsNoTracking().Include(entry => entry.ShipIds).SingleAsync(Token);

        Assert.Equal(player.Account.UserId, stored.PlayerId);
        Assert.Equal(player.PlanetId, stored.HomePlanetId);
        Assert.NotEqual(0, stored.SectorId);
        Assert.Equal(ships, stored.ShipIds.OrderBy(entry => entry.Position).Select(entry => entry.ShipId));

        JsonElement read = await ReadAsync(player);

        Assert.Equal(stored.Id, read.GetProperty("fleets").EnumerateArray().Single().GetProperty("id").GetInt64());
    }

    /// <summary>
    /// What a fleet carries and deals is the sum of the summaries of the projects of its ships.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_adds_up_what_its_ships_carry_and_deal()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 3);

        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Сума", ship_ids = ships });
        JsonElement fleet = view.GetProperty("fleets")[0];
        long designId = fleet.GetProperty("ships")[0].GetProperty("design_id").GetInt64();

        using HttpResponseMessage design = await player.Client.GetAsync($"/api/v1/fleet/designs/{designId}", Token);
        using JsonDocument body = JsonDocument.Parse(await design.Content.ReadAsStringAsync(Token));
        JsonElement summary = body.RootElement.GetProperty("summary");

        Assert.True(summary.GetProperty("integrity").GetDecimal() > 0);
        Assert.Equal(3 * summary.GetProperty("hold_capacity").GetDecimal(), fleet.GetProperty("hold_capacity").GetDecimal());
        Assert.Equal(3 * summary.GetProperty("integrity").GetDecimal(), fleet.GetProperty("integrity").GetDecimal());
        Assert.Equal(3 * summary.GetProperty("damage_min").GetDecimal(), fleet.GetProperty("damage_min").GetDecimal());
        Assert.Equal(3 * summary.GetProperty("damage_max").GetDecimal(), fleet.GetProperty("damage_max").GetDecimal());
        Assert.Equal(3 * summary.GetProperty("shield_capacity").GetDecimal(), fleet.GetProperty("shield_capacity").GetDecimal());
    }

    /// <summary>
    /// A fleet row added straight to the table, with no code change, reaches the player.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_row_written_to_the_table_reaches_the_player()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 2);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            database.Fleets.Add(new FleetEntity
            {
                PlayerId = player.Account.UserId,
                Name = "Із бази",
                HomePlanetId = player.PlanetId,
                PlanetId = player.PlanetId,
                SectorId = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                ShipIds = [new FleetShipEntity { ShipId = ships[0], Position = 1 }],
            });
            await database.SaveChangesAsync(Token);
        }

        JsonElement view = await ReadAsync(player);

        Assert.Equal("Із бази", view.GetProperty("fleets").EnumerateArray().Single().GetProperty("name").GetString());
        Assert.Equal(ships[1], view.GetProperty("free_ships").EnumerateArray().Single().GetProperty("id").GetInt64());
    }

    /// <summary>
    /// The number of the fleets follows the level of the spaceport, and a refusal names the limit.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_limit_of_the_fleets_follows_the_level_of_the_spaceport()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 3);

        await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Один", ship_ids = new[] { ships[0] } });

        using (HttpResponseMessage refused = await SendAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Два", ship_ids = new[] { ships[1] } }))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Equal("fleet_fleet_limit_reached", await TestAccounts.ReadErrorAsync(refused));
        }

        await SetSpaceportAsync(player, 2);
        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Два", ship_ids = new[] { ships[1] } });

        Assert.Equal(2, view.GetProperty("limit").GetInt32());
        Assert.Equal(2, view.GetProperty("used").GetInt32());

        await SetSpaceportAsync(player, 0);
        JsonElement without = await ReadAsync(player);

        Assert.Equal(0, without.GetProperty("limit").GetInt32());
        Assert.Equal(2, without.GetProperty("fleets").GetArrayLength());
    }

    /// <summary>
    /// A fleet needs at least one ship.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_without_ships_is_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);

        using HttpResponseMessage empty = await SendAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Пустий", ship_ids = Array.Empty<long>() });

        Assert.False(empty.IsSuccessStatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();

        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Fleets.CountAsync(Token));
    }

    /// <summary>
    /// A fleet can be without a commander: the answer carries no commander and nothing is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_needs_no_commander()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 1);

        JsonElement view = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Сам", ship_ids = ships });
        JsonElement fleet = view.GetProperty("fleets").EnumerateArray().Single();

        Assert.False(fleet.TryGetProperty("commander_id", out _));
        Assert.Single(fleet.GetProperty("ships").EnumerateArray());
    }

    /// <summary>
    /// Twenty requests at once do not pass the limit of the fleets.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_requests_do_not_pass_the_limit_of_the_fleets()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 3);
        List<long> ships = await SeedShipsAsync(player, ParallelCount);

        HttpResponseMessage[] responses = await Task.WhenAll(
            ships.Select((shipId, index) => SendAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот " + index, ship_ids = new[] { shipId } })));

        try
        {
            Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.OK));

            foreach (HttpResponseMessage refused in responses.Where(response => response.StatusCode != HttpStatusCode.OK))
            {
                Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
                Assert.Equal("fleet_fleet_limit_reached", await TestAccounts.ReadErrorAsync(refused));
            }
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Fleets.CountAsync(Token));
    }

    /// <summary>
    /// A ship is in one fleet at most, even when twenty requests name it at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_ship_is_in_one_fleet_even_under_twenty_parallel_requests()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 12);
        List<long> ships = await SeedShipsAsync(player, 1);

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, ParallelCount)
                .Select(index => SendAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Флот " + index, ship_ids = ships })));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));

            foreach (HttpResponseMessage refused in responses.Where(response => response.StatusCode != HttpStatusCode.OK))
            {
                Assert.Equal("fleet_ship_in_fleet", await TestAccounts.ReadErrorAsync(refused));
            }
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        Assert.Equal(1, await database.Fleets.CountAsync(Token));
        Assert.Equal(1, await database.FleetShips.CountAsync(Token));
    }

    /// <summary>
    /// The size of a fleet is not limited, a ship from another body is refused, a ship of
    /// another player is unknown, and a ship is not put into a second fleet.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Ships_join_a_fleet_without_a_size_limit_and_only_from_its_body()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 2);
        using PlanetsTestPlayer stranger = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, BigFleetSize + 1);
        List<long> far = await SeedShipsAsync(player, 1, ElsewherePlanetId);
        List<long> foreign = await SeedShipsAsync(stranger, 1);

        JsonElement created = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Великий", ship_ids = ships.Take(1) });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();
        JsonElement grown = await CommandAsync(player, AddShips, new { fleet_id = fleetId, ship_ids = ships.Skip(1).Take(BigFleetSize - 1) });

        Assert.Equal(BigFleetSize, grown.GetProperty("fleets")[0].GetProperty("ships").GetArrayLength());

        using (HttpResponseMessage elsewhere = await SendAsync(player, AddShips, new { fleet_id = fleetId, ship_ids = far }))
        {
            Assert.Equal("fleet_ship_elsewhere", await TestAccounts.ReadErrorAsync(elsewhere));
        }

        using (HttpResponseMessage unknown = await SendAsync(player, AddShips, new { fleet_id = fleetId, ship_ids = foreign }))
        {
            Assert.Equal("fleet_ship_unknown", await TestAccounts.ReadErrorAsync(unknown));
        }

        using (HttpResponseMessage twice = await SendAsync(player, AddShips, new { fleet_id = fleetId, ship_ids = ships.Take(1) }))
        {
            Assert.Equal("fleet_ship_in_fleet", await TestAccounts.ReadErrorAsync(twice));
        }

        JsonElement last = await CommandAsync(player, AddShips, new { fleet_id = fleetId, ship_ids = ships.Skip(BigFleetSize).Take(1) });

        Assert.Equal(BigFleetSize + 1, last.GetProperty("fleets")[0].GetProperty("ships").GetArrayLength());
    }

    /// <summary>
    /// A ship leaves a fleet and stands free on its body; the last ship of a fleet stays.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_ship_leaves_the_fleet_and_the_last_ship_stays()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 2);

        JsonElement created = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Два", ship_ids = ships });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();

        JsonElement view = await CommandAsync(player, RemoveShips, new { fleet_id = fleetId, ship_ids = new[] { ships[0] } });

        Assert.Equal(ships[1], view.GetProperty("fleets")[0].GetProperty("ships")[0].GetProperty("id").GetInt64());
        Assert.Equal(ships[0], view.GetProperty("free_ships").EnumerateArray().Single().GetProperty("id").GetInt64());

        using (HttpResponseMessage lastShip = await SendAsync(player, RemoveShips, new { fleet_id = fleetId, ship_ids = new[] { ships[1] } }))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, lastShip.StatusCode);
            Assert.Equal("fleet_fleet_last_ship", await TestAccounts.ReadErrorAsync(lastShip));
        }

        using (HttpResponseMessage notThere = await SendAsync(player, RemoveShips, new { fleet_id = fleetId, ship_ids = new[] { ships[0] } }))
        {
            Assert.Equal("fleet_ship_not_in_fleet", await TestAccounts.ReadErrorAsync(notThere));
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        Assert.Equal(2, await database.Ships.CountAsync(Token));
        Assert.Equal(1, await database.FleetShips.CountAsync(Token));
    }

    /// <summary>
    /// Ships move between two fleets on the same body; the fleet they leave keeps one ship, and
    /// two fleets on different bodies do not exchange ships.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Ships_move_between_fleets_on_the_same_body_only()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 3);
        List<long> ships = await SeedShipsAsync(player, 4);

        JsonElement first = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Перший", ship_ids = ships.Take(3) });
        long firstId = first.GetProperty("fleets")[0].GetProperty("id").GetInt64();
        JsonElement second = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Другий", ship_ids = ships.Skip(3) });
        long secondId = second.GetProperty("fleets")[1].GetProperty("id").GetInt64();

        JsonElement moved = await CommandAsync(player, TransferShips, new { fleet_id = firstId, target_fleet_id = secondId, ship_ids = new[] { ships[2] } });

        Assert.Equal(2, moved.GetProperty("fleets")[0].GetProperty("ships").GetArrayLength());
        Assert.Equal(
            new[] { ships[3], ships[2] },
            moved.GetProperty("fleets")[1].GetProperty("ships").EnumerateArray().Select(ship => ship.GetProperty("id").GetInt64()));

        using (HttpResponseMessage emptied = await SendAsync(player, TransferShips, new { fleet_id = firstId, target_fleet_id = secondId, ship_ids = ships.Take(2) }))
        {
            Assert.Equal("fleet_fleet_last_ship", await TestAccounts.ReadErrorAsync(emptied));
        }

        using (HttpResponseMessage same = await SendAsync(player, TransferShips, new { fleet_id = firstId, target_fleet_id = firstId, ship_ids = ships.Take(1) }))
        {
            Assert.Equal("fleet_fleets_apart", await TestAccounts.ReadErrorAsync(same));
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            FleetEntity other = await database.Fleets.SingleAsync(entry => entry.Id == secondId, Token);
            other.PlanetId = ElsewherePlanetId;
            await database.SaveChangesAsync(Token);
        }

        using HttpResponseMessage apart = await SendAsync(player, TransferShips, new { fleet_id = firstId, target_fleet_id = secondId, ship_ids = ships.Take(1) });

        Assert.Equal("fleet_fleets_apart", await TestAccounts.ReadErrorAsync(apart));
    }

    /// <summary>
    /// A fleet is renamed within the limit of the name, and the limit is a parameter.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_is_renamed_within_the_limit_of_the_name()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 1);
        JsonElement created = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Старий", ship_ids = ships });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();

        JsonElement renamed = await CommandAsync(player, Rename, new { fleet_id = fleetId, name = "Новий" });

        Assert.Equal("Новий", renamed.GetProperty("fleets")[0].GetProperty("name").GetString());

        using (HttpResponseMessage blank = await SendAsync(player, Rename, new { fleet_id = fleetId, name = "   " }))
        {
            Assert.Equal("validation_failed", await TestAccounts.ReadErrorAsync(blank));
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, FleetConfigKeys.FleetNameMaxLength, "5");
        }

        using (HttpResponseMessage tooLong = await SendAsync(player, Rename, new { fleet_id = fleetId, name = "Шість!" }))
        {
            Assert.Equal("fleet_name_invalid", await TestAccounts.ReadErrorAsync(tooLong));
        }

        JsonElement fits = await CommandAsync(player, Rename, new { fleet_id = fleetId, name = "П'ять" });

        Assert.Equal("П'ять", fits.GetProperty("fleets")[0].GetProperty("name").GetString());
    }

    /// <summary>
    /// A player cannot read or change the fleet of another player.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fleet_of_another_player_is_unknown()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await FoundAsync(spaceportLevel: 1);
        using PlanetsTestPlayer stranger = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(owner, 1);
        List<long> others = await SeedShipsAsync(stranger, 1);
        JsonElement created = await CommandAsync(owner, Fleets, new { planet_id = owner.PlanetId, name = "Чужий", ship_ids = ships });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();

        foreach ((string route, object body) in new (string, object)[]
        {
            (Rename, new { fleet_id = fleetId, name = "Захоплений" }),
            (Dissolve, new { fleet_id = fleetId }),
            (AddShips, new { fleet_id = fleetId, ship_ids = others }),
            (RemoveShips, new { fleet_id = fleetId, ship_ids = ships }),
        })
        {
            using HttpResponseMessage response = await SendAsync(stranger, route, body);

            Assert.Equal("fleet_fleet_unknown", await TestAccounts.ReadErrorAsync(response));
        }

        using HttpResponseMessage onForeignBody = await SendAsync(stranger, Fleets, new { planet_id = owner.PlanetId, name = "Чужа планета", ship_ids = others });

        Assert.False(onForeignBody.IsSuccessStatusCode);

        JsonElement view = await ReadAsync(stranger);

        Assert.Empty(view.GetProperty("fleets").EnumerateArray());
        Assert.Equal(others[0], view.GetProperty("free_ships").EnumerateArray().Single().GetProperty("id").GetInt64());
    }

    /// <summary>
    /// A disbanded fleet leaves its ships free on their body.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_disbanded_fleet_leaves_its_ships_free()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 2);
        JsonElement created = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Тимчасовий", ship_ids = ships });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();

        JsonElement view = await CommandAsync(player, Dissolve, new { fleet_id = fleetId });

        Assert.Empty(view.GetProperty("fleets").EnumerateArray());
        Assert.Equal(ships, view.GetProperty("free_ships").EnumerateArray().Select(ship => ship.GetProperty("id").GetInt64()));

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        Assert.Equal(2, await database.Ships.CountAsync(Token));
        Assert.Equal(0, await database.FleetShips.CountAsync(Token));

        JsonElement again = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Знову", ship_ids = ships });

        Assert.Equal(2, again.GetProperty("fleets")[0].GetProperty("ships").GetArrayLength());
    }

    /// <summary>
    /// A lost fleet is gone with every ship in it, and the other ships stay.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_lost_fleet_takes_its_ships_with_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 2);
        List<long> ships = await SeedShipsAsync(player, 3);
        JsonElement created = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Загиблий", ship_ids = ships.Take(2) });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IFleetModule>().LoseFleetAsync(fleetId, Token);
        }

        using IServiceScope check = _factory.Services.CreateScope();
        FleetDbContext database = check.ServiceProvider.GetRequiredService<FleetDbContext>();

        Assert.Equal(0, await database.Fleets.CountAsync(Token));
        Assert.Equal(0, await database.FleetShips.CountAsync(Token));
        Assert.Equal(ships.Skip(2), await database.Ships.AsNoTracking().Select(ship => ship.Id).ToListAsync(Token));

        FleetFacts facts = await check.ServiceProvider.GetRequiredService<IFleetModule>().GetFleetFactsAsync(player.Account.UserId, Token);

        Assert.Equal(0, facts.FleetCount);
        Assert.Equal(1, facts.ShipCount);
        Assert.Equal(0, facts.ShipsInFleets);

        await Assert.ThrowsAsync<Aetheris.Core.Errors.GameException>(
            () => check.ServiceProvider.GetRequiredService<IFleetModule>().LoseFleetAsync(fleetId, Token));
    }

    /// <summary>
    /// The facts for the rating count the fleets, the ships in them and what the ships cost.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_facts_for_the_rating_count_fleets_and_ships()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 3);
        await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Рейтинг", ship_ids = ships.Take(2) });

        using IServiceScope scope = _factory.Services.CreateScope();
        FleetFacts facts = await scope.ServiceProvider.GetRequiredService<IFleetModule>().GetFleetFactsAsync(player.Account.UserId, Token);

        Assert.Equal(1, facts.FleetCount);
        Assert.Equal(3, facts.ShipCount);
        Assert.Equal(2, facts.ShipsInFleets);
    }

    /// <summary>
    /// A ship in a fleet is not taken apart; the shipyard names the fleet as the reason.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_ship_in_a_fleet_is_not_taken_apart()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await FoundAsync(spaceportLevel: 1);
        List<long> ships = await SeedShipsAsync(player, 2);
        JsonElement created = await CommandAsync(player, Fleets, new { planet_id = player.PlanetId, name = "Бойовий", ship_ids = ships.Take(1) });
        long fleetId = created.GetProperty("fleets")[0].GetProperty("id").GetInt64();

        using (HttpResponseMessage refused = await SendAsync(player, Disassemble, new { ship_id = ships[0] }))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
            Assert.Equal("fleet_ship_in_fleet", await TestAccounts.ReadErrorAsync(refused));
        }

        using IServiceScope scope = _factory.Services.CreateScope();

        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Ships.CountAsync(Token));

        // The ships list names the fleet of a ship, and the free filter leaves it out.
        HttpResponseMessage all = await player.Client.GetAsync(Ships, Token);
        HttpResponseMessage free = await player.Client.GetAsync(Ships + "?free=true", Token);

        using (all)
        using (free)
        {
            using JsonDocument allBody = JsonDocument.Parse(await all.Content.ReadAsStringAsync(Token));
            using JsonDocument freeBody = JsonDocument.Parse(await free.Content.ReadAsStringAsync(Token));

            Assert.Equal(2, allBody.RootElement.GetArrayLength());
            Assert.Equal(fleetId, allBody.RootElement[0].GetProperty("fleet_id").GetInt64());
            Assert.Equal(JsonValueKind.Null, allBody.RootElement[1].GetProperty("fleet_id").ValueKind);
            Assert.Equal(ships[1], freeBody.RootElement.EnumerateArray().Single().GetProperty("id").GetInt64());
        }
    }

    /// <summary>
    /// The fleets need a signed-in player.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_fleets_need_a_signed_in_player()
    {
        using HttpClient anonymous = _factory.CreateBrowserClient();
        using HttpResponseMessage response = await anonymous.GetAsync(Fleets, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Prepares the world, the production catalog and the fleet module.
    /// </summary>
    /// <returns>A task that completes when the modules are ready.</returns>
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

    /// <summary>
    /// Founds a colony and sets the level of its spaceport.
    /// </summary>
    /// <param name="spaceportLevel">The level of the spaceport.</param>
    /// <returns>The player.</returns>
    private async Task<PlanetsTestPlayer> FoundAsync(int spaceportLevel)
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetSpaceportAsync(player, spaceportLevel);

        return player;
    }

    private async Task SetSpaceportAsync(PlanetsTestPlayer player, int level)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Spaceport, level);
    }

    /// <summary>
    /// Writes a project and finished ships of it, as the shipyard leaves them.
    /// </summary>
    /// <param name="player">The owner of the ships.</param>
    /// <param name="count">How many ships.</param>
    /// <param name="planetId">The body the ships stand on; the home body when omitted.</param>
    /// <returns>The ships, in the order they were written.</returns>
    private async Task<List<long>> SeedShipsAsync(PlanetsTestPlayer player, int count, long? planetId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([HullCode], Token);
        FleetDbContext database = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        long standsOn = planetId ?? player.PlanetId;
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
            .. Enumerable.Range(0, count).Select(_ => new ShipEntity
            {
                PlayerId = player.Account.UserId,
                PlanetId = standsOn,
                ShipDesignId = design.Id,
                Crew = ShipCrew,
                UpkeepPerHour = ShipUpkeep,
                StateCode = ShipStates.Ready,
                CreatedAt = now,
                ModuleIds = [new ShipModuleEntity { PartTypeId = partIds[HullCode], MarkLevel = 1, ConditionPercent = 100, Quantity = 1, IsHull = true }],
            }),
        ];
        database.Ships.AddRange(ships);
        await database.SaveChangesAsync(Token);

        return [.. ships.Select(ship => ship.Id)];
    }

    private static async Task<JsonElement> ReadAsync(PlanetsTestPlayer player)
    {
        using HttpResponseMessage response = await player.Client.GetAsync(Fleets, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token)).RootElement.Clone();
    }

    private static async Task<JsonElement> CommandAsync(PlanetsTestPlayer player, string route, object body)
    {
        using HttpResponseMessage response = await SendAsync(player, route, body);
        string text = await response.Content.ReadAsStringAsync(Token);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);

        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> SendAsync(PlanetsTestPlayer player, string route, object body) =>
        TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));
}

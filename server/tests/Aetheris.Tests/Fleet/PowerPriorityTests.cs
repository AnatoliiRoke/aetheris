using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Fleet.Data;
using Aetheris.Host.Observability;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the shutdown order of a slot and of the new catalog fields (task 00062).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PowerPriorityTests
{
    private const string Designs = "/api/v1/fleet/designs";

    private const string Catalog = "/api/v1/fleet/ship-catalog";

    private const string DesignBureau = "building_10";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerPriorityTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public PowerPriorityTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The catalog of the constructor returns the new fields, and the production catalog
    /// returns them for the drone hulls and for the camouflage module.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_catalog_returns_the_new_fields()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        HttpResponseMessage response = await player.Client.GetAsync($"{Catalog}?planet_id={player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        int queries = QueryCount(response);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement[] hulls = [.. body.RootElement.GetProperty("hulls").EnumerateArray()];
        int moduleCount = body.RootElement.GetProperty("modules").GetArrayLength();
        Assert.True(queries < moduleCount, $"The catalog used {queries} queries for {moduleCount} modules.");
        JsonElement[] lightHulls = [.. hulls.Where(hull => hull.GetProperty("part_group_code").GetString() == "pg_12")];
        Assert.NotEmpty(lightHulls);
        Assert.All(lightHulls, hull =>
        {
            Assert.True(hull.GetProperty("part_group_id").GetInt32() > 0);
            Assert.Equal(0.6m, hull.GetProperty("signature").GetDecimal());
            Assert.Equal(800m, hull.GetProperty("leash_radius").GetDecimal());
        });

        JsonElement laser = body.RootElement.GetProperty("modules").EnumerateArray()
            .Single(module => module.GetProperty("code").GetString() == "part_m_13");
        Assert.Equal(6, laser.GetProperty("accuracy").EnumerateObject().Count());
        Assert.Equal(100m, laser.GetProperty("integrity").GetDecimal());
        Assert.Equal(20, laser.GetProperty("default_power_priority").GetInt32());

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        ShipCatalogDto catalog = await scope.ServiceProvider.GetRequiredService<IProductionModule>().GetShipCatalogAsync(Token);

        foreach (string code in new[] { "part_h_5", "part_h_6", "part_h_7" })
        {
            ShipHullDto drone = catalog.Hulls.Single(hull => hull.Code == code);
            Assert.Equal(0.2m, drone.Signature);
            Assert.Equal(0m, drone.LeashRadius);
        }

        ShipModuleDto camouflage = catalog.Modules.Single(module => module.Code == "part_m_61");
        Assert.Equal(0.35m, camouflage.SignatureCut);
        Assert.Equal((short)30, camouflage.DefaultPowerPriority);
    }

    /// <summary>
    /// A save without an order stores the order of the group. An order outside 0..1000 is refused.
    /// A sent order is stored and read back.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_save_stores_the_shutdown_order()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        await SetBureauLevelAsync(player.PlanetId, 3);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        ShipCatalogDto catalog = await scope.ServiceProvider.GetRequiredService<IProductionModule>().GetShipCatalogAsync(Token);
        ShipModuleDto laser = catalog.Modules.Single(module => module.Code == "part_m_13");
        ShipHullDto hull = catalog.Hulls.First(candidate =>
            candidate.PartGroupCode == "pg_12"
            && candidate.Role == "combat"
            && candidate.Slots >= 3
            && Fits(laser, candidate.Id));
        ShipModuleDto drive = catalog.Modules.First(module =>
            module.Component == "drive" && module.PartGroupCode == "pg_1" && Fits(module, hull.Id));
        ShipModuleDto reactor = catalog.Modules.First(module =>
            module.Component == "reactor"
            && module.PartGroupCode == "pg_2"
            && Fits(module, hull.Id)
            && module.Power >= drive.EnergyUse + laser.EnergyUse);

        long designId = await CreateIdAsync(player, hull.Id);

        HttpResponseMessage saved = await SaveAsync(player, designId, hull.Id, [
            Slot(drive.Id, 0, null),
            Slot(reactor.Id, 1, null),
            Slot(laser.Id, 2, null),
        ]);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using JsonDocument first = JsonDocument.Parse(await saved.Content.ReadAsStringAsync(Token));
        Assert.Equal(0, Priority(first, drive.Id));
        Assert.Equal(0, Priority(first, reactor.Id));
        Assert.Equal(20, Priority(first, laser.Id));

        HttpResponseMessage refused = await SaveAsync(player, designId, hull.Id, [
            Slot(drive.Id, 0, null),
            Slot(reactor.Id, 1, null),
            Slot(laser.Id, 2, 1001),
        ]);
        Assert.Equal("fleet_power_priority_invalid", await TestAccounts.ReadErrorAsync(refused));

        HttpResponseMessage changed = await SaveAsync(player, designId, hull.Id, [
            Slot(drive.Id, 0, null),
            Slot(reactor.Id, 1, null),
            Slot(laser.Id, 2, 7),
        ]);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using JsonDocument second = JsonDocument.Parse(await changed.Content.ReadAsStringAsync(Token));
        Assert.Equal(7, Priority(second, laser.Id));
    }

    /// <summary>
    /// The migration statement names every pair of the embedded file and runs on the test database.
    /// </summary>
    /// <returns>A task that completes when the statement has run.</returns>
    [Fact]
    public async Task The_migration_statement_names_every_pair()
    {
        await using Stream stream = typeof(ShipDesignSlotPowerPriority).Assembly
            .GetManifestResourceStream("Aetheris.Fleet.power-priority-defaults.json")
            ?? throw new InvalidOperationException("The embedded shutdown orders are missing.");
        Dictionary<string, int> pairs = JsonSerializer.Deserialize<Dictionary<string, int>>(stream)
            ?? throw new InvalidOperationException("The embedded shutdown orders are empty.");
        string sql = ShipDesignSlotPowerPriority.BackfillSql;

        Assert.Equal(pairs.Count, Regex.Count(sql, @"\(\d+, \d+\)"));

        foreach (KeyValuePair<string, int> pair in pairs)
        {
            Assert.Contains($"({pair.Key}, {pair.Value})", sql, StringComparison.Ordinal);
        }

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        FleetDbContext fleet = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        await fleet.Database.ExecuteSqlRawAsync(ShipDesignSlotPowerPriority.BackfillSql, Token);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, Aetheris.Production.Configuration.ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private async Task SetBureauLevelAsync(long planetId, int level)
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, planetId, DesignBureau, level);
    }

    private async Task<long> CreateIdAsync(PlanetsTestPlayer player, int hullId)
    {
        HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            player.Client,
            Designs,
            new { planet_id = player.PlanetId, name = "Порядок", hull_part_type_id = hullId, has_race_bonus_slot = false },
            Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return body.RootElement.GetProperty("id").GetInt64();
    }

    private static Task<HttpResponseMessage> SaveAsync(PlanetsTestPlayer player, long designId, int hullId, object[] slots) =>
        TestAccounts.SendCommandAsync(
            player.Client,
            Designs + "/save",
            new { design_id = designId, hull_part_type_id = hullId, has_race_bonus_slot = false, slots },
            Guid.NewGuid().ToString("N"));

    private static object Slot(int partId, int index, int? priority) => new
    {
        slot_index = index,
        part_type_id = partId,
        mark_level = 1,
        has_race_bonus = false,
        power_priority = priority,
    };

    private static bool Fits(ShipModuleDto module, int hullId) =>
        module.CompatibleHullIds.Count == 0 || module.CompatibleHullIds.Contains(hullId);

    private static int QueryCount(HttpResponseMessage response)
    {
        Match count = Regex.Match(
            response.Headers.GetValues(RequestTraceMiddleware.ServerTimingHeader).Single(),
            "desc=\"(?<count>\\d+) ",
            RegexOptions.CultureInvariant);

        Assert.True(count.Success);

        return int.Parse(count.Groups["count"].Value, CultureInfo.InvariantCulture);
    }

    private static int Priority(JsonDocument body, int partId) =>
        body.RootElement.GetProperty("slots").EnumerateArray()
            .Single(slot => slot.GetProperty("part_type_id").GetInt32() == partId)
            .GetProperty("power_priority")
            .GetInt32();
}

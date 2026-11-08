using System.Net;
using System.Text.Json;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the ammunition of the weapons: which round a weapon fires, that a weapon that fires
/// ammunition has no damage of its own, and that the damage of a project is counted from the rounds
/// (task 00040, decision of the author of 08.11.2026).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AmmunitionDamageTests
{
    private const string Designs = "/api/v1/fleet/designs";
    private const string Catalog = "/api/v1/fleet/ship-catalog";
    private const string DesignBureau = "building_10";

    private const string Cannon = "part_m_21";
    private const string Autocannon = "part_m_22";
    private const string Railgun = "part_m_23";
    private const string LauncherX1 = "part_m_24";
    private const string LauncherX2 = "part_m_25";
    private const string LauncherX3 = "part_m_26";
    private const string LauncherX1B = "part_m_84";
    private const string SteelShell = "part_m_43";
    private const string CobaltShell = "part_m_44";
    private const string RailSlug = "part_m_45";
    private const string ArrowX1 = "part_m_46";
    private const string LightningX1 = "part_m_47";
    private const string SpearX2 = "part_m_48";
    private const string ValkyrieX3 = "part_m_49";
    private const string ThermonuclearTorpedo = "part_m_50";
    private const string ProtonTorpedo = "part_m_51";
    private const string UraniumTorpedo = "part_m_52";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmmunitionDamageTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public AmmunitionDamageTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The seed binds every weapon to the ammunition it fires, and to no other.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_binds_every_weapon_to_the_ammunition_it_fires()
    {
        await ResetAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        string[] codes = [Cannon, Autocannon, Railgun, LauncherX1, LauncherX2, LauncherX3, LauncherX1B, SteelShell, CobaltShell, RailSlug, ArrowX1, LightningX1, SpearX2, ValkyrieX3, ThermonuclearTorpedo, ProtonTorpedo, UraniumTorpedo];
        IReadOnlyDictionary<string, int> ids = await scope.ServiceProvider.GetRequiredService<IProductionModule>().FindPartIdsByFileCodeAsync(codes, Token);
        Dictionary<int, string> codeById = ids.ToDictionary(pair => pair.Value, pair => pair.Key);
        List<(int Weapon, int Round)> links = (await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartAmmunitionCompatibilities
            .AsNoTracking()
            .Where(link => !link.IsArchived)
            .Select(link => new { link.PartTypeId, link.AmmunitionPartTypeId })
            .ToListAsync(Token))
            .Select(link => (link.PartTypeId, link.AmmunitionPartTypeId))
            .ToList();

        Dictionary<string, string[]> expected = new()
        {
            [Cannon] = [SteelShell, CobaltShell],
            [Autocannon] = [SteelShell, CobaltShell],
            [Railgun] = [RailSlug],
            [LauncherX1] = [ArrowX1, LightningX1],
            [LauncherX1B] = [ArrowX1, LightningX1, SpearX2],
            [LauncherX2] = [ValkyrieX3, ThermonuclearTorpedo, ProtonTorpedo],
            [LauncherX3] = [ThermonuclearTorpedo, ProtonTorpedo, UraniumTorpedo],
        };

        foreach ((string weapon, string[] rounds) in expected)
        {
            string[] actual = [.. links.Where(link => link.Weapon == ids[weapon]).Select(link => codeById[link.Round]).Order(StringComparer.Ordinal)];

            Assert.Equal([.. rounds.Order(StringComparer.Ordinal)], actual);
        }

        Assert.Equal(expected.Values.Sum(rounds => rounds.Length), links.Count(link => codeById.ContainsKey(link.Weapon)));
    }

    /// <summary>
    /// A weapon that fires ammunition has no damage of its own: the damage lives in the rounds.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_weapon_that_fires_ammunition_has_no_damage_of_its_own()
    {
        await ResetAsync();

        using IServiceScope scope = _factory.Services.CreateScope();
        ProductionDbContext production = scope.ServiceProvider.GetRequiredService<ProductionDbContext>();
        IReadOnlyDictionary<string, int> ids = await scope.ServiceProvider.GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([Cannon, Autocannon, Railgun, LauncherX1, LauncherX2, LauncherX3, LauncherX1B, SteelShell, RailSlug], Token);
        int[] weapons = [ids[Cannon], ids[Autocannon], ids[Railgun], ids[LauncherX1], ids[LauncherX2], ids[LauncherX3], ids[LauncherX1B]];

        List<int> damageOnWeapons = await production.PartCharacteristicValues.AsNoTracking()
            .Where(value => weapons.Contains(value.PartTypeId) && !value.IsArchived)
            .Join(production.Characteristics.Where(characteristic => characteristic.Kind == CharacteristicKinds.Damage), value => value.CharacteristicId, characteristic => characteristic.Id, (value, _) => value.PartTypeId)
            .ToListAsync(Token);
        List<int> damageOnRounds = await production.PartCharacteristicValues.AsNoTracking()
            .Where(value => (value.PartTypeId == ids[SteelShell] || value.PartTypeId == ids[RailSlug]) && !value.IsArchived)
            .Join(production.Characteristics.Where(characteristic => characteristic.Kind == CharacteristicKinds.Damage), value => value.CharacteristicId, characteristic => characteristic.Id, (value, _) => value.PartTypeId)
            .ToListAsync(Token);

        Assert.Empty(damageOnWeapons);
        Assert.Equal(2, damageOnRounds.Count);
    }

    /// <summary>
    /// The damage of a project is the damage of the rounds its weapons fire, counted at the shots of
    /// the weapon: the least and the greatest of the rounds it accepts, summed over the weapons.
    /// A round the weapon does not fire never counts.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_damage_of_a_project_is_counted_from_the_rounds_its_weapons_fire()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, DesignBureau, 5);
        }

        using IServiceScope read = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> ids = await read.ServiceProvider.GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([Cannon, Autocannon, Railgun], Token);
        decimal steel = await DamageOfRoundAsync(read.ServiceProvider, SteelShell);
        decimal cobalt = await DamageOfRoundAsync(read.ServiceProvider, CobaltShell);
        decimal slug = await DamageOfRoundAsync(read.ServiceProvider, RailSlug);

        using JsonDocument design = await BuildDesignAsync(player, ids, [Cannon, Autocannon, Railgun]);
        JsonElement summary = design.RootElement.GetProperty("summary");
        decimal autocannonShots = await RateOfAsync(read.ServiceProvider, Autocannon);

        // The cannon fires the steel and the cobalt shell, the autocannon the same three times a round,
        // the railgun only the rail slug. The uranium torpedo, which the cannon once counted, is not there.
        decimal least = steel + (steel * autocannonShots) + slug;
        decimal greatest = cobalt + (cobalt * autocannonShots) + slug;

        Assert.Equal(least, summary.GetProperty("damage_min").GetDecimal());
        Assert.Equal(greatest, summary.GetProperty("damage_max").GetDecimal());
        Assert.Equal(least, summary.GetProperty("kinetic_damage_min").GetDecimal());
        Assert.Equal(greatest, summary.GetProperty("kinetic_damage_max").GetDecimal());
        Assert.Equal(0m, summary.GetProperty("energy_damage").GetDecimal());
    }

    /// <summary>
    /// The same count for the launchers: a launcher fires the missiles and the torpedoes bound to it,
    /// whatever their kind of damage, at the shots of the launcher.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_damage_of_a_launcher_is_counted_from_its_missiles_and_torpedoes()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, DesignBureau, 20);
        }

        using IServiceScope read = _factory.Services.CreateScope();
        string[] launchers = [LauncherX1, LauncherX1B, LauncherX2, LauncherX3];
        Dictionary<string, string[]> rounds = new()
        {
            [LauncherX1] = [ArrowX1, LightningX1],
            [LauncherX1B] = [ArrowX1, LightningX1, SpearX2],
            [LauncherX2] = [ValkyrieX3, ThermonuclearTorpedo, ProtonTorpedo],
            [LauncherX3] = [ThermonuclearTorpedo, ProtonTorpedo, UraniumTorpedo],
        };
        IReadOnlyDictionary<string, int> ids = await read.ServiceProvider.GetRequiredService<IProductionModule>().FindPartIdsByFileCodeAsync(launchers, Token);

        foreach (string launcher in launchers)
        {
            decimal shots = await RateOfAsync(read.ServiceProvider, launcher);
            List<decimal> damage = [];

            foreach (string round in rounds[launcher])
            {
                damage.Add(await DamageOfRoundAsync(read.ServiceProvider, round));
            }

            using JsonDocument design = await BuildDesignAsync(player, ids, [launcher]);
            JsonElement summary = design.RootElement.GetProperty("summary");

            Assert.Equal(damage.Min() * shots, summary.GetProperty("damage_min").GetDecimal());
            Assert.Equal(damage.Max() * shots, summary.GetProperty("damage_max").GetDecimal());
        }
    }

    /// <summary>
    /// Builds and reads a project of the weapons on the first hull that takes them with a drive and a
    /// reactor of enough power.
    /// </summary>
    private async Task<JsonDocument> BuildDesignAsync(PlanetsTestPlayer player, IReadOnlyDictionary<string, int> ids, string[] weaponCodes)
    {
        using JsonDocument catalog = await ReadCatalogAsync(player);
        JsonElement[] modules = [.. catalog.RootElement.GetProperty("modules").EnumerateArray()];
        JsonElement[] weapons = [.. weaponCodes.Select(code => modules.Single(module => module.GetProperty("id").GetInt32() == ids[code]))];
        decimal needed = weapons.Sum(weapon => weapon.GetProperty("energy_use").GetDecimal());

        JsonElement? chosen = null;
        JsonElement? chosenDrive = null;
        JsonElement? chosenReactor = null;

        foreach (JsonElement entry in catalog.RootElement.GetProperty("hulls").EnumerateArray())
        {
            int entryId = entry.GetProperty("id").GetInt32();
            JsonElement? entryDrive = Fitting(modules, entry, "drive", 0m);
            JsonElement? entryReactor = entryDrive is null ? null : Fitting(modules, entry, "reactor", needed + entryDrive.Value.GetProperty("energy_use").GetDecimal());

            if (entry.GetProperty("slots").GetInt32() >= 5
                && weapons.All(weapon => weapon.GetProperty("compatible_hull_ids").EnumerateArray().Any(id => id.GetInt32() == entryId))
                && entryDrive is not null
                && entryReactor is not null)
            {
                chosen = entry;
                chosenDrive = entryDrive;
                chosenReactor = entryReactor;
                break;
            }
        }

        Assert.NotNull(chosen);

        int hullId = chosen.Value.GetProperty("id").GetInt32();
        JsonElement drive = chosenDrive!.Value;
        JsonElement reactor = chosenReactor!.Value;

        long designId = await CreateAsync(player, hullId, [drive.GetProperty("id").GetInt32(), reactor.GetProperty("id").GetInt32(), .. weapons.Select(weapon => weapon.GetProperty("id").GetInt32())]);

        return await ReadDesignAsync(player, designId);
    }

    private static async Task<decimal> RateOfAsync(IServiceProvider services, string fileCode)
    {
        IReadOnlyDictionary<string, int> ids = await services.GetRequiredService<IProductionModule>().FindPartIdsByFileCodeAsync([fileCode], Token);
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();

        return await production.PartCharacteristicValues.AsNoTracking()
            .Where(value => value.PartTypeId == ids[fileCode])
            .Join(production.Characteristics.Where(characteristic => characteristic.Code == "characteristic_6"), value => value.CharacteristicId, characteristic => characteristic.Id, (value, _) => value.NumberValue)
            .SingleAsync(Token);
    }

    private static JsonElement? Fitting(JsonElement[] modules, JsonElement hull, string component, decimal power)
    {
        int hullId = hull.GetProperty("id").GetInt32();

        foreach (JsonElement module in modules.OrderBy(entry => entry.GetProperty("energy_use").GetDecimal()))
        {
            bool fits = module.GetProperty("compatible_hull_ids").EnumerateArray().Any(id => id.GetInt32() == hullId)
                && string.Equals(module.GetProperty("component").GetString(), component, StringComparison.Ordinal)
                && (component != "reactor" || module.GetProperty("power").GetDecimal() >= power);

            if (fits)
            {
                return module;
            }
        }

        return null;
    }

    private static async Task<decimal> DamageOfRoundAsync(IServiceProvider services, string fileCode)
    {
        IReadOnlyDictionary<string, int> ids = await services.GetRequiredService<IProductionModule>().FindPartIdsByFileCodeAsync([fileCode], Token);
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();

        return await production.PartCharacteristicValues.AsNoTracking()
            .Where(value => value.PartTypeId == ids[fileCode])
            .Join(production.Characteristics.Where(characteristic => characteristic.Kind == CharacteristicKinds.Damage), value => value.CharacteristicId, characteristic => characteristic.Id, (value, _) => value.NumberValue)
            .SingleAsync(Token);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }

    private static async Task<JsonDocument> ReadCatalogAsync(PlanetsTestPlayer player)
    {
        HttpResponseMessage response = await player.Client.GetAsync($"{Catalog}?planet_id={player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }

    private static async Task<JsonDocument> ReadDesignAsync(PlanetsTestPlayer player, long designId)
    {
        HttpResponseMessage response = await player.Client.GetAsync($"{Designs}/{designId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }

    private static async Task<long> CreateAsync(PlanetsTestPlayer player, int hullId, int[] parts)
    {
        HttpResponseMessage created = await TestAccounts.SendCommandAsync(
            player.Client,
            Designs,
            new { planet_id = player.PlanetId, name = "Калібр", hull_part_type_id = hullId, has_race_bonus_slot = false },
            Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token));
        long designId = body.RootElement.GetProperty("id").GetInt64();

        HttpResponseMessage saved = await TestAccounts.SendCommandAsync(
            player.Client,
            Designs + "/save",
            new
            {
                design_id = designId,
                hull_part_type_id = hullId,
                has_race_bonus_slot = false,
                slots = parts.Select((id, index) => new { slot_index = index, part_type_id = id, mark_level = 1, has_race_bonus = false }).ToArray(),
            },
            Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        return designId;
    }
}

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Core.Security;
using Aetheris.Core.Text;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the neutral codes of the reference rows, the texts in the database and the admin
/// screen «Тексти» (task 00022).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class NeutralCodeTests
{
    private const string Reason = "Text change in a test";

    private static readonly string[] Languages = ["uk", "en"];

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeutralCodeTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public NeutralCodeTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The starting data has only neutral codes, every name has a text in both languages, the
    /// group that was «Кристали» is named «Мінерали», and the new group «Кристали» with its
    /// resource and the hidden share of the fields exists as data (acceptance criterion 1,
    /// decision 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_starting_data_has_neutral_codes_and_texts_in_the_database()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        PlanetsDbContext planets = scope.ServiceProvider.GetRequiredService<PlanetsDbContext>();
        ILocalizationTexts texts = scope.ServiceProvider.GetRequiredService<ILocalizationTexts>();

        List<ResourceGroupEntity> groups = await world.ResourceGroups.AsNoTracking().ToListAsync(Token);
        List<ResourceTypeEntity> resources = await world.ResourceTypes.AsNoTracking().ToListAsync(Token);
        List<CelestialObjectTypeEntity> types = await world.ObjectTypes.AsNoTracking().ToListAsync(Token);
        List<PlanetSizeClassEntity> sizes = await world.SizeClasses.AsNoTracking().ToListAsync(Token);
        List<RaceEntity> races = await world.Races.AsNoTracking().ToListAsync(Token);
        List<GenerationRuleGroupEntity> ruleGroups = await world.RuleGroups.AsNoTracking().ToListAsync(Token);
        List<BuildingTypeEntity> buildings = await planets.BuildingTypes.AsNoTracking().ToListAsync(Token);

        Assert.All(groups, group => Assert.Matches(NeutralCode("resource_group"), group.Code));
        Assert.All(resources, resource => Assert.Matches(NeutralCode("resource"), resource.Code));
        Assert.All(types, type => Assert.Matches(NeutralCode("object_type"), type.Code));
        Assert.All(sizes, size => Assert.Matches(NeutralCode("size_class"), size.Code));
        Assert.All(races, race => Assert.Matches(NeutralCode("race"), race.Code));
        Assert.All(ruleGroups, group => Assert.Matches(NeutralCode("rule_group"), group.Code));
        Assert.All(buildings, building => Assert.Matches(NeutralCode("building"), building.Code));

        List<string> nameKeys =
        [
            .. groups.Select(group => group.NameKey),
            .. resources.Select(resource => resource.NameKey),
            .. resources.Select(resource => resource.DescriptionKey),
            .. types.Select(type => type.NameKey),
            .. sizes.Select(size => size.NameKey),
            .. races.Select(race => race.NameKey),
            .. races.Select(race => race.DescriptionKey),
            .. races.Select(race => race.MoodKey),
            .. races.SelectMany(race => race.TraitKeys),
            .. buildings.Select(building => building.NameKey),
            .. buildings.Select(building => building.DescriptionKey),
        ];

        foreach (string language in Languages)
        {
            LocalizationPack pack = await texts.GetPackAsync(language, Token);
            List<string> missing = [.. nameKeys.Where(key => !pack.Texts.ContainsKey(key))];

            Assert.True(missing.Count == 0, $"Keys without a text in {language}: {string.Join(", ", missing)}");
        }

        LocalizationPack ukrainian = await texts.GetPackAsync("uk", Token);
        ResourceGroupEntity minerals = groups.Single(group => group.Code == SeedCodes.MineralGroup);
        ResourceGroupEntity crystals = groups.Single(group => group.Code == SeedCodes.CrystalGroup);
        ResourceTypeEntity crystal = resources.Single(resource => resource.Code == SeedCodes.Crystal);

        Assert.Equal("Мінерали", ukrainian.Texts[minerals.NameKey]);
        Assert.Equal("Кристали", ukrainian.Texts[crystals.NameKey]);
        Assert.False(crystals.AllowedOnPlanet);
        Assert.Equal(crystals.Id, crystal.ResourceGroupId);
        Assert.All(
            types.Where(type => type.ObjectClass == WorldObjectClasses.AsteroidField),
            field =>
            {
                Assert.Equal(crystal.Id, field.HiddenResourceTypeId);
                Assert.Equal(0.04m, field.HiddenShareMin);
                Assert.Equal(0.1m, field.HiddenShareMax);
            });

        Assert.Equal(SeedCodes.Terrestrial, Assert.Single(types, type => type.IsStartDefault).Code);
        Assert.Equal(SeedCodes.AdminCenter, Assert.Single(buildings, building => building.IsStartBuilding).Code);
        Assert.All(races, race => Assert.Equal(buildings.Single(building => building.IsStartBuilding).Id, race.StartBuildingTypeId));
    }

    /// <summary>
    /// The pack of a language is public, carries a version and answers an unchanged request with
    /// 304; a language that is not a code is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_pack_of_a_language_is_public_and_versioned()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        using HttpResponseMessage first = await client.GetAsync("/api/v1/texts/en", Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.NotNull(first.Headers.ETag);

        using JsonDocument pack = JsonDocument.Parse(await first.Content.ReadAsStringAsync(Token));
        Assert.Equal("Iron", pack.RootElement.GetProperty("texts").GetProperty(ResourceNameKey(SeedCodes.Iron)).GetString());

        using HttpRequestMessage again = new(HttpMethod.Get, "/api/v1/texts/en");
        again.Headers.IfNoneMatch.Add(first.Headers.ETag);
        using HttpResponseMessage unchanged = await client.SendAsync(again, Token);

        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/texts/EN-1", Token)).StatusCode);
    }

    /// <summary>
    /// A resource renamed on the screen «Тексти» has the new name in the next pack at once,
    /// without a restart; the change needs a reason, keeps the old and the new text in the
    /// journal, and an older version gets 409 (acceptance criteria 2 and 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_renamed_resource_has_its_new_name_at_once()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        using HttpClient visitor = _factory.CreateBrowserClient();
        string key = ResourceNameKey(SeedCodes.Iron);

        using JsonDocument rows = await GetAsync(admin, $"/texts?search={key}&language=uk");
        JsonElement row = Assert.Single(rows.RootElement.GetProperty("items").EnumerateArray());
        int version = row.GetProperty("version").GetInt32();

        Assert.Equal("Залізо", row.GetProperty("text").GetString());
        Assert.Equal(Languages, rows.RootElement.GetProperty("languages").EnumerateArray().Select(language => language.GetString()));

        HttpResponseMessage withoutReason = await admin.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/texts",
            new { key, language = "uk", text = "Ферум", version, reason = string.Empty },
            Token);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);

        HttpResponseMessage renamed = await admin.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/texts",
            new { key, language = "uk", text = "Ферум", version, reason = Reason },
            Token);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        using JsonDocument pack = JsonDocument.Parse(await visitor.GetStringAsync("/api/v1/texts/uk", Token));
        Assert.Equal("Ферум", pack.RootElement.GetProperty("texts").GetProperty(key).GetString());

        HttpResponseMessage stale = await admin.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/texts",
            new { key, language = "uk", text = "Залізяка", version, reason = Reason },
            Token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        AdminAuditLogEntity audit = await LastAuditAsync(owner, "admin.text_updated");
        Assert.Equal("Залізо", audit.OldValue);
        Assert.Equal("Ферум", audit.NewValue);
        Assert.Equal(Reason, audit.Reason);

        HttpResponseMessage unknown = await admin.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/texts",
            new { key = "world.resource.nothing.name", language = "uk", text = "Ніщо", version = 0, reason = Reason },
            Token);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    /// <summary>
    /// The filter of the missing texts shows a key without a text in a language, and the screen
    /// adds that text with the version 0.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_missing_translation_is_listed_and_added()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        string key = ResourceNameKey(SeedCodes.Copper);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
            await main.LocalizationTexts.Where(text => text.Key == key && text.Language == "en").ExecuteDeleteAsync(Token);
            scope.ServiceProvider.GetRequiredService<ILocalizationTexts>().Invalidate();
        }

        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);

        using JsonDocument missing = await GetAsync(admin, "/texts?language=en&missing=true");
        JsonElement row = Assert.Single(missing.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(key, row.GetProperty("key").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("text").ValueKind);
        Assert.Equal(0, row.GetProperty("version").GetInt32());

        HttpResponseMessage added = await admin.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/texts",
            new { key, language = "en", text = "Copper", version = 0, reason = Reason },
            Token);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        using JsonDocument none = await GetAsync(admin, "/texts?language=en&missing=true");
        Assert.Empty(none.RootElement.GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// A new resource is added from the admin panel with its texts: the server gives the code,
    /// the name and the description are required in both languages, a deleted code is never
    /// given again, and twenty parallel creations get twenty different codes (acceptance
    /// criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_resource_gets_a_code_from_the_server_and_its_texts()
    {
        const int parallelCount = 20;

        await WorldTestData.ResetWorldAsync(_factory);

        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        int groupId = await GroupIdAsync(SeedCodes.MetalGroup);

        HttpResponseMessage incomplete = await admin.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/resources",
            NewResource(groupId, "Олово", "Tin", description: false),
            Token);
        await AssertFieldErrorAsync(incomplete, "description.uk", "required");

        HttpResponseMessage unknownGroup = await admin.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/resources",
            NewResource(int.MaxValue, "Олово", "Tin"),
            Token);
        await AssertFieldErrorAsync(unknownGroup, "resource_group_id", "unknown");

        HttpResponseMessage created = await admin.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/resources",
            NewResource(groupId, "Олово", "Tin"),
            Token);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        int resourceId = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("id").GetInt32();
        string code;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
            ResourceTypeEntity resource = await world.ResourceTypes.AsNoTracking().SingleAsync(entry => entry.Id == resourceId, Token);
            code = resource.Code;

            // The code comes from the counter of the server and is never given twice, so the
            // test checks the shape of the code and not its number: the number depends on the
            // resources that exist at that moment (task 00013).
            Assert.Matches("^resource_[0-9]+$", code);
            Assert.Equal(groupId, resource.ResourceGroupId);

            LocalizationPack english = await scope.ServiceProvider.GetRequiredService<ILocalizationTexts>().GetPackAsync("en", Token);
            Assert.Equal("Tin", english.Texts[resource.NameKey]);
            Assert.Equal("Tin description", english.Texts[resource.DescriptionKey]);

            // The row is removed by hand; its number is never given again.
            await world.ResourceTypes.Where(entry => entry.Id == resourceId).ExecuteDeleteAsync(Token);
        }

        AdminAuditLogEntity audit = await LastAuditAsync(owner, "admin.world_reference_row_added");
        Assert.Contains(code, audit.NewValue, StringComparison.Ordinal);
        Assert.Equal(Reason, audit.Reason);

        HttpResponseMessage[] parallel = await Task.WhenAll(Enumerable.Range(0, parallelCount).Select(index =>
            admin.PostAsJsonAsync(
                AdminEndpoints.Prefix + "/world/resources",
                NewResource(groupId, "Сплав " + index, "Alloy " + index),
                Token)));

        Assert.All(parallel, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            List<string> codes = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceTypes.AsNoTracking()
                .Where(resource => resource.Id > resourceId)
                .Select(resource => resource.Code)
                .ToListAsync(Token);

            Assert.Equal(parallelCount, codes.Distinct(StringComparer.Ordinal).Count());
            Assert.DoesNotContain(code, codes);
            Assert.All(codes, value => Assert.Matches("^resource_[0-9]+$", value));

            // The codes follow one another, so the counter of the server gives every row its
            // own number and never the same one twice (task 00013).
            int[] numbers = [.. codes
                .Select(value => int.Parse(value["resource_".Length..], CultureInfo.InvariantCulture))
                .OrderBy(number => number)];

            Assert.Equal(numbers[0] + parallelCount - 1, numbers[^1]);
            Assert.Equal(numbers.Length, numbers.Distinct().Count());
        }
    }

    /// <summary>
    /// The landing page reads the races without a sign-in: their keys, pictures and colours come
    /// from the data.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_races_are_public_with_their_keys_and_pictures()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient visitor = _factory.CreateBrowserClient();
        using JsonDocument races = JsonDocument.Parse(await visitor.GetStringAsync(WorldEndpoints.PublicRacesRoute, Token));
        JsonElement first = races.RootElement[0];

        Assert.Equal("world.race.race_1.name", first.GetProperty("name_key").GetString());
        Assert.Equal("world.race.race_1.description", first.GetProperty("description_key").GetString());
        Assert.Equal(2, first.GetProperty("trait_keys").GetArrayLength());
        Assert.Equal("art-07", first.GetProperty("image_key").GetString());
        Assert.Equal("race-1", first.GetProperty("color_key").GetString());
    }

    private static string ResourceNameKey(string code) => WorldTextKeys.Name(WorldTextKeys.Resource, code);

    private static object NewResource(int groupId, string ukrainian, string english, bool description = true) => new
    {
        reason = Reason,
        resource_group_id = groupId,
        name = new Dictionary<string, string> { ["uk"] = ukrainian, ["en"] = english },
        description = description
            ? new Dictionary<string, string> { ["uk"] = ukrainian + " опис", ["en"] = english + " description" }
            : new Dictionary<string, string> { ["en"] = english + " description" },
    };

    private static async Task<JsonDocument> GetAsync(HttpClient client, string path) =>
        JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + path, Token));

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, string field, string code)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using JsonDocument error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Assert.Contains(code, error.RootElement.GetProperty("details").GetProperty(field).EnumerateArray().Select(entry => entry.GetString()));
    }

    private async Task<int> GroupIdAsync(string code)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().ResourceGroups
            .Where(group => group.Code == code)
            .Select(group => group.Id)
            .SingleAsync(Token);
    }

    private async Task<AdminAuditLogEntity> LastAuditAsync(TestAdmin admin, string action)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        return await main.AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == admin.AdminId && record.Action == action)
            .OrderByDescending(record => record.Id)
            .FirstAsync(Token);
    }

    private static Regex NeutralCode(string group) => new("^" + group + "_[0-9]+$");
}

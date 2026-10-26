using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the world screens of the admin panel (task 00007): the generation of sectors in
/// the background and the tables of what was generated.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminGenerationTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminGenerationTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AdminGenerationTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The estimate, the start, the work in the background and the summary; a second run is
    /// refused while the first one is queued; the history and the journal show the run
    /// (acceptance criteria 4 and 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_generation_runs_in_the_background_and_only_one_at_a_time()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);

        using JsonDocument estimate = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + "/world/generation/estimate?sector_count=2", Token));
        Assert.Equal(2 * (12 + 20) / 2, estimate.RootElement.GetProperty("estimated_system_count").GetInt32());
        Assert.True(estimate.RootElement.GetProperty("allowed").GetBoolean());
        Assert.Equal(GenerationRuleSets.Current, estimate.RootElement.GetProperty("rule_sets")[0].GetProperty("code").GetString());

        HttpResponseMessage started = await StartAsync(client, 2, 4242);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        using JsonDocument queued = JsonDocument.Parse(await started.Content.ReadAsStringAsync(Token));
        long runId = queued.RootElement.GetProperty("id").GetInt64();
        Assert.Equal(GenerationRunStatuses.Queued, queued.RootElement.GetProperty("status").GetString());

        HttpResponseMessage second = await StartAsync(client, 1, null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(WorldErrorCodes.GenerationRunning, await TestAccounts.ReadErrorAsync(second));

        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.Zero);

        using JsonDocument done = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + $"/world/generation-runs/{runId}", Token));
        JsonElement run = done.RootElement;
        Assert.Equal(GenerationRunStatuses.Succeeded, run.GetProperty("status").GetString());

        List<int> sectorIds = [.. run.GetProperty("summary").GetProperty("sector_ids").EnumerateArray().Select(id => id.GetInt32())];
        Assert.Equal(2, sectorIds.Count);
        Assert.Equal(4242, run.GetProperty("summary").GetProperty("sectors")[0].GetProperty("seed").GetInt64());

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

            int systems = await world.Systems.CountAsync(system => sectorIds.Contains(system.SectorId), Token);
            int gasGiants = await world.Objects
                .Where(entry => sectorIds.Contains(entry.SectorId))
                .Join(world.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => type.ObjectClass)
                .CountAsync(objectClass => objectClass == WorldObjectClasses.GasGiant, Token);
            int fields = await world.Fields.CountAsync(field => sectorIds.Contains(field.SectorId), Token);

            JsonElement byClass = run.GetProperty("summary").GetProperty("object_counts_by_class");
            Assert.Equal(systems, run.GetProperty("summary").GetProperty("system_count").GetInt32());
            Assert.InRange(systems, 2 * 12, 2 * 20);
            Assert.Equal(gasGiants, byClass.TryGetProperty(WorldObjectClasses.GasGiant, out JsonElement giants) ? giants.GetInt32() : 0);
            Assert.Equal(fields, byClass.GetProperty(WorldObjectClasses.AsteroidField).GetInt32());
            Assert.True(byClass.GetProperty(WorldObjectClasses.Planet).GetInt32() > 0);

            AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
            AdminAuditLogEntity audit = await main.AdminAuditLogs.AsNoTracking()
                .SingleAsync(record => record.AdminId == owner.AdminId && record.Action == "admin.world_generation_started", Token);
            Assert.Equal("generation_run:" + runId, audit.Target);
            Assert.Contains("seed=4242", audit.NewValue, StringComparison.Ordinal);
            Assert.True(await main.AdminAuditLogs.AnyAsync(
                record => record.AdminId == owner.AdminId && record.Action == "admin.world_generation_started.rejected",
                Token));
        }

        using JsonDocument history = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + "/world/generation-runs", Token));
        JsonElement row = Assert.Single(history.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(owner.AdminId, row.GetProperty("initiator_id").GetString());
        Assert.Equal(2, row.GetProperty("sector_count").GetInt32());
        Assert.Equal(4242, row.GetProperty("seed").GetInt64());

        // The lock is released: the next run may start.
        Assert.Equal(HttpStatusCode.Accepted, (await StartAsync(client, 1, null)).StatusCode);
    }

    /// <summary>
    /// A run larger than the limits of the configuration and an unknown rule set are refused,
    /// and a role without the permission cannot start a run.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_generation_beyond_the_limits_is_refused()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);

        HttpResponseMessage tooMany = await StartAsync(client, 7, null);
        HttpResponseMessage unknownRules = await client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/generation-runs",
            new { sector_count = 1, rule_set = "legacy" },
            Token);

        TestAdmin balance = await TestAdmins.CreateAsync(_factory, AdminRoles.Balance);
        using HttpClient balanceClient = await TestAdmins.SignInAsync(_factory, balance);
        HttpResponseMessage forbidden = await StartAsync(balanceClient, 1, null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMany.StatusCode);
        Assert.Equal(WorldErrorCodes.GenerationTooLarge, await TestAccounts.ReadErrorAsync(tooMany));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownRules.StatusCode);
        Assert.Equal(WorldErrorCodes.RuleSetUnknown, await TestAccounts.ReadErrorAsync(unknownRules));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<WorldDbContext>().GenerationRuns.AnyAsync(Token));
    }

    /// <summary>
    /// The same seed gives the same sectors: the same systems at the same places with the same
    /// bodies and deposits (acceptance criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_same_seed_gives_the_same_sectors()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);

        List<int> first = await GenerateAsync(client, 2, 9001);
        List<int> second = await GenerateAsync(client, 2, 9001);

        Assert.Equal(await DescribeAsync(first[0]), await DescribeAsync(second[0]));
        Assert.Equal(await DescribeAsync(first[1]), await DescribeAsync(second[1]));
        Assert.NotEqual(await DescribeAsync(first[0]), await DescribeAsync(first[1]));
    }

    /// <summary>
    /// The tables of sectors, systems, bodies and fields open, are searched, filtered and paged,
    /// and match the database (acceptance criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_world_tables_are_searched_filtered_and_paged()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        SectorEntity sector = await world.Sectors.OrderBy(entry => entry.Id).FirstAsync(Token);
        int systemCount = await world.Systems.CountAsync(system => system.SectorId == sector.Id, Token);

        using JsonDocument sectors = await GetAsync(client, "/world/sectors?search=" + Uri.EscapeDataString(sector.Name));
        JsonElement sectorRow = Assert.Single(sectors.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(sector.Id, sectorRow.GetProperty("id").GetInt32());
        Assert.Equal(systemCount, sectorRow.GetProperty("system_count").GetInt32());

        using JsonDocument systems = await GetAsync(client, $"/world/systems?sector_id={sector.Id}&page=2&page_size=5&sort=number");
        Assert.Equal(systemCount, systems.RootElement.GetProperty("total").GetInt32());
        List<int> numbers = [.. systems.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("number").GetInt32())];
        Assert.Equal([6, 7, 8, 9, 10], numbers);

        StarSystemEntity system = await world.Systems.Where(entry => entry.SectorId == sector.Id).OrderBy(entry => entry.Number).FirstAsync(Token);
        int bodies = await world.Planets.CountAsync(planet => planet.SystemId == system.Id, Token);

        using JsonDocument bodyRows = await GetAsync(client, $"/world/objects?system_id={system.Id}&page_size=100");
        Assert.Equal(bodies, bodyRows.RootElement.GetProperty("total").GetInt32());
        Assert.All(bodyRows.RootElement.GetProperty("items").EnumerateArray(), item =>
        {
            Assert.Equal(system.Id, item.GetProperty("system_id").GetInt64());
            Assert.Contains(item.GetProperty("object_class").GetString(), WorldTestData.BodyClasses);
        });

        using JsonDocument giants = await GetAsync(client, $"/world/objects?sector_id={sector.Id}&object_class={WorldObjectClasses.GasGiant}&page_size=100");
        Assert.All(giants.RootElement.GetProperty("items").EnumerateArray(), item =>
            Assert.Equal(WorldObjectClasses.GasGiant, item.GetProperty("object_class").GetString()));

        int freeFields = await world.Fields.CountAsync(field => field.SectorId == sector.Id && field.SystemId == null, Token);
        using JsonDocument fields = await GetAsync(client, $"/world/fields?sector_id={sector.Id}&free=true&page_size=100");
        Assert.Equal(freeFields, fields.RootElement.GetProperty("total").GetInt32());

        JsonElement field = fields.RootElement.GetProperty("items")[0];
        long fieldId = field.GetProperty("id").GetInt64();
        int shares = await world.FieldCompositions.CountAsync(share => share.AsteroidFieldId == fieldId, Token);
        Assert.Equal(shares, field.GetProperty("composition_ids").GetArrayLength());
        Assert.True(shares > 0);

        using JsonDocument byId = await GetAsync(client, $"/world/fields?search={fieldId}");
        Assert.Equal(fieldId, Assert.Single(byId.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt64());
    }

    /// <summary>
    /// No answer of the admin panel carries a password hash, a token, a security stamp or an
    /// e-mail address (acceptance criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task No_admin_answer_carries_sensitive_data()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, owner);

        await GenerateAsync(client, 1, 77);
        await client.PutAsJsonAsync(AdminEndpoints.Prefix + "/config/" + WorldConfigKeys.MaxSectorsPerRun, new { value = "9", reason = "Sensitive data test" }, Token);

        string[] paths =
        [
            "/auth/me",
            "/config?page_size=500",
            $"/config/{WorldConfigKeys.MaxSectorsPerRun}/history",
            "/clock",
            "/audit?page_size=500",
            "/world/reference",
            "/world/sectors",
            "/world/systems",
            "/world/objects",
            "/world/fields",
            "/world/generation/estimate",
            "/world/generation-runs",
        ];

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        List<string?> hashes = await main.Users.Select(user => user.PasswordHash).ToListAsync(Token);
        List<string?> stamps = await main.Users.Select(user => user.SecurityStamp).ToListAsync(Token);
        List<string> tokens = await main.RefreshTokens.Select(token => token.TokenHash).ToListAsync(Token);

        foreach (string path in paths)
        {
            HttpResponseMessage response = await client.GetAsync(AdminEndpoints.Prefix + path, Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string body = await response.Content.ReadAsStringAsync(Token);

            Assert.DoesNotContain(player.Email, body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(owner.Account.Email, body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"password_hash\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"security_stamp\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token_hash\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"email\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.All(hashes.OfType<string>(), hash => Assert.DoesNotContain(hash, body, StringComparison.Ordinal));
            Assert.All(stamps.OfType<string>(), stamp => Assert.DoesNotContain(stamp, body, StringComparison.Ordinal));
            Assert.All(tokens, token => Assert.DoesNotContain(token, body, StringComparison.Ordinal));
        }
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, int sectorCount, long? seed) =>
        client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/world/generation-runs",
            new { sector_count = sectorCount, seed, rule_set = GenerationRuleSets.Current },
            Token);

    private static async Task<JsonDocument> GetAsync(HttpClient client, string path)
    {
        HttpResponseMessage response = await client.GetAsync(AdminEndpoints.Prefix + path, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
    }

    private async Task<List<int>> GenerateAsync(HttpClient client, int sectorCount, long seed)
    {
        HttpResponseMessage started = await StartAsync(client, sectorCount, seed);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);

        using JsonDocument queued = JsonDocument.Parse(await started.Content.ReadAsStringAsync(Token));
        long runId = queued.RootElement.GetProperty("id").GetInt64();

        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.Zero);

        using JsonDocument done = await GetAsync(client, $"/world/generation-runs/{runId}");
        Assert.Equal(GenerationRunStatuses.Succeeded, done.RootElement.GetProperty("status").GetString());

        return [.. done.RootElement.GetProperty("summary").GetProperty("sector_ids").EnumerateArray().Select(id => id.GetInt32())];
    }

    private async Task<List<string>> DescribeAsync(int sectorId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        var systems = await world.Systems.AsNoTracking()
            .Where(system => system.SectorId == sectorId)
            .OrderBy(system => system.Number)
            .Select(system => new { system.Id, system.Number, system.X, system.Y, system.Z })
            .ToListAsync(Token);

        var bodies = await world.Planets.AsNoTracking()
            .Where(planet => planet.SectorId == sectorId)
            .Join(world.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new
            {
                planet.Id,
                planet.SystemId,
                entry.Orbit,
                entry.ObjectTypeId,
                planet.SizeClassId,
                planet.Size,
            })
            .ToListAsync(Token);

        var deposits = await world.Deposits.AsNoTracking()
            .Where(deposit => deposit.SectorId == sectorId)
            .Select(deposit => new { deposit.PlanetId, deposit.ResourceId, deposit.Coefficient })
            .ToListAsync(Token);

        Dictionary<long, int> numberOfSystem = systems.ToDictionary(system => system.Id, system => system.Number);

        return
        [
            .. systems.Select(system => $"system {system.Number} {system.X} {system.Y} {system.Z}"),
            .. bodies
                .Select(body => (Key: $"{numberOfSystem[body.SystemId]}/{body.Orbit}", Body: body))
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => $"body {entry.Key} {entry.Body.ObjectTypeId} {entry.Body.SizeClassId} {entry.Body.Size} "
                    + string.Join(',', deposits
                        .Where(deposit => deposit.PlanetId == entry.Body.Id)
                        .OrderBy(deposit => deposit.ResourceId)
                        .Select(deposit => $"{deposit.ResourceId}:{deposit.Coefficient}"))),
        ];
    }
}

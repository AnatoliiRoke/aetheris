using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the world API: the actions of an administrator need the role and the second
/// factor and write the audit log and the generation log (task 00004, acceptance criteria 7
/// and 10); the reads and the start command of a player pass the pipeline of the core.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class WorldApiTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldApiTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public WorldApiTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Without an administrator session and without the owner role the galaxy does not grow;
    /// both refusals leave the world unchanged.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Adding_a_sector_needs_the_owner_role_and_the_second_factor()
    {
        await WorldTestData.ResetWorldAsync(_factory);
        int sectorsBefore = await CountSectorsAsync();

        using HttpClient anonymous = _factory.CreateBrowserClient();
        await TestAccounts.UseCsrfTokenAsync(anonymous);
        HttpResponseMessage withoutSession = await anonymous.PostAsJsonAsync(AdminEndpoints.Prefix + "/world/sectors", new { }, Token);

        (TestAccount balance, string balanceKey) = await CreateAdminAsync(AdminRoles.Balance);
        using HttpClient balanceClient = await SignInAdminAsync(balance, balanceKey);
        HttpResponseMessage wrongRole = await balanceClient.PostAsJsonAsync(AdminEndpoints.Prefix + "/world/sectors", new { }, Token);

        using HttpClient player = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(player, await TestAccounts.RegisterConfirmedAsync(_factory));
        HttpResponseMessage asPlayer = await player.PostAsJsonAsync(AdminEndpoints.Prefix + "/world/sectors", new { }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutSession.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
        Assert.True(asPlayer.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        Assert.Equal(sectorsBefore, await CountSectorsAsync());
    }

    /// <summary>
    /// The owner adds a sector at the edge of the galaxy; the action is in the audit log and in
    /// the generation log, and the statistics show the new sector (acceptance criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_owner_adds_a_sector_at_the_edge_and_the_action_is_logged()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        (TestAccount owner, string key) = await CreateAdminAsync(AdminRoles.Owner);
        using HttpClient client = await SignInAdminAsync(owner, key);
        string adminId = await ReadAdminIdAsync(owner);

        HttpResponseMessage response = await client.PostAsJsonAsync(AdminEndpoints.Prefix + "/world/sectors", new { seed = 777 }, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        int sectorId = body.RootElement.GetProperty("id").GetInt32();
        int layoutIndex = body.RootElement.GetProperty("layout_index").GetInt32();

        Assert.Equal(777, body.RootElement.GetProperty("seed").GetInt64());

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        AetherisDbContext main = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        int outermostBefore = (await world.Sectors.Where(sector => sector.Id != sectorId).Select(sector => sector.LayoutIndex).ToListAsync(Token))
            .Max(SectorLayout.Ring);
        Assert.True(SectorLayout.Ring(layoutIndex) >= outermostBefore);

        AdminAuditLogEntity audit = Assert.Single(await main.AdminAuditLogs
            .Where(record => record.AdminId == adminId && record.Action == "admin.world_sector_added")
            .ToListAsync(Token));
        Assert.Equal("sector:" + sectorId, audit.Target);
        Assert.Equal("777", audit.NewValue);

        WorldGenerationLogEntity log = await world.GenerationLog.SingleAsync(
            entry => entry.SectorId == sectorId && entry.Action == WorldGenerationActions.SectorGenerated,
            Token);
        Assert.Equal(adminId, log.ActorId);
        Assert.Equal(777, log.Seed);

        HttpResponseMessage stats = await client.GetAsync(AdminEndpoints.Prefix + $"/world/sectors/{sectorId}/stats", Token);
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);

        using JsonDocument statistics = JsonDocument.Parse(await stats.Content.ReadAsStringAsync(Token));
        Assert.True(statistics.RootElement.GetProperty("system_count").GetInt32() > 0);
        Assert.True(statistics.RootElement.GetProperty("free_colonizable_count").GetInt32() > 0);
        Assert.True(statistics.RootElement.GetProperty("field_count").GetInt32() > 0);
        Assert.Equal(1, statistics.RootElement.GetProperty("nebula_count").GetInt32());
    }

    /// <summary>
    /// An empty sector is generated again with a new seed; a sector with an owner is refused,
    /// and the refusal is audited too.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Only_an_empty_sector_is_generated_again()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        (TestAccount owner, string key) = await CreateAdminAsync(AdminRoles.Owner);
        using HttpClient client = await SignInAdminAsync(owner, key);
        string adminId = await ReadAdminIdAsync(owner);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<Aetheris.World.IWorldModule>().AllocateStartPlanetAsync("resident", Token);
        }

        HttpResponseMessage refused = await client.PostAsJsonAsync(AdminEndpoints.Prefix + "/world/sectors/1/regenerate", new { seed = 5 }, Token);
        HttpResponseMessage regenerated = await client.PostAsJsonAsync(AdminEndpoints.Prefix + "/world/sectors/3/regenerate", new { seed = 5 }, Token);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);

        using IServiceScope check = _factory.Services.CreateScope();
        WorldDbContext world = check.ServiceProvider.GetRequiredService<WorldDbContext>();
        AetherisDbContext main = check.ServiceProvider.GetRequiredService<AetherisDbContext>();

        Assert.Equal(5, (await world.Sectors.SingleAsync(sector => sector.Id == 3, Token)).Seed);
        Assert.True(await world.Systems.AnyAsync(system => system.SectorId == 3, Token));
        Assert.True(await world.GenerationLog.AnyAsync(entry => entry.SectorId == 3 && entry.Action == WorldGenerationActions.SectorRegenerated, Token));
        Assert.True(await main.AdminAuditLogs.AnyAsync(record => record.AdminId == adminId && record.Action == "admin.world_sector_regenerated.rejected", Token));
        Assert.True(await main.AdminAuditLogs.AnyAsync(record => record.AdminId == adminId && record.Action == "admin.world_sector_regenerated", Token));
    }

    /// <summary>
    /// A newcomer takes the start body through the command pipeline and then sees it, and
    /// only it, through the read API in snake_case JSON.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_newcomer_takes_the_start_body_and_sees_only_it()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient anonymous = _factory.CreateBrowserClient();
        HttpResponseMessage closed = await anonymous.GetAsync(WorldEndpoints.Prefix + "/sectors", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, closed.StatusCode);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        HttpResponseMessage start = await TestAccounts.SendCommandAsync(client, AllocateStartPlanetCommand.RoutePath, new { }, "start-1");
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using JsonDocument result = JsonDocument.Parse(await start.Content.ReadAsStringAsync(Token));
        long planetId = result.RootElement.GetProperty("planet_id").GetInt64();
        long systemId = result.RootElement.GetProperty("system_id").GetInt64();
        int sectorId = result.RootElement.GetProperty("sector_id").GetInt32();

        HttpResponseMessage repeated = await TestAccounts.SendCommandAsync(client, AllocateStartPlanetCommand.RoutePath, new { }, "start-2");
        using JsonDocument again = JsonDocument.Parse(await repeated.Content.ReadAsStringAsync(Token));
        Assert.Equal(planetId, again.RootElement.GetProperty("planet_id").GetInt64());
        Assert.True(again.RootElement.GetProperty("already_allocated").GetBoolean());

        using JsonDocument sectors = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + "/sectors", Token));
        using JsonDocument systems = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + $"/sectors/{sectorId}/systems", Token));
        using JsonDocument content = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + $"/systems/{systemId}", Token));
        using JsonDocument reference = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + "/reference", Token));

        Assert.True(sectors.RootElement.GetArrayLength() >= 1);
        Assert.Equal(systemId, Assert.Single(systems.RootElement.EnumerateArray()).GetProperty("id").GetInt64());

        JsonElement own = Assert.Single(content.RootElement.GetProperty("objects").EnumerateArray());
        Assert.Equal(planetId, own.GetProperty("planet_id").GetInt64());
        Assert.NotEqual(0, own.GetProperty("deposit_ids").GetArrayLength());
        Assert.Equal(0d, own.GetProperty("z").GetDouble());
        Assert.NotEqual(0, reference.RootElement.GetProperty("resources").GetArrayLength());

        long otherSystem = await FirstOtherSystemAsync(systemId);
        HttpResponseMessage hidden = await client.GetAsync(WorldEndpoints.Prefix + $"/systems/{otherSystem}", Token);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    private async Task<long> FirstOtherSystemAsync(long systemId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Systems
            .Where(system => system.Id != systemId)
            .Select(system => system.Id)
            .FirstAsync(Token);
    }

    private async Task<int> CountSectorsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Sectors.CountAsync(Token);
    }

    private async Task<(TestAccount Account, string Key)> CreateAdminAsync(string role)
    {
        string email = TestAccounts.NewEmail();

        using IServiceScope scope = _factory.Services.CreateScope();
        string key = await scope.ServiceProvider.GetRequiredService<AdminAccountService>()
            .CreateAsync(email, TestAccounts.StrongPassword, "World administrator", [role]);

        return (new TestAccount(email, TestAccounts.StrongPassword, string.Empty), key);
    }

    private async Task<HttpClient> SignInAdminAsync(TestAccount admin, string key)
    {
        HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/auth/login",
            new { email = admin.Email, password = admin.Password, code = TestAccounts.ComputeTotp(key) },
            Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await TestAccounts.UseCsrfTokenAsync(client);

        return client;
    }

    private async Task<string> ReadAdminIdAsync(TestAccount admin)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        return await dbContext.AdminAccounts
            .Where(account => dbContext.Users.Any(user => user.Id == account.IdentityUserId && user.Email == admin.Email))
            .Select(account => account.Id)
            .SingleAsync(Token);
    }
}

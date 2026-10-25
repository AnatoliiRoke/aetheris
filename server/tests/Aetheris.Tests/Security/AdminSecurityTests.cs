using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the admin panel protection.
/// </summary>
/// <remarks>
/// Acceptance criterion 7 of task 00002: no administrator session without the one-time
/// code, every admin action writes an audit record, and the application cannot change or
/// remove an audit record.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class AdminSecurityTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminSecurityTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AdminSecurityTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// The right password without a code, and with a wrong code, does not sign in.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_administrator_cannot_sign_in_without_the_one_time_code()
    {
        (TestAccount admin, string key) = await CreateAdminAsync(AdminRoles.Owner);

        using HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage withoutCode = await AdminLoginAsync(client, admin, code: null);
        HttpResponseMessage wrongCode = await AdminLoginAsync(client, admin, WrongCode(TestAccounts.ComputeTotp(key)));

        Assert.Equal(HttpStatusCode.Unauthorized, withoutCode.StatusCode);
        Assert.Equal(GameErrorCodes.SecondFactorRequired, await TestAccounts.ReadErrorAsync(withoutCode));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCode.StatusCode);
        Assert.Equal(GameErrorCodes.SecondFactorRequired, await TestAccounts.ReadErrorAsync(wrongCode));

        HttpResponseMessage ping = await client.GetAsync(AdminEndpoints.Prefix + "/ping", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, ping.StatusCode);

        HttpResponseMessage withCode = await AdminLoginAsync(client, admin, TestAccounts.ComputeTotp(key));
        Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
    }

    /// <summary>
    /// Every admin action writes an audit record with the old and the new value.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_admin_action_writes_an_audit_record()
    {
        (TestAccount admin, string key) = await CreateAdminAsync(AdminRoles.Balance);
        using HttpClient client = await SignInAdminAsync(admin, key);

        string adminId = await ReadAdminIdAsync(admin);

        HttpResponseMessage ping = await client.GetAsync(AdminEndpoints.Prefix + "/ping", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, ping.StatusCode);

        HttpResponseMessage change = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/config/" + ConfigSeed.HelloMultiplier,
            new { value = "5" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        List<AdminAuditLogEntity> records = await dbContext.AdminAuditLogs
            .Where(record => record.AdminId == adminId)
            .OrderBy(record => record.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains(records, record => record.Action == "admin.sign_in");
        Assert.Contains(records, record => record.Action == "admin.ping");

        AdminAuditLogEntity changeRecord = Assert.Single(records, record => record.Action == "admin.config_changed");
        Assert.Equal(ConfigSeed.HelloMultiplier, changeRecord.Target);
        Assert.Equal("5", changeRecord.NewValue);
        Assert.NotNull(changeRecord.OldValue);

        // The change is applied in memory once it is stored.
        Assert.Equal(5, scope.ServiceProvider.GetRequiredService<IGameConfig>().GetInt64(ConfigSeed.HelloMultiplier));

        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ConfigSeed.HelloMultiplier, changeRecord.OldValue);
    }

    /// <summary>
    /// A value outside the bounds and an action of another role are refused, and the
    /// refusal is audited too.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Rights_follow_the_administrator_roles()
    {
        (TestAccount admin, string key) = await CreateAdminAsync(AdminRoles.Balance);
        using HttpClient client = await SignInAdminAsync(admin, key);

        HttpResponseMessage clock = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/clock/multiplier",
            new { multiplier = 2 },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, clock.StatusCode);

        HttpResponseMessage outOfBounds = await client.PutAsJsonAsync(
            AdminEndpoints.Prefix + "/config/" + ConfigSeed.HelloMultiplier,
            new { value = "-5" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, outOfBounds.StatusCode);

        string adminId = await ReadAdminIdAsync(admin);
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        Assert.True(await dbContext.AdminAuditLogs.AnyAsync(
            record => record.AdminId == adminId && record.Action == "admin.config_changed.rejected",
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An audit record cannot be changed or removed: neither through the application nor
    /// through SQL.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_audit_log_cannot_be_changed_or_removed()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        AdminAuditLogEntity record = new()
        {
            AdminId = "test-admin",
            Action = "test.action",
            NewValue = "original",
            At = DateTimeOffset.UtcNow,
        };

        dbContext.AdminAuditLogs.Add(record);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        record.NewValue = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        dbContext.Entry(record).State = EntityState.Unchanged;

        dbContext.AdminAuditLogs.Remove(record);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        dbContext.ChangeTracker.Clear();

        long id = record.Id;

        PostgresException update = await Assert.ThrowsAsync<PostgresException>(() => dbContext.Database.ExecuteSqlAsync(
            $"UPDATE admin_audit_log SET new_value = 'tampered' WHERE id = {id}",
            TestContext.Current.CancellationToken));
        PostgresException delete = await Assert.ThrowsAsync<PostgresException>(() => dbContext.Database.ExecuteSqlAsync(
            $"DELETE FROM admin_audit_log WHERE id = {id}",
            TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, update.SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, delete.SqlState);

        AdminAuditLogEntity stored = await dbContext.AdminAuditLogs
            .AsNoTracking()
            .SingleAsync(entry => entry.Id == id, TestContext.Current.CancellationToken);

        Assert.Equal("original", stored.NewValue);
    }

    private async Task<(TestAccount Account, string Key)> CreateAdminAsync(string role)
    {
        string email = TestAccounts.NewEmail();

        using IServiceScope scope = _factory.Services.CreateScope();
        AdminAccountService accounts = scope.ServiceProvider.GetRequiredService<AdminAccountService>();

        string key = await accounts.CreateAsync(email, TestAccounts.StrongPassword, "Test administrator", [role]);

        return (new TestAccount(email, TestAccounts.StrongPassword, string.Empty), key);
    }

    private async Task<HttpClient> SignInAdminAsync(TestAccount admin, string key)
    {
        HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage response = await AdminLoginAsync(client, admin, TestAccounts.ComputeTotp(key));
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
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> AdminLoginAsync(HttpClient client, TestAccount admin, string? code) =>
        client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/auth/login",
            new { email = admin.Email, password = admin.Password, code },
            TestContext.Current.CancellationToken);

    private static string WrongCode(string rightCode)
    {
        int value = int.Parse(rightCode, CultureInfo.InvariantCulture);

        return ((value + 1) % 1_000_000).ToString("000000", CultureInfo.InvariantCulture);
    }
}

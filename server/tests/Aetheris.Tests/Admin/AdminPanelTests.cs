using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Admin;

/// <summary>
/// Tests of the admin panel of task 00007: the access, the session, the sign-in limits, the
/// switch of the second factor, the roles and permissions, and the configuration parameters.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AdminPanelTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminPanelTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AdminPanelTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Without a sign-in every admin request is refused, and the session of a player opens none
    /// of them (acceptance criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_admin_request_is_refused_without_an_administrator_session()
    {
        List<RouteEndpoint> endpoints = AdminRoutes();
        Assert.True(endpoints.Count > 20, "The admin routes were not found.");

        using HttpClient anonymous = _factory.CreateBrowserClient();
        await TestAccounts.UseCsrfTokenAsync(anonymous);

        using HttpClient player = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(player, await TestAccounts.RegisterConfirmedAsync(_factory));

        List<string> open = [];

        foreach (RouteEndpoint endpoint in endpoints)
        {
            foreach ((string caller, HttpClient client) in new[] { ("anonymous", anonymous), ("player", player) })
            {
                HttpResponseMessage response = await SendAsync(client, endpoint);

                if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden))
                {
                    open.Add($"{caller}: {endpoint.RoutePattern.RawText} answered {(int)response.StatusCode}");
                }
            }
        }

        Assert.True(open.Count == 0, string.Join(Environment.NewLine, open));
    }

    /// <summary>
    /// The session ends after the idle time of the configuration, not after a fixed one
    /// (acceptance criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_session_ends_after_the_configured_idle_time()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        string? previous = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.AdminSessionMinutes, "5");

        try
        {
            TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);

            using HttpClient recent = await TestAdmins.SignInAsync(_factory, admin);
            await MoveLastActivityAsync(admin.AdminId, TimeSpan.FromMinutes(4));
            HttpResponseMessage stillOpen = await recent.GetAsync(AdminEndpoints.Prefix + "/ping", Token);

            using HttpClient idle = await TestAdmins.SignInAsync(_factory, admin);
            await MoveLastActivityAsync(admin.AdminId, TimeSpan.FromMinutes(6));
            HttpResponseMessage expired = await idle.GetAsync(AdminEndpoints.Prefix + "/ping", Token);
            HttpResponseMessage afterwards = await idle.GetAsync(AdminEndpoints.Prefix + "/ping", Token);

            Assert.Equal(HttpStatusCode.OK, stillOpen.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
            Assert.Equal(AdminErrorCodes.SessionExpired, await TestAccounts.ReadErrorAsync(expired));
            Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);

            AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
            Assert.Contains(
                await dbContext.AdminSessions.AsNoTracking().Where(session => session.AdminAccountId == admin.AdminId).ToListAsync(Token),
                session => session.EndReason == AdminSessionEndReasons.Idle && session.EndedAt != null);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.AdminSessionMinutes, previous!);
        }
    }

    /// <summary>
    /// The sign-out ends the session on the server: the old cookie opens nothing afterwards.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_sign_out_ends_the_session_on_the_server()
    {
        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        HttpClient client = await TestAdmins.SignInAsync(_factory, admin);

        HttpResponseMessage signOut = await client.PostAsync(AdminEndpoints.Prefix + "/auth/logout", null, Token);
        HttpResponseMessage afterwards = await client.GetAsync(AdminEndpoints.Prefix + "/ping", Token);

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        AdminSessionEntity session = await dbContext.AdminSessions.AsNoTracking().SingleAsync(entry => entry.AdminAccountId == admin.AdminId, Token);

        Assert.Equal(AdminSessionEndReasons.SignedOut, session.EndReason);
        client.Dispose();
    }

    /// <summary>
    /// An administrator account is locked after fewer failures than a player account, and the
    /// sign-in of the admin panel has its own, stricter rate limit (acceptance criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_administrator_sign_in_has_stricter_limits_than_the_player_one()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        SecuritySettings settings = scope.ServiceProvider.GetRequiredService<SecuritySettings>();
        Assert.True(settings.AdminLoginMaxFailedAttempts < settings.LoginMaxFailedAttempts);
        Assert.True(
            int.Parse(ConfigSeed.Find(SecurityKeys.RateLimitPermits(RateLimitGroups.AdminAuth))!.DefaultValue, System.Globalization.CultureInfo.InvariantCulture)
            < int.Parse(ConfigSeed.Find(SecurityKeys.RateLimitPermits(RateLimitGroups.Auth))!.DefaultValue, System.Globalization.CultureInfo.InvariantCulture));

        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = _factory.CreateBrowserClient();

        for (int attempt = 0; attempt < settings.AdminLoginMaxFailedAttempts; attempt++)
        {
            HttpResponseMessage wrong = await TestAdmins.LoginAsync(client, admin, password: "Wrong-password-" + attempt + "-xyz");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        HttpResponseMessage locked = await TestAdmins.LoginAsync(client, admin, TestAccounts.ComputeTotp(admin.Key));

        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal(GameErrorCodes.AccountLocked, await TestAccounts.ReadErrorAsync(locked));

        string permits = SecurityKeys.RateLimitPermits(RateLimitGroups.AdminAuth);
        string? previous = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, permits, "2");

        try
        {
            TestAdmin other = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
            List<HttpStatusCode> statuses = [];

            // Every attempt comes from a new browser of the same address: the limit counts the address.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                using HttpClient browser = _factory.CreateBrowserClient();
                statuses.Add((await TestAdmins.LoginAsync(browser, other, TestAccounts.ComputeTotp(other.Key))).StatusCode);
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, permits, previous!);
        }
    }

    /// <summary>
    /// The switch of the second factor: off, the password is enough; on, the one-time code is
    /// required, and a session opened without it ends (acceptance criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_second_factor_follows_its_switch()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);

        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.AdminRequireTotp, "false");

        using HttpClient withoutCode = _factory.CreateBrowserClient();
        HttpResponseMessage off = await TestAdmins.LoginAsync(withoutCode, admin);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        using JsonDocument me = JsonDocument.Parse(await withoutCode.GetStringAsync(AdminEndpoints.Prefix + "/auth/me", Token));
        Assert.Equal(AetherisClaims.NoSecondFactor, me.RootElement.GetProperty("second_factor").GetString());
        Assert.False(me.RootElement.GetProperty("require_totp").GetBoolean());

        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.AdminRequireTotp, "true");

        try
        {
            HttpResponseMessage oldSession = await withoutCode.GetAsync(AdminEndpoints.Prefix + "/ping", Token);

            using HttpClient client = _factory.CreateBrowserClient();
            HttpResponseMessage missing = await TestAdmins.LoginAsync(client, admin);
            HttpResponseMessage withCode = await TestAdmins.LoginAsync(client, admin, TestAccounts.ComputeTotp(admin.Key));

            Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
            Assert.Equal(GameErrorCodes.SecondFactorRequired, await TestAccounts.ReadErrorAsync(oldSession));
            Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
            Assert.Equal(GameErrorCodes.SecondFactorRequired, await TestAccounts.ReadErrorAsync(missing));
            Assert.Equal(HttpStatusCode.OK, withCode.StatusCode);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.AdminRequireTotp, "false");
        }
    }

    /// <summary>
    /// The roles and the permissions are tables; the main role has every permission, and every
    /// admin endpoint has an action code that is a permission (acceptance criterion 8).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_admin_endpoint_is_a_permission_of_the_main_role()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        List<string> permissions = await dbContext.AdminPermissions.Select(permission => permission.Code).ToListAsync(Token);
        int ownerId = await dbContext.AdminRoles.Where(role => role.Code == AdminRoles.Owner).Select(role => role.Id).SingleAsync(Token);
        int ownerGrants = await dbContext.AdminRolePermissions.CountAsync(grant => grant.AdminRoleId == ownerId, Token);

        List<string> missing = AdminRoutes()
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? endpoint.RoutePattern.RawText!)
            .Where(code => !permissions.Contains(code))
            .ToList();

        Assert.True(missing.Count == 0, "Admin endpoints without a permission: " + string.Join(", ", missing));
        Assert.Equal(permissions.Count, ownerGrants);
    }

    /// <summary>
    /// The permission check refuses an endpoint without an action code and an action the roles
    /// of the administrator do not grant (acceptance criterion 8).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_admin_endpoint_without_an_action_code_does_not_pass()
    {
        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Moderation);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, admin);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        string sessionId = await dbContext.AdminSessions
            .Where(session => session.AdminAccountId == admin.AdminId)
            .Select(session => session.Id)
            .SingleAsync(Token);

        object? nameless = await InvokeFilterAsync(scope.ServiceProvider, admin.AdminId, sessionId, null);
        object? notGranted = await InvokeFilterAsync(scope.ServiceProvider, admin.AdminId, sessionId, "admin.config_changed");
        object? granted = await InvokeFilterAsync(scope.ServiceProvider, admin.AdminId, sessionId, "admin.ping");

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(nameless).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(notGranted).StatusCode);
        Assert.Equal("reached", granted);
    }

    /// <summary>
    /// A wrong type, a value out of bounds, a missing reason and a read-only parameter are
    /// refused; a valid change works at once, is audited with both values and the reason, and
    /// the rollback brings the old value back (acceptance criterion 7).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_parameter_is_changed_with_a_reason_and_rolled_back()
    {
        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, admin);
        using IServiceScope scope = _factory.Services.CreateScope();
        IGameConfig config = scope.ServiceProvider.GetRequiredService<IGameConfig>();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        string key = ConfigSeed.HelloMultiplier;
        string before = config.GetRaw(key)!;
        int versionsBefore = await dbContext.ConfigVersions.CountAsync(Token);

        HttpResponseMessage wrongType = await ChangeAsync(client, key, "abc", "Type check");
        HttpResponseMessage outOfBounds = await ChangeAsync(client, key, "5000", "Bounds check");
        HttpResponseMessage withoutReason = await ChangeAsync(client, key, "7", null);
        HttpResponseMessage readOnly = await ChangeAsync(client, SecurityKeys.AdminRequireTotp, "true", "Read-only check");

        Assert.Equal(HttpStatusCode.BadRequest, wrongType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, outOfBounds.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        Assert.Equal(AdminErrorCodes.ReadOnlyParameter, await TestAccounts.ReadErrorAsync(readOnly));
        Assert.Equal(before, config.GetRaw(key));

        HttpResponseMessage changed = await ChangeAsync(client, key, "7", "Balance test");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // The new value is in use at once, without a restart.
        Assert.Equal(7, config.GetInt64(key));
        Assert.Equal(versionsBefore + 1, await dbContext.ConfigVersions.CountAsync(Token));

        AdminAuditLogEntity audit = await dbContext.AdminAuditLogs.AsNoTracking()
            .Where(record => record.AdminId == admin.AdminId && record.Action == "admin.config_changed")
            .SingleAsync(Token);
        Assert.Equal(key, audit.Target);
        Assert.Equal(before, audit.OldValue);
        Assert.Equal("7", audit.NewValue);
        Assert.Equal("Balance test", audit.Reason);

        using JsonDocument history = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + $"/config/{key}/history", Token));
        JsonElement change = history.RootElement.GetProperty("items")[0];
        long changeId = change.GetProperty("id").GetInt64();
        Assert.Equal(before, change.GetProperty("old_value").GetString());

        HttpResponseMessage rolledBack = await client.PostAsJsonAsync(
            AdminEndpoints.Prefix + $"/config/{key}/rollback",
            new { change_id = changeId, reason = "Rollback test" },
            Token);
        Assert.Equal(HttpStatusCode.OK, rolledBack.StatusCode);
        Assert.Equal(before, config.GetRaw(key));

        ConfigParameterChangeEntity rollback = await dbContext.ConfigParameterChanges.AsNoTracking()
            .Where(entry => entry.Key == key)
            .OrderByDescending(entry => entry.Id)
            .FirstAsync(Token);
        Assert.Equal(changeId, rollback.RolledBackChangeId);
        Assert.Equal("7", rollback.OldValue);
        Assert.Equal(before, rollback.NewValue);
    }

    /// <summary>
    /// The parameter table shows the type, the bounds and the read-only mark, and is searched
    /// and filtered on the server.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_parameter_table_is_searched_and_filtered()
    {
        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, admin);

        using JsonDocument found = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + "/config?search=hello_multiplier", Token));
        JsonElement row = Assert.Single(found.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(ConfigSeed.HelloMultiplier, row.GetProperty("key").GetString());
        Assert.Equal(ConfigValueTypes.Integer, row.GetProperty("type").GetString());
        Assert.Equal("0", row.GetProperty("minimum").GetString());
        Assert.Equal("1000", row.GetProperty("maximum").GetString());
        Assert.False(row.GetProperty("read_only").GetBoolean());

        using JsonDocument readOnly = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + "/config?read_only=true&page_size=500", Token));
        List<string?> readOnlyKeys = [.. readOnly.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("key").GetString())];
        Assert.Contains(SecurityKeys.AdminRequireTotp, readOnlyKeys);
        Assert.DoesNotContain(ConfigSeed.HelloMultiplier, readOnlyKeys);

        using JsonDocument paged = JsonDocument.Parse(await client.GetStringAsync(AdminEndpoints.Prefix + "/config?group=security&page=2&page_size=5", Token));
        Assert.Equal(5, paged.RootElement.GetProperty("items").GetArrayLength());
        Assert.True(paged.RootElement.GetProperty("total").GetInt32() > 10);
        Assert.All(paged.RootElement.GetProperty("items").EnumerateArray(), item => Assert.StartsWith("security.", item.GetProperty("key").GetString(), StringComparison.Ordinal));

        HttpResponseMessage tooLong = await client.GetAsync(AdminEndpoints.Prefix + "/config?search=" + new string('a', 300), Token);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient client, string key, string value, string? reason) =>
        client.PutAsJsonAsync(AdminEndpoints.Prefix + "/config/" + key, new { value, reason }, Token);

    private List<RouteEndpoint> AdminRoutes() =>
        [.. _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith(AdminEndpoints.Prefix, StringComparison.Ordinal))
            .Where(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName != AdminEndpoints.SignInAction)];

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, RouteEndpoint endpoint)
    {
        string path = "/" + string.Join('/', endpoint.RoutePattern.PathSegments.Select(segment =>
            segment.IsSimple && segment.Parts[0] is Microsoft.AspNetCore.Routing.Patterns.RoutePatternLiteralPart literal ? literal.Content : "1"));
        string method = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.FirstOrDefault() ?? HttpMethods.Get;

        using HttpRequestMessage request = new(new HttpMethod(method), path);

        if (method != HttpMethods.Get)
        {
            request.Content = JsonContent.Create(new { });
        }

        return await client.SendAsync(request, Token);
    }

    private async Task MoveLastActivityAsync(string adminId, TimeSpan ago)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        DateTimeOffset moment = DateTimeOffset.UtcNow - ago;

        await dbContext.AdminSessions
            .Where(session => session.AdminAccountId == adminId && session.EndedAt == null)
            .OrderByDescending(session => session.CreatedAt)
            .Take(1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.LastActivityAt, moment), Token);
    }

    private static async Task<object?> InvokeFilterAsync(IServiceProvider services, string adminId, string sessionId, string? actionCode)
    {
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(AetherisClaims.AdminId, adminId), new Claim(AetherisClaims.AdminSessionId, sessionId)],
                AuthSchemes.Admin)),
        };

        EndpointMetadataCollection metadata = actionCode is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(new EndpointNameMetadata(actionCode));

        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, actionCode ?? "nameless"));

        return await new AdminAccessFilter().InvokeAsync(
            new DefaultEndpointFilterInvocationContext(httpContext),
            _ => ValueTask.FromResult<object?>("reached"));
    }
}

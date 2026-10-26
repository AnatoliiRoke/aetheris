using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Core.Security;
using Aetheris.Host.Admin;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Accounts;

/// <summary>
/// Tests of the account page (task 00006, acceptance criterion 10).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccountSettingsTests
{
    private const string ChangedPassword = "Silver-orchard-beacon-64";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountSettingsTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AccountSettingsTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The password changes only with the current one; the other sessions end, the current
    /// one goes on.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_password_changes_with_the_current_one_and_ends_the_other_sessions()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient current = _factory.CreateBrowserClient();
        using HttpClient other = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(current, account);
        await TestAccounts.SignInAsync(other, account);

        HttpResponseMessage wrong = await ChangePasswordAsync(current, "Not-the-password-1", ChangedPassword, "password-1");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains(AccountActions.WrongPassword, await AccountTestSupport.FieldErrorsAsync(wrong, "current_password"));

        HttpResponseMessage changed = await ChangePasswordAsync(current, account.Password, ChangedPassword, "password-2");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, account.Email, ChangedPassword)).StatusCode);
    }

    /// <summary>
    /// The nickname changes once per the configured 30 days; the first change after the
    /// registration is free, and another account's nickname is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_nickname_changes_not_more_often_than_the_interval()
    {
        TestAccount other = await TestAccounts.RegisterConfirmedAsync(_factory);
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, account);

        HttpResponseMessage taken = await ChangeNicknameAsync(client, other.Nickname.ToLowerInvariant(), "nick-1");
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Contains(NicknameRules.Taken, await AccountTestSupport.FieldErrorsAsync(taken, "nickname"));

        string firstName = TestAccounts.NewNickname();
        HttpResponseMessage first = await ChangeNicknameAsync(client, firstName, "nick-2");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        HttpResponseMessage tooSoon = await ChangeNicknameAsync(client, TestAccounts.NewNickname(), "nick-3");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooSoon.StatusCode);
        Assert.Equal(AccountActions.NicknameChangeTooSoon, await TestAccounts.ReadErrorAsync(tooSoon));

        using JsonDocument me = await AccountTestSupport.MeAsync(client);
        Assert.Equal(firstName, me.RootElement.GetProperty("nickname").GetString());

        using IServiceScope scope = _factory.Services.CreateScope();
        Assert.Equal(TimeSpan.FromDays(30), scope.ServiceProvider.GetRequiredService<SecuritySettings>().NicknameChangeInterval);

        // The new nickname signs in, the old one no longer does.
        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, firstName, account.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await AccountTestSupport.LoginFreshAsync(_factory, account.Nickname, account.Password)).StatusCode);
    }

    /// <summary>
    /// "Sign out everywhere" ends the session of every device.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Signing_out_everywhere_ends_every_session()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient first = _factory.CreateBrowserClient();
        using HttpClient second = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(first, account);
        await TestAccounts.SignInAsync(second, account);

        HttpResponseMessage response = await TestAccounts.SendCommandAsync(first, AccountActions.Prefix + "/sign-out-everywhere", new { }, "everywhere-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        Assert.False(await dbContext.RefreshTokens.AnyAsync(token => token.UserId == account.UserId && token.RevokedAt == null, Token));
    }

    /// <summary>
    /// The deletion needs the password, waits the configured 7 days and is cancelled by a
    /// sign-in; without the sign-in the account is deleted at the end of the delay.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_deletion_waits_and_a_sign_in_cancels_it()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, account);

        HttpResponseMessage wrong = await DeleteAsync(client, "Not-the-password-1", "delete-1");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        HttpResponseMessage requested = await DeleteAsync(client, account.Password, "delete-2");
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        PlayerProfileEntity pending = await dbContext.PlayerProfiles.AsNoTracking().SingleAsync(entry => entry.UserId == account.UserId, Token);

        Assert.NotNull(pending.DeletionDueAt);
        Assert.InRange(pending.DeletionDueAt!.Value - pending.DeletionRequestedAt!.Value, TimeSpan.FromDays(7), TimeSpan.FromDays(7));

        // A sign-in within the delay cancels the deletion; the event of that request does nothing.
        using HttpClient again = _factory.CreateBrowserClient();
        HttpResponseMessage signIn = await AccountTestSupport.LoginAsync(again, account.Email, account.Password);
        using (JsonDocument body = JsonDocument.Parse(await signIn.Content.ReadAsStringAsync(Token)))
        {
            Assert.True(body.RootElement.GetProperty("deletion_cancelled").GetBoolean());
        }

        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(8));
        Assert.True(await dbContext.Users.AnyAsync(user => user.Id == account.UserId, Token));

        // A new request without a sign-in ends with the deletion.
        await TestAccounts.UseCsrfTokenAsync(again);
        Assert.Equal(HttpStatusCode.OK, (await DeleteAsync(again, account.Password, "delete-3")).StatusCode);
        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(8));

        Assert.False(await dbContext.Users.AnyAsync(user => user.Id == account.UserId, Token));
        Assert.False(await dbContext.PlayerProfiles.AnyAsync(entry => entry.UserId == account.UserId, Token));
        Assert.False(await dbContext.AccountAccessLog.AnyAsync(entry => entry.UserId == account.UserId, Token));
    }

    /// <summary>
    /// A player has no way to change the own address; the support does, and the change is
    /// written to the audit log without the full addresses.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Only_the_support_changes_the_email_address_and_the_change_is_audited()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient player = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(player, account);

        HttpResponseMessage byPlayer = await TestAccounts.SendCommandAsync(player, AccountActions.Prefix + "/change-email", new { email = TestAccounts.NewEmail() }, "email-1");
        Assert.True(byPlayer.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);

        using HttpClient support = await AccountTestSupport.SignInAdminAsync(_factory, AdminRoles.Support);
        string newEmail = TestAccounts.NewEmail();

        HttpResponseMessage changed = await support.PutAsJsonAsync(
            AdminEndpoints.Prefix + $"/players/{account.UserId}/email",
            new { email = newEmail },
            Token);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        AdminAuditLogEntity record = await dbContext.AdminAuditLogs.AsNoTracking()
            .Where(entry => entry.Action == "admin.player_email_changed" && entry.Target == account.UserId)
            .SingleAsync(Token);

        Assert.DoesNotContain(account.Email, record.OldValue ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(newEmail, record.NewValue ?? string.Empty, StringComparison.Ordinal);
        Assert.EndsWith("@example.test", record.NewValue, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Unauthorized, (await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, newEmail, account.Password)).StatusCode);

        // The journal of the sign-ins is for the admin panel only.
        HttpResponseMessage journal = await support.GetAsync(AdminEndpoints.Prefix + $"/players/{account.UserId}/access-log", Token);
        Assert.Equal(HttpStatusCode.OK, journal.StatusCode);
        Assert.Contains(AccountAccessKinds.SignIn, await journal.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await player.GetAsync(AdminEndpoints.Prefix + $"/players/{account.UserId}/access-log", Token)).StatusCode);
    }

    private static Task<HttpResponseMessage> ChangePasswordAsync(HttpClient client, string current, string next, string requestId) =>
        TestAccounts.SendCommandAsync(
            client,
            AccountActions.Prefix + "/change-password",
            new Dictionary<string, object?>
            {
                ["current_password"] = current,
                ["new_password"] = next,
                ["new_password_confirmation"] = next,
            },
            requestId);

    private static Task<HttpResponseMessage> ChangeNicknameAsync(HttpClient client, string nickname, string requestId) =>
        TestAccounts.SendCommandAsync(client, AccountActions.Prefix + "/change-nickname", new { nickname }, requestId);

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string password, string requestId) =>
        TestAccounts.SendCommandAsync(client, AccountActions.Prefix + "/delete", new { password }, requestId);
}

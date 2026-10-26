using System.Net;
using Aetheris.Core.Errors;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Accounts;

/// <summary>
/// Tests of the sign-in of task 00006 (acceptance criteria 2, 4 and 6).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SignInTests
{
    private const string WrongPassword = "Wrong-password-7";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignInTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public SignInTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// The nickname signs in like the e-mail address, without regard to case.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_nickname_signs_in_like_the_email_address()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, account.Nickname, account.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, account.Nickname.ToUpperInvariant(), account.Password)).StatusCode);
    }

    /// <summary>
    /// An account whose address is not confirmed signs in; <c>/me</c> tells the client so.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_unconfirmed_account_signs_in_and_sees_its_state()
    {
        TestAccount account = await TestAccounts.RegisterAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, account);

        using System.Text.Json.JsonDocument me = await AccountTestSupport.MeAsync(client);

        Assert.False(me.RootElement.GetProperty("email_confirmed").GetBoolean());
        Assert.Equal(account.Nickname, me.RootElement.GetProperty("nickname").GetString());
        Assert.Equal(account.Email, me.RootElement.GetProperty("email").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, me.RootElement.GetProperty("civilization").ValueKind);
    }

    /// <summary>
    /// After the configured failures in a row the name is locked, and the answer is the same
    /// for a registered and for an unknown name.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_series_of_failures_locks_the_name_alike_for_known_and_unknown_names()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        string unknown = TestAccounts.NewEmail();

        using IServiceScope scope = _factory.Services.CreateScope();
        int maxFailures = scope.ServiceProvider.GetRequiredService<SecuritySettings>().LoginMaxFailedAttempts;

        Assert.Equal(5, maxFailures);

        using HttpClient client = _factory.CreateBrowserClient();

        for (int attempt = 0; attempt < maxFailures; attempt++)
        {
            HttpResponseMessage known = await AccountTestSupport.LoginAsync(client, account.Email, WrongPassword);
            HttpResponseMessage stranger = await AccountTestSupport.LoginAsync(client, unknown, WrongPassword);

            Assert.Equal(GameErrorCodes.InvalidCredentials, await TestAccounts.ReadErrorAsync(known));
            Assert.Equal(GameErrorCodes.InvalidCredentials, await TestAccounts.ReadErrorAsync(stranger));
        }

        HttpResponseMessage lockedKnown = await AccountTestSupport.LoginAsync(client, account.Email, account.Password);
        HttpResponseMessage lockedStranger = await AccountTestSupport.LoginAsync(client, unknown, account.Password);

        Assert.Equal(HttpStatusCode.Forbidden, lockedKnown.StatusCode);
        Assert.Equal(lockedKnown.StatusCode, lockedStranger.StatusCode);
        Assert.Equal(GameErrorCodes.AccountLocked, await TestAccounts.ReadErrorAsync(lockedKnown));
        Assert.Equal(GameErrorCodes.AccountLocked, await TestAccounts.ReadErrorAsync(lockedStranger));
    }

    /// <summary>
    /// The failures from one address lock the address, whatever names they tried.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_series_of_failures_from_one_address_locks_the_address()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        string? previous = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.LoginIpMaxFailedAttempts, "3");

        try
        {
            // A success starts the count of the address anew.
            Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password)).StatusCode);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                HttpResponseMessage failed = await AccountTestSupport.LoginFreshAsync(_factory, TestAccounts.NewEmail(), WrongPassword);
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }

            HttpResponseMessage locked = await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password);

            Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
            Assert.Equal(GameErrorCodes.AccountLocked, await TestAccounts.ReadErrorAsync(locked));
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, SecurityKeys.LoginIpMaxFailedAttempts, previous!);
        }

        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password)).StatusCode);
    }

    /// <summary>
    /// "Remember me" gives a refresh cookie with an expiry of the configured 30 days; without
    /// it both cookies end with the browser.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Remember_me_gives_a_lasting_cookie_and_its_absence_a_session_cookie()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        HttpResponseMessage remembered = await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password, rememberMe: true);
        HttpResponseMessage forgotten = await AccountTestSupport.LoginFreshAsync(_factory, account.Email, account.Password, rememberMe: false);

        string lasting = RefreshCookie(remembered);
        string session = RefreshCookie(forgotten);

        Assert.Contains("expires=", lasting, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max-age", session, StringComparison.OrdinalIgnoreCase);

        string expires = lasting.Split(';').Select(part => part.Trim()).Single(part => part.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..];
        DateTimeOffset expiry = DateTimeOffset.Parse(expires, System.Globalization.CultureInfo.InvariantCulture);

        Assert.InRange(expiry, DateTimeOffset.UtcNow.AddDays(29), DateTimeOffset.UtcNow.AddDays(31));
    }

    private static string RefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith(AuthCookies.Refresh + "=", StringComparison.Ordinal));
}

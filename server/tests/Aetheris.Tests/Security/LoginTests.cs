using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Errors;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the sign-in and of the account flows.
/// </summary>
/// <remarks>
/// Acceptance criterion 1 of task 00002: the right password, a wrong password, the lockout
/// after a series of failures, and an unconfirmed e-mail address.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class LoginTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public LoginTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// The right password signs in and sets the session and refresh cookies with the
    /// protective attributes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_right_password_signs_in_with_protected_cookies()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await LoginAsync(client, account.Email, account.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<string> cookies = response.Headers.GetValues("Set-Cookie").ToList();

        foreach (string name in new[] { AuthCookies.Session, AuthCookies.Refresh })
        {
            string cookie = Assert.Single(cookies, value => value.StartsWith(name + "=", StringComparison.Ordinal));

            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        }

        HttpResponseMessage me = await client.GetAsync(AuthEndpoints.Prefix + "/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(account.UserId, await me.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>
    /// A wrong password and an unknown address get the same answer.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_wrong_password_and_an_unknown_account_are_refused_alike()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage wrongPassword = await LoginAsync(client, account.Email, "another-wrong-password-7");
        HttpResponseMessage unknownAccount = await LoginAsync(client, TestAccounts.NewEmail(), account.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAccount.StatusCode);
        Assert.Equal(GameErrorCodes.InvalidCredentials, await TestAccounts.ReadErrorAsync(wrongPassword));
        Assert.Equal(GameErrorCodes.InvalidCredentials, await TestAccounts.ReadErrorAsync(unknownAccount));
        Assert.False(wrongPassword.Headers.Contains("Set-Cookie"));
    }

    /// <summary>
    /// After the configured number of failures the account is locked, even for the right
    /// password, and the lockout is written to the security journal.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_series_of_failures_locks_the_account()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        int maxFailures = scope.ServiceProvider.GetRequiredService<SecuritySettings>().LoginMaxFailedAttempts;

        using HttpClient client = _factory.CreateBrowserClient();

        for (int attempt = 0; attempt < maxFailures; attempt++)
        {
            HttpResponseMessage failed = await LoginAsync(client, account.Email, "another-wrong-password-7");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        HttpResponseMessage locked = await LoginAsync(client, account.Email, account.Password);

        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        Assert.Equal(GameErrorCodes.AccountLocked, await TestAccounts.ReadErrorAsync(locked));

        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        Assert.True(await dbContext.SecurityEvents.AnyAsync(
            entry => entry.Type == SecurityEventTypes.AccountLocked && entry.UserId == account.UserId,
            TestContext.Current.CancellationToken));

        // The lockout is temporary: it has an end.
        DateTimeOffset? lockoutEnd = await dbContext.Users
            .Where(user => user.Id == account.UserId)
            .Select(user => user.LockoutEnd)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(lockoutEnd);
        Assert.True(lockoutEnd > DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The right password of an account whose e-mail address is not confirmed does not sign in.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_unconfirmed_email_address_does_not_sign_in()
    {
        TestAccount account = await TestAccounts.RegisterAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await LoginAsync(client, account.Email, account.Password);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GameErrorCodes.EmailNotConfirmed, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// Registering a known address answers like a new one, so the answer does not reveal
    /// which addresses are registered.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Registering_a_known_address_reveals_nothing()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/register",
            new { email = account.Email, password = TestAccounts.StrongPassword },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    /// <summary>
    /// A short or common password is refused with the rule codes; the codes never repeat
    /// the password.
    /// </summary>
    /// <param name="password">The weak password.</param>
    /// <param name="code">The expected rule code.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("short1", ConfigPasswordValidator.TooShort)]
    [InlineData("password1234", ConfigPasswordValidator.Common)]
    [InlineData("aaaaaaaaaaaaaaaa", ConfigPasswordValidator.TooFewUniqueCharacters)]
    public async Task A_weak_password_is_refused(string password, string code)
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/register",
            new { email = TestAccounts.NewEmail(), password },
            TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, body, StringComparison.Ordinal);
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refresh token works once; a second use of it ends every session of the account.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_refresh_token_is_rotated_and_a_reused_token_is_refused()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        HttpResponseMessage login = await LoginAsync(client, account.Email, account.Password);
        string firstRefresh = ReadCookie(login, AuthCookies.Refresh);

        HttpResponseMessage refreshed = await RefreshAsync(client, firstRefresh);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);

        string secondRefresh = ReadCookie(refreshed, AuthCookies.Refresh);
        Assert.NotEqual(firstRefresh, secondRefresh);

        HttpResponseMessage reused = await RefreshAsync(client, firstRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // The reuse revoked the second token as well.
        HttpResponseMessage afterReuse = await RefreshAsync(client, secondRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    /// <summary>
    /// A password reset changes the password: the old one stops working and the new one
    /// signs in.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_password_reset_replaces_the_password()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        const string newPassword = "amber-meadow-compass-58";

        using HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage forgot = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/forgot-password",
            new { email = account.Email },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        AccountEmail email = _factory.Emails.Last(account.Email, AccountEmailKinds.PasswordReset);

        HttpResponseMessage reset = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/reset-password",
            new { userId = email.UserId, token = email.Token, newPassword },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(client, account.Email, account.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, account.Email, newPassword)).StatusCode);
    }

    /// <summary>
    /// The FluentValidation pipeline refuses a malformed request before the endpoint runs.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_malformed_request_is_refused_by_validation()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/register",
            new { email = "not-an-address", password = string.Empty },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GameErrorCodes.ValidationFailed, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// A new confirmation e-mail goes only to an account that still waits for it, and the
    /// answer is the same for every address.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_confirmation_email_is_sent_only_to_an_unconfirmed_account()
    {
        TestAccount unconfirmed = await TestAccounts.RegisterAsync(_factory);
        TestAccount confirmed = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();

        foreach (string email in new[] { unconfirmed.Email, confirmed.Email, TestAccounts.NewEmail() })
        {
            HttpResponseMessage response = await client.PostAsJsonAsync(
                AuthEndpoints.Prefix + "/resend-confirmation",
                new { email },
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        Assert.Equal(2, _factory.Emails.Sent.Count(email => email.Address == unconfirmed.Email && email.Kind == AccountEmailKinds.Confirmation));
        Assert.Equal(1, _factory.Emails.Sent.Count(email => email.Address == confirmed.Email && email.Kind == AccountEmailKinds.Confirmation));
    }

    /// <summary>
    /// The client learns from the server whether the Google sign-in is enabled; without
    /// credentials it is not.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_providers_say_that_google_is_off_without_credentials()
    {
        using HttpClient client = _factory.CreateBrowserClient();

        string body = await client.GetStringAsync(AuthEndpoints.Prefix + "/providers", TestContext.Current.CancellationToken);

        Assert.Equal("{\"google\":false}", body);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/login",
            new { email, password },
            TestContext.Current.CancellationToken);

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        // A request with the refresh cookie needs an anti-forgery token; the token is
        // issued for an anonymous caller, because this request carries no session.
        HttpResponseMessage csrf = await client.GetAsync(AuthEndpoints.Prefix + "/csrf", TestContext.Current.CancellationToken);
        string csrfCookie = ReadCookie(csrf, AuthCookies.Csrf);
        string token = (await csrf.Content.ReadFromJsonAsync<CsrfResponse>(TestContext.Current.CancellationToken))!.Token;

        using HttpRequestMessage request = new(HttpMethod.Post, AuthEndpoints.Prefix + "/refresh");
        request.Headers.Add("Cookie", $"{AuthCookies.Refresh}={refreshToken}; {AuthCookies.Csrf}={csrfCookie}");
        request.Headers.Add(AuthCookies.CsrfHeader, token);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static string ReadCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])
            .Single(cookie => cookie.StartsWith(name + "=", StringComparison.Ordinal))
            .Substring(name.Length + 1);

    private sealed record CsrfResponse(string Token);
}

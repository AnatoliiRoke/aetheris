using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Errors;
using Aetheris.Host.Auth;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aetheris.Tests.Accounts;

/// <summary>
/// Tests of the password reset (task 00006, acceptance criterion 5).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PasswordResetTests
{
    private const string NewPassword = "Copper-river-morning-31";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordResetTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public PasswordResetTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A registered and an unknown address get the same answer.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_answer_is_the_same_for_a_known_and_an_unknown_address()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage known = await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/forgot-password", new { email = account.Email }, Token);
        HttpResponseMessage unknown = await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/forgot-password", new { email = TestAccounts.NewEmail() }, Token);

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(Token), await unknown.Content.ReadAsStringAsync(Token));
    }

    /// <summary>
    /// The reset link works once and lives the configured hour; the reset ends the sessions
    /// that were open before it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_reset_link_works_once_and_ends_the_old_sessions()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient oldSession = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(oldSession, account);
        Assert.Equal(HttpStatusCode.OK, (await oldSession.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);

        using HttpClient client = _factory.CreateBrowserClient();
        await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/forgot-password", new { email = account.Email }, Token);
        AccountEmail email = _factory.Emails.Last(account.Email, AccountEmailKinds.PasswordReset);
        object body = new { userId = email.UserId, token = email.Token, newPassword = NewPassword };

        HttpResponseMessage first = await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/reset-password", body, Token);
        HttpResponseMessage second = await client.PostAsJsonAsync(AuthEndpoints.Prefix + "/reset-password", body, Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(GameErrorCodes.InvalidToken, await TestAccounts.ReadErrorAsync(second));

        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync(AuthEndpoints.Prefix + "/me", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AccountTestSupport.LoginAsync(client, account.Email, NewPassword)).StatusCode);

        using IServiceScope scope = _factory.Services.CreateScope();
        SecuritySettings settings = scope.ServiceProvider.GetRequiredService<SecuritySettings>();

        Assert.Equal(TimeSpan.FromHours(1), settings.ResetLifetime);
        Assert.Equal(settings.ResetLifetime, scope.ServiceProvider.GetRequiredService<IOptions<ResetTokenProviderOptions>>().Value.TokenLifespan);
    }
}

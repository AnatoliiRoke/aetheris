using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Host.Admin;
using Aetheris.Host.Auth;
using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Accounts;

/// <summary>
/// Shared steps of the account tests of task 00006.
/// </summary>
public static class AccountTestSupport
{
    /// <summary>
    /// Posts a registration.
    /// </summary>
    /// <param name="client">A browser client.</param>
    /// <param name="email">The e-mail address.</param>
    /// <param name="nickname">The nickname.</param>
    /// <param name="password">The password.</param>
    /// <param name="confirmation">The repeated password, by default the same.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string nickname,
        string password,
        string? confirmation = null) =>
        client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/register",
            new Dictionary<string, object?>
            {
                ["email"] = email,
                ["nickname"] = nickname,
                ["password"] = password,
                ["password_confirmation"] = confirmation ?? password,
            },
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Posts a sign-in.
    /// </summary>
    /// <param name="client">A browser client.</param>
    /// <param name="login">The e-mail address or the nickname.</param>
    /// <param name="password">The password.</param>
    /// <param name="rememberMe">Whether the session outlives the browser.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string login, string password, bool rememberMe = false) =>
        client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/login",
            TestAccounts.LoginBody(login, password, rememberMe),
            TestContext.Current.CancellationToken);

    /// <summary>
    /// Posts a sign-in from a new browser, the way a second device does. A browser that is
    /// already signed in would need the anti-forgery token of its session first.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="login">The e-mail address or the nickname.</param>
    /// <param name="password">The password.</param>
    /// <param name="rememberMe">Whether the session outlives the browser.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> LoginFreshAsync(AetherisTestFactory factory, string login, string password, bool rememberMe = false)
    {
        ArgumentNullException.ThrowIfNull(factory);

        using HttpClient client = factory.CreateBrowserClient();

        return await LoginAsync(client, login, password, rememberMe);
    }

    /// <summary>
    /// Reads the error codes of one field of a validation error.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="field">The field.</param>
    /// <returns>The codes.</returns>
    public static async Task<List<string>> FieldErrorsAsync(HttpResponseMessage response, string field)
    {
        ArgumentNullException.ThrowIfNull(response);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.TryGetProperty("details", out JsonElement details) && details.TryGetProperty(field, out JsonElement codes)
            ? [.. codes.EnumerateArray().Select(code => code.GetString()!)]
            : [];
    }

    /// <summary>
    /// Builds a unique nickname of an exact length from letters and digits.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <returns>The nickname.</returns>
    public static string NicknameOfLength(int length)
    {
        string unique = "Z" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        return unique[..length];
    }

    /// <summary>
    /// Reads the body of a successful answer of <c>/me</c>.
    /// </summary>
    /// <param name="client">A signed-in client.</param>
    /// <returns>The answer.</returns>
    public static async Task<JsonDocument> MeAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        return JsonDocument.Parse(await client.GetStringAsync(AuthEndpoints.Prefix + "/me", TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Creates an administrator with a role and signs it in with the second factor.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="role">The role.</param>
    /// <returns>A signed-in client of the administrator.</returns>
    public static async Task<HttpClient> SignInAdminAsync(AetherisTestFactory factory, string role)
    {
        ArgumentNullException.ThrowIfNull(factory);

        string email = TestAccounts.NewEmail();
        string key;

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            key = await scope.ServiceProvider.GetRequiredService<AdminAccountService>()
                .CreateAsync(email, TestAccounts.StrongPassword, "Support administrator", [role]);
        }

        HttpClient client = factory.CreateBrowserClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/auth/login",
            new { email, password = TestAccounts.StrongPassword, code = TestAccounts.ComputeTotp(key) },
            TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        await TestAccounts.UseCsrfTokenAsync(client);

        return client;
    }
}

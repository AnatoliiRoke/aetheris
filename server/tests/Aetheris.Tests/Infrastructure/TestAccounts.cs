using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Host.Auth;
using Aetheris.Host.Security;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// A test account.
/// </summary>
/// <param name="Email">The e-mail address.</param>
/// <param name="Password">The password.</param>
/// <param name="UserId">The account identifier.</param>
public sealed record TestAccount(string Email, string Password, string UserId);

/// <summary>
/// Helpers that drive the account flows the way the client does.
/// </summary>
public static class TestAccounts
{
    /// <summary>
    /// A password that passes every rule of the seed.
    /// </summary>
    public const string StrongPassword = "violet-harbor-lantern-42";

    /// <summary>
    /// The number of digits of an authenticator code.
    /// </summary>
    private const int TotpDigits = 6;

    /// <summary>
    /// The length of a step of an authenticator code, in seconds.
    /// </summary>
    private const int TotpStepSeconds = 30;

    /// <summary>
    /// Creates a unique e-mail address in the reserved test domain.
    /// </summary>
    /// <returns>The address.</returns>
    public static string NewEmail() => $"player-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// Registers an account without confirming it.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="email">The e-mail address, or a new one.</param>
    /// <returns>The account.</returns>
    public static async Task<TestAccount> RegisterAsync(AetherisTestFactory factory, string? email = null)
    {
        string address = email ?? NewEmail();

        using HttpClient client = factory.CreateBrowserClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/register",
            new { email = address, password = StrongPassword },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        string userId = factory.Emails.Last(address, AccountEmailKinds.Confirmation).UserId;

        return new TestAccount(address, StrongPassword, userId);
    }

    /// <summary>
    /// Registers and confirms an account.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <returns>The account.</returns>
    public static async Task<TestAccount> RegisterConfirmedAsync(AetherisTestFactory factory)
    {
        TestAccount account = await RegisterAsync(factory);
        AccountEmail email = factory.Emails.Last(account.Email, AccountEmailKinds.Confirmation);

        using HttpClient client = factory.CreateBrowserClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/confirm-email",
            new { userId = email.UserId, token = email.Token },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return account;
    }

    /// <summary>
    /// Signs in and prepares the client for state-changing requests: it keeps the session
    /// cookies and sends the anti-forgery token.
    /// </summary>
    /// <param name="client">A browser client.</param>
    /// <param name="account">The account.</param>
    /// <returns>A task that completes when the client is signed in.</returns>
    public static async Task SignInAsync(HttpClient client, TestAccount account)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/login",
            new { email = account.Email, password = account.Password },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await UseCsrfTokenAsync(client);
    }

    /// <summary>
    /// Fetches an anti-forgery token for the current session and sends it with every request.
    /// </summary>
    /// <param name="client">A browser client.</param>
    /// <returns>A task that completes when the header is set.</returns>
    public static async Task UseCsrfTokenAsync(HttpClient client)
    {
        using JsonDocument csrf = JsonDocument.Parse(
            await client.GetStringAsync(AuthEndpoints.Prefix + "/csrf", TestContext.Current.CancellationToken));

        client.DefaultRequestHeaders.Remove(AuthCookies.CsrfHeader);
        client.DefaultRequestHeaders.Add(AuthCookies.CsrfHeader, csrf.RootElement.GetProperty("token").GetString());
    }

    /// <summary>
    /// Sends a command with a request identifier.
    /// </summary>
    /// <param name="client">A signed-in client.</param>
    /// <param name="route">The route of the command.</param>
    /// <param name="body">The request body.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The response.</returns>
    public static async Task<HttpResponseMessage> SendCommandAsync(HttpClient client, string route, object body, string requestId)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, route) { Content = JsonContent.Create(body) };
        request.Headers.Add(AuthCookies.IdempotencyHeader, requestId);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Reads the error code of an error response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The code.</returns>
    public static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("error").GetString();
    }

    /// <summary>
    /// Computes the current code of an authenticator app (RFC 6238, as used by ASP.NET Core
    /// Identity: HMAC-SHA1, 30-second steps, six digits).
    /// </summary>
    /// <param name="base32Key">The authenticator key.</param>
    /// <returns>The code.</returns>
    public static string ComputeTotp(string base32Key)
    {
        byte[] key = FromBase32(base32Key);
        long step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / TotpStepSeconds;

        byte[] counter = BitConverter.GetBytes(step);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        byte[] hash = HMACSHA1.HashData(key, counter);
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        return (binary % (int)Math.Pow(10, TotpDigits)).ToString(new string('0', TotpDigits), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        string text = input.TrimEnd('=').ToUpperInvariant();
        List<byte> bytes = [];
        int buffer = 0;
        int bits = 0;

        foreach (char character in text)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character, StringComparison.Ordinal);
            bits += 5;

            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. bytes];
    }
}

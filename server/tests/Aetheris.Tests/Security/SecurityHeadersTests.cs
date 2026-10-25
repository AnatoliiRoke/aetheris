using System.Net;
using System.Text;
using Aetheris.Core.Errors;
using Aetheris.Host.Auth;
using Aetheris.Host.Commands;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the response headers, CORS, the anti-forgery token, the size limit and the
/// error bodies.
/// </summary>
/// <remarks>
/// Acceptance criterion 5 of task 00002, plus the other request protections of section 2.3.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SecurityHeadersTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public SecurityHeadersTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// Every response carries the security headers and no <c>Server</c> header.
    /// </summary>
    /// <param name="path">The requested path.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/ping")]
    [InlineData("/api/v1/does-not-exist")]
    public async Task Every_response_carries_the_security_headers(string path)
    {
        using HttpClient client = _factory.CreateBrowserClient();
        HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(SecurityHeadersMiddleware.ContentSecurityPolicy, Header(response, "Content-Security-Policy"));
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.False(response.Headers.Contains("Server"));
    }

    /// <summary>
    /// Outside development every HTTPS response carries HSTS.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Hsts_is_sent_outside_development()
    {
        await using ProductionTestFactory production = new();

        using HttpClient client = production.CreateClient(new WebApplicationFactoryClientOptions
        {
            // HSTS is never sent for localhost, so the test uses another host name.
            BaseAddress = new Uri("https://aetheris.test"),
        });

        HttpResponseMessage response = await client.GetAsync("/api/v1/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("max-age=", Header(response, "Strict-Transport-Security"), StringComparison.Ordinal);
    }

    /// <summary>
    /// CORS allows our own client and nobody else.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Cors_allows_only_our_client()
    {
        using HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage ours = await PreflightAsync(client, AetherisTestFactory.ClientOrigin);
        HttpResponseMessage foreign = await PreflightAsync(client, "https://foreign.example");

        Assert.Equal(AetherisTestFactory.ClientOrigin, Header(ours, "Access-Control-Allow-Origin"));
        Assert.False(foreign.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// A state-changing request with a session cookie and without the anti-forgery token is
    /// refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_cookie_request_without_the_csrf_token_is_refused()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, player);
        client.DefaultRequestHeaders.Remove(AuthCookies.CsrfHeader);

        HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            client,
            EchoCommand.RoutePath,
            new { message = "hello" },
            Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GameErrorCodes.CsrfTokenInvalid, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// A body over the configured size is refused before it is read.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_body_over_the_limit_is_refused()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        long maxBodyBytes = _factory.Services.GetRequiredService<SecuritySettings>().RequestMaxBodyBytes;
        string oversized = new('x', (int)maxBodyBytes + 1);

        HttpResponseMessage response = await client.PostAsync(
            AuthEndpoints.Prefix + "/login",
            new StringContent(oversized, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(GameErrorCodes.PayloadTooLarge, await TestAccounts.ReadErrorAsync(response));
    }

    /// <summary>
    /// A broken request gets a bare error code: no stack trace, no framework message.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Errors_do_not_expose_internals()
    {
        using HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage response = await client.PostAsync(
            AuthEndpoints.Prefix + "/login",
            new StringContent("{ not json", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("{\"error\":", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
    }

    private static string Header(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out IEnumerable<string>? values))
        {
            return string.Join(",", values);
        }

        return response.Content.Headers.TryGetValues(name, out IEnumerable<string>? contentValues)
            ? string.Join(",", contentValues)
            : string.Empty;
    }

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        using HttpRequestMessage request = new(HttpMethod.Options, EchoCommand.RoutePath);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A test host that runs in the production environment.
    /// </summary>
    private sealed class ProductionTestFactory : AetherisTestFactory
    {
        protected override string EnvironmentName => "Production";
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Aetheris.Host.Auth;
using Aetheris.Host.Realtime;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Opens SignalR connections to the in-memory test server, with or without a session.
/// </summary>
/// <remarks>
/// HTTP requests go through the test server handler and the WebSocket through its WebSocket
/// client, so no real port is used. The session cookie is attached to both.
/// </remarks>
public static class HubConnections
{
    /// <summary>
    /// Signs in and returns the <c>Cookie</c> header of the session.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="account">The account.</param>
    /// <returns>The cookie header value.</returns>
    public static async Task<string> GetSessionCookieAsync(AetherisTestFactory factory, TestAccount account)
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });

        HttpResponseMessage response = await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/login",
            TestAccounts.LoginBody(account.Email, account.Password),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string session = response.Headers.GetValues("Set-Cookie")
            .Select(cookie => cookie.Split(';')[0])
            .Single(cookie => cookie.StartsWith(AuthCookies.Session + "=", StringComparison.Ordinal));

        return session;
    }

    /// <summary>
    /// Builds a connection to the game hub.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="cookie">The <c>Cookie</c> header, or <see langword="null"/> for an anonymous connection.</param>
    /// <returns>The connection, not started yet.</returns>
    public static HubConnection Create(AetherisTestFactory factory, string? cookie)
    {
        WebSocketClient webSocketClient = factory.Server.CreateWebSocketClient();

        if (cookie is not null)
        {
            webSocketClient.ConfigureRequest = request => request.Headers.Cookie = cookie;
        }

        Uri hubUri = new(new Uri("https://localhost"), GameHub.Route);

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.HttpMessageHandlerFactory = _ => new CookieHandler(factory.Server.CreateHandler(), cookie);

                options.WebSocketFactory = async (_, cancellationToken) =>
                {
                    WebSocket socket = await webSocketClient.ConnectAsync(hubUri, cancellationToken);

                    return socket;
                };
            })
            .Build();
    }

    private sealed class CookieHandler : DelegatingHandler
    {
        private readonly string? _cookie;

        public CookieHandler(HttpMessageHandler inner, string? cookie)
            : base(inner)
        {
            _cookie = cookie;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_cookie is not null)
            {
                request.Headers.Add("Cookie", _cookie);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}

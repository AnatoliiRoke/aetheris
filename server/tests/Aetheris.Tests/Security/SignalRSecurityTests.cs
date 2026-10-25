using System.Net;
using Aetheris.Core.Errors;
using Aetheris.Host.Commands;
using Aetheris.Host.Realtime;
using Aetheris.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the SignalR hub protection.
/// </summary>
/// <remarks>
/// Acceptance criterion 6 of task 00002: a connection without a session is refused, and a
/// subscription to the data of another player is refused.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SignalRSecurityTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRSecurityTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public SignalRSecurityTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// A connection without a session is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_connection_without_a_session_is_refused()
    {
        await using HubConnection connection = HubConnections.Create(_factory, cookie: null);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => connection.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    /// <summary>
    /// A player can subscribe to their own data and not to the data of another player.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_subscription_to_the_data_of_another_player_is_refused()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);
        TestAccount other = await TestAccounts.RegisterConfirmedAsync(_factory);

        string cookie = await HubConnections.GetSessionCookieAsync(_factory, player);
        await using HubConnection connection = HubConnections.Create(_factory, cookie);
        await connection.StartAsync(TestContext.Current.CancellationToken);

        await connection.InvokeAsync(
            nameof(GameHub.Subscribe),
            PlayerObjectResolver.Type,
            player.UserId,
            TestContext.Current.CancellationToken);

        HubException refused = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync(
            nameof(GameHub.Subscribe),
            PlayerObjectResolver.Type,
            other.UserId,
            TestContext.Current.CancellationToken));

        Assert.Contains(GameErrorCodes.NotYourObject, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A hub request from a foreign web page is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_hub_request_from_a_foreign_origin_is_refused()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        using HttpRequestMessage request = new(HttpMethod.Post, GameHub.Route + "/negotiate?negotiateVersion=1");
        request.Headers.Add("Origin", "https://foreign.example");

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

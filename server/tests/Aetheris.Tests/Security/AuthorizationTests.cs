using System.Net;
using Aetheris.Core.Errors;
using Aetheris.Host.Admin;
using Aetheris.Host.Commands;
using Aetheris.Host.Data;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the ownership check and of the separation of the admin routes.
/// </summary>
/// <remarks>
/// Acceptance criterion 2 of task 00002: a player cannot run a command on an object of
/// another player, and a player gets <c>403</c> on <c>/api/admin/...</c>.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class AuthorizationTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public AuthorizationTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// A command on an object of another player is refused with <c>not_your_object</c>,
    /// changes nothing and is written to the security journal.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_player_cannot_act_on_an_object_of_another_player()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);
        TestAccount other = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, player);

        HttpResponseMessage response = await TestAccounts.SendCommandAsync(
            client,
            TestNoteCommand.RoutePath,
            new { playerId = other.UserId, text = "hello" },
            Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GameErrorCodes.NotYourObject, await TestAccounts.ReadErrorAsync(response));

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        Assert.False(await dbContext.ChangeJournal.AnyAsync(
            entry => entry.ObjectId == other.UserId,
            TestContext.Current.CancellationToken));
        Assert.True(await dbContext.SecurityEvents.AnyAsync(
            entry => entry.Type == SecurityEventTypes.OwnershipDenied && entry.UserId == player.UserId,
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// A signed-in player gets <c>403</c> on an admin route.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_player_gets_403_on_the_admin_routes()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, player);

        HttpResponseMessage ping = await client.GetAsync(AdminEndpoints.Prefix + "/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, ping.StatusCode);
        Assert.Equal(GameErrorCodes.AccessDenied, await TestAccounts.ReadErrorAsync(ping));
    }

    /// <summary>
    /// An anonymous caller gets <c>401</c> on the admin routes and on the commands.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="route">The route.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("GET", AdminEndpoints.Prefix + "/ping")]
    [InlineData("POST", EchoCommand.RoutePath)]
    public async Task An_anonymous_caller_gets_401(string method, string route)
    {
        using HttpClient client = _factory.CreateBrowserClient();
        using HttpRequestMessage request = new(new HttpMethod(method), route);

        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(GameErrorCodes.NotAuthenticated, await TestAccounts.ReadErrorAsync(response));
    }
}

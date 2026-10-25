using System.Net;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Host.Commands;
using Aetheris.Host.Data;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the request identifier.
/// </summary>
/// <remarks>
/// Acceptance criterion 3 of task 00002: a command with the same request identifier runs
/// once.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class IdempotencyTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public IdempotencyTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// A repeated command gets the stored response and does not run again; a new identifier
    /// runs the command again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_repeated_command_runs_once()
    {
        using HttpClient client = await SignedInClientAsync();
        string requestId = Guid.NewGuid().ToString();

        HttpResponseMessage first = await TestAccounts.SendCommandAsync(client, EchoCommand.RoutePath, new { message = "hello" }, requestId);
        HttpResponseMessage second = await TestAccounts.SendCommandAsync(client, EchoCommand.RoutePath, new { message = "hello" }, requestId);
        HttpResponseMessage third = await TestAccounts.SendCommandAsync(client, EchoCommand.RoutePath, new { message = "hello" }, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(second.Headers.Contains(CommandEndpoints.ReplayedHeader));

        long firstExecution = await ReadExecutionAsync(first);
        long secondExecution = await ReadExecutionAsync(second);
        long thirdExecution = await ReadExecutionAsync(third);

        Assert.Equal(firstExecution, secondExecution);
        Assert.True(thirdExecution > firstExecution, "A new request identifier must run the command again.");
    }

    /// <summary>
    /// A repeated command does not repeat its change: the change journal holds one record.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_repeated_command_changes_the_state_once()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, player);

        string requestId = Guid.NewGuid().ToString();
        object body = new { playerId = player.UserId, text = "once" };

        for (int attempt = 0; attempt < 3; attempt++)
        {
            HttpResponseMessage response = await TestAccounts.SendCommandAsync(client, TestNoteCommand.RoutePath, body, requestId);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        int records = await dbContext.ChangeJournal.CountAsync(
            entry => entry.RequestId == requestId,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, records);
    }

    /// <summary>
    /// The same identifier with another body is refused instead of replayed.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_reused_identifier_with_another_body_is_refused()
    {
        using HttpClient client = await SignedInClientAsync();
        string requestId = Guid.NewGuid().ToString();

        await TestAccounts.SendCommandAsync(client, EchoCommand.RoutePath, new { message = "first" }, requestId);
        HttpResponseMessage other = await TestAccounts.SendCommandAsync(client, EchoCommand.RoutePath, new { message = "second" }, requestId);

        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal(GameErrorCodes.DuplicateRequest, await TestAccounts.ReadErrorAsync(other));
    }

    /// <summary>
    /// A command without a request identifier is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_command_without_an_identifier_is_refused()
    {
        using HttpClient client = await SignedInClientAsync();

        HttpResponseMessage response = await client.PostAsync(
            EchoCommand.RoutePath,
            new StringContent("{\"message\":\"hello\"}", System.Text.Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(GameErrorCodes.ValidationFailed, await TestAccounts.ReadErrorAsync(response));
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        TestAccount player = await TestAccounts.RegisterConfirmedAsync(_factory);
        HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, player);

        return client;
    }

    private static async Task<long> ReadExecutionAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("execution").GetInt64();
    }
}

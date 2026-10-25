using Aetheris.Host.Realtime;
using Aetheris.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Realtime;

/// <summary>
/// Tests the SignalR hub: a client connects and receives a numbered event.
/// </summary>
/// <remarks>
/// This is acceptance criterion 5 of task 00001.
/// The client talks to the in-memory test server through <see cref="HubConnections"/>.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class SignalRTests
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(15);

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignalRTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory.</param>
    public SignalRTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// A client connects to the hub and receives a test event that carries a sequence number.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_client_receives_a_test_event_with_a_sequence_number()
    {
        // Since task 00002 only a signed-in player can connect.
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        string cookie = await HubConnections.GetSessionCookieAsync(_factory, account);

        await using HubConnection connection = HubConnections.Create(_factory, cookie);

        await connection.StartAsync(TestContext.Current.CancellationToken);

        TaskCompletionSource<GameEventEnvelope> received = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using IDisposable subscription = connection.On<GameEventEnvelope>(
            GameEventPublisher.ReceiveMethod,
            envelope => received.TrySetResult(envelope));

        await connection.InvokeAsync(
            "PublishTestEvent",
            "test_event",
            "{\"value\":1}",
            TestContext.Current.CancellationToken);

        Task completed = await Task.WhenAny(
            received.Task,
            Task.Delay(ReceiveTimeout, TestContext.Current.CancellationToken));

        Assert.True(
            completed == received.Task,
            $"No event was received within {ReceiveTimeout.TotalSeconds:0} seconds.");

        GameEventEnvelope envelope = await received.Task;

        Assert.True(envelope.Seq > 0, "The event sequence number must be positive.");
        Assert.Equal("test_event", envelope.Type);
        Assert.Equal("{\"value\":1}", envelope.Payload);
    }

    /// <summary>
    /// Two consecutive events receive strictly increasing sequence numbers.
    /// </summary>
    [Fact]
    public void Event_sequence_numbers_increase()
    {
        GameEventPublisher publisher = _factory.Services.GetRequiredService<GameEventPublisher>();

        GameEventEnvelope first = publisher.Enqueue("first", "{}");
        GameEventEnvelope second = publisher.Enqueue("second", "{}");

        Assert.True(second.Seq > first.Seq, "Sequence numbers must increase.");
    }
}

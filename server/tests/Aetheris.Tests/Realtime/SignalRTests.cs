using Aetheris.Host.Realtime;
using Aetheris.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Realtime;

/// <summary>
/// Tests the SignalR hub: a client connects and receives a numbered event.
/// </summary>
/// <remarks>
/// This is acceptance criterion 5 of task 00001.
/// </remarks>
public sealed class SignalRTests : IClassFixture<AetherisTestFactory>
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
        string hubUrl = _factory.Server.BaseAddress
            ?? throw new InvalidOperationException("The test server has no base address.");

        await using HubConnection connection = new HubConnectionBuilder()
            .WithUrl(new Uri(hubUrl, GameHub.Route), options =>
                options.Transports = HttpTransportType.WebSockets)
            .Build();

        await connection.StartAsync().ConfigureAwait(false);

        TaskCompletionSource<GameEventEnvelope> received = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using IDisposable subscription = connection.On<GameEventEnvelope>(
            GameEventPublisher.ReceiveMethod,
            envelope => received.TrySetResult(envelope)).ConfigureAwait(false);

        await connection.InvokeAsync("PublishTestEvent", "test_event", "{\"value\":1}").ConfigureAwait(false);

        Task completed = await Task.WhenAny(received.Task, Task.Delay(ReceiveTimeout)).ConfigureAwait(false);

        Assert.True(
            completed == received.Task,
            $"No event was received within {ReceiveTimeout.TotalSeconds:0} seconds.");

        GameEventEnvelope envelope = await received.Task.ConfigureAwait(false);

        Assert.True(envelope.Seq > 0, "The event sequence number must be positive.");
        Assert.Equal("test_event", envelope.Type);
        Assert.Equal("{\"value\":1}", envelope.Payload);
    }

    /// <summary>
    /// Two consecutive events receive strictly increasing sequence numbers.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Event_sequence_numbers_increase()
    {
        GameEventPublisher publisher = _factory.Services.GetRequiredService<GameEventPublisher>();

        GameEventEnvelope first = publisher.Enqueue("first", "{}");
        GameEventEnvelope second = publisher.Enqueue("second", "{}");

        Assert.True(second.Seq > first.Seq, "Sequence numbers must increase.");
    }
}

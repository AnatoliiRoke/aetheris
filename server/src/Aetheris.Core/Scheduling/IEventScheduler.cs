namespace Aetheris.Core.Scheduling;

/// <summary>
/// A request to run an action at a moment of game time (documentation/tech/07).
/// </summary>
/// <param name="SectorId">The sector that owns the event.</param>
/// <param name="Type">The event type, answered by an <see cref="IScheduledEventHandler"/>.</param>
/// <param name="RunAt">The moment of game time when the event runs.</param>
/// <param name="Payload">The JSON payload of the event.</param>
/// <param name="DedupeKey">An optional key that keeps the same action from creating two pending events.</param>
public sealed record ScheduledEventRequest(
    int SectorId,
    string Type,
    DateTimeOffset RunAt,
    string Payload,
    string? DedupeKey = null);

/// <summary>
/// A due event handed to its handler.
/// </summary>
/// <param name="Id">The identifier of the event.</param>
/// <param name="SectorId">The sector that owns the event.</param>
/// <param name="Type">The event type.</param>
/// <param name="RunAt">The moment of game time the event was scheduled for.</param>
/// <param name="Payload">The JSON payload of the event.</param>
public sealed record ScheduledEvent(long Id, int SectorId, string Type, DateTimeOffset RunAt, string Payload);

/// <summary>
/// Stores events of the scheduler.
/// </summary>
/// <remarks>
/// The event is written in the transaction of the current request
/// (<see cref="Data.IDatabaseSession"/>), so it exists exactly when the change that caused
/// it is committed.
/// </remarks>
public interface IEventScheduler
{
    /// <summary>
    /// Adds an event. A pending event with the same dedupe key is not added twice.
    /// </summary>
    /// <param name="request">The event.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the event was added.</returns>
    Task<bool> ScheduleAsync(ScheduledEventRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Executes the due events of one type.
/// </summary>
/// <remarks>
/// The handler runs inside the transaction that marks the event done, so its changes and
/// the status of the event are committed together, and an event is never applied twice.
/// A module registers one handler per event type it owns.
/// </remarks>
public interface IScheduledEventHandler
{
    /// <summary>
    /// Gets the event type this handler executes.
    /// </summary>
    string EventType { get; }

    /// <summary>
    /// Executes an event.
    /// </summary>
    /// <param name="scheduledEvent">The due event.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the event has been applied.</returns>
    Task HandleAsync(ScheduledEvent scheduledEvent, CancellationToken cancellationToken);
}

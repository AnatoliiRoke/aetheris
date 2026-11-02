using System.Diagnostics;
using Aetheris.Core.Configuration;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Observability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog.Context;

namespace Aetheris.Host.Scheduling;

/// <summary>
/// Executes the due scheduled events strictly in the order of their game time
/// (documentation/tech/07, section 3).
/// </summary>
/// <remarks>
/// Every event runs in its own transaction: the row is locked, the handler of its type
/// applies the consequences, and the event is marked done. A repeated pass after a crash
/// finds the event still pending and applies it once. A failing handler marks its event
/// failed and does not stop the queue.
/// </remarks>
public sealed class ScheduledEventRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IGameClock _clock;
    private readonly IGameConfig _config;
    private readonly ObservabilitySettings _observability;
    private readonly ILogger<ScheduledEventRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledEventRunner"/> class.
    /// </summary>
    /// <param name="scopeFactory">Creates a scope for every event.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="config">The configuration with the batch size.</param>
    /// <param name="observability">The parameters of the tracing, for the timing of the events.</param>
    /// <param name="logger">The logger.</param>
    public ScheduledEventRunner(
        IServiceScopeFactory scopeFactory,
        IGameClock clock,
        IGameConfig config,
        ObservabilitySettings observability,
        ILogger<ScheduledEventRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(observability);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _clock = clock;
        _config = config;
        _observability = observability;
        _logger = logger;
    }

    /// <summary>
    /// Executes the events whose game time has come.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of events that were applied.</returns>
    public async Task<int> RunDueEventsAsync(CancellationToken cancellationToken = default)
    {
        long passTimestamp = Stopwatch.GetTimestamp();
        DateTimeOffset gameNow = _clock.GameNow;
        int batchSize = (int)Math.Max(1, _config.GetInt64(SchedulerKeys.BatchSize, 1));

        List<long> dueIds;

        await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
        {
            AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

            dueIds = await dbContext.ScheduledEvents
                .AsNoTracking()
                .Where(scheduled => scheduled.Status == ScheduledEventStatus.Pending && scheduled.RunAt <= gameNow)
                .OrderBy(scheduled => scheduled.RunAt)
                .ThenBy(scheduled => scheduled.Seq)
                .ThenBy(scheduled => scheduled.Id)
                .Select(scheduled => scheduled.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        int applied = 0;

        foreach (long id in dueIds)
        {
            if (await RunOneAsync(id, cancellationToken).ConfigureAwait(false))
            {
                applied++;
            }
        }

        // An empty pass is the common case and is not written, so the log is not filled every
        // few seconds.
        if (dueIds.Count > 0 && _observability.Current.TraceEnabled)
        {
            _logger.LogInformation(
                "Scheduler pass: {DueEvents} due, {AppliedEvents} applied in {PassMs:0.0} ms.",
                dueIds.Count,
                applied,
                Stopwatch.GetElapsedTime(passTimestamp).TotalMilliseconds);
        }

        return applied;
    }

    private async Task<bool> RunOneAsync(long id, CancellationToken cancellationToken)
    {
        // Every event is a unit of work of its own: its identifier ties its log lines together
        // and its SQL commands are counted like those of a request (task 00036).
        ObservabilitySnapshot observability = _observability.Current;
        RequestTrace? trace = observability.TraceEnabled ? new RequestTrace("event-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
        long eventTimestamp = Stopwatch.GetTimestamp();

        using IDisposable? traceScope = trace?.Begin();
        using IDisposable? logScope = trace is null ? null : LogContext.PushProperty("RequestId", trace.RequestId);

        (bool handled, string? eventType) = await RunOneCoreAsync(id, cancellationToken).ConfigureAwait(false);

        if (handled && trace is not null)
        {
            double eventMilliseconds = Stopwatch.GetElapsedTime(eventTimestamp).TotalMilliseconds;

            _logger.Log(
                eventMilliseconds >= observability.SlowRequestMilliseconds ? LogLevel.Warning : LogLevel.Information,
                "Scheduled event {EventId} of type {EventType} handled in {EventMs:0.0} ms, db {DbMs:0.0} ms in {DbCommands} commands.",
                id,
                eventType,
                eventMilliseconds,
                trace.DatabaseMilliseconds,
                trace.Commands);
        }

        return handled;
    }

    private async Task<(bool Handled, string? EventType)> RunOneCoreAsync(long id, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();

        ScheduledEventEntity? scheduled;

        await using (IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            // The lock keeps a second runner away from the same event; a skipped row is
            // handled by whoever holds it.
            scheduled = (await dbContext.ScheduledEvents
                .FromSql($"SELECT * FROM scheduled_event WHERE id = {id} AND status = {ScheduledEventStatus.Pending} FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false)).FirstOrDefault();

            if (scheduled is null)
            {
                return (false, null);
            }

            IScheduledEventHandler? handler = services.GetServices<IScheduledEventHandler>()
                .FirstOrDefault(candidate => string.Equals(candidate.EventType, scheduled.Type, StringComparison.Ordinal));

            try
            {
                if (handler is null)
                {
                    throw new InvalidOperationException($"No handler is registered for the event type '{scheduled.Type}'.");
                }

                await handler.HandleAsync(
                    new ScheduledEvent(scheduled.Id, scheduled.SectorId, scheduled.Type, scheduled.RunAt, scheduled.Payload),
                    cancellationToken).ConfigureAwait(false);

                scheduled.Status = ScheduledEventStatus.Done;
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                return (true, scheduled.Type);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The scheduled event {EventId} of type {EventType} failed.", scheduled.Id, scheduled.Type);
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        dbContext.ChangeTracker.Clear();

        await dbContext.ScheduledEvents
            .Where(entry => entry.Id == id && entry.Status == ScheduledEventStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.Status, ScheduledEventStatus.Failed), cancellationToken)
            .ConfigureAwait(false);

        return (false, null);
    }
}

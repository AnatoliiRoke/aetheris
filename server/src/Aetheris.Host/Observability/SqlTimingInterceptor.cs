using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aetheris.Host.Observability;

/// <summary>
/// Measures every SQL command of every database context of the server (task 00036).
/// </summary>
/// <remarks>
/// <para>
/// The duration and the count go to the <see cref="RequestTrace"/> of the current request.
/// A command slower than <c>observability.sql.slow_ms</c> is written as a warning, and with
/// <c>observability.sql.verbose</c> every command is written.
/// </para>
/// <para>
/// Only the text of the command is written, with the names of its parameters
/// (<c>@p0</c>), never their values, which may hold a password or an e-mail address. The
/// interceptor is registered once in the service provider of the application, so every context
/// of every module gets it without a line in the module.
/// </para>
/// </remarks>
public sealed class SqlTimingInterceptor : DbCommandInterceptor
{
    private readonly ObservabilitySettings _settings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlTimingInterceptor"/> class.
    /// </summary>
    /// <param name="settings">The parameters of the observability.</param>
    /// <param name="loggerFactory">Creates the logger.</param>
    public SqlTimingInterceptor(ObservabilitySettings settings, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _settings = settings;
        _logger = loggerFactory.CreateLogger("Aetheris.Sql");
    }

    /// <inheritdoc />
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(command, eventData.Duration, failed: false);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, failed: false);

        return new ValueTask<int>(result);
    }

    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(command, eventData.Duration, failed: false);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, failed: false);

        return new ValueTask<DbDataReader>(result);
    }

    /// <inheritdoc />
    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Record(command, eventData.Duration, failed: false);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, failed: false);

        return new ValueTask<object?>(result);
    }

    /// <inheritdoc />
    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Record(command, eventData.Duration, failed: true);
    }

    /// <inheritdoc />
    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record(command, eventData.Duration, failed: true);

        return Task.CompletedTask;
    }

    private void Record(DbCommand command, TimeSpan duration, bool failed)
    {
        ObservabilitySnapshot settings = _settings.Current;

        if (!settings.TraceEnabled)
        {
            return;
        }

        RequestTrace.Current?.AddCommand(duration);

        double milliseconds = duration.TotalMilliseconds;
        bool slow = milliseconds >= settings.SqlSlowMilliseconds;

        if (!slow && !settings.SqlVerbose && !failed)
        {
            return;
        }

        string text = SqlText.Shorten(command.CommandText, settings.SqlMaxTextLength);

        if (slow || failed)
        {
            _logger.LogWarning(
                "Slow or failed SQL command: {SqlMs:0.0} ms, failed {SqlFailed}: {SqlText:l}",
                milliseconds,
                failed,
                text);
        }
        else
        {
            _logger.LogInformation("SQL command: {SqlMs:0.0} ms: {SqlText:l}", milliseconds, text);
        }
    }
}

/// <summary>
/// Measures the transactions of the database contexts, to find long locks (task 00036).
/// </summary>
public sealed class TransactionTimingInterceptor : DbTransactionInterceptor
{
    private readonly ObservabilitySettings _settings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionTimingInterceptor"/> class.
    /// </summary>
    /// <param name="settings">The parameters of the observability.</param>
    /// <param name="loggerFactory">Creates the logger.</param>
    public TransactionTimingInterceptor(ObservabilitySettings settings, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _settings = settings;
        _logger = loggerFactory.CreateLogger("Aetheris.Sql");
    }

    /// <inheritdoc />
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
    {
        Record("committed", eventData.Duration);
    }

    /// <inheritdoc />
    public override Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record("committed", eventData.Duration);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        Record("rolled back", eventData.Duration);
    }

    /// <inheritdoc />
    public override Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Record("rolled back", eventData.Duration);

        return Task.CompletedTask;
    }

    private void Record(string outcome, TimeSpan duration)
    {
        ObservabilitySnapshot settings = _settings.Current;

        if (!settings.TraceEnabled)
        {
            return;
        }

        double milliseconds = duration.TotalMilliseconds;

        if (milliseconds >= settings.SqlSlowMilliseconds)
        {
            _logger.LogWarning("Slow transaction {TransactionOutcome}: {TransactionMs:0.0} ms.", outcome, milliseconds);
        }
        else if (settings.SqlVerbose)
        {
            _logger.LogInformation("Transaction {TransactionOutcome}: {TransactionMs:0.0} ms.", outcome, milliseconds);
        }
    }
}

/// <summary>
/// Measures the saves of the database contexts (task 00036).
/// </summary>
public sealed class SaveChangesTimingInterceptor : SaveChangesInterceptor
{
    private readonly ObservabilitySettings _settings;
    private readonly ILogger _logger;
    private readonly ConditionalWeakTable<DbContext, StartMark> _starts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveChangesTimingInterceptor"/> class.
    /// </summary>
    /// <param name="settings">The parameters of the observability.</param>
    /// <param name="loggerFactory">Creates the logger.</param>
    public SaveChangesTimingInterceptor(ObservabilitySettings settings, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _settings = settings;
        _logger = loggerFactory.CreateLogger("Aetheris.Sql");
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Start(eventData.Context);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Start(eventData.Context);

        return new ValueTask<InterceptionResult<int>>(result);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Finish(eventData.Context, result);

        return result;
    }

    /// <inheritdoc />
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Finish(eventData.Context, result);

        return new ValueTask<int>(result);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Finish(eventData.Context, saved: -1);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Finish(eventData.Context, saved: -1);

        return Task.CompletedTask;
    }

    private void Start(DbContext? context)
    {
        if (context is null || !_settings.Current.TraceEnabled)
        {
            return;
        }

        _starts.AddOrUpdate(context, new StartMark(TimeProvider.System.GetTimestamp()));
    }

    private void Finish(DbContext? context, int saved)
    {
        if (context is null || !_starts.TryGetValue(context, out StartMark? mark))
        {
            return;
        }

        _starts.Remove(context);

        ObservabilitySnapshot settings = _settings.Current;
        double milliseconds = TimeProvider.System.GetElapsedTime(mark.Timestamp).TotalMilliseconds;

        if (milliseconds >= settings.SqlSlowMilliseconds)
        {
            _logger.LogWarning(
                "Slow save of {ContextName}: {SaveMs:0.0} ms, {SavedRows} rows.",
                context.GetType().Name,
                milliseconds,
                saved);
        }
        else if (settings.SqlVerbose)
        {
            _logger.LogInformation(
                "Save of {ContextName}: {SaveMs:0.0} ms, {SavedRows} rows.",
                context.GetType().Name,
                milliseconds,
                saved);
        }
    }

    private sealed class StartMark(long timestamp)
    {
        public long Timestamp { get; } = timestamp;
    }
}

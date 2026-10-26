using Aetheris.Host.Data;
using Aetheris.Host.Security;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Commands;

/// <summary>
/// Removes the request identifiers older than <c>security.idempotency.retention_hours</c>.
/// </summary>
public sealed class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotencyCleanupService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to get a database context.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public IdempotencyCleanupService(
        IServiceScopeFactory scopeFactory,
        SecuritySettings settings,
        TimeProvider timeProvider,
        ILogger<IdempotencyCleanupService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Removes the expired request identifiers once.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of removed rows.</returns>
    public async Task<int> CleanAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset threshold = _timeProvider.GetUtcNow() - _settings.IdempotencyRetention;

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        int removed = await dbContext.RequestIdempotency
            .Where(entry => entry.CreatedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // The sign-in attempts only count within the lockout window (task 00006).
        DateTimeOffset attemptThreshold = _timeProvider.GetUtcNow() - _settings.LoginLockout;

        removed += await dbContext.SignInAttempts
            .Where(entry => entry.At < attemptThreshold)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return removed;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int removed = await CleanAsync(stoppingToken).ConfigureAwait(false);
                _logger.LogDebug("Removed {Count} expired request identifiers.", removed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // A failed cleanup is retried at the next interval; it must not stop the server.
                _logger.LogError(exception, "Failed to remove expired request identifiers.");
            }

            // The interval is read again every time, so a change in the database applies.
            await Task.Delay(_settings.IdempotencyCleanupInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
        }
    }
}

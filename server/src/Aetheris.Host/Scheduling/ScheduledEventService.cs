using Aetheris.Core.Configuration;

namespace Aetheris.Host.Scheduling;

/// <summary>
/// Wakes the scheduler at the interval from the configuration.
/// </summary>
/// <remarks>
/// The first version checks the table at a fixed interval. The in-memory queue per sector
/// of documentation/tech/07, section 3.1 replaces the polling once the number of events
/// needs it; the order and the once-only execution stay in <see cref="ScheduledEventRunner"/>.
/// </remarks>
public sealed class ScheduledEventService : BackgroundService
{
    private readonly ScheduledEventRunner _runner;
    private readonly IGameConfig _config;
    private readonly ILogger<ScheduledEventService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledEventService"/> class.
    /// </summary>
    /// <param name="runner">The runner of the due events.</param>
    /// <param name="config">The configuration with the interval.</param>
    /// <param name="logger">The logger.</param>
    public ScheduledEventService(ScheduledEventRunner runner, IGameConfig config, ILogger<ScheduledEventService> logger)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _runner = runner;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _runner.RunDueEventsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "The scheduler pass failed.");
            }

            TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, _config.GetInt64(SchedulerKeys.PollSeconds, 1)));

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

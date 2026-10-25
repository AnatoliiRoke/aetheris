namespace Aetheris.Host.Configuration;

/// <summary>
/// Background service that reloads the game configuration on a fixed interval.
/// </summary>
/// <remarks>
/// The actual reload logic lives in <see cref="ConfigReloader"/>, so that it can be called
/// directly from tests and from the admin panel.
/// </remarks>
public sealed class ConfigReloadService : BackgroundService
{
    /// <summary>
    /// The number of seconds between two reloads of the game configuration.
    /// </summary>
    private const int ReloadIntervalSeconds = 30;

    private static readonly TimeSpan ReloadInterval = TimeSpan.FromSeconds(ReloadIntervalSeconds);

    private readonly ConfigReloader _reloader;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigReloadService"/> class.
    /// </summary>
    /// <param name="reloader">The reloader.</param>
    public ConfigReloadService(ConfigReloader reloader)
    {
        ArgumentNullException.ThrowIfNull(reloader);

        _reloader = reloader;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _reloader.ReloadAsync(stoppingToken).ConfigureAwait(false);

        // PeriodicTimer waits asynchronously, so a slow database cannot block the loop and
        // no reload notifications pile up.
        using PeriodicTimer timer = new(ReloadInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await _reloader.ReloadAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}

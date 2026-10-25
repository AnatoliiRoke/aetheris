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
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromSeconds(30);

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
        await _reloader.ReloadAsync(stoppingToken);

        using Timer timer = new(
            _ => _reloader.ReloadAsync(stoppingToken).GetAwaiter().GetResult(),
            state: null,
            dueTime: ReloadInterval,
            period: ReloadInterval);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}

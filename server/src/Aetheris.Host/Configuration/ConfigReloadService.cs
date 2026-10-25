using Aetheris.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Host.Configuration;

/// <summary>
/// Keeps the in-memory game configuration in sync with the database.
/// </summary>
/// <remarks>
/// Reloading replaces the whole parameter set atomically, so a rule that reads two
/// parameters always sees values from the same configuration version
/// (task 00001, section 2.4).
/// </remarks>
public sealed class ConfigReloadService : BackgroundService
{
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameConfig _config;
    private readonly ILogger<ConfigReloadService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigReloadService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to resolve the parameter store.</param>
    /// <param name="config">The in-memory configuration to update.</param>
    /// <param name="logger">The logger.</param>
    public ConfigReloadService(
        IServiceScopeFactory scopeFactory,
        GameConfig config,
        ILogger<ConfigReloadService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Performs an immediate reload of the configuration.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the reload has finished.</returns>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IConfigParameterStore store = scope.ServiceProvider.GetRequiredService<IConfigParameterStore>();

            IReadOnlyList<KeyValuePair<string, ConfigParameter>> values =
                await store.ReadAllAsync(cancellationToken).ConfigureAwait(false);

            _config.Replace(values);

            _logger.LogDebug(
                "Game configuration reloaded: {Count} parameters, version {Version}.",
                values.Count,
                _config.Version);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // A failed reload must not take the server down: the previous values stay in use.
            _logger.LogError(exception, "Failed to reload the game configuration.");
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReloadAsync(stoppingToken).ConfigureAwait(false);

        using Timer timer = new(
            _ => ReloadAsync(stoppingToken).GetAwaiter().GetResult(),
            state: null,
            dueTime: ReloadInterval,
            period: ReloadInterval);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken).ConfigureAwait(false);
    }
}

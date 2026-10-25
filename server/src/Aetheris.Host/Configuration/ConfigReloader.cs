using Aetheris.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Host.Configuration;

/// <summary>
/// Reloads the game configuration from the database.
/// </summary>
/// <remarks>
/// Reloading replaces the whole parameter set atomically, so a rule that reads two
/// parameters always sees values from the same configuration version
/// (task 00001, section 2.4).
/// </remarks>
public sealed class ConfigReloader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GameConfig _config;
    private readonly ILogger<ConfigReloader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigReloader"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to resolve the parameter store.</param>
    /// <param name="config">The in-memory configuration to update.</param>
    /// <param name="logger">The logger.</param>
    public ConfigReloader(
        IServiceScopeFactory scopeFactory,
        GameConfig config,
        ILogger<ConfigReloader> logger)
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
                await store.ReadAllAsync(cancellationToken);

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
}

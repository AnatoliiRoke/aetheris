using Aetheris.Market.Services;

namespace Aetheris.Market;

/// <summary>
/// The implementation of <see cref="IMarketModule"/>: a thin front of the services of the
/// module.
/// </summary>
public sealed class MarketModule : IMarketModule
{
    private readonly MarketBootstrapService _bootstrap;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the module.</param>
    public MarketModule(MarketBootstrapService bootstrap)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);

        _bootstrap = bootstrap;
    }

    /// <inheritdoc />
    public string ModuleName => "Market";

    /// <inheritdoc />
    public Task EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);
}

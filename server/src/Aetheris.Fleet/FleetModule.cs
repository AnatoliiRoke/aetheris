using Aetheris.Fleet.Api;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet;

/// <summary>
/// The implementation of <see cref="IFleetModule"/>.
/// </summary>
/// <remarks>
/// The module keeps the projects of the ships and the ships themselves in its own tables, so
/// it works on the context of the request (tasks 00038 and 00039).
/// </remarks>
public sealed class FleetModule : IFleetModule
{
    private readonly FleetBootstrapService _bootstrap;
    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetModule"/> class.
    /// </summary>
    /// <param name="bootstrap">The preparation of the hull levels.</param>
    /// <param name="fleets">The fleets of the players.</param>
    public FleetModule(FleetBootstrapService bootstrap, FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(fleets);

        _bootstrap = bootstrap;
        _fleets = fleets;
    }

    /// <inheritdoc />
    public string ModuleName => "Fleet";

    /// <inheritdoc />
    public Task<FleetBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default) =>
        _bootstrap.EnsureAsync(cancellationToken);

    /// <inheritdoc />
    public Task LoseFleetAsync(long fleetId, CancellationToken cancellationToken = default) =>
        _fleets.LoseAsync(fleetId, cancellationToken);

    /// <inheritdoc />
    public Task<FleetFacts> GetFleetFactsAsync(string playerId, CancellationToken cancellationToken = default) =>
        _fleets.GetFactsAsync(playerId, cancellationToken);
}

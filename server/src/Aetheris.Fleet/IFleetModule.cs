using Aetheris.Fleet.Api;
using Aetheris.Fleet.Services;

namespace Aetheris.Fleet;

/// <summary>
/// The public contract of the Fleet module (task 00038): the projects of the ships.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8). The
/// module reads the world, the bodies and the production through their contracts and keeps
/// its own tables.
/// </remarks>
public interface IFleetModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Prepares the module: writes the hull levels a shipyard opens when they are missing.
    /// A repeated call leaves a row that is already there.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    Task<FleetBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loses a fleet for good: the fleet and every ship in it are gone, with the crew (task 00040).
    /// The battles of a later stage call this; there is no commander to lose yet.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the fleet is lost.</returns>
    Task LoseFleetAsync(long fleetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads what the other modules need to know about the fleets of a player, for example the
    /// rating (task 00040). It only reads: the formula of the rating is not changed here.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the fleets and the ships, and what the ships cost.</returns>
    Task<FleetFacts> GetFleetFactsAsync(string playerId, CancellationToken cancellationToken = default);
}

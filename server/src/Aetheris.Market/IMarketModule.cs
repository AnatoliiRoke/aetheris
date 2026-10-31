namespace Aetheris.Market;

/// <summary>
/// The public contract of the market module: the levels of the market, the lots, the deals, the
/// deliveries and the exchanges (task 00024).
/// </summary>
/// <remarks>
/// The bodies, the buildings, the resources, the parts and the currencies belong to the other
/// modules, so this module reads them through their contracts and the other modules change their
/// balances through this one (documentation/tech/01, section 8).
/// </remarks>
public interface IMarketModule
{
    /// <summary>Gets the module name, used in logs and diagnostics.</summary>
    string ModuleName { get; }

    /// <summary>
    /// Fills the tables of the module with the starting data when they are empty.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the data is written.</returns>
    Task EnsureAsync(CancellationToken cancellationToken = default);
}

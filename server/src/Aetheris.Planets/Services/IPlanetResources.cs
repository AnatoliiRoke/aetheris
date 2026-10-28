namespace Aetheris.Planets.Services;

/// <summary>
/// The balance of a body, for the modules that pay for something on a body (task 00012).
/// </summary>
/// <remarks>
/// The bodies module owns the balance, so the other modules change it through this contract
/// and never through its tables (documentation/tech/01, section 8). A change joins the
/// transaction of the request, so the payment and the record of the queue are committed
/// together.
/// </remarks>
public interface IPlanetResources
{
    /// <summary>
    /// Takes the amounts from the balance of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="amounts">The amounts by resource.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are taken.</returns>
    /// <exception cref="Core.Errors.GameException">The body lacks the resources.</exception>
    Task TakeAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the amounts back to the balance of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="amounts">The amounts by resource.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are given back.</returns>
    Task GiveAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the balance of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The amounts by resource.</returns>
    Task<IReadOnlyDictionary<int, long>> GetBalanceAsync(
        long planetId,
        string playerId,
        CancellationToken cancellationToken = default);
}

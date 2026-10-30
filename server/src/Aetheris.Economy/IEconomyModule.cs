using Aetheris.Economy.Api;

namespace Aetheris.Economy;

/// <summary>
/// The public contract of the economy module: the population of the bodies and the balances
/// of the civilizations (task 00015).
/// </summary>
/// <remarks>
/// The bodies, the buildings, the technologies and the resources belong to the other modules,
/// so this module reads them through their contracts and the other modules change the
/// balances through this one (documentation/tech/01, section 8).
/// </remarks>
public interface IEconomyModule
{
    /// <summary>Gets the module name, used in logs and diagnostics.</summary>
    string ModuleName { get; }

    /// <summary>
    /// Fills the tables of the module with the starting data when they are empty.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the data is written.</returns>
    Task EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the population of a body of a player at a moment of game time, with the largest
    /// population it may have and the speed it grows or dies down with.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The population, or <see langword="null"/> when the body is not a colony of the player.</returns>
    Task<PopulationFacts?> GetPopulationAsync(string playerId, long planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the populations of the bodies of a player at a moment of game time.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The populations of the bodies.</returns>
    Task<IReadOnlyList<PopulationFacts>> GetPopulationsAsync(string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the balance of a currency of a civilization at a moment of game time.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="resourceTypeId">The currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The balance and the income it gets in an hour.</returns>
    Task<CurrencyBalanceFacts> GetBalanceAsync(
        string playerId,
        int resourceTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the balances of all the currencies of a civilization at a moment of game time.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The amounts by currency; a currency the player has none of is there with zero.</returns>
    Task<IReadOnlyDictionary<int, long>> GetBalancesAsync(string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to take inhabitants of a body, for the crew of a ship: all of them or none, in one
    /// change that two crews never share.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="amount">How many inhabitants to take.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the inhabitants are taken, <see langword="false"/> when the body has fewer of them and nothing is taken.</returns>
    /// <exception cref="Core.Errors.GameException">The body is not a colony of the player, or the amount is negative.</exception>
    Task<bool> TryTakePopulationAsync(string playerId, long planetId, long amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives inhabitants back to a body, for a crew that returns. A population above the largest
    /// population is not refused: it dies down.
    /// </summary>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="amount">How many inhabitants to give back.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the inhabitants are given back.</returns>
    /// <exception cref="Core.Errors.GameException">The body is not a colony of the player, or the amount is negative.</exception>
    Task GivePopulationAsync(string playerId, long planetId, long amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the amounts from the balance of a currency of a civilization. The balance never
    /// goes below zero.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="amounts">The amounts by currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are taken.</returns>
    /// <exception cref="Core.Errors.GameException">The civilization lacks the currency.</exception>
    Task TakeAsync(
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives the amounts back to the balance of a currency of a civilization.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="amounts">The amounts by currency.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are given back.</returns>
    Task GiveAsync(
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings the balances and the populations of a civilization to a moment with the rates as
    /// they are, so a change of the bonuses of the player applies only from that moment on
    /// (documentation/tech/09, section 5.2).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="moment">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the balances are fixed.</returns>
    Task FixPlayerAsync(string playerId, DateTimeOffset moment, CancellationToken cancellationToken = default);
}

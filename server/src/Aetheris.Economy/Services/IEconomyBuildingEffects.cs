namespace Aetheris.Economy.Services;

/// <summary>
/// The effects of the buildings of the bodies, for the module of the economy (task 00015,
/// decision 2).
/// </summary>
/// <remarks>
/// The contract is declared here and implemented in the bodies module, so the economy does
/// not depend on the bodies module and the bodies module does not depend on the economy.
/// This is the pattern of the project: the contract is declared by the module that reads the
/// data and implemented by the module that owns it (see <c>ITechnologyLevels</c>).
/// </remarks>
public interface IEconomyBuildingEffects
{
    /// <summary>
    /// Gets what the buildings and the technologies of the owner give the economy of bodies, in
    /// one read for all of them.
    /// </summary>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The effects by body; a body that is not in the answer has no effects.</returns>
    Task<IReadOnlyDictionary<long, PlanetEconomyEffects>> GetAsync(
        IReadOnlyCollection<long> planetIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effects of the buildings of the bodies of one owner, so the owners of the bodies are
    /// not read again.
    /// </summary>
    /// <param name="planetIds">The bodies; every one of them belongs to the owner.</param>
    /// <param name="ownerPlayerId">The owner of the bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The effects by body.</returns>
    Task<IReadOnlyDictionary<long, PlanetEconomyEffects>> GetAsync(
        IReadOnlyCollection<long> planetIds,
        string ownerPlayerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What the buildings of a body give its economy (task 00015).
/// </summary>
/// <param name="PopulationMax">The largest population of the body, with the bonus of the technologies of its owner: the one count the bodies module shows too.</param>
/// <param name="PopulationGrowthEffect">The sum of the growth effects of the buildings of the body: the multiplier of the speed the population grows with, as the data gives it for the level (it has a fraction); a body without such a building has zero.</param>
/// <param name="CreditIncomeBonus">The sum of the credit income effects of the buildings of the body: the share of the income the buildings of the body add.</param>
public sealed record PlanetEconomyEffects(long PopulationMax, decimal PopulationGrowthEffect, decimal CreditIncomeBonus = 0m);

namespace Aetheris.Economy.Services;

/// <summary>
/// The effects of the buildings of a body, for the module of the economy (task 00015,
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
    /// Gets the sum of the effects of a kind of the buildings of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="effectType">The kind of the effect, one of <see cref="EconomyCodes"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum of the values of the effect.</returns>
    Task<long> GetEffectTotalAsync(long planetId, string effectType, CancellationToken cancellationToken = default);
}

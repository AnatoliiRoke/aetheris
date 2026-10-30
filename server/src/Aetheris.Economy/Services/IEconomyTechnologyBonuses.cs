namespace Aetheris.Economy.Services;

/// <summary>
/// The bonuses the technologies of a player give the economy (task 00015, decision 2).
/// </summary>
/// <remarks>
/// The contract is declared here and implemented in the research module, so the economy does
/// not depend on the research module and the research module does not depend on the economy.
/// </remarks>
public interface IEconomyTechnologyBonuses
{
    /// <summary>
    /// Gets the bonuses the technologies of a player give the population and the credits.
    /// </summary>
    /// <param name="playerId">The player, or <see langword="null"/> for a body without an owner.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bonuses.</returns>
    Task<EconomyBonuses> GetBonusesAsync(string? playerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The bonuses the technologies of a player give the economy (task 00015).
/// </summary>
/// <param name="PopulationGrowthMultiplier">The multiplier of the speed the population grows with.</param>
/// <param name="CreditIncomeMultiplier">The multiplier of the income of the credits.</param>
/// <remarks>
/// Every multiplier is a separate bonus of the technologies, so a technology changes only the
/// parameter it belongs to. A multiplier of one changes nothing. The multiplier of the largest
/// population is not here: the bodies module applies it, so the largest population is counted once.
/// </remarks>
public sealed record EconomyBonuses(
    decimal PopulationGrowthMultiplier = 1m,
    decimal CreditIncomeMultiplier = 1m);

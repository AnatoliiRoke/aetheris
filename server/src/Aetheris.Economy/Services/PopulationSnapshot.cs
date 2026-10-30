namespace Aetheris.Economy.Services;

/// <summary>
/// The population of a body at a moment of game time, with the rules that were applied to it
/// (task 00015).
/// </summary>
/// <param name="Amount">The number of the inhabitants.</param>
/// <param name="Maximum">The largest population the body may have.</param>
/// <param name="GrowthPerHour">How many inhabitants the population grows or dies down with in an hour.</param>
/// <param name="OverLimit">Whether the population is above the largest population it may have.</param>
/// <param name="CreditsPerInhabitant">How many credits one inhabitant brings in an hour.</param>
/// <param name="Facts">The facts for the client.</param>
internal sealed record PopulationSnapshot(
    long Amount,
    long Maximum,
    decimal GrowthPerHour,
    bool OverLimit,
    decimal CreditsPerInhabitant,
    Api.PopulationFacts Facts);

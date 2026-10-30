namespace Aetheris.Economy.Services;

/// <summary>
/// The costs of a civilization, split by where they are spent (task 00015, decision 5).
/// </summary>
/// <param name="PlanetResources">The costs that lie on a body and are paid from the balance of the body.</param>
/// <param name="Currencies">The costs that belong to the civilization and are paid from its balance.</param>
public sealed record SplitCosts(
    IReadOnlyDictionary<int, long> PlanetResources,
    IReadOnlyDictionary<int, long> Currencies)
{
    /// <summary>Gets the costs that are paid from the balance of the body.</summary>
    public IReadOnlyDictionary<int, long> Planet => PlanetResources;

    /// <summary>Gets the costs that are paid from the balance of the civilization.</summary>
    public IReadOnlyDictionary<int, long> Currency => Currencies;

    /// <summary>Gets whether the costs are empty.</summary>
    public bool IsEmpty => PlanetResources.Count == 0 && Currencies.Count == 0;
}

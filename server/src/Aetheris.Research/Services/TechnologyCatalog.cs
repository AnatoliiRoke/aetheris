namespace Aetheris.Research.Services;

/// <summary>
/// The tree of the technologies, their levels and their effects as they are in memory.
/// </summary>
/// <param name="Technologies">The technologies in the order of the lists.</param>
public sealed record TechnologyCatalog(IReadOnlyList<TechnologyInfo> Technologies)
{
    private readonly Dictionary<int, TechnologyInfo> _technologiesById =
        Technologies.ToDictionary(technology => technology.Id);

    private readonly Dictionary<string, IReadOnlyList<TechnologyInfo>> _technologiesByBranch =
        Technologies
            .GroupBy(technology => technology.Branch, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TechnologyInfo>)[.. group],
                StringComparer.Ordinal);

    /// <summary>
    /// Finds a technology.
    /// </summary>
    /// <param name="technologyId">The technology.</param>
    /// <returns>The technology, or <see langword="null"/>.</returns>
    public TechnologyInfo? FindTechnology(int technologyId) => _technologiesById.GetValueOrDefault(technologyId);

    /// <summary>
    /// Gets the technologies of a branch, in the order of the lists.
    /// </summary>
    /// <param name="branch">The branch, one of <see cref="TechnologyBranches"/>.</param>
    /// <returns>The technologies of the branch; empty when the branch has none.</returns>
    public IReadOnlyList<TechnologyInfo> TechnologiesOf(string branch) =>
        _technologiesByBranch.GetValueOrDefault(branch, []);
}

/// <summary>A technology with its levels.</summary>
/// <param name="Id">The technology.</param>
/// <param name="Code">The neutral code of the technology.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="Branch">The branch of the tree, one of <see cref="TechnologyBranches"/>.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="MaxLevel">The largest level of the technology.</param>
/// <param name="ResearchCenterLevel">The level of the science centre the research needs.</param>
/// <param name="Levels">The levels by number.</param>
public sealed record TechnologyInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    string Branch,
    int SortOrder,
    int MaxLevel,
    int ResearchCenterLevel,
    IReadOnlyDictionary<int, TechnologyLevelInfo> Levels);

/// <summary>A level of a technology.</summary>
/// <param name="Level">The level.</param>
/// <param name="DurationSeconds">The time of the research in game seconds.</param>
/// <param name="Costs">The costs by resource.</param>
/// <param name="Effects">The effects of the level.</param>
public sealed record TechnologyLevelInfo(
    int Level,
    long DurationSeconds,
    IReadOnlyDictionary<int, long> Costs,
    IReadOnlyList<TechnologyEffectInfo> Effects);

/// <summary>An effect of a level of a technology.</summary>
/// <param name="EffectType">The effect type, one of <see cref="TechnologyEffectTypes"/>.</param>
/// <param name="UnlockKind">The kind of the subject the effect opens, one of <see cref="TechnologyUnlockKinds"/>.</param>
/// <param name="UnlockTargetId">The subject the effect opens.</param>
/// <param name="Value">The value of the effect.</param>
public sealed record TechnologyEffectInfo(string EffectType, string? UnlockKind, int? UnlockTargetId, decimal Value);

namespace Aetheris.Research.Services;

/// <summary>
/// The tree of the technologies, their branches, levels and effects as they are in memory.
/// </summary>
/// <param name="Technologies">The technologies in the order of the lists, the archived ones included.</param>
/// <param name="Branches">The branches in the order of the tree, the archived ones included.</param>
public sealed record TechnologyCatalog(IReadOnlyList<TechnologyInfo> Technologies, IReadOnlyList<TechnologyBranchInfo> Branches)
{
    private readonly Dictionary<int, TechnologyInfo> _technologiesById =
        Technologies.ToDictionary(technology => technology.Id);

    private readonly Dictionary<int, TechnologyBranchInfo> _branchesById =
        Branches.ToDictionary(branch => branch.Id);

    private readonly Dictionary<int, IReadOnlyList<TechnologyInfo>> _technologiesByBranch =
        Technologies
            .GroupBy(technology => technology.BranchId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TechnologyInfo>)[.. group]);

    /// <summary>
    /// Finds a technology.
    /// </summary>
    /// <param name="technologyId">The technology.</param>
    /// <returns>The technology, or <see langword="null"/>.</returns>
    public TechnologyInfo? FindTechnology(int technologyId) => _technologiesById.GetValueOrDefault(technologyId);

    /// <summary>
    /// Finds a branch.
    /// </summary>
    /// <param name="branchId">The branch.</param>
    /// <returns>The branch, or <see langword="null"/>.</returns>
    public TechnologyBranchInfo? FindBranch(int branchId) => _branchesById.GetValueOrDefault(branchId);

    /// <summary>
    /// Gets the technologies of a branch, in the order of the lists.
    /// </summary>
    /// <param name="branchId">The branch.</param>
    /// <returns>The technologies of the branch; empty when the branch has none.</returns>
    public IReadOnlyList<TechnologyInfo> TechnologiesOf(int branchId) =>
        _technologiesByBranch.GetValueOrDefault(branchId, []);

    /// <summary>
    /// Checks whether a technology is open for a new research: neither it nor its branch is
    /// archived (task 00023, decision 5).
    /// </summary>
    /// <param name="technology">The technology.</param>
    /// <returns><see langword="true"/> when a research of it may start.</returns>
    public bool IsOffered(TechnologyInfo technology)
    {
        ArgumentNullException.ThrowIfNull(technology);

        return !technology.IsArchived && FindBranch(technology.BranchId) is { IsArchived: false };
    }
}

/// <summary>A branch of the tree.</summary>
/// <param name="Id">The branch.</param>
/// <param name="Code">The neutral code of the branch.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="SortOrder">The order in the tree.</param>
/// <param name="IsArchived">Whether the branch is archived.</param>
public sealed record TechnologyBranchInfo(int Id, string Code, string NameKey, int SortOrder, bool IsArchived);

/// <summary>A technology with its levels.</summary>
/// <param name="Id">The technology.</param>
/// <param name="Code">The neutral code of the technology.</param>
/// <param name="NameKey">The localisation key of the name.</param>
/// <param name="DescriptionKey">The localisation key of the description.</param>
/// <param name="BranchId">The branch of the tree.</param>
/// <param name="SortOrder">The order in the lists.</param>
/// <param name="MaxLevel">The largest level of the technology.</param>
/// <param name="ResearchCenterLevel">The level of the science centre the research needs.</param>
/// <param name="Levels">The levels by number.</param>
/// <param name="IsArchived">Whether the technology is archived: it works for its owners, no new research of it starts.</param>
public sealed record TechnologyInfo(
    int Id,
    string Code,
    string NameKey,
    string DescriptionKey,
    int BranchId,
    int SortOrder,
    int MaxLevel,
    int ResearchCenterLevel,
    IReadOnlyDictionary<int, TechnologyLevelInfo> Levels,
    bool IsArchived = false);

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

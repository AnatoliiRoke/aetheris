namespace Aetheris.Research.Services;

/// <summary>
/// The localisation keys of the branches of the tree of the technologies (task 00012).
/// </summary>
/// <remarks>
/// The branches are logical codes of the program, and the key of a branch is built from its
/// code, so a new branch arrives as a new row and its texts.
/// </remarks>
public static class ResearchBranchTextKeys
{
    /// <summary>The prefix of the keys of the branches.</summary>
    public const string Branch = "research.branch";

    /// <summary>
    /// Builds the key of the name of a branch.
    /// </summary>
    /// <param name="branch">The branch, one of <see cref="TechnologyBranches"/>.</param>
    /// <returns>The key.</returns>
    public static string Name(string branch) => $"{Branch}.{branch}.name";
}

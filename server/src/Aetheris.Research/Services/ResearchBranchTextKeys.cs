using Aetheris.World.Data;

namespace Aetheris.Research.Services;

/// <summary>
/// The neutral codes and the localisation keys of the branches of the tree of the technologies
/// (task 00023, decision 8): <c>branch_1</c>, <c>research.technology_branch.branch_1.name</c>.
/// </summary>
/// <remarks>
/// The branches are rows of the table <c>technology_branch</c>, so the code knows no branch: a new
/// branch arrives as a row and its texts.
/// </remarks>
public static class ResearchBranchTextKeys
{
    /// <summary>The group of the neutral codes of the branches.</summary>
    public const string CodeGroup = "branch";

    /// <summary>The prefix of the keys of the branches.</summary>
    public const string Branch = "research.technology_branch";

    /// <summary>
    /// Builds the key of the name of a branch.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string Name(string code) => WorldTextKeys.Name(Branch, code);

    /// <summary>
    /// Builds the key of the description of a branch.
    /// </summary>
    /// <param name="code">The neutral code.</param>
    /// <returns>The key.</returns>
    public static string Description(string code) => WorldTextKeys.Description(Branch, code);
}

namespace Aetheris.Research.Data.Entities;

/// <summary>
/// A technology of the tree of the technologies (task 00012, section 3.1).
/// </summary>
/// <remarks>
/// <para>
/// The code is given by the server and carries no meaning, and the name and the description
/// are localisation keys, so a new technology arrives as a row and its texts
/// (documentation/tech/09, sections 3.1 and 4).
/// </para>
/// <para>
/// The branch and the kinds of the effects are logical codes of the program
/// (documentation/tech/09, section 3.4), and the levels of the technology are rows of the
/// table of the levels.
/// </para>
/// </remarks>
public sealed class TechnologyEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the technology.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the branch of the tree, one of <see cref="Services.TechnologyBranches"/>.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>Gets or sets the order of the technology in the lists of its branch.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the largest level of the technology; one for a key technology.</summary>
    public int MaxLevel { get; set; } = 1;

    /// <summary>
    /// Gets or sets the level of the science centre the research of the technology needs.
    /// </summary>
    /// <remarks>
    /// The level is checked on the body the research starts from, because the research of a
    /// technology starts on a chosen body (task 00012, decisions 5 and 6).
    /// </remarks>
    public int ResearchCenterLevel { get; set; } = 1;

    /// <summary>Gets or sets the levels of the technology.</summary>
    public List<TechnologyLevelEntity> LevelIds { get; set; } = [];
}

namespace Aetheris.Research.Data.Entities;

/// <summary>
/// A branch of the tree of the technologies (task 00023, decision 8).
/// </summary>
/// <remarks>
/// A branch is data: a new branch is a new row with a neutral code given by the server and its
/// texts, so the tree gets it without a change of the code. A branch without technologies is
/// shown empty (task 00012, decision 11).
/// </remarks>
public sealed class TechnologyBranchEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the branch, given by the server.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture, or <see langword="null"/> for the neutral placeholder.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets the order of the branch in the tree.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the branch is archived: it is not shown for new
    /// researches, while the technologies of it the players already have keep working.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

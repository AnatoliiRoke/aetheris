namespace Aetheris.Research.Data.Entities;

/// <summary>
/// The cost of a level of a technology, in the resources of a body (task 00012, section 3.1).
/// </summary>
public sealed class TechnologyLevelCostEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the level of the technology.</summary>
    public int TechnologyLevelId { get; set; }

    /// <summary>Gets or sets the resource of the cost.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount of the resource.</summary>
    public long Amount { get; set; }
}

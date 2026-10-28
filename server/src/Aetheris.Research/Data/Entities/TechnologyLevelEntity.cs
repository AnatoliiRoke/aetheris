namespace Aetheris.Research.Data.Entities;

/// <summary>
/// A level of a technology: what it costs, how long it takes and what it gives (task 00012,
/// section 3.1).
/// </summary>
/// <remarks>
/// The requirements of the level live in the common requirements module, so the research
/// module never reads the tables of another module.
/// </remarks>
public sealed class TechnologyLevelEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the technology.</summary>
    public int TechnologyId { get; set; }

    /// <summary>Gets or sets the level of the technology.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the time of the research in game seconds.</summary>
    public long DurationSeconds { get; set; }

    /// <summary>Gets or sets the costs of the research by resource.</summary>
    public List<TechnologyLevelCostEntity> CostIds { get; set; } = [];

    /// <summary>Gets or sets the effects of the level.</summary>
    public List<TechnologyLevelEffectEntity> EffectIds { get; set; } = [];
}

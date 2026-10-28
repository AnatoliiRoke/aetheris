namespace Aetheris.Research.Data.Entities;

/// <summary>
/// A record of the queue of the researches of a player (task 00012, decision 5a).
/// </summary>
/// <remarks>
/// <para>
/// The queue is one for the whole civilization and the researches follow one another, so the
/// record that is under way holds the moment it finishes and the ones that wait have none.
/// </para>
/// <para>
/// The record names the body the research starts from, because the level of the science
/// centre of that body opens the technology (task 00012, decisions 5 and 6).
/// </para>
/// </remarks>
public sealed class ResearchQueueItemEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body the research starts from.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the technology.</summary>
    public int TechnologyId { get; set; }

    /// <summary>Gets or sets the level of the technology the record researches.</summary>
    public int TargetLevel { get; set; }

    /// <summary>Gets or sets the position of the record in the queue.</summary>
    public int Position { get; set; }

    /// <summary>Gets or sets the moment the research starts.</summary>
    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Gets or sets the moment the research finishes, or <see langword="null"/> when it waits.</summary>
    public DateTimeOffset? FinishesAt { get; set; }

    /// <summary>Gets or sets the resources the research paid for.</summary>
    public List<ResearchQueueCostEntity> CostIds { get; set; } = [];
}

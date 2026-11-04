namespace Aetheris.Research.Data.Entities;

/// <summary>
/// What a player has learned of a technology on a body (task 00012, decision 5).
/// </summary>
/// <remarks>
/// <para>
/// A technology is researched from a body, so every body has its own level of it: the level a
/// body has is the one its own research gave it. The level of the civilization is the largest
/// of the levels of its bodies, so the subjects a technology opens are open for the whole
/// civilization once any of its bodies has researched it.
/// </para>
/// <para>
/// The knowledge of a body cannot be lost: the row stays when the body does.
/// </para>
/// </remarks>
public sealed class PlayerTechnologyEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body the research ran on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the technology.</summary>
    public int TechnologyId { get; set; }

    /// <summary>Gets or sets the largest level of the technology the body has learned.</summary>
    public int Level { get; set; }
}

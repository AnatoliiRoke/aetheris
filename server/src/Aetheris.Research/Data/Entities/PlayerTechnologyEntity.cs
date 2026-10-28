namespace Aetheris.Research.Data.Entities;

/// <summary>
/// What a player has learned of a technology (task 00012, decision 5).
/// </summary>
/// <remarks>
/// The knowledge belongs to the player and not to a body, so it applies to every body of
/// the civilization and it cannot be lost.
/// </remarks>
public sealed class PlayerTechnologyEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the technology.</summary>
    public int TechnologyId { get; set; }

    /// <summary>Gets or sets the largest level of the technology the player has learned.</summary>
    public int Level { get; set; }
}

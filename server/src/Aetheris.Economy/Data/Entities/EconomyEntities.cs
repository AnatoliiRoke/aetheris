namespace Aetheris.Economy.Data.Entities;

/// <summary>
/// The population of a body with the moment of game time it was last counted at (task 00015).
/// </summary>
/// <remarks>
/// The population grows to the largest population the body has and dies down above it, so
/// the amount is kept with the moment it was counted at, and the value at a later moment is
/// counted when it is read (documentation/tech/09, section 5.2). The largest population
/// comes from the buildings and the technologies of the other modules, so this row holds no
/// foreign key into their tables.
/// </remarks>
public sealed class PlanetPopulationEntity
{
    /// <summary>Gets or sets the body, the primary key.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the sector of the body.</summary>
    public int SectorId { get; set; }

    /// <summary>Gets or sets the owner of the body.</summary>
    public string? OwnerPlayerId { get; set; }

    /// <summary>Gets or sets the number of the inhabitants at the moment of the last count.</summary>
    public long Amount { get; set; }

    /// <summary>Gets or sets the moment of game time the population was last counted at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

namespace Aetheris.Fleet.Data.Entities;

/// <summary>
/// A project of a ship: the hull the player chose and the modules he put into its slots
/// (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// <para>
/// A project belongs to the player, not to a body: it is available on every body of the
/// civilization (decision 6). The body in <see cref="PlanetId"/> is the one the project was
/// made on, so the builder shows it in the place it belongs to.
/// </para>
/// <para>
/// The parts live in the tables of the production and the bodies in the tables of the world,
/// so this table keeps only their identifiers and carries no foreign keys into the tables of
/// the other modules (documentation/tech/08, rule 5).
/// </para>
/// </remarks>
public sealed class ShipDesignEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owner of the project.</summary>
    public string OwnerPlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the body the project was made on.</summary>
    public long PlanetId { get; set; }

    /// <summary>Gets or sets the name of the project.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the hull of the project.</summary>
    public int HullPartTypeId { get; set; }

    /// <summary>
    /// Gets or sets whether the project has the extra slot of the race: it is allowed for the
    /// heavy hulls and the super hulls on the higher levels of the technology of the hull
    /// (decision 11).
    /// </summary>
    public bool HasRaceBonusSlot { get; set; }

    /// <summary>Gets or sets the moment of game time the project was created at.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the moment of game time the project was changed at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets whether the project is archived: it is kept, so a ship already built of it does not lose its link (task 00023).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; a change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A slot of a project of a ship: the part the player put into it (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// The slots are numbered from zero and have no gaps: the builder shows them in a row, so a
/// gap would be a hole on the screen.
/// </remarks>
public sealed class ShipDesignSlotEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the project the slot belongs to.</summary>
    public long DesignId { get; set; }

    /// <summary>Gets or sets the number of the slot, from zero.</summary>
    public int SlotIndex { get; set; }

    /// <summary>Gets or sets the part in the slot.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the mark of the part in the slot.</summary>
    public int MarkLevel { get; set; }

    /// <summary>Gets or sets whether the part in the slot carries the bonus of the race that made it.</summary>
    public bool HasRaceBonus { get; set; }
}

namespace Aetheris.Production.Data.Entities;

/// <summary>
/// A characteristic of the parts: a value every part of a group may have, for example the
/// thrust of a drive or the damage of a weapon (task 00027, section 3.1).
/// </summary>
/// <remarks>
/// <para>
/// The characteristic is data: its name and its unit of measure live in the texts of the
/// database, and a new characteristic arrives as a row and not as code. So a new group of
/// the parts gets its characteristics without a release of the server
/// (documentation/tech/09, section 2.2).
/// </para>
/// <para>
/// The kind of the value is the kind of the program: a number. A characteristic that needs a
/// table by the level keeps its levels in the values of the part, not here.
/// </para>
/// </remarks>
public sealed class CharacteristicEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the characteristic, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the unit of measure, or <see langword="null"/> when it has none.</summary>
    public string? UnitKey { get; set; }

    /// <summary>Gets or sets the key of the picture of the characteristic, or <see langword="null"/>.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Gets or sets the order of the characteristic in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether the characteristic is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A characteristic of a group of the parts: which characteristics the parts of the group
/// have, in which order they are shown and whether they are required (task 00027, section 3.1).
/// </summary>
/// <remarks>
/// The link is data, so a new characteristic is bound to a group in the admin panel and never
/// by a change of the code.
/// </remarks>
public sealed class PartGroupCharacteristicEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the group of the parts.</summary>
    public int PartGroupId { get; set; }

    /// <summary>Gets or sets the characteristic.</summary>
    public int CharacteristicId { get; set; }

    /// <summary>Gets or sets the order of the characteristic in the list of the group.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether a part of the group must have the value of the characteristic.</summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the link is archived: the link stays where it
    /// already is, but no part of the group gets the characteristic anew (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// The value of a characteristic of a part (task 00027, section 3.1).
/// </summary>
/// <remarks>
/// <para>
/// The value belongs to the part and to the characteristic, so the pairs are unique: a part
/// has one value of a characteristic and never two.
/// </para>
/// <para>
/// A characteristic that depends on the level of the part keeps a row for every level in
/// <see cref="PartCharacteristicLevelValueEntity"/>; this row then holds the value of the
/// first level.
/// </para>
/// </remarks>
public sealed class PartCharacteristicValueEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the characteristic.</summary>
    public int CharacteristicId { get; set; }

    /// <summary>Gets or sets the value of the characteristic.</summary>
    public decimal NumberValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value is archived: the value stays where it
    /// already is, but no part gets the characteristic anew (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets the values of the characteristic by the level of the part.</summary>
    public List<PartCharacteristicLevelValueEntity> Levels { get; set; } = [];
}
/// <summary>
/// The value of a characteristic of a part on one level of the part, for the characteristics
/// that change with the level (task 00027, section 3.1).
/// </summary>
public sealed class PartCharacteristicLevelValueEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the value of the characteristic of the part.</summary>
    public int PartCharacteristicValueId { get; set; }

    /// <summary>Gets or sets the level of the part the value belongs to.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the value of the characteristic on the level.</summary>
    public decimal NumberValue { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

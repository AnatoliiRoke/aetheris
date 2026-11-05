namespace Aetheris.Production.Data.Entities;

/// <summary>
/// A hull a part fits: the parts of a ship are not free of its hull, so the biggest reactor
/// does not go on a light transport (task 00042, section 2).
/// </summary>
/// <remarks>
/// <para>
/// The link is data: the fitting is a list of the concrete hulls of the part, not a class and
/// not a weight, so a new hull arrives as a row and a new part is fitted to it in the admin
/// panel, without a release of the server (documentation/tech/09, section 2.2).
/// </para>
/// <para>
/// Both sides of the link are rows of the catalog of the parts: a hull is a part of a group of
/// the hulls. The drones are parts too, but they are carried in the hold of a ship and are
/// never fitted to a hull, so they have no rows here (task 00042, decision 3).
/// </para>
/// </remarks>
public sealed class PartHullCompatibilityEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the hull the part fits.</summary>
    public int HullPartTypeId { get; set; }

    /// <summary>Gets or sets a value indicating whether the link is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// A race a part belongs to: the parts of a race are the parts its ships are built of
/// (task 00042, decision 4).
/// </summary>
/// <remarks>
/// <para>
/// The link is data, so a new part of a race is bound to that race in the admin panel and
/// never by a change of the code (documentation/tech/09, section 2.2).
/// </para>
/// <para>
/// A part may belong to several races: the parts shared by all of them are fitted to every
/// race, and a part of one race only is fitted to that race only.
/// </para>
/// </remarks>
public sealed class PartRaceCompatibilityEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the part.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the race the part belongs to.</summary>
    public int RaceId { get; set; }

    /// <summary>Gets or sets a value indicating whether the link is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

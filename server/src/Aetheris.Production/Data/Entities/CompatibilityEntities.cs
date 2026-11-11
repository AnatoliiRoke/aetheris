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

/// <summary>
/// A kind of ammunition a weapon fires: the damage of a weapon that fires ammunition is the damage of
/// the rounds it accepts, so the weapon has no damage of its own (task 00040, decision of the author
/// of 08.11.2026).
/// </summary>
/// <remarks>
/// <para>
/// The link is data: a cobalt shell is fired by the cannon and the autocannon and a rail slug by the
/// railgun, and a new round is bound to the weapon that fires it as a row, without a release of the
/// server (documentation/tech/09, section 2.2).
/// </para>
/// <para>
/// Both sides of the link are rows of the catalog of the parts: the weapon is a part of a group of
/// the weapons and the ammunition is a part of the group of the ammunition.
/// </para>
/// </remarks>
public sealed class PartAmmunitionCompatibilityEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the weapon.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the ammunition the weapon fires.</summary>
    public int AmmunitionPartTypeId { get; set; }

    /// <summary>Gets or sets a value indicating whether the link is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// What a module of extraction takes and where it takes it from (task 00055): one row is one way the
/// module takes from one kind of place.
/// </summary>
/// <remarks>
/// <para>
/// The row is data, so a new module of extraction, or a new resource a module takes, arrives as a row
/// in the admin panel and not as a change of the code of the fleet (documentation/tech/08, rules 1
/// and 2). The fleet reads the rows through the catalog of the ships.
/// </para>
/// <para>
/// The kind of the place is one of <see cref="Api.ExtractionPlaceKinds"/> and the way is one of
/// <see cref="Api.ExtractionModes"/>. The way «all shares» names no resource, the way «listed»
/// names one resource, and the way «group evenly» names a group of the resources. The resource and the
/// group live in the world module, so this module keeps their identifiers and its table carries no
/// foreign key into the tables of the other modules.
/// </para>
/// </remarks>
public sealed class PartExtractionEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the module of extraction.</summary>
    public int PartTypeId { get; set; }

    /// <summary>Gets or sets the kind of the place the module takes from.</summary>
    public string PlaceKind { get; set; } = string.Empty;

    /// <summary>Gets or sets the way the module takes from the place.</summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>Gets or sets the resource the module takes, for the way «listed».</summary>
    public int? ResourceId { get; set; }

    /// <summary>Gets or sets the group of the resources the module takes, for the way «group evenly».</summary>
    public int? ResourceGroupId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the module takes the mass it processes from the place:
    /// the yield of the module is the mass the place loses, and the part that does not become a
    /// resource is the waste. Only a place that has a mass, an asteroid field, loses it.
    /// </summary>
    public bool ReducesMass { get; set; }

    /// <summary>Gets or sets a value indicating whether the row is archived (task 00023, decision 5).</summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

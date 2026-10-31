namespace Aetheris.Market.Data.Entities;

/// <summary>
/// A level of the market: the clan one, the sector one and the galactic one (task 00024,
/// decision 2).
/// </summary>
/// <remarks>
/// The level is data: its visibility, its commission and whether it is active live in the table,
/// so a new level arrives as a row and never as a change of the code. The clan level exists in
/// the table and in the interface but stays inactive while there are no clans.
/// </remarks>
public sealed class MarketLevelEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the neutral code of the level, given by the server (task 00022).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scope of the level: who sees the lots of it. The scope is a structural
    /// property of the level, not a game entity, so it lives in the column of the level.
    /// </summary>
    public string Visibility { get; set; } = MarketVisibilities.Galactic;

    /// <summary>
    /// Gets or sets the commission of the level in per cent: the part of the price of a lot that
    /// the level takes from the seller and that never reaches the buyers.
    /// </summary>
    public decimal CommissionPercent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the level is active: an inactive level takes no
    /// lots and shows none.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the order of the level in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the localisation key of the name.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the localisation key of the description.</summary>
    public string DescriptionKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the picture of the level, or <see langword="null"/>.</summary>
    public string? ImageKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the level is archived: it keeps its lots, but no
    /// new lot gets it (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

/// <summary>
/// The scopes of the levels of the market: who sees the lots of a level.
/// </summary>
/// <remarks>
/// The scopes are logical codes of the program, so the program reads the same codes whatever the
/// level is, and a new scope arrives with one reader.
/// </remarks>
public static class MarketVisibilities
{
    /// <summary>Only the members of the clan of the seller see the lots.</summary>
    public const string Clan = "clan";

    /// <summary>The players of the sector of the seller see the lots.</summary>
    public const string Sector = "sector";

    /// <summary>Every player sees the lots.</summary>
    public const string Galactic = "galactic";

    /// <summary>
    /// Gets every scope, in the order of the form of the admin panel.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Clan, Sector, Galactic];
}

/// <summary>
/// The reference price of an item: the price of one unit of a resource of a body or of a part
/// in the trade currency (task 00024, section 3, point 10).
/// </summary>
/// <remarks>
/// The reference price is data: the administrator sets it in the admin panel, and a lot may be
/// put up only when its item has one. The price of a lot is a share of it, bounded around it.
/// </remarks>
public sealed class ReferencePriceEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the resource of the price, or <see langword="null"/> when the price is of a part.</summary>
    public int? ResourceTypeId { get; set; }

    /// <summary>Gets or sets the part of the price, or <see langword="null"/> when the price is of a resource.</summary>
    public int? PartTypeId { get; set; }

    /// <summary>Gets or sets the price of one unit of the item in the trade currency.</summary>
    public long Price { get; set; }

    /// <summary>Gets or sets the order of the price in the lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the price is archived: the price of the items that
    /// already have a lot stays, but no new lot takes it (task 00023, decision 5).
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>Gets or sets the version of the row, raised by every change; an admin change with an older version is refused.</summary>
    public int Version { get; set; } = 1;
}

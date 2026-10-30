namespace Aetheris.Economy.Data.Entities;

/// <summary>
/// The balance of a currency of a civilization (task 00015).
/// </summary>
/// <remarks>
/// <para>
/// The credits belong to the whole civilization and not to a body, so the balance is keyed by
/// the player and the currency. A currency is a resource with the scope of a civilization, so
/// the row keeps only its identifier.
/// </para>
/// <para>
/// The balance is never negative: every change checks that the player has the amount before
/// it takes it (documentation/tech/09, section 5.1).
/// </para>
/// </remarks>
public sealed class CivilizationBalanceEntity
{
    /// <summary>Gets or sets the primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the player who owns the balance.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the currency of the balance.</summary>
    public int ResourceTypeId { get; set; }

    /// <summary>Gets or sets the amount of the currency.</summary>
    public long Amount { get; set; }

    /// <summary>Gets or sets the moment of game time the balance was last counted at.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the version of the row, raised by every change; a change with an older
    /// version is refused.
    /// </summary>
    public int Version { get; set; } = 1;
}

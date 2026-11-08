namespace Aetheris.Economy.Data.Entities;

/// <summary>
/// The upkeep of the ships of a civilization, in credits per hour (task 00039).
/// </summary>
/// <remarks>
/// The economy module owns this row so a count of the credits never reads the tables of the
/// fleet. The fleet writes the sum after it has counted the past interval with the old rate,
/// and the next count uses the new one. A player with no ships has no row, which is a rate of
/// zero.
/// </remarks>
public sealed class ShipUpkeepEntity
{
    /// <summary>Gets or sets the player.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the credits the ships of the player cost per hour.</summary>
    public decimal PerHour { get; set; }
}

namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>game_clock_state</c> table: the anchor of the game clock, so the game time
/// never goes back after a restart (code audit of 04.11.2026).
/// </summary>
/// <remarks>
/// The table holds one row. At the real time <see cref="RealAnchor"/> the game time was
/// <see cref="GameAnchor"/>, and one real second advances the game by <see cref="Multiplier"/>
/// seconds; the server reads the row when it starts and writes it when the multiplier changes.
/// </remarks>
public sealed class GameClockStateEntity
{
    /// <summary>
    /// The identifier of the only row of the table.
    /// </summary>
    public const int SingleRowId = 1;

    /// <summary>
    /// Gets or sets the primary key; always <see cref="SingleRowId"/>.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the real time of the anchor.
    /// </summary>
    public DateTimeOffset RealAnchor { get; set; }

    /// <summary>
    /// Gets or sets the game time at the real time of the anchor.
    /// </summary>
    public DateTimeOffset GameAnchor { get; set; }

    /// <summary>
    /// Gets or sets how many game seconds one real second makes.
    /// </summary>
    public double Multiplier { get; set; }

    /// <summary>
    /// Gets or sets the real time the row was written.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

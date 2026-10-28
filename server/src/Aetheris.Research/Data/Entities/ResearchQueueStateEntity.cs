namespace Aetheris.Research.Data.Entities;

/// <summary>
/// The state of the queue of the researches of a player: the owner and the paid places of the
/// queue (task 00012, decision 5a).
/// </summary>
/// <remarks>
/// Every change of the queue locks this row first, so two commands of one player never spend
/// the same resources twice.
/// </remarks>
public sealed class ResearchQueueStateEntity
{
    /// <summary>Gets or sets the player, the primary key.</summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the paid places added to the queue of the researches.</summary>
    public int ExtraQueueSlots { get; set; }
}

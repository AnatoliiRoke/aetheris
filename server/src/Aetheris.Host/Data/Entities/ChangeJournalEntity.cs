namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>change_journal</c> table: one recorded change of a value.
/// </summary>
/// <remarks>
/// Written in the same transaction as the change (documentation/tech/05, section 5).
/// </remarks>
public sealed class ChangeJournalEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the change.
    /// </summary>
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the player or administrator who caused the change.
    /// </summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request identifier of the command that made the change.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Gets or sets the type of the changed object.
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the changed object.
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the changed value.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value before the change.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the value after the change.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets why the value changed.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

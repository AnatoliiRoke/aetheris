namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>admin_audit_log</c> table: an append-only record of admin actions.
/// </summary>
/// <remarks>
/// The log is never edited (documentation/tech/05-bezpeka-ta-antychit.md, section 8).
/// </remarks>
public sealed class AdminAuditLogEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the administrator who performed the action.
    /// </summary>
    public string AdminId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the action name.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the object the action targeted.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// Gets or sets the value before the change.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the value after the change.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the action.
    /// </summary>
    public DateTimeOffset At { get; set; }
}

namespace Aetheris.Core.Admin;

/// <summary>
/// Records what the current administrator action changed.
/// </summary>
/// <remarks>
/// The host writes one audit record for every administrator request
/// (documentation/tech/05, section 8). An action of a module states the changed object and
/// the old and new values through this interface; the record itself, the second factor and
/// the role check are done by the host, never by the module.
/// </remarks>
public interface IAdminAuditRecorder
{
    /// <summary>
    /// Records what the action changed.
    /// </summary>
    /// <param name="target">The changed object.</param>
    /// <param name="oldValue">The value before the change.</param>
    /// <param name="newValue">The value after the change.</param>
    void Record(string target, string? oldValue, string? newValue);

    /// <summary>
    /// Records the reason the administrator gave for the change.
    /// </summary>
    /// <param name="reason">The reason.</param>
    void RecordReason(string reason);

    /// <summary>
    /// Schedules work that may only run after the change and its audit record are stored.
    /// </summary>
    /// <param name="action">The work.</param>
    void OnCommitted(Func<Task> action);
}

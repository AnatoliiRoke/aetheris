namespace Aetheris.Host.Scheduling;

/// <summary>
/// The states of a scheduled event (documentation/tech/07, section 2).
/// </summary>
public static class ScheduledEventStatus
{
    /// <summary>
    /// The event waits for its time.
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// The event was applied.
    /// </summary>
    public const string Done = "done";

    /// <summary>
    /// The event was cancelled and is never applied.
    /// </summary>
    public const string Cancelled = "cancelled";

    /// <summary>
    /// The handler failed; the event stays for an administrator.
    /// </summary>
    public const string Failed = "failed";
}

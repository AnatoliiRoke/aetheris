namespace Aetheris.Host.Scheduling;

/// <summary>
/// The configuration keys of the event scheduler.
/// </summary>
public static class SchedulerKeys
{
    /// <summary>
    /// The interval of the check for due events, in seconds.
    /// </summary>
    public const string PollSeconds = "scheduler.poll_seconds";

    /// <summary>
    /// The largest number of due events handled in one pass.
    /// </summary>
    public const string BatchSize = "scheduler.batch_size";
}

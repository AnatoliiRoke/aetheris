namespace Aetheris.Host.Observability;

/// <summary>
/// The measurements of one unit of work, a request or a scheduled event: how many SQL
/// commands it ran and how long the database took.
/// </summary>
/// <remarks>
/// The unit of work is found through an <see cref="AsyncLocal{T}"/>, so the interceptors of
/// the database contexts add to it without knowing the request. The counters are updated with
/// interlocked operations, because the commands of a unit of work may overlap.
/// </remarks>
public sealed class RequestTrace
{
    private static readonly AsyncLocal<RequestTrace?> CurrentHolder = new();

    private long _databaseTicks;
    private int _commands;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestTrace"/> class.
    /// </summary>
    /// <param name="requestId">The identifier that ties the log lines of the unit of work together.</param>
    public RequestTrace(string requestId)
    {
        ArgumentException.ThrowIfNullOrEmpty(requestId);

        RequestId = requestId;
    }

    /// <summary>
    /// Gets the unit of work of the current flow of execution, or <see langword="null"/> when none is open.
    /// </summary>
    public static RequestTrace? Current => CurrentHolder.Value;

    /// <summary>
    /// Gets the identifier that ties the log lines of the unit of work together.
    /// </summary>
    public string RequestId { get; }

    /// <summary>
    /// Gets the number of SQL commands run so far.
    /// </summary>
    public int Commands => Volatile.Read(ref _commands);

    /// <summary>
    /// Gets the time the database took so far, in milliseconds.
    /// </summary>
    public double DatabaseMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref _databaseTicks)).TotalMilliseconds;

    /// <summary>
    /// Makes this unit of work the current one for the flow of execution that follows.
    /// </summary>
    /// <returns>A scope that ends the unit of work when it is disposed.</returns>
    public IDisposable Begin()
    {
        CurrentHolder.Value = this;

        return new Scope();
    }

    /// <summary>
    /// Adds one SQL command to the unit of work.
    /// </summary>
    /// <param name="duration">How long the command took.</param>
    public void AddCommand(TimeSpan duration)
    {
        Interlocked.Add(ref _databaseTicks, duration.Ticks);
        Interlocked.Increment(ref _commands);
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() => CurrentHolder.Value = null;
    }
}

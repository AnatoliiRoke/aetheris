using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Keeps every log event the server writes, after the enrichers ran.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    /// <summary>
    /// Gets every event written so far.
    /// </summary>
    public IReadOnlyCollection<LogEvent> Events => _events;

    /// <inheritdoc />
    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>
    /// Renders every event as the text a log reader would see: the message, every property
    /// and the exception.
    /// </summary>
    /// <returns>One text per event.</returns>
    public List<string> RenderAll() =>
        _events
            .Select(logEvent => string.Join(
                " | ",
                logEvent.RenderMessage(),
                string.Join(", ", logEvent.Properties.Select(property => $"{property.Key}={property.Value}")),
                logEvent.Exception?.ToString() ?? string.Empty))
            .ToList();
}

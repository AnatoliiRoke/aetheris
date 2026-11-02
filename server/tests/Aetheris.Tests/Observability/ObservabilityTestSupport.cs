using Aetheris.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace Aetheris.Tests.Observability;

/// <summary>
/// Helpers of the tests of the tracing: reading the properties of the log events and changing
/// the parameters for the time of one test.
/// </summary>
internal static class ObservabilityTestSupport
{
    /// <summary>
    /// Gets the events that were written after a mark.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="mark">The number of events at the time of the mark.</param>
    /// <returns>The events written since.</returns>
    public static List<LogEvent> Since(AetherisTestFactory factory, int mark) => [.. factory.Logs.Events.Skip(mark)];

    /// <summary>
    /// Gets the text of a scalar property of an event.
    /// </summary>
    /// <param name="logEvent">The event.</param>
    /// <param name="name">The name of the property.</param>
    /// <returns>The text, or <see langword="null"/> when the property is missing or is not a scalar.</returns>
    public static string? Text(LogEvent logEvent, string name) =>
        logEvent.Properties.TryGetValue(name, out LogEventPropertyValue? value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;

    /// <summary>
    /// Finds the log line of a request by its path.
    /// </summary>
    /// <param name="events">The events.</param>
    /// <param name="path">The path of the request.</param>
    /// <returns>The line, or <see langword="null"/>.</returns>
    public static LogEvent? RequestLine(IEnumerable<LogEvent> events, string path) =>
        events.LastOrDefault(logEvent =>
            logEvent.MessageTemplate.Text.StartsWith("HTTP {RequestMethod} {RequestPath} responded", StringComparison.Ordinal)
            && Text(logEvent, "RequestPath") == path);

    /// <summary>
    /// Sets parameters, runs an action, and puts the previous values back.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="values">The parameters and their test values.</param>
    /// <param name="action">The action to run with the values.</param>
    /// <returns>A task that completes when the values are restored.</returns>
    public static async Task WithParametersAsync(AetherisTestFactory factory, Dictionary<string, string> values, Func<Task> action)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        Dictionary<string, string?> previous = [];

        foreach ((string key, string value) in values)
        {
            previous[key] = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, value);
        }

        try
        {
            await action();
        }
        finally
        {
            foreach ((string key, string? value) in previous)
            {
                await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, value!);
            }
        }
    }
}

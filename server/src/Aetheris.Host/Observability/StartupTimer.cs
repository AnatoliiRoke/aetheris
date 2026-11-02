using System.Diagnostics;

namespace Aetheris.Host.Observability;

/// <summary>
/// Measures a step of the start of the server and writes how long it took (task 00036).
/// </summary>
public static class StartupTimer
{
    /// <summary>
    /// Runs a step and writes its duration.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="step">The step.</param>
    /// <returns>A task that completes when the step has finished.</returns>
    public static async Task RunAsync(ILogger logger, string stepName, Func<Task> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        await RunAsync(logger, stepName, async () =>
        {
            await step().ConfigureAwait(false);

            return true;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a step that gives a result and writes its duration.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="logger">The logger.</param>
    /// <param name="stepName">The name of the step.</param>
    /// <param name="step">The step.</param>
    /// <returns>The result of the step.</returns>
    public static async Task<T> RunAsync<T>(ILogger logger, string stepName, Func<Task<T>> step)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrEmpty(stepName);
        ArgumentNullException.ThrowIfNull(step);

        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            return await step().ConfigureAwait(false);
        }
        finally
        {
            logger.LogInformation(
                "Startup step {StepName} took {StepMs:0.0} ms.",
                stepName,
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
        }
    }
}

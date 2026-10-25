using OpenTelemetry.Trace;

namespace Aetheris.Host.Observability;

/// <summary>
/// Helpers for the OpenTelemetry tracing setup.
/// </summary>
public static class TracingSetupExtensions
{
    /// <summary>
    /// The configuration key that holds the OTLP collector endpoint.
    /// </summary>
    public const string OtlpEndpointKey = "OpenTelemetry:OtlpEndpoint";

    /// <summary>
    /// Adds the OTLP exporter when a collector endpoint is configured.
    /// </summary>
    /// <param name="tracing">The tracing builder to extend.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <remarks>
    /// Without a collector the exporter is skipped, so the server starts cleanly on a
    /// development machine. The endpoint is read from
    /// <c>OpenTelemetry:OtlpEndpoint</c> or from the standard
    /// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> environment variable.
    /// </remarks>
    public static TracerProviderBuilder AddOtlpExporterIfConfigured(
        this TracerProviderBuilder tracing,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(tracing);
        ArgumentNullException.ThrowIfNull(configuration);

        string? endpoint = configuration[OtlpEndpointKey]
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return tracing;
        }

        return tracing.AddOtlpExporter(options => options.Endpoint = new Uri(endpoint));
    }
}

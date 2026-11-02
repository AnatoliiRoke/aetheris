using Aetheris.Core.Configuration;

namespace Aetheris.Host.Observability;

/// <summary>
/// The configuration keys of the tracing of the requests, the responses and the SQL commands
/// (task 00036).
/// </summary>
/// <remarks>
/// Every threshold, switch and list lives in the database under these keys, so an
/// administrator changes them without a restart. The starting values and the bounds are
/// the definitions of this class, which <see cref="Data.ConfigSeed"/> adds to its own.
/// </remarks>
public static class ObservabilityKeys
{
    /// <summary>Whether the tracing of the requests is on: the request identifier, the timing headers and the SQL accounting.</summary>
    public const string HttpTraceEnabled = "observability.http_trace.enabled";

    /// <summary>The largest length of a request identifier that a client may send.</summary>
    public const string RequestIdMaxLength = "observability.http_trace.request_id_max_length";

    /// <summary>The headers whose values may appear in the log, separated by commas.</summary>
    public const string AllowedHeaders = "observability.http_trace.allowed_headers";

    /// <summary>The duration from which a request is logged as slow, in milliseconds.</summary>
    public const string SlowRequestMilliseconds = "observability.slow_request_ms";

    /// <summary>Whether the JSON bodies of the requests and the responses are written to the log.</summary>
    public const string HttpBodiesEnabled = "observability.http_bodies.enabled";

    /// <summary>The largest number of bytes of one body that is written to the log.</summary>
    public const string HttpBodiesMaxBytes = "observability.http_bodies.max_bytes";

    /// <summary>The parts of the names of the JSON fields whose values are masked, separated by commas.</summary>
    public const string HttpBodiesMaskedFields = "observability.http_bodies.masked_fields";

    /// <summary>The path prefixes whose bodies are never written, separated by commas.</summary>
    public const string HttpBodiesExcludedPaths = "observability.http_bodies.excluded_paths";

    /// <summary>The duration from which an SQL command, a transaction or a save is logged as slow, in milliseconds.</summary>
    public const string SqlSlowMilliseconds = "observability.sql.slow_ms";

    /// <summary>Whether every SQL command is written to the log with its duration.</summary>
    public const string SqlVerbose = "observability.sql.verbose";

    /// <summary>The largest number of characters of the text of an SQL command in the log.</summary>
    public const string SqlMaxTextLength = "observability.sql.max_text_length";

    /// <summary>
    /// The definitions that the seed adds to the parameters of the server.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> Definitions { get; } =
    [
        new(HttpTraceEnabled, "true", null, null, "Whether the tracing of the requests is on: the request identifier, the headers X-Request-Id, X-Trace-Id and Server-Timing, the SQL accounting."),
        new(RequestIdMaxLength, "64", "8", "256", "Largest length of the request identifier that a client may send; a longer or malformed one is replaced."),
        new(AllowedHeaders, "Content-Type,Content-Length,Accept,Origin,User-Agent,X-Request-Id,Idempotency-Key", null, null, "Headers whose values may appear in the log, separated by commas; the values of all other headers are masked."),
        new(SlowRequestMilliseconds, "1000", "0", "600000", "Duration from which a request is logged as a warning, in milliseconds."),
        new(HttpBodiesEnabled, "false", null, null, "Whether the JSON bodies of the requests and the responses are written to the log; on only in the development."),
        new(HttpBodiesMaxBytes, "4096", "256", "1048576", "Largest number of bytes of one body that is written to the log; the rest is cut."),
        new(HttpBodiesMaskedFields, "password,token,refresh,csrf,email,login,cookie,authorization,secret,key,totp,authenticator", null, null, "Parts of the names of the JSON fields whose values are masked in the log, separated by commas."),
        new(HttpBodiesExcludedPaths, "/health,/metrics,/hubs,/api/admin/auth", null, null, "Path prefixes whose bodies are never written to the log, separated by commas."),
        new(SqlSlowMilliseconds, "200", "0", "600000", "Duration from which an SQL command, a transaction or a save is logged as a warning, in milliseconds."),
        new(SqlVerbose, "false", null, null, "Whether every SQL command is written to the log with its duration; the values of the parameters are never written."),
        new(SqlMaxTextLength, "1000", "100", "20000", "Largest number of characters of the text of an SQL command in the log."),
    ];
}

using System.Diagnostics;
using System.Globalization;
using Serilog.Context;

namespace Aetheris.Host.Observability;

/// <summary>
/// Gives every request an identifier, measures it and, when asked, logs its bodies
/// (task 00036).
/// </summary>
/// <remarks>
/// <para>
/// The middleware stands before the protection pipeline, so a rejected request (401, 403, 429)
/// is traced too. It keeps the identifier the client sent in <c>X-Request-Id</c> when it is
/// short and made of plain characters, and makes a new one otherwise. The identifier is a
/// property of every log line of the request, a tag of its <see cref="Activity"/> and a header
/// of the response, next to <c>X-Trace-Id</c> and <c>Server-Timing</c>.
/// </para>
/// <para>
/// With <c>observability.http_trace.enabled</c> off the middleware only calls the next one.
/// Bodies are kept only with <c>observability.http_bodies.enabled</c>, only for JSON and only
/// up to the limit: the streams under the request and the response are wrapped by a tee, which
/// copies at most the limit and forwards everything else untouched.
/// </para>
/// </remarks>
public sealed class RequestTraceMiddleware
{
    /// <summary>
    /// The header with the identifier of the request, in the request and in the response.
    /// </summary>
    public const string RequestIdHeader = "X-Request-Id";

    /// <summary>
    /// The header with the identifier of the trace of the request.
    /// </summary>
    public const string TraceIdHeader = "X-Trace-Id";

    /// <summary>
    /// The header with the timing of the request.
    /// </summary>
    public const string ServerTimingHeader = "Server-Timing";

    /// <summary>
    /// The key under which the measurements of the request are kept in <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string TraceItemKey = "Aetheris.RequestTrace";

    /// <summary>
    /// The key under which the counter of the response bytes is stored in <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string ResponseCaptureItemKey = "Aetheris.ResponseCapture";

    /// <summary>
    /// The key under which a flag is stored in <see cref="HttpContext.Items"/> that tells whether the bodies of the request are kept.
    /// </summary>
    public const string BodiesCapturedItemKey = "Aetheris.BodiesCaptured";

    private const string JsonMediaType = "application/json";
    private const string RequestIdTagName = "aetheris.request_id";
    private const string RequestIdPropertyName = "RequestId";

    private readonly RequestDelegate _next;
    private readonly ObservabilitySettings _settings;
    private readonly ILogger<RequestTraceMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestTraceMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="settings">The parameters of the observability.</param>
    /// <param name="logger">The logger.</param>
    public RequestTraceMiddleware(RequestDelegate next, ObservabilitySettings settings, ILogger<RequestTraceMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        _next = next;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ObservabilitySnapshot settings = _settings.Current;

        if (!settings.TraceEnabled)
        {
            await _next(httpContext).ConfigureAwait(false);

            return;
        }

        long startTimestamp = Stopwatch.GetTimestamp();
        string requestId = ResolveRequestId(httpContext, settings);
        string traceId = Activity.Current?.TraceId.ToString() ?? ActivityTraceId.CreateRandom().ToString();
        RequestTrace trace = new(requestId);

        Activity.Current?.SetTag(RequestIdTagName, requestId);
        httpContext.Items[TraceItemKey] = trace;

        httpContext.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = httpContext.Response.Headers;

            headers[RequestIdHeader] = requestId;
            headers[TraceIdHeader] = traceId;
            headers[ServerTimingHeader] = FormatServerTiming(Stopwatch.GetElapsedTime(startTimestamp), trace);

            return Task.CompletedTask;
        });

        BodyCapture? requestCapture = null;
        BodyCapture? responseCapture = null;
        CaptureWriteStream? responseStream = null;
        Stream originalResponseBody = httpContext.Response.Body;
        bool keepBodies = settings.BodiesEnabled;

        // The response stream counts the bytes of every response, so the log line can tell its
        // size; it keeps a copy only when the bodies are on. A path of the excluded list, and a
        // WebSocket, is left alone.
        if (!IsExcluded(httpContext, settings))
        {
            if (keepBodies && IsJson(httpContext.Request.ContentType))
            {
                requestCapture = new BodyCapture(settings.BodiesMaxBytes);
                httpContext.Request.Body = new CaptureReadStream(httpContext.Request.Body, requestCapture);
            }

            responseCapture = new BodyCapture(keepBodies ? settings.BodiesMaxBytes : 0);
            responseStream = new CaptureWriteStream(
                originalResponseBody,
                responseCapture,
                () => keepBodies && IsJson(httpContext.Response.ContentType));
            httpContext.Response.Body = responseStream;
            httpContext.Items[ResponseCaptureItemKey] = responseCapture;
            httpContext.Items[BodiesCapturedItemKey] = keepBodies;
        }

        using IDisposable traceScope = trace.Begin();

        // The line of the request is written by Serilog itself and reads the log context; the lines of the
        // code that logs through Microsoft.Extensions.Logging read a scope, and there the hosting
        // already has a RequestId of its own, so the inner scope of this middleware replaces it.
        using IDisposable logScope = LogContext.PushProperty(RequestIdPropertyName, requestId);
        using IDisposable? scopeOfRequestId = _logger.BeginScope(new Dictionary<string, object> { [RequestIdPropertyName] = requestId });

        try
        {
            await _next(httpContext).ConfigureAwait(false);
        }
        finally
        {
            if (responseStream is not null)
            {
                httpContext.Response.Body = originalResponseBody;
            }

            LogBodies(httpContext, settings, requestCapture, responseCapture, responseStream);
        }
    }

    /// <summary>
    /// Gets the number of bytes of the response, from the capture when there is one and from the
    /// declared length otherwise.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>The number of bytes, or <see langword="null"/> when it is not known.</returns>
    public static long? ResponseBytes(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return httpContext.Items.TryGetValue(ResponseCaptureItemKey, out object? captured) && captured is BodyCapture { TotalBytes: > 0 } capture
            ? capture.TotalBytes
            : httpContext.Response.ContentLength;
    }

    private static string ResolveRequestId(HttpContext httpContext, ObservabilitySnapshot settings)
    {
        string? sent = httpContext.Request.Headers[RequestIdHeader].FirstOrDefault();

        return IsValidRequestId(sent, settings.RequestIdMaxLength)
            ? sent!
            : Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    }

    private static bool IsValidRequestId(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength)
        {
            return false;
        }

        foreach (char symbol in value)
        {
            bool allowed = char.IsAsciiLetterOrDigit(symbol) || symbol is '-' or '_' or '.' or ':';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExcluded(HttpContext httpContext, ObservabilitySnapshot settings)
    {
        if (httpContext.WebSockets.IsWebSocketRequest)
        {
            return true;
        }

        PathString path = httpContext.Request.Path;

        return settings.ExcludedPaths.Any(prefix => path.StartsWithSegments(new PathString(prefix), StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsJson(string? contentType) =>
        contentType is not null && contentType.StartsWith(JsonMediaType, StringComparison.OrdinalIgnoreCase);

    private static string FormatServerTiming(TimeSpan total, RequestTrace trace)
    {
        string queries = trace.Commands == 1 ? "query" : "queries";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"total;dur={total.TotalMilliseconds:0.0}, db;dur={trace.DatabaseMilliseconds:0.0};desc=\"{trace.Commands} {queries}\"");
    }

    private void LogBodies(
        HttpContext httpContext,
        ObservabilitySnapshot settings,
        BodyCapture? requestCapture,
        BodyCapture? responseCapture,
        CaptureWriteStream? responseStream)
    {
        if (!settings.BodiesEnabled)
        {
            return;
        }

        bool hasRequestBody = requestCapture is { TotalBytes: > 0 };
        bool hasResponseBody = responseStream is { Kept: true } && responseCapture is { TotalBytes: > 0 };

        if (!hasRequestBody && !hasResponseBody)
        {
            return;
        }

        string requestBody = hasRequestBody ? BodyMasker.Mask(requestCapture!.Captured, requestCapture.TotalBytes, settings) : "-";
        string responseBody = hasResponseBody ? BodyMasker.Mask(responseCapture!.Captured, responseCapture.TotalBytes, settings) : "-";

        // The headers travel as properties of the same event, so a reader of the console sees the
        // bodies and a reader of structured logs sees the headers too.
        using IDisposable requestHeaders = LogContext.PushProperty("RequestHeaders", MaskHeaders(httpContext.Request.Headers, settings), destructureObjects: true);
        using IDisposable responseHeaders = LogContext.PushProperty("ResponseHeaders", MaskHeaders(httpContext.Response.Headers, settings), destructureObjects: true);

        _logger.LogInformation(
            "HTTP bodies {RequestMethod} {RequestPath} {StatusCode}: request {RequestBody:l} response {ResponseBody:l}",
            httpContext.Request.Method,
            httpContext.Request.Path.Value,
            httpContext.Response.StatusCode,
            requestBody,
            responseBody);
    }

    private static Dictionary<string, string> MaskHeaders(IHeaderDictionary headers, ObservabilitySnapshot settings)
    {
        Dictionary<string, string> masked = new(headers.Count, StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in headers)
        {
            masked[header.Key] = settings.AllowedHeaders.Contains(header.Key)
                ? header.Value.ToString()
                : SensitiveDataMaskingEnricher.Mask;
        }

        return masked;
    }
}

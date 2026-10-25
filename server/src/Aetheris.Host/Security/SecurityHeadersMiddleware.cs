namespace Aetheris.Host.Security;

/// <summary>
/// Adds the security headers to every response (task 00002, section 2.3).
/// </summary>
/// <remarks>
/// The server only answers API and SignalR requests; the client is served separately. The
/// content policy therefore allows nothing to load from a server response and forbids
/// every kind of embedding. The <c>Server</c> header is removed, so the response does not
/// name the web server.
/// </remarks>
public sealed class SecurityHeadersMiddleware
{
    /// <summary>
    /// The content security policy of the API responses.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);

        _next = next;
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.OnStarting(() =>
        {
            IHeaderDictionary headers = httpContext.Response.Headers;

            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            // API answers depend on the account; no shared cache may keep them.
            if (httpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.CacheControl = "no-store";
            }

            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        return _next(httpContext);
    }
}

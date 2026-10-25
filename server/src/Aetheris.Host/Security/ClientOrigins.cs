using Aetheris.Core.Errors;

namespace Aetheris.Host.Security;

/// <summary>
/// The addresses of our own client: the only origins that CORS allows, the only origins a
/// SignalR connection may come from, and the only targets of a redirect after a sign-in.
/// </summary>
/// <remarks>
/// The list is deployment configuration, not a tunable number, so it lives in
/// <c>appsettings</c> under <c>Cors:AllowedOrigins</c> (task 00002, section 2.3).
/// </remarks>
public sealed class ClientOrigins
{
    /// <summary>
    /// The configuration key of the allowed origins.
    /// </summary>
    public const string ConfigurationKey = "Cors:AllowedOrigins";

    private readonly string[] _origins;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientOrigins"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public ClientOrigins(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _origins = (configuration.GetSection(ConfigurationKey).Get<string[]>() ?? [])
            .Select(origin => origin.TrimEnd('/'))
            .Where(origin => origin.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Gets the allowed origins.
    /// </summary>
    public IReadOnlyList<string> Origins => _origins;

    /// <summary>
    /// Checks whether an origin is one of ours.
    /// </summary>
    /// <param name="origin">The value of the <c>Origin</c> header.</param>
    /// <returns><see langword="true"/> when the origin is allowed.</returns>
    public bool IsAllowed(string origin) =>
        _origins.Contains(origin.TrimEnd('/'), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the address to send the browser to after a sign-in.
    /// </summary>
    /// <param name="returnUrl">The address the client asked for.</param>
    /// <returns>The address when it belongs to our client, otherwise the client's start page.</returns>
    public string SafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl)
            && Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? target)
            && IsAllowed(target.GetLeftPart(UriPartial.Authority)))
        {
            return target.ToString();
        }

        return _origins.Length > 0 ? _origins[0] + "/" : "/";
    }
}

/// <summary>
/// Rejects SignalR requests whose <c>Origin</c> header names a foreign site.
/// </summary>
/// <remarks>
/// A browser does not apply CORS to a WebSocket, so a foreign page could otherwise open a
/// connection with the player's cookie. Clients that send no <c>Origin</c> header are not
/// browsers and cannot carry a browser's cookie.
/// </remarks>
public sealed class HubOriginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ClientOrigins _origins;
    private readonly PathString _hubPrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="HubOriginMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="origins">The allowed origins.</param>
    /// <param name="hubPrefix">The path prefix of the hubs.</param>
    public HubOriginMiddleware(RequestDelegate next, ClientOrigins origins, PathString hubPrefix)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(origins);

        _next = next;
        _origins = origins;
        _hubPrefix = hubPrefix;
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        string origin = httpContext.Request.Headers.Origin.ToString();

        if (httpContext.Request.Path.StartsWithSegments(_hubPrefix, StringComparison.OrdinalIgnoreCase)
            && origin.Length > 0
            && !_origins.IsAllowed(origin))
        {
            await ApiErrors.WriteAsync(httpContext, StatusCodes.Status403Forbidden, GameErrorCodes.AccessDenied)
                .ConfigureAwait(false);

            return;
        }

        await _next(httpContext).ConfigureAwait(false);
    }
}

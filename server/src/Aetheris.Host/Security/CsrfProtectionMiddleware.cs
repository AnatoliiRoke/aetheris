using Aetheris.Core.Errors;
using Microsoft.AspNetCore.Antiforgery;

namespace Aetheris.Host.Security;

/// <summary>
/// Rejects state-changing API requests that carry an authentication cookie but no valid
/// anti-forgery token (task 00002, section 2.3).
/// </summary>
/// <remarks>
/// <para>
/// A browser attaches cookies to a request that another site makes. The anti-forgery token
/// proves that the request comes from our client: the client reads it from
/// <c>GET /api/v1/auth/csrf</c> and sends it in the <c>X-XSRF-TOKEN</c> header.
/// </para>
/// <para>
/// Requests without an authentication cookie (sign-in, registration) cannot ride on a
/// session, so they need no token. The SignalR hub is protected by the
/// <c>SameSite=Strict</c> cookie and by the origin check.
/// </para>
/// </remarks>
public sealed class CsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;
    private readonly SecurityEventLog _securityLog;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsrfProtectionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="antiforgery">The anti-forgery service.</param>
    /// <param name="securityLog">The security journal.</param>
    public CsrfProtectionMiddleware(RequestDelegate next, IAntiforgery antiforgery, SecurityEventLog securityLog)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(antiforgery);
        ArgumentNullException.ThrowIfNull(securityLog);

        _next = next;
        _antiforgery = antiforgery;
        _securityLog = securityLog;
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (RequiresToken(httpContext) && !await _antiforgery.IsRequestValidAsync(httpContext).ConfigureAwait(false))
        {
            await _securityLog
                .WriteAsync(SecurityEventTypes.CsrfRejected, httpContext, httpContext.User.GetPlayerId(), httpContext.Request.Path)
                .ConfigureAwait(false);

            await ApiErrors
                .WriteAsync(httpContext, StatusCodes.Status403Forbidden, GameErrorCodes.CsrfTokenInvalid)
                .ConfigureAwait(false);

            return;
        }

        await _next(httpContext).ConfigureAwait(false);
    }

    private static bool RequiresToken(HttpContext httpContext)
    {
        HttpRequest request = httpContext.Request;

        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method))
        {
            return false;
        }

        if (!request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return AuthCookies.All.Any(request.Cookies.ContainsKey);
    }
}

using Aetheris.Core.Errors;
using Microsoft.AspNetCore.Http.Features;

namespace Aetheris.Host.Security;

/// <summary>
/// Limits the size of a request body to <c>security.request.max_body_bytes</c>.
/// </summary>
/// <remarks>
/// A request that declares a larger body is rejected before it is read. For a body without
/// a declared length the limit is handed to the web server, which stops reading at it. The
/// value is read for every request, so a change in the database applies at once.
/// </remarks>
public sealed class RequestLimitsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecuritySettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimitsMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="settings">The security settings.</param>
    public RequestLimitsMiddleware(RequestDelegate next, SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(settings);

        _next = next;
        _settings = settings;
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        long maxBodyBytes = _settings.RequestMaxBodyBytes;

        if (httpContext.Request.ContentLength > maxBodyBytes)
        {
            await ApiErrors
                .WriteAsync(httpContext, StatusCodes.Status413PayloadTooLarge, GameErrorCodes.PayloadTooLarge)
                .ConfigureAwait(false);

            return;
        }

        IHttpMaxRequestBodySizeFeature? sizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (sizeFeature is { IsReadOnly: false })
        {
            sizeFeature.MaxRequestBodySize = maxBodyBytes;
        }

        await _next(httpContext).ConfigureAwait(false);
    }
}

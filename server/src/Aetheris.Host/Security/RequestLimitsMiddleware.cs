using Aetheris.Core.Errors;
using Aetheris.Host.Admin;
using Microsoft.AspNetCore.Http.Features;

namespace Aetheris.Host.Security;

/// <summary>
/// Limits the size of a request body to <c>security.request.max_body_bytes</c>.
/// </summary>
/// <remarks>
/// A request that declares a larger body is rejected before it is read. For a body without
/// a declared length the limit is handed to the web server, which stops reading at it. The
/// value is read for every request, so a change in the database applies at once. The upload of a
/// picture of the admin panel has the limit of the pictures instead when it is larger
/// (task 00023, decision 2).
/// </remarks>
public sealed class RequestLimitsMiddleware
{
    private static readonly PathString ImageUploadPath = new(AdminEndpoints.Prefix + ReferenceImageEndpoints.UploadRoute);

    private readonly RequestDelegate _next;
    private readonly SecuritySettings _settings;
    private readonly ReferenceImageStore _images;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimitsMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="images">The pictures of the reference rows, for the limit of an upload.</param>
    public RequestLimitsMiddleware(RequestDelegate next, SecuritySettings settings, ReferenceImageStore images)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(images);

        _next = next;
        _settings = settings;
        _images = images;
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        long maxBodyBytes = httpContext.Request.Path.Equals(ImageUploadPath, StringComparison.OrdinalIgnoreCase)
            ? Math.Max(_settings.RequestMaxBodyBytes, _images.MaxBytes + 1)
            : _settings.RequestMaxBodyBytes;

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

using Aetheris.Core.Errors;
using Microsoft.AspNetCore.Diagnostics;

namespace Aetheris.Host.Security;

/// <summary>
/// Turns an unhandled exception into an error body without internal details.
/// </summary>
/// <remarks>
/// A <see cref="GameException"/> becomes its own code and details; anything else becomes
/// <c>internal_error</c>. The exception itself goes to the log and to Sentry, never to the
/// client: no stack trace, no table name, no SQL (task 00002, section 2.3).
/// </remarks>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiExceptionHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        ApiError error;
        int statusCode;

        if (exception is GameException gameException)
        {
            statusCode = ApiErrors.StatusFor(gameException.Code);
            error = new ApiError(gameException.Code, gameException.Details);
        }
        else if (exception is BadHttpRequestException badRequest)
        {
            statusCode = badRequest.StatusCode;
            error = new ApiError(ApiErrors.CodeFor(statusCode));
        }
        else
        {
            _logger.LogError(exception, "Unhandled exception while processing {RequestPath}.", httpContext.Request.Path);

            statusCode = StatusCodes.Status500InternalServerError;
            error = new ApiError(GameErrorCodes.InternalError);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken).ConfigureAwait(false);

        return true;
    }
}

using System.Text.Json.Serialization;
using Aetheris.Core.Errors;

namespace Aetheris.Host.Security;

/// <summary>
/// The error body of the API: <c>{ "error": "code", "details": {...} }</c>
/// (documentation/tech/04, section 8).
/// </summary>
/// <param name="Error">The stable error code, used by the client for localisation.</param>
/// <param name="Details">Optional structured details.</param>
public sealed record ApiError(
    string Error,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, object?>? Details = null);

/// <summary>
/// Builds error responses that never expose internals: no stack traces, no table names,
/// no messages of the framework.
/// </summary>
public static class ApiErrors
{
    /// <summary>
    /// Creates an error result.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The stable error code.</param>
    /// <param name="details">Optional structured details.</param>
    /// <returns>The result.</returns>
    public static IResult Result(int statusCode, string code, IReadOnlyDictionary<string, object?>? details = null) =>
        Results.Json(new ApiError(code, details), statusCode: statusCode);

    /// <summary>
    /// Creates a validation error result from the failed rules.
    /// </summary>
    /// <param name="failures">The failed rules, keyed by the property name.</param>
    /// <returns>The result.</returns>
    public static IResult Validation(IReadOnlyDictionary<string, string[]> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        Dictionary<string, object?> details = failures.ToDictionary(
            failure => failure.Key,
            failure => (object?)failure.Value,
            StringComparer.Ordinal);

        return Result(StatusCodes.Status400BadRequest, GameErrorCodes.ValidationFailed, details);
    }

    /// <summary>
    /// Writes an error body directly to a response.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="code">The stable error code.</param>
    /// <returns>A task that completes when the body is written.</returns>
    public static Task WriteAsync(HttpContext httpContext, int statusCode, string code)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = statusCode;

        return httpContext.Response.WriteAsJsonAsync(new ApiError(code));
    }

    /// <summary>
    /// Maps a domain error code to its HTTP status.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <returns>The HTTP status code.</returns>
    public static int StatusFor(string code) => code switch
    {
        GameErrorCodes.NotYourObject or GameErrorCodes.AccessDenied => StatusCodes.Status403Forbidden,
        GameErrorCodes.NotFound => StatusCodes.Status404NotFound,
        GameErrorCodes.NotAuthenticated => StatusCodes.Status401Unauthorized,
        GameErrorCodes.ValidationFailed => StatusCodes.Status400BadRequest,
        GameErrorCodes.DuplicateRequest => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status422UnprocessableEntity,
    };

    /// <summary>
    /// Gets the error code of a bare status code, for the responses that the framework
    /// produces without a body.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>The error code.</returns>
    public static string CodeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status401Unauthorized => GameErrorCodes.NotAuthenticated,
        StatusCodes.Status403Forbidden => GameErrorCodes.AccessDenied,
        StatusCodes.Status404NotFound => GameErrorCodes.NotFound,
        StatusCodes.Status413PayloadTooLarge => GameErrorCodes.PayloadTooLarge,
        StatusCodes.Status429TooManyRequests => GameErrorCodes.RateLimited,
        StatusCodes.Status400BadRequest => GameErrorCodes.ValidationFailed,
        _ => GameErrorCodes.InternalError,
    };
}

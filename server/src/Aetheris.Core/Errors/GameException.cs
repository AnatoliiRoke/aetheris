namespace Aetheris.Core.Errors;

/// <summary>
/// A domain error with a stable code and optional details.
/// </summary>
/// <remarks>
/// The API turns this into the response body described in
/// documentation/tech/04-api.md, section 8:
/// <c>{ "error": "validation_failed", "details": {...} }</c>.
/// </remarks>
public class GameException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GameException"/> class.
    /// </summary>
    /// <param name="code">The stable error code.</param>
    /// <param name="message">A human readable message, used for logs and for administrators.</param>
    /// <param name="details">Optional structured details sent to the client.</param>
    /// <param name="innerException">The cause of this error, if any.</param>
    public GameException(
        string code,
        string message,
        IReadOnlyDictionary<string, object?>? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        Code = code;
        Details = details ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Gets the stable error code, used by the client for localisation.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the structured details of the error.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Details { get; }
}

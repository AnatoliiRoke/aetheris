namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>request_idempotency</c> table: a request identifier that was already
/// executed, and the response that was sent for it.
/// </summary>
/// <remarks>
/// A repeated command with the same identifier gets the stored response and changes
/// nothing (documentation/tech/04, section 3). The rows are removed after
/// <c>security.idempotency.retention_hours</c>.
/// </remarks>
public sealed class RequestIdempotencyEntity
{
    /// <summary>
    /// Gets or sets the identifier of the player who sent the request.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the request identifier sent by the client.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the route of the command.
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the request body, used to detect a reused identifier.
    /// </summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP status of the stored response.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the JSON body of the stored response.
    /// </summary>
    public string? ResponseBody { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the first execution.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}

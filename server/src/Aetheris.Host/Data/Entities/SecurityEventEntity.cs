namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>security_event</c> table: one event of the security journal.
/// </summary>
/// <remarks>
/// Sign-ins, failed sign-ins, lockouts, password changes, triggered rate limits and
/// administrator sign-ins (task 00002, section 2.7). The row never holds a password, a
/// token or an e-mail address: the account is named by its identifier only.
/// </remarks>
public sealed class SecurityEventEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the event.
    /// </summary>
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Gets or sets the event type, one of <see cref="Security.SecurityEventTypes"/>.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the account the event belongs to, if known.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the remote IP address of the request.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets a short machine-readable detail, for example the rate limit group.
    /// </summary>
    public string? Detail { get; set; }
}

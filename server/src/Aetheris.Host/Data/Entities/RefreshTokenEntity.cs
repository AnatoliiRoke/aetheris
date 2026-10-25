namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>refresh_token</c> table: a refresh token of a player session.
/// </summary>
/// <remarks>
/// Only the SHA-256 hash of the token is stored, so a copy of the database does not give
/// access to any session. A token is used once: refreshing revokes it and issues a new one.
/// A revoked token that comes back revokes every token of the account.
/// </remarks>
public sealed class RefreshTokenEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the account.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA-256 hash of the token, hex encoded.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiry timestamp.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the token was revoked, if it was.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }
}

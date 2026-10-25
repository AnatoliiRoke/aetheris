namespace Aetheris.Host.Data.Entities;

/// <summary>
/// Entity for the <c>admin_account</c> table: an administrator account.
/// </summary>
/// <remarks>
/// Kept separate from the player accounts (documentation/tech/03-model-danykh.md,
/// section 3.7). Administrator sign-in is handled by ASP.NET Core Identity, which stores
/// its own identity rows; this table holds the administrator profile and the roles used
/// by the admin panel (balance, support, moderation).
/// </remarks>
public sealed class AdminAccountEntity
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identity user key this administrator account is linked to.
    /// </summary>
    public string IdentityUserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the administrator.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a comma separated list of administrator roles.
    /// </summary>
    public string Roles { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether two-factor authentication is enforced.
    /// </summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}

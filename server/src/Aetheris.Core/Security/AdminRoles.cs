namespace Aetheris.Core.Security;

/// <summary>
/// The roles of an administrator account (documentation/tech/05, section 8).
/// </summary>
/// <remarks>
/// The roles are stored in the <c>admin_account.roles</c> column as a comma separated list
/// and become claims of type <see cref="ClaimType"/> when the administrator signs in.
/// </remarks>
public static class AdminRoles
{
    /// <summary>
    /// The claim type that carries an administrator role.
    /// </summary>
    public const string ClaimType = "aetheris:admin_role";

    /// <summary>
    /// The main role: every right, including the time multiplier.
    /// </summary>
    public const string Owner = "owner";

    /// <summary>
    /// Changes the configuration parameters.
    /// </summary>
    public const string Balance = "balance";

    /// <summary>
    /// Handles player support.
    /// </summary>
    public const string Support = "support";

    /// <summary>
    /// Moderates player content.
    /// </summary>
    public const string Moderation = "moderation";

    /// <summary>
    /// Gets every known role.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Owner, Balance, Support, Moderation];
}

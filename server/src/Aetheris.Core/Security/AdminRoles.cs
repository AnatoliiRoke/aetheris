namespace Aetheris.Core.Security;

/// <summary>
/// The codes of the starting administrator roles (documentation/tech/05, section 8).
/// </summary>
/// <remarks>
/// The roles themselves are data: rows of <c>admin_role</c> with their permissions in
/// <c>admin_role_permission</c>, seeded from <c>admin-access-seed.json</c> (task 00007,
/// decision 4). A new role needs no constant here; these codes only name the starting roles
/// for the command line and the tests.
/// </remarks>
public static class AdminRoles
{
    /// <summary>
    /// The claim type that carries the identifier of the administrator account.
    /// </summary>
    public const string AdminIdClaimType = "aetheris:admin_id";

    /// <summary>
    /// The main role: every permission.
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
}

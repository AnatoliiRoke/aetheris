namespace Aetheris.Core.Security;

/// <summary>
/// The names of the authorization policies of the server.
/// </summary>
/// <remarks>
/// A feature protects its endpoint by naming one of these policies; it never writes its
/// own permission check (task 00002, section 2.2). The policies themselves are defined
/// once, in the host.
/// </remarks>
public static class SecurityPolicies
{
    /// <summary>
    /// A signed-in player.
    /// </summary>
    public const string Player = "player";

    /// <summary>
    /// A signed-in administrator with an open session. What the administrator may do is decided
    /// per endpoint by the permissions of the roles in the database (task 00007, decision 4).
    /// </summary>
    public const string Admin = "admin";
}

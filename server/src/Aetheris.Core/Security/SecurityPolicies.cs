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
    /// A signed-in administrator who passed the second factor, with any administrator role.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// An administrator with the owner role: the time multiplier and every other right.
    /// </summary>
    public const string AdminOwner = "admin.owner";

    /// <summary>
    /// An administrator who may change the configuration parameters.
    /// </summary>
    public const string AdminBalance = "admin.balance";

    /// <summary>
    /// An administrator who handles player support and reads the audit log.
    /// </summary>
    public const string AdminSupport = "admin.support";

    /// <summary>
    /// An administrator who moderates player content.
    /// </summary>
    public const string AdminModeration = "admin.moderation";
}

namespace Aetheris.Core.Security;

/// <summary>
/// The rate limit groups of the server.
/// </summary>
/// <remarks>
/// <para>
/// Every group reads its limit from the configuration in the database:
/// <c>security.rate_limit.{group}.permit_limit</c> requests per
/// <c>security.rate_limit.{group}.window_seconds</c> seconds. A change of the values takes
/// effect without a restart.
/// </para>
/// <para>
/// On top of the groups, every request counts against the per-IP limit
/// <see cref="Ip"/>.
/// </para>
/// </remarks>
public static class RateLimitGroups
{
    /// <summary>
    /// The global limit per IP address, applied to every request.
    /// </summary>
    public const string Ip = "ip";

    /// <summary>
    /// Sign-in, registration and password reset, counted per IP address. Stricter than the
    /// other groups, because these requests are the target of password guessing.
    /// </summary>
    public const string Auth = "auth";

    /// <summary>
    /// Player commands, counted per player.
    /// </summary>
    public const string Command = "command";

    /// <summary>
    /// Administrator requests, counted per administrator.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// The sign-in of an administrator, counted per IP address. Stricter than <see cref="Auth"/>:
    /// the admin panel is the most valuable target (task 00007, decision 5).
    /// </summary>
    public const string AdminAuth = "admin_auth";

    /// <summary>
    /// Gets the groups that endpoints can name. The <see cref="Ip"/> limit is global and
    /// is not named by endpoints.
    /// </summary>
    public static IReadOnlyList<string> EndpointGroups { get; } = [Auth, AdminAuth, Command, Admin];
}

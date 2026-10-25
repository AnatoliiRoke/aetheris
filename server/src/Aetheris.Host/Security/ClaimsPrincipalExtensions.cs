using System.Security.Claims;

namespace Aetheris.Host.Security;

/// <summary>
/// Reads the identity of the caller.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Gets the account identifier of a signed-in caller.
    /// </summary>
    /// <param name="principal">The caller.</param>
    /// <returns>The identifier, or <see langword="null"/> for an anonymous caller.</returns>
    public static string? GetPlayerId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.Identity?.IsAuthenticated == true
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;
    }

    /// <summary>
    /// Gets the administrator account identifier of a signed-in administrator.
    /// </summary>
    /// <param name="principal">The caller.</param>
    /// <returns>The identifier, or <see langword="null"/> when the caller is not an administrator.</returns>
    public static string? GetAdminId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(AetherisClaims.AdminId);
    }
}

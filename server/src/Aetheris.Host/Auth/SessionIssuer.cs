using Aetheris.Host.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Aetheris.Host.Auth;

/// <summary>
/// Starts and ends a player session: the short session cookie and the refresh token cookie.
/// </summary>
public sealed class SessionIssuer
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly RefreshTokenService _refreshTokens;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionIssuer"/> class.
    /// </summary>
    /// <param name="signInManager">The sign-in manager.</param>
    /// <param name="refreshTokens">The refresh token service.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    public SessionIssuer(
        SignInManager<IdentityUser> signInManager,
        RefreshTokenService refreshTokens,
        SecuritySettings settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(refreshTokens);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _signInManager = signInManager;
        _refreshTokens = refreshTokens;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Starts a session with a new refresh token.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="user">The account.</param>
    /// <returns>A task that completes when the cookies are set.</returns>
    public async Task StartAsync(HttpContext httpContext, IdentityUser user)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);

        (string token, DateTimeOffset expiresAt) =
            await _refreshTokens.IssueAsync(user.Id, httpContext.RequestAborted).ConfigureAwait(false);

        await ContinueAsync(httpContext, user, token, expiresAt).ConfigureAwait(false);
    }

    /// <summary>
    /// Continues a session with an already rotated refresh token.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="user">The account.</param>
    /// <param name="refreshToken">The new refresh token.</param>
    /// <param name="refreshExpiresAt">The expiry of the refresh token.</param>
    /// <returns>A task that completes when the cookies are set.</returns>
    public async Task ContinueAsync(
        HttpContext httpContext,
        IdentityUser user,
        string refreshToken,
        DateTimeOffset refreshExpiresAt)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(user);

        // The session cookie lives only for the configured short time; the browser drops
        // it when it closes, and the refresh cookie starts a new one.
        AuthenticationProperties properties = new()
        {
            IsPersistent = false,
            ExpiresUtc = _timeProvider.GetUtcNow() + _settings.SessionAccessLifetime,
        };

        await _signInManager.SignInAsync(user, properties).ConfigureAwait(false);

        httpContext.Response.Cookies.Append(AuthCookies.Refresh, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = AuthCookies.RefreshPath,
            Expires = refreshExpiresAt,
            IsEssential = true,
        });
    }

    /// <summary>
    /// Ends the session and removes the cookies.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>A task that completes when the session is ended.</returns>
    public async Task EndAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (httpContext.Request.Cookies.TryGetValue(AuthCookies.Refresh, out string? token))
        {
            await _refreshTokens.RevokeAsync(token, httpContext.RequestAborted).ConfigureAwait(false);
        }

        await _signInManager.SignOutAsync().ConfigureAwait(false);

        httpContext.Response.Cookies.Delete(AuthCookies.Refresh, new CookieOptions
        {
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = AuthCookies.RefreshPath,
        });
    }
}

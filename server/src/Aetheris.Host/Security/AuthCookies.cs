namespace Aetheris.Host.Security;

/// <summary>
/// The names of the cookies and headers of the session and of the anti-forgery protection.
/// </summary>
/// <remarks>
/// Every cookie is <c>HttpOnly</c>, <c>Secure</c> and <c>SameSite=Strict</c>
/// (task 00002, section 2.1).
/// </remarks>
public static class AuthCookies
{
    /// <summary>The short-lived player session cookie.</summary>
    public const string Session = "aetheris.session";

    /// <summary>The refresh token cookie, sent to the authentication routes only.</summary>
    public const string Refresh = "aetheris.refresh";

    /// <summary>The administrator session cookie, sent to the admin routes only.</summary>
    public const string Admin = "aetheris.admin";

    /// <summary>The anti-forgery cookie.</summary>
    public const string Csrf = "aetheris.csrf";

    /// <summary>The header that carries the anti-forgery token.</summary>
    public const string CsrfHeader = "X-XSRF-TOKEN";

    /// <summary>The header that carries the request identifier of a command.</summary>
    public const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>The path of the refresh cookie.</summary>
    public const string RefreshPath = "/api/v1/auth";

    /// <summary>The path of the administrator cookie.</summary>
    public const string AdminPath = "/api/admin";

    /// <summary>
    /// Gets the cookies that authenticate a request. A state-changing request that carries
    /// one of them needs an anti-forgery token.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [Session, Refresh, Admin];
}

/// <summary>
/// The authentication schemes of the server.
/// </summary>
public static class AuthSchemes
{
    /// <summary>The administrator cookie scheme, separate from the player session.</summary>
    public const string Admin = "AetherisAdmin";
}

/// <summary>
/// The claims that the server adds to a session.
/// </summary>
public static class AetherisClaims
{
    /// <summary>The claim that marks a session that passed the second factor.</summary>
    public const string SecondFactor = "aetheris:mfa";

    /// <summary>The value of <see cref="SecondFactor"/> for a time-based one-time code.</summary>
    public const string Totp = "totp";

    /// <summary>The claim that carries the identifier of the administrator account.</summary>
    public const string AdminId = "aetheris:admin_id";
}

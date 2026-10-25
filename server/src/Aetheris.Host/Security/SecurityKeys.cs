namespace Aetheris.Host.Security;

/// <summary>
/// The configuration keys of the protection mechanisms.
/// </summary>
/// <remarks>
/// Every number of the protection lives in the database, under these keys
/// (task 00002, section 1a). The starting values and the bounds are in
/// <see cref="Data.ConfigSeed"/>.
/// </remarks>
public static class SecurityKeys
{
    /// <summary>The smallest password length.</summary>
    public const string PasswordMinLength = "security.password.min_length";

    /// <summary>The largest password length.</summary>
    public const string PasswordMaxLength = "security.password.max_length";

    /// <summary>The smallest number of different characters in a password.</summary>
    public const string PasswordMinUniqueCharacters = "security.password.min_unique_chars";

    /// <summary>The PBKDF2 iterations of the password hash.</summary>
    public const string PasswordHashIterations = "security.password.hash_iterations";

    /// <summary>The largest e-mail address length.</summary>
    public const string EmailMaxLength = "security.account.email_max_length";

    /// <summary>The largest length of a confirmation or reset token.</summary>
    public const string TokenMaxLength = "security.account.token_max_length";

    /// <summary>The failed sign-ins in a row before the account is locked.</summary>
    public const string LoginMaxFailedAttempts = "security.login.max_failed_attempts";

    /// <summary>How long a locked account stays locked, in minutes.</summary>
    public const string LoginLockoutMinutes = "security.login.lockout_minutes";

    /// <summary>The lifetime of the session cookie, in minutes.</summary>
    public const string SessionAccessMinutes = "security.session.access_minutes";

    /// <summary>The lifetime of the refresh token, in days.</summary>
    public const string SessionRefreshDays = "security.session.refresh_days";

    /// <summary>The lifetime of the administrator session, in minutes.</summary>
    public const string AdminSessionMinutes = "security.admin.session_minutes";

    /// <summary>The largest time multiplier an administrator may set.</summary>
    public const string AdminMaxClockMultiplier = "security.admin.max_clock_multiplier";

    /// <summary>The largest number of audit records returned at once.</summary>
    public const string AdminAuditPageSize = "security.admin.audit_page_size";

    /// <summary>The largest request body, in bytes.</summary>
    public const string RequestMaxBodyBytes = "security.request.max_body_bytes";

    /// <summary>The largest SignalR message, in bytes.</summary>
    public const string SignalRMaxMessageBytes = "security.signalr.max_message_bytes";

    /// <summary>The largest length of the request identifier.</summary>
    public const string IdempotencyKeyMaxLength = "security.idempotency.max_key_length";

    /// <summary>How long a request identifier is remembered, in hours.</summary>
    public const string IdempotencyRetentionHours = "security.idempotency.retention_hours";

    /// <summary>The interval of the cleanup of old request identifiers, in minutes.</summary>
    public const string IdempotencyCleanupMinutes = "security.idempotency.cleanup_interval_minutes";

    /// <summary>The failed sign-ins within the window that raise an alert.</summary>
    public const string AlertFailedSignInThreshold = "security.alerts.failed_sign_in_threshold";

    /// <summary>The window of the failed sign-in alert, in minutes.</summary>
    public const string AlertFailedSignInWindowMinutes = "security.alerts.failed_sign_in_window_minutes";

    /// <summary>The HSTS max-age, in days.</summary>
    public const string HstsMaxAgeDays = "security.hsts.max_age_days";

    /// <summary>
    /// Gets the key of the number of requests allowed per window in a rate limit group.
    /// </summary>
    /// <param name="group">The rate limit group.</param>
    /// <returns>The configuration key.</returns>
    public static string RateLimitPermits(string group) => $"security.rate_limit.{group}.permit_limit";

    /// <summary>
    /// Gets the key of the window length of a rate limit group, in seconds.
    /// </summary>
    /// <param name="group">The rate limit group.</param>
    /// <returns>The configuration key.</returns>
    public static string RateLimitWindow(string group) => $"security.rate_limit.{group}.window_seconds";
}

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

    /// <summary>Whether a password must contain a digit: 1 or 0.</summary>
    public const string PasswordRequireDigit = "security.password.require_digit";

    /// <summary>Whether a password must contain an upper-case letter: 1 or 0.</summary>
    public const string PasswordRequireUppercase = "security.password.require_uppercase";

    /// <summary>The PBKDF2 iterations of the password hash.</summary>
    public const string PasswordHashIterations = "security.password.hash_iterations";

    /// <summary>The largest e-mail address length.</summary>
    public const string EmailMaxLength = "security.account.email_max_length";

    /// <summary>The largest length of a confirmation or reset token.</summary>
    public const string TokenMaxLength = "security.account.token_max_length";

    /// <summary>The failed sign-ins in a row before the account is locked.</summary>
    public const string LoginMaxFailedAttempts = "security.login.max_failed_attempts";

    /// <summary>The failed sign-ins in a row from one IP address before it is locked.</summary>
    public const string LoginIpMaxFailedAttempts = "security.login.ip_max_failed_attempts";

    /// <summary>How long a locked account stays locked, in minutes.</summary>
    public const string LoginLockoutMinutes = "security.login.lockout_minutes";

    /// <summary>The lifetime of the session cookie, in minutes.</summary>
    public const string SessionAccessMinutes = "security.session.access_minutes";

    /// <summary>The lifetime of the refresh token, in days.</summary>
    public const string SessionRefreshDays = "security.session.refresh_days";

    /// <summary>The lifetime of a session with "remember me", in days.</summary>
    public const string SessionRememberDays = "security.session.remember_days";

    /// <summary>How often the session cookie is checked against the account, in seconds.</summary>
    public const string SessionStampCheckSeconds = "security.session.stamp_check_seconds";

    /// <summary>The smallest nickname length.</summary>
    public const string NicknameMinLength = "security.account.nickname_min_length";

    /// <summary>The largest nickname length.</summary>
    public const string NicknameMaxLength = "security.account.nickname_max_length";

    /// <summary>The characters a nickname may contain, as an <see cref="Core.Text.AllowedCharacters"/> definition.</summary>
    public const string NicknameCharacters = "security.account.nickname_characters";

    /// <summary>The days between two changes of the nickname.</summary>
    public const string NicknameChangeIntervalDays = "security.account.nickname_change_days";

    /// <summary>The lifetime of the e-mail confirmation link, in hours.</summary>
    public const string ConfirmationLifetimeHours = "security.account.confirmation_hours";

    /// <summary>The smallest pause between two confirmation e-mails, in minutes.</summary>
    public const string ConfirmationResendMinutes = "security.account.confirmation_resend_minutes";

    /// <summary>The lifetime of the password reset link, in minutes.</summary>
    public const string ResetLifetimeMinutes = "security.account.reset_minutes";

    /// <summary>How long an account without a confirmed e-mail address lives, in days.</summary>
    public const string UnconfirmedLifetimeDays = "security.account.unconfirmed_days";

    /// <summary>The delay of a requested account deletion, in days.</summary>
    public const string DeletionDelayDays = "security.account.deletion_days";

    /// <summary>The registrations from one IP address within the window.</summary>
    public const string RegistrationIpLimit = "security.registration.ip_limit";

    /// <summary>The window of the registration limit, in hours.</summary>
    public const string RegistrationIpWindowHours = "security.registration.ip_window_hours";

    /// <summary>Whether the captcha is asked on suspicion: 1 or 0.</summary>
    public const string CaptchaEnabled = "security.registration.captcha_enabled";

    /// <summary>The registrations from one IP address within the window after which the captcha is asked.</summary>
    public const string CaptchaAfterRegistrations = "security.registration.captcha_after";

    /// <summary>The lifetime of the administrator session, in minutes.</summary>
    public const string AdminSessionMinutes = "security.admin.session_minutes";

    /// <summary>The largest time multiplier an administrator may set.</summary>
    public const string AdminMaxClockMultiplier = "security.admin.max_clock_multiplier";

    /// <summary>The largest number of audit records returned at once.</summary>
    public const string AdminAuditPageSize = "security.admin.audit_page_size";

    /// <summary>Whether an administrator signs in with the one-time code of an authenticator app.</summary>
    public const string AdminRequireTotp = "admin.require_totp";

    /// <summary>The longest life of an administrator session, however active, in hours.</summary>
    public const string AdminSessionMaxHours = "security.admin.session_max_hours";

    /// <summary>The failed administrator sign-ins in a row before the account is locked.</summary>
    public const string AdminLoginMaxFailedAttempts = "security.admin.login_max_failed_attempts";

    /// <summary>How long a locked administrator account stays locked, in minutes.</summary>
    public const string AdminLoginLockoutMinutes = "security.admin.login_lockout_minutes";

    /// <summary>The largest number of rows of an admin table returned at once.</summary>
    public const string AdminPageSize = "security.admin.page_size";

    /// <summary>The largest length of the search text of an admin table.</summary>
    public const string AdminSearchMaxLength = "security.admin.search_max_length";

    /// <summary>The smallest length of the reason of an administrator change.</summary>
    public const string AdminReasonMinLength = "security.admin.reason_min_length";

    /// <summary>The largest length of the reason of an administrator change.</summary>
    public const string AdminReasonMaxLength = "security.admin.reason_max_length";

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

using Aetheris.Core.Configuration;
using Aetheris.Host.Data;

namespace Aetheris.Host.Security;

/// <summary>
/// Typed read access to the numeric parameters of the protection mechanisms.
/// </summary>
/// <remarks>
/// <para>
/// Every value is read from <see cref="IGameConfig"/> on every call, so a change in the
/// database takes effect after the next configuration reload, without a restart.
/// </para>
/// <para>
/// A value that is missing from the database falls back to the seed value, and a value
/// outside the bounds of its definition is moved to the nearest bound: a typo in the admin
/// panel can never switch a protection off.
/// </para>
/// </remarks>
public sealed class SecuritySettings
{
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecuritySettings"/> class.
    /// </summary>
    /// <param name="config">The configuration.</param>
    public SecuritySettings(IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
    }

    /// <summary>Gets the smallest password length.</summary>
    public int PasswordMinLength => ReadInt32(SecurityKeys.PasswordMinLength);

    /// <summary>Gets the largest password length.</summary>
    public int PasswordMaxLength => ReadInt32(SecurityKeys.PasswordMaxLength);

    /// <summary>Gets the smallest number of different characters in a password.</summary>
    public int PasswordMinUniqueCharacters => ReadInt32(SecurityKeys.PasswordMinUniqueCharacters);

    /// <summary>Gets the PBKDF2 iterations of the password hash.</summary>
    public int PasswordHashIterations => ReadInt32(SecurityKeys.PasswordHashIterations);

    /// <summary>Gets the largest e-mail address length.</summary>
    public int EmailMaxLength => ReadInt32(SecurityKeys.EmailMaxLength);

    /// <summary>Gets the largest length of a confirmation or reset token.</summary>
    public int TokenMaxLength => ReadInt32(SecurityKeys.TokenMaxLength);

    /// <summary>Gets the failed sign-ins in a row before the account is locked.</summary>
    public int LoginMaxFailedAttempts => ReadInt32(SecurityKeys.LoginMaxFailedAttempts);

    /// <summary>Gets how long a locked account stays locked.</summary>
    public TimeSpan LoginLockout => TimeSpan.FromMinutes(Read(SecurityKeys.LoginLockoutMinutes));

    /// <summary>Gets the lifetime of the session cookie.</summary>
    public TimeSpan SessionAccessLifetime => TimeSpan.FromMinutes(Read(SecurityKeys.SessionAccessMinutes));

    /// <summary>Gets the lifetime of the refresh token.</summary>
    public TimeSpan SessionRefreshLifetime => TimeSpan.FromDays(Read(SecurityKeys.SessionRefreshDays));

    /// <summary>Gets the lifetime of the administrator session.</summary>
    public TimeSpan AdminSessionLifetime => TimeSpan.FromMinutes(Read(SecurityKeys.AdminSessionMinutes));

    /// <summary>Gets the largest time multiplier an administrator may set.</summary>
    public double AdminMaxClockMultiplier => Read(SecurityKeys.AdminMaxClockMultiplier);

    /// <summary>Gets the largest number of audit records returned at once.</summary>
    public int AdminAuditPageSize => ReadInt32(SecurityKeys.AdminAuditPageSize);

    /// <summary>Gets the largest request body, in bytes.</summary>
    public long RequestMaxBodyBytes => (long)Read(SecurityKeys.RequestMaxBodyBytes);

    /// <summary>Gets the largest SignalR message, in bytes.</summary>
    public long SignalRMaxMessageBytes => (long)Read(SecurityKeys.SignalRMaxMessageBytes);

    /// <summary>Gets the largest length of the request identifier.</summary>
    public int IdempotencyKeyMaxLength => ReadInt32(SecurityKeys.IdempotencyKeyMaxLength);

    /// <summary>Gets how long a request identifier is remembered.</summary>
    public TimeSpan IdempotencyRetention => TimeSpan.FromHours(Read(SecurityKeys.IdempotencyRetentionHours));

    /// <summary>Gets the interval of the cleanup of old request identifiers.</summary>
    public TimeSpan IdempotencyCleanupInterval => TimeSpan.FromMinutes(Read(SecurityKeys.IdempotencyCleanupMinutes));

    /// <summary>Gets the failed sign-ins within the window that raise an alert.</summary>
    public int AlertFailedSignInThreshold => ReadInt32(SecurityKeys.AlertFailedSignInThreshold);

    /// <summary>Gets the window of the failed sign-in alert.</summary>
    public TimeSpan AlertFailedSignInWindow => TimeSpan.FromMinutes(Read(SecurityKeys.AlertFailedSignInWindowMinutes));

    /// <summary>Gets the HSTS max-age.</summary>
    public TimeSpan HstsMaxAge => TimeSpan.FromDays(Read(SecurityKeys.HstsMaxAgeDays));

    /// <summary>Gets the length of the neutral echo message.</summary>
    public int EchoMaxLength => ReadInt32(ConfigSeed.EchoMaxLength);

    /// <summary>
    /// Gets the limit of a rate limit group.
    /// </summary>
    /// <param name="group">The rate limit group.</param>
    /// <returns>The number of requests allowed per window, and the window.</returns>
    public (int PermitLimit, TimeSpan Window) RateLimit(string group)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);

        int permits = ReadInt32(SecurityKeys.RateLimitPermits(group));
        TimeSpan window = TimeSpan.FromSeconds(Read(SecurityKeys.RateLimitWindow(group)));

        return (permits, window);
    }

    private int ReadInt32(string key) => (int)Read(key);

    private double Read(string key)
    {
        ConfigParameterDefinition definition = ConfigSeed.Find(key)
            ?? throw new InvalidOperationException($"The parameter '{key}' has no definition in the seed.");

        double fallback = double.Parse(definition.DefaultValue, System.Globalization.CultureInfo.InvariantCulture);
        double value = _config.GetDouble(key, fallback);

        return definition.Clamp(value);
    }
}

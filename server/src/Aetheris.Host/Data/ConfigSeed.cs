using Aetheris.Core.Configuration;
using Aetheris.Core.Security;
using Aetheris.Host.Scheduling;
using Aetheris.Host.Security;
using Aetheris.World.Configuration;

namespace Aetheris.Host.Data;

/// <summary>
/// The initial parameters written into a fresh database.
/// </summary>
/// <remarks>
/// The core has no game rules, so it only seeds neutral <c>test.*</c> parameters, the
/// <c>security.*</c> parameters of its own protection mechanisms and the
/// <c>scheduler.*</c> parameters of the event scheduler. A module brings its own
/// definitions, which are added here, for example the <c>world.*</c> parameters. Every parameter can be changed later from the admin
/// panel within its bounds; the seed only provides the starting point for a new database,
/// so that the rules always find a value.
/// </remarks>
public static class ConfigSeed
{
    /// <summary>
    /// The neutral test parameter that shows how a value flows from the database into the code.
    /// </summary>
    public const string HelloMultiplier = "test.hello_multiplier";

    /// <summary>
    /// The largest length of the message of the neutral echo command.
    /// </summary>
    public const string EchoMaxLength = "test.echo.max_length";

    private static readonly ConfigParameterDefinition[] CoreDefinitions =
    [
        new(HelloMultiplier, "1", "0", "1000", "Neutral example parameter used by the configuration tests."),
        new(EchoMaxLength, "256", "1", "4096", "Largest message length of the neutral echo command."),

        new(SecurityKeys.PasswordMinLength, "12", "8", "128", "Smallest password length."),
        new(SecurityKeys.PasswordMaxLength, "128", "16", "1024", "Largest password length."),
        new(SecurityKeys.PasswordMinUniqueCharacters, "5", "1", "64", "Smallest number of different characters in a password."),
        new(SecurityKeys.PasswordHashIterations, "210000", "100000", "2000000", "PBKDF2 iterations of the password hash; read at start."),
        new(SecurityKeys.EmailMaxLength, "256", "64", "320", "Largest e-mail address length."),
        new(SecurityKeys.TokenMaxLength, "2048", "256", "8192", "Largest length of a confirmation or reset token."),

        new(SecurityKeys.LoginMaxFailedAttempts, "5", "1", "100", "Failed sign-ins in a row before the account is locked."),
        new(SecurityKeys.LoginLockoutMinutes, "15", "1", "1440", "How long a locked account stays locked."),
        new(SecurityKeys.SessionAccessMinutes, "15", "1", "1440", "Lifetime of the session cookie."),
        new(SecurityKeys.SessionRefreshDays, "14", "1", "90", "Lifetime of the refresh token."),
        new(SecurityKeys.AdminSessionMinutes, "30", "1", "480", "Lifetime of the administrator session."),
        new(SecurityKeys.AdminMaxClockMultiplier, "100", "1", "10000", "Largest time multiplier an administrator may set."),
        new(SecurityKeys.AdminAuditPageSize, "100", "1", "1000", "Largest number of audit records returned at once."),

        new(SecurityKeys.RateLimitPermits(RateLimitGroups.Ip), "300", "1", "100000", "Requests per window per IP address."),
        new(SecurityKeys.RateLimitWindow(RateLimitGroups.Ip), "60", "1", "3600", "Window of the IP limit, in seconds."),
        new(SecurityKeys.RateLimitPermits(RateLimitGroups.Auth), "20", "1", "10000", "Sign-in, registration and reset requests per window per IP address."),
        new(SecurityKeys.RateLimitWindow(RateLimitGroups.Auth), "60", "1", "3600", "Window of the sign-in limit, in seconds."),
        new(SecurityKeys.RateLimitPermits(RateLimitGroups.Command), "120", "1", "100000", "Commands per window per player."),
        new(SecurityKeys.RateLimitWindow(RateLimitGroups.Command), "60", "1", "3600", "Window of the command limit, in seconds."),
        new(SecurityKeys.RateLimitPermits(RateLimitGroups.Admin), "60", "1", "10000", "Administrator requests per window per administrator."),
        new(SecurityKeys.RateLimitWindow(RateLimitGroups.Admin), "60", "1", "3600", "Window of the administrator limit, in seconds."),

        new(SecurityKeys.RequestMaxBodyBytes, "65536", "1024", "10485760", "Largest request body, in bytes."),
        new(SecurityKeys.SignalRMaxMessageBytes, "65536", "1024", "10485760", "Largest SignalR message, in bytes; read at start."),
        new(SecurityKeys.IdempotencyKeyMaxLength, "64", "16", "256", "Largest length of the request identifier."),
        new(SecurityKeys.IdempotencyRetentionHours, "24", "1", "720", "How long a request identifier is remembered."),
        new(SecurityKeys.IdempotencyCleanupMinutes, "60", "1", "1440", "Interval of the cleanup of old request identifiers."),
        new(SecurityKeys.AlertFailedSignInThreshold, "20", "1", "100000", "Failed sign-ins within the window that raise an alert."),
        new(SecurityKeys.AlertFailedSignInWindowMinutes, "10", "1", "1440", "Window of the failed sign-in alert, in minutes."),
        new(SecurityKeys.HstsMaxAgeDays, "365", "1", "730", "HSTS max-age, in days; read at start."),

        new(SchedulerKeys.PollSeconds, "5", "1", "3600", "Interval of the check for due scheduled events, in seconds."),
        new(SchedulerKeys.BatchSize, "100", "1", "10000", "Largest number of due events handled in one pass."),
    ];

    private static readonly ConfigParameterDefinition[] Definitions = [.. CoreDefinitions, .. WorldConfigKeys.Definitions];

    private static readonly Dictionary<string, ConfigParameterDefinition> DefinitionsByKey =
        Definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);

    /// <summary>
    /// Gets every known parameter definition.
    /// </summary>
    public static IReadOnlyList<ConfigParameterDefinition> All => Definitions;

    /// <summary>
    /// Finds the definition of a parameter.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <returns>The definition, or <see langword="null"/> when the key is not known.</returns>
    public static ConfigParameterDefinition? Find(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return DefinitionsByKey.GetValueOrDefault(key);
    }

    /// <summary>
    /// The parameters and their initial values.
    /// </summary>
    /// <returns>The seed parameters, ready to be inserted.</returns>
    public static IReadOnlyList<(string Key, string Value)> GetParameters()
    {
        return Definitions.Select(definition => (definition.Key, definition.DefaultValue)).ToList();
    }
}

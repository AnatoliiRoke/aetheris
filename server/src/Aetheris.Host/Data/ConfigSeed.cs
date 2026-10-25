using System.Globalization;
using Aetheris.Core.Security;
using Aetheris.Host.Security;

namespace Aetheris.Host.Data;

/// <summary>
/// A parameter that the server knows about: its key, its starting value and its bounds.
/// </summary>
/// <param name="Key">The dotted parameter key.</param>
/// <param name="DefaultValue">The value written into a fresh database.</param>
/// <param name="Minimum">The smallest numeric value an administrator may set, or <see langword="null"/>.</param>
/// <param name="Maximum">The largest numeric value an administrator may set, or <see langword="null"/>.</param>
/// <param name="Description">What the parameter controls.</param>
/// <remarks>
/// The values are kept as text, exactly as they are stored in <c>config_parameter</c>.
/// </remarks>
public sealed record ConfigParameterDefinition(
    string Key,
    string DefaultValue,
    string? Minimum,
    string? Maximum,
    string Description)
{
    /// <summary>
    /// Checks that a value fits the bounds of the parameter.
    /// </summary>
    /// <param name="value">The raw value to check.</param>
    /// <returns><see langword="true"/> when the value is acceptable.</returns>
    public bool Accepts(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (Minimum is null && Maximum is null)
        {
            return true;
        }

        if (!TryParse(value, out double number))
        {
            return false;
        }

        return (Minimum is null || (TryParse(Minimum, out double minimum) && number >= minimum))
            && (Maximum is null || (TryParse(Maximum, out double maximum) && number <= maximum));
    }

    /// <summary>
    /// Brings a numeric value into the bounds of the parameter.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <returns>The value, moved to the nearest bound when it is outside.</returns>
    public double Clamp(double value)
    {
        if (Minimum is not null && TryParse(Minimum, out double minimum) && value < minimum)
        {
            return minimum;
        }

        if (Maximum is not null && TryParse(Maximum, out double maximum) && value > maximum)
        {
            return maximum;
        }

        return value;
    }

    private static bool TryParse(string text, out double number) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
}

/// <summary>
/// The initial parameters written into a fresh database.
/// </summary>
/// <remarks>
/// The core has no game rules, so it only seeds neutral <c>test.*</c> parameters and the
/// <c>security.*</c> parameters of its own protection mechanisms. A feature adds its own
/// parameters here when it arrives. Every parameter can be changed later from the admin
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

    private static readonly ConfigParameterDefinition[] Definitions =
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
    ];

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

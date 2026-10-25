using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Security;

/// <summary>
/// The types of the security journal events.
/// </summary>
public static class SecurityEventTypes
{
    /// <summary>A player or an administrator signed in.</summary>
    public const string SignInSucceeded = "sign_in_succeeded";

    /// <summary>A sign-in failed: unknown account, wrong password or wrong second factor.</summary>
    public const string SignInFailed = "sign_in_failed";

    /// <summary>An account was locked after a series of failed sign-ins.</summary>
    public const string AccountLocked = "account_locked";

    /// <summary>A new account was registered.</summary>
    public const string AccountRegistered = "account_registered";

    /// <summary>An e-mail address was confirmed.</summary>
    public const string EmailConfirmed = "email_confirmed";

    /// <summary>A password reset was requested.</summary>
    public const string PasswordResetRequested = "password_reset_requested";

    /// <summary>A password was changed.</summary>
    public const string PasswordChanged = "password_changed";

    /// <summary>A session was refreshed.</summary>
    public const string SessionRefreshed = "session_refreshed";

    /// <summary>A revoked refresh token was presented again; every session of the account was revoked.</summary>
    public const string RefreshTokenReused = "refresh_token_reused";

    /// <summary>A player signed out.</summary>
    public const string SignedOut = "signed_out";

    /// <summary>A rate limit rejected a request.</summary>
    public const string RateLimitExceeded = "rate_limit_exceeded";

    /// <summary>A command or a subscription targeted an object of another player.</summary>
    public const string OwnershipDenied = "ownership_denied";

    /// <summary>A request with an authentication cookie came without a valid anti-forgery token.</summary>
    public const string CsrfRejected = "csrf_rejected";

    /// <summary>An administrator signed in with the second factor.</summary>
    public const string AdminSignInSucceeded = "admin_sign_in_succeeded";

    /// <summary>An administrator sign-in failed.</summary>
    public const string AdminSignInFailed = "admin_sign_in_failed";

    /// <summary>The failed sign-ins within the alert window crossed the threshold.</summary>
    public const string FailedSignInAlert = "failed_sign_in_alert";
}

/// <summary>
/// Writes the security journal: a row in <c>security_event</c> and a structured log entry.
/// </summary>
/// <remarks>
/// <para>
/// Every event is written in its own database context and transaction, so an event about
/// a rejected request is kept even when the request itself is rolled back.
/// </para>
/// <para>
/// The events never carry a password, a token or an e-mail address: the account is named
/// by its identifier only (task 00002, section 2.7). A series of failed sign-ins raises an
/// error-level alert, which Sentry reports when a DSN is configured.
/// </para>
/// </remarks>
public sealed class SecurityEventLog
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SecurityEventLog> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityEventLog"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to get a separate database context.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public SecurityEventLog(
        IServiceScopeFactory scopeFactory,
        SecuritySettings settings,
        TimeProvider timeProvider,
        ILogger<SecurityEventLog> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _settings = settings;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Writes one event.
    /// </summary>
    /// <param name="type">The event type, one of <see cref="SecurityEventTypes"/>.</param>
    /// <param name="httpContext">The request the event belongs to, used for the IP address.</param>
    /// <param name="userId">The identifier of the account, if known.</param>
    /// <param name="detail">A short machine-readable detail.</param>
    /// <returns>A task that completes when the event is stored.</returns>
    public async Task WriteAsync(string type, HttpContext? httpContext, string? userId = null, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        string? ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
        DateTimeOffset now = _timeProvider.GetUtcNow();

        _logger.LogInformation(
            "Security event {SecurityEventType} for account {UserId}, detail {SecurityEventDetail}.",
            type,
            userId ?? "-",
            detail ?? "-");

        // The request may already be aborted; the event is stored anyway.
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        dbContext.SecurityEvents.Add(new SecurityEventEntity
        {
            At = now,
            Type = type,
            UserId = userId,
            IpAddress = ipAddress,
            Detail = detail,
        });

        await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

        if (type is SecurityEventTypes.SignInFailed or SecurityEventTypes.AdminSignInFailed)
        {
            await CheckFailedSignInAlertAsync(dbContext, now).ConfigureAwait(false);
        }
    }

    private async Task CheckFailedSignInAlertAsync(AetherisDbContext dbContext, DateTimeOffset now)
    {
        DateTimeOffset windowStart = now - _settings.AlertFailedSignInWindow;
        int threshold = _settings.AlertFailedSignInThreshold;

        int failures = await dbContext.SecurityEvents
            .Where(entry => entry.At >= windowStart)
            .Where(entry => entry.Type == SecurityEventTypes.SignInFailed
                || entry.Type == SecurityEventTypes.AdminSignInFailed)
            .CountAsync(CancellationToken.None)
            .ConfigureAwait(false);

        // The alert fires once, at the moment the threshold is crossed, not for every
        // failure after it.
        if (failures != threshold)
        {
            return;
        }

        _logger.LogError(
            "Security alert: {FailureCount} failed sign-ins within {WindowMinutes} minutes.",
            failures,
            _settings.AlertFailedSignInWindow.TotalMinutes);

        dbContext.SecurityEvents.Add(new SecurityEventEntity
        {
            At = now,
            Type = SecurityEventTypes.FailedSignInAlert,
            Detail = $"failures={failures}",
        });

        await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }
}

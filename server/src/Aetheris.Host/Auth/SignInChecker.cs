using Aetheris.Host.Security;
using Microsoft.AspNetCore.Identity;

namespace Aetheris.Host.Auth;

/// <summary>
/// The outcome of a password check.
/// </summary>
public enum SignInOutcome
{
    /// <summary>The password is right and the account may sign in.</summary>
    Succeeded,

    /// <summary>The account does not exist or the password is wrong.</summary>
    InvalidCredentials,

    /// <summary>The account is locked after a series of failed sign-ins.</summary>
    LockedOut,

    /// <summary>The password is right, but the e-mail address is not confirmed yet.</summary>
    EmailNotConfirmed,
}

/// <summary>
/// The result of a password check.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="User">The account, when it exists.</param>
public sealed record SignInCheckResult(SignInOutcome Outcome, IdentityUser? User);

/// <summary>
/// The lockout of a kind of sign-in.
/// </summary>
/// <param name="MaxFailedAttempts">The failed attempts in a row before the account is locked.</param>
/// <param name="Lockout">How long the account stays locked.</param>
/// <remarks>
/// The players use the <c>security.login.*</c> parameters; the administrators have their own,
/// stricter ones (task 00007, decision 5).
/// </remarks>
public sealed record SignInLimits(int MaxFailedAttempts, TimeSpan Lockout);

/// <summary>
/// Checks a password and keeps the count of failed sign-ins, shared by the player and the
/// administrator sign-in (task 00002, section 2.1).
/// </summary>
/// <remarks>
/// <para>
/// The lockout is temporary: after <c>security.login.max_failed_attempts</c> failures in a
/// row the account is locked for <c>security.login.lockout_minutes</c>. Both values come
/// from the database for every attempt, so ASP.NET Core Identity's own lockout counter is
/// switched off and this class decides.
/// </para>
/// <para>
/// An unknown account costs the same password hash as a known one, so the answer time does
/// not tell which e-mail addresses are registered.
/// </para>
/// </remarks>
public sealed class SignInChecker
{
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<IdentityUser>().HashPassword(new IdentityUser(), Guid.NewGuid().ToString()));

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SecuritySettings _settings;
    private readonly SecurityEventLog _securityLog;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignInChecker"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="securityLog">The security journal.</param>
    /// <param name="timeProvider">The clock.</param>
    public SignInChecker(
        UserManager<IdentityUser> userManager,
        SecuritySettings settings,
        SecurityEventLog securityLog,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(securityLog);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _userManager = userManager;
        _settings = settings;
        _securityLog = securityLog;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Checks the e-mail address or the nickname, and the password.
    /// </summary>
    /// <param name="login">The e-mail address or the nickname; a nickname never contains <c>@</c>.</param>
    /// <param name="password">The password.</param>
    /// <param name="httpContext">The request, used for the security journal.</param>
    /// <param name="failureEventType">The security event written for a failure.</param>
    /// <param name="requireConfirmedEmail">Whether an account without a confirmed e-mail address is refused.</param>
    /// <param name="limits">The lockout to apply, or <see langword="null"/> for the one of the players.</param>
    /// <returns>The result of the check.</returns>
    /// <remarks>
    /// A player signs in before the confirmation and only gets the actions allowed without it
    /// (task 00006, decision 2); an administrator always needs a confirmed address.
    /// </remarks>
    public async Task<SignInCheckResult> CheckAsync(
        string login,
        string password,
        HttpContext httpContext,
        string failureEventType,
        bool requireConfirmedEmail = true,
        SignInLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(login);
        ArgumentNullException.ThrowIfNull(httpContext);

        IdentityUser? user = login.Contains('@', StringComparison.Ordinal)
            ? await _userManager.FindByEmailAsync(login).ConfigureAwait(false)
            : await _userManager.FindByNameAsync(login).ConfigureAwait(false);

        if (user is null)
        {
            new PasswordHasher<IdentityUser>().VerifyHashedPassword(new IdentityUser(), DummyHash.Value, password);
            await _securityLog.WriteAsync(failureEventType, httpContext, null, "unknown_account").ConfigureAwait(false);

            return new SignInCheckResult(SignInOutcome.InvalidCredentials, null);
        }

        if (await _userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            await _securityLog.WriteAsync(failureEventType, httpContext, user.Id, "locked_out").ConfigureAwait(false);

            return new SignInCheckResult(SignInOutcome.LockedOut, user);
        }

        if (!await _userManager.CheckPasswordAsync(user, password).ConfigureAwait(false))
        {
            await RegisterFailureAsync(user, httpContext, failureEventType, "wrong_password", limits).ConfigureAwait(false);

            return new SignInCheckResult(SignInOutcome.InvalidCredentials, user);
        }

        if (requireConfirmedEmail && !user.EmailConfirmed)
        {
            await _securityLog.WriteAsync(failureEventType, httpContext, user.Id, "email_not_confirmed").ConfigureAwait(false);

            return new SignInCheckResult(SignInOutcome.EmailNotConfirmed, user);
        }

        return new SignInCheckResult(SignInOutcome.Succeeded, user);
    }

    /// <summary>
    /// Counts one failed attempt and locks the account when the limit is reached.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <param name="httpContext">The request, used for the security journal.</param>
    /// <param name="failureEventType">The security event written for the failure.</param>
    /// <param name="detail">The reason of the failure.</param>
    /// <param name="limits">The lockout to apply, or <see langword="null"/> for the one of the players.</param>
    /// <returns>A task that completes when the failure is recorded.</returns>
    public async Task RegisterFailureAsync(
        IdentityUser user,
        HttpContext httpContext,
        string failureEventType,
        string detail,
        SignInLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        SignInLimits applied = limits ?? new SignInLimits(_settings.LoginMaxFailedAttempts, _settings.LoginLockout);

        await _securityLog.WriteAsync(failureEventType, httpContext, user.Id, detail).ConfigureAwait(false);

        // Identity's own limit is switched off; this only increments the counter.
        await _userManager.AccessFailedAsync(user).ConfigureAwait(false);

        int failures = await _userManager.GetAccessFailedCountAsync(user).ConfigureAwait(false);

        if (failures < applied.MaxFailedAttempts)
        {
            return;
        }

        await _userManager.SetLockoutEnabledAsync(user, true).ConfigureAwait(false);
        await _userManager
            .SetLockoutEndDateAsync(user, _timeProvider.GetUtcNow() + applied.Lockout)
            .ConfigureAwait(false);
        await _userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        await _securityLog.WriteAsync(SecurityEventTypes.AccountLocked, httpContext, user.Id).ConfigureAwait(false);
    }

    /// <summary>
    /// Clears the count of failed attempts after a successful sign-in.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <returns>A task that completes when the count is cleared.</returns>
    public Task ResetFailuresAsync(IdentityUser user) => _userManager.ResetAccessFailedCountAsync(user);
}

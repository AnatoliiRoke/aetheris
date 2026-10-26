using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Core.Errors;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// The action codes and the routes of the account commands (task 00006, decision 10).
/// </summary>
public static class AccountActions
{
    /// <summary>
    /// The action code of every account setting. It is allowed before the e-mail confirmation
    /// through the database list <c>unconfirmed_allowed_action</c>.
    /// </summary>
    public const string Settings = "account.settings";

    /// <summary>The route prefix of the account commands.</summary>
    public const string Prefix = "/api/v1/account";

    /// <summary>The error code of a confirmation e-mail asked again too soon.</summary>
    public const string ConfirmationTooSoon = "confirmation_too_soon";

    /// <summary>The error code of a nickname changed again too soon.</summary>
    public const string NicknameChangeTooSoon = "nickname_change_too_soon";

    /// <summary>The error code of a wrong current password.</summary>
    public const string WrongPassword = "wrong_password";
}

/// <summary>
/// The base of the account commands: they are account settings, allowed before the
/// e-mail confirmation and before the onboarding.
/// </summary>
/// <typeparam name="TRequest">The request.</typeparam>
/// <typeparam name="TResponse">The response.</typeparam>
public abstract class AccountCommand<TRequest, TResponse> : Command<TRequest, TResponse>
    where TRequest : class
{
    /// <inheritdoc />
    public override string ActionCode => AccountActions.Settings;

    /// <inheritdoc />
    public override bool RequiresOnboarding => false;

    /// <summary>
    /// Throws a validation error of one field.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="codes">The error codes.</param>
    /// <returns>Never returns.</returns>
    protected static GameException Invalid(string field, IEnumerable<string> codes) =>
        new(GameErrorCodes.ValidationFailed, "The request did not pass validation.", new Dictionary<string, object?>
        {
            [field] = codes.Distinct(StringComparer.Ordinal).ToArray(),
        });

    /// <summary>
    /// Finds the account of the command.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="context">The context of the command.</param>
    /// <returns>The account.</returns>
    protected static async Task<IdentityUser> FindUserAsync(UserManager<IdentityUser> userManager, CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(context);

        return await userManager.FindByIdAsync(context.PlayerId).ConfigureAwait(false)
            ?? throw new GameException(GameErrorCodes.NotAuthenticated, "The account does not exist.");
    }
}

/// <summary>
/// Sends the confirmation e-mail again, from the banner of an unconfirmed account.
/// </summary>
public sealed class ResendConfirmationCommand : AccountCommand<ResendConfirmationCommand.Request, ResendConfirmationCommand.Response>
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ConfirmationMailer _mailer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResendConfirmationCommand"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="mailer">The confirmation mailer.</param>
    public ResendConfirmationCommand(UserManager<IdentityUser> userManager, ConfirmationMailer mailer)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(mailer);

        _userManager = userManager;
        _mailer = mailer;
    }

    /// <inheritdoc />
    public override string Route => AccountActions.Prefix + "/resend-confirmation";

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        IdentityUser user = await FindUserAsync(_userManager, context).ConfigureAwait(false);

        if (user.EmailConfirmed)
        {
            return new Response("already_confirmed");
        }

        TimeSpan? retryAfter = await _mailer.SendAsync(user, cancellationToken).ConfigureAwait(false);

        if (retryAfter is { } wait)
        {
            throw new GameException(AccountActions.ConfirmationTooSoon, "The last e-mail was sent too recently.", new Dictionary<string, object?>
            {
                ["retry_after_seconds"] = (long)Math.Ceiling(wait.TotalSeconds),
            });
        }

        return new Response("sent");
    }

    /// <summary>The request: the intent only.</summary>
    public sealed record Request;

    /// <summary>The outcome.</summary>
    /// <param name="Status"><c>sent</c> or <c>already_confirmed</c>.</param>
    public sealed record Response([property: JsonPropertyName("status")] string Status);
}

/// <summary>
/// Changes the password; the current one is needed. Every other session ends, the current
/// one goes on with a new token.
/// </summary>
public sealed class ChangePasswordCommand : AccountCommand<ChangePasswordCommand.Request, ChangePasswordCommand.Response>
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RefreshTokenService _refreshTokens;
    private readonly SessionIssuer _sessions;
    private readonly SecuritySettings _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangePasswordCommand"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="refreshTokens">The refresh tokens.</param>
    /// <param name="sessions">The session issuer.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="httpContextAccessor">The current request.</param>
    public ChangePasswordCommand(
        UserManager<IdentityUser> userManager,
        RefreshTokenService refreshTokens,
        SessionIssuer sessions,
        SecuritySettings settings,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(refreshTokens);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        _userManager = userManager;
        _refreshTokens = refreshTokens;
        _sessions = sessions;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override string Route => AccountActions.Prefix + "/change-password";

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.CurrentPassword).AccountPassword(_settings);
        rules.RuleFor(request => request.NewPassword).AccountPassword(_settings);
        rules.RuleFor(request => request.NewPasswordConfirmation).NotEmpty();
    }

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityUser user = await FindUserAsync(_userManager, context).ConfigureAwait(false);

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword!).ConfigureAwait(false))
        {
            throw Invalid("current_password", [AccountActions.WrongPassword]);
        }

        if (!string.Equals(request.NewPassword, request.NewPasswordConfirmation, StringComparison.Ordinal))
        {
            throw Invalid("new_password_confirmation", [RegistrationService.PasswordMismatch]);
        }

        IdentityResult changed = await _userManager.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword!).ConfigureAwait(false);

        if (!changed.Succeeded)
        {
            throw Invalid("new_password", changed.Errors.Select(error => error.Code));
        }

        HttpContext httpContext = _httpContextAccessor.HttpContext!;
        bool persistent = httpContext.Request.Cookies.TryGetValue(AuthCookies.Refresh, out string? token)
            && token is not null
            && await _refreshTokens.IsPersistentAsync(token, cancellationToken).ConfigureAwait(false);

        // The password change renewed the security stamp, so the other session cookies stop
        // working; their refresh tokens are revoked here.
        await _refreshTokens.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _sessions.StartAsync(httpContext, user, persistent).ConfigureAwait(false);

        return new Response("changed");
    }

    /// <summary>The request.</summary>
    /// <param name="CurrentPassword">The current password.</param>
    /// <param name="NewPassword">The new password.</param>
    /// <param name="NewPasswordConfirmation">The new password, repeated.</param>
    public sealed record Request(
        [property: JsonPropertyName("current_password")] string? CurrentPassword,
        [property: JsonPropertyName("new_password")] string? NewPassword,
        [property: JsonPropertyName("new_password_confirmation")] string? NewPasswordConfirmation);

    /// <summary>The outcome.</summary>
    /// <param name="Status">Always <c>changed</c>.</param>
    public sealed record Response([property: JsonPropertyName("status")] string Status);
}

/// <summary>
/// Changes the nickname, not more often than <c>security.account.nickname_change_days</c>.
/// </summary>
/// <remarks>The first change after the registration is allowed at once.</remarks>
public sealed class ChangeNicknameCommand : AccountCommand<ChangeNicknameCommand.Request, ChangeNicknameCommand.Response>
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly NicknameRules _rules;
    private readonly AetherisDbContext _dbContext;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeNicknameCommand"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="rules">The nickname rules.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    public ChangeNicknameCommand(
        UserManager<IdentityUser> userManager,
        NicknameRules rules,
        AetherisDbContext dbContext,
        SecuritySettings settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _userManager = userManager;
        _rules = rules;
        _dbContext = dbContext;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public override string Route => AccountActions.Prefix + "/change-nickname";

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.Nickname).NotEmpty();
    }

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityUser user = await FindUserAsync(_userManager, context).ConfigureAwait(false);
        string nickname = NicknameRules.Normalize(request.Nickname);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        PlayerProfileEntity? profile = await _dbContext.PlayerProfiles
            .SingleOrDefaultAsync(entry => entry.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            profile = new PlayerProfileEntity { UserId = user.Id, RegisteredAt = now };
            _dbContext.PlayerProfiles.Add(profile);
        }

        if (profile.NicknameChangedAt is { } changedAt && changedAt + _settings.NicknameChangeInterval > now)
        {
            throw new GameException(AccountActions.NicknameChangeTooSoon, "The nickname was changed too recently.", new Dictionary<string, object?>
            {
                ["next_change_at"] = changedAt + _settings.NicknameChangeInterval,
            });
        }

        List<string> errors = await _rules.ValidateAsync(nickname, user.Id, cancellationToken).ConfigureAwait(false);

        if (errors.Count > 0)
        {
            throw Invalid("nickname", errors);
        }

        // The name and its normalized form are written without a new security stamp, so the
        // sessions of the player go on.
        user.UserName = nickname;
        await _userManager.UpdateNormalizedUserNameAsync(user).ConfigureAwait(false);
        profile.NicknameChangedAt = now;

        return new Response(nickname, now + _settings.NicknameChangeInterval);
    }

    /// <summary>The request.</summary>
    /// <param name="Nickname">The new nickname.</param>
    public sealed record Request([property: JsonPropertyName("nickname")] string? Nickname);

    /// <summary>The outcome.</summary>
    /// <param name="Nickname">The stored nickname.</param>
    /// <param name="NextChangeAt">The earliest moment of the next change.</param>
    public sealed record Response(
        [property: JsonPropertyName("nickname")] string Nickname,
        [property: JsonPropertyName("next_change_at")] DateTimeOffset NextChangeAt);
}

/// <summary>
/// Ends every session of the account, the current one included.
/// </summary>
public sealed class SignOutEverywhereCommand : AccountCommand<SignOutEverywhereCommand.Request, SignOutEverywhereCommand.Response>
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RefreshTokenService _refreshTokens;
    private readonly SessionIssuer _sessions;
    private readonly SecurityEventLog _securityLog;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignOutEverywhereCommand"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="refreshTokens">The refresh tokens.</param>
    /// <param name="sessions">The session issuer.</param>
    /// <param name="securityLog">The security journal.</param>
    /// <param name="httpContextAccessor">The current request.</param>
    public SignOutEverywhereCommand(
        UserManager<IdentityUser> userManager,
        RefreshTokenService refreshTokens,
        SessionIssuer sessions,
        SecurityEventLog securityLog,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(refreshTokens);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(securityLog);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        _userManager = userManager;
        _refreshTokens = refreshTokens;
        _sessions = sessions;
        _securityLog = securityLog;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override string Route => AccountActions.Prefix + "/sign-out-everywhere";

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        IdentityUser user = await FindUserAsync(_userManager, context).ConfigureAwait(false);
        HttpContext httpContext = _httpContextAccessor.HttpContext!;

        // A new security stamp stops the session cookies of the other devices at their next
        // request; the revoked refresh tokens cannot start new ones.
        await _refreshTokens.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await _sessions.EndAsync(httpContext).ConfigureAwait(false);
        await _securityLog.WriteAsync(SecurityEventTypes.SignedOut, httpContext, user.Id, "everywhere").ConfigureAwait(false);

        return new Response("signed_out");
    }

    /// <summary>The request: the intent only.</summary>
    public sealed record Request;

    /// <summary>The outcome.</summary>
    /// <param name="Status">Always <c>signed_out</c>.</param>
    public sealed record Response([property: JsonPropertyName("status")] string Status);
}

/// <summary>
/// Requests the deletion of the account (GDPR): the password is needed, the account is
/// deleted after <c>security.account.deletion_days</c>, and a sign-in before that cancels
/// the deletion. Every session ends.
/// </summary>
public sealed class DeleteAccountCommand : AccountCommand<DeleteAccountCommand.Request, DeleteAccountCommand.Response>
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AccountLifecycle _lifecycle;
    private readonly RefreshTokenService _refreshTokens;
    private readonly SessionIssuer _sessions;
    private readonly SecuritySettings _settings;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAccountCommand"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="lifecycle">The account lifecycle.</param>
    /// <param name="refreshTokens">The refresh tokens.</param>
    /// <param name="sessions">The session issuer.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="httpContextAccessor">The current request.</param>
    public DeleteAccountCommand(
        UserManager<IdentityUser> userManager,
        AccountLifecycle lifecycle,
        RefreshTokenService refreshTokens,
        SessionIssuer sessions,
        SecuritySettings settings,
        IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(refreshTokens);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        _userManager = userManager;
        _lifecycle = lifecycle;
        _refreshTokens = refreshTokens;
        _sessions = sessions;
        _settings = settings;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override string Route => AccountActions.Prefix + "/delete";

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.Password).AccountPassword(_settings);
    }

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IdentityUser user = await FindUserAsync(_userManager, context).ConfigureAwait(false);

        if (!await _userManager.CheckPasswordAsync(user, request.Password!).ConfigureAwait(false))
        {
            throw Invalid("password", [AccountActions.WrongPassword]);
        }

        DateTimeOffset dueAt = await _lifecycle.RequestDeletionAsync(user.Id, cancellationToken).ConfigureAwait(false);

        await _refreshTokens.RevokeAllAsync(user.Id, cancellationToken).ConfigureAwait(false);
        await _userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await _sessions.EndAsync(_httpContextAccessor.HttpContext!).ConfigureAwait(false);

        return new Response(dueAt);
    }

    /// <summary>The request.</summary>
    /// <param name="Password">The password of the account.</param>
    public sealed record Request([property: JsonPropertyName("password")] string? Password);

    /// <summary>The outcome.</summary>
    /// <param name="DeletionDueAt">The moment the account will be deleted.</param>
    public sealed record Response([property: JsonPropertyName("deletion_due_at")] DateTimeOffset DeletionDueAt);
}

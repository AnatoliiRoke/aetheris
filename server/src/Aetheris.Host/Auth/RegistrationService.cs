using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aetheris.Host.Auth;

/// <summary>
/// Checks the answer of a captcha.
/// </summary>
/// <remarks>
/// The captcha is asked only on suspicion and only when <c>security.registration.captcha_enabled</c>
/// is on (task 00006, decision 8). A provider is connected by a later task; until then
/// <see cref="UnconfiguredCaptchaVerifier"/> refuses every answer, so switching the captcha on
/// stops the suspicious registrations.
/// </remarks>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Checks an answer.
    /// </summary>
    /// <param name="token">The answer sent by the client.</param>
    /// <param name="ipAddress">The remote IP address.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the answer is right.</returns>
    Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken cancellationToken);
}

/// <summary>
/// The captcha check without a provider: no answer passes.
/// </summary>
public sealed class UnconfiguredCaptchaVerifier : ICaptchaVerifier
{
    /// <inheritdoc />
    public Task<bool> VerifyAsync(string? token, string? ipAddress, CancellationToken cancellationToken) => Task.FromResult(false);
}

/// <summary>
/// The outcome of a registration.
/// </summary>
public enum RegistrationOutcome
{
    /// <summary>The registration was accepted, or the address was already registered.</summary>
    Accepted,

    /// <summary>A field did not pass its rules.</summary>
    Invalid,

    /// <summary>Too many registrations came from the address.</summary>
    LimitReached,

    /// <summary>The captcha is needed and its answer is missing or wrong.</summary>
    CaptchaRequired,
}

/// <summary>
/// The result of a registration.
/// </summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Errors">The error codes by field, for <see cref="RegistrationOutcome.Invalid"/>.</param>
/// <param name="UserId">The new account, when one was created.</param>
public sealed record RegistrationResult(
    RegistrationOutcome Outcome,
    IReadOnlyDictionary<string, string[]> Errors,
    string? UserId = null);

/// <summary>
/// Registers a new account (task 00006, decisions 1, 2, 6, 7 and 8).
/// </summary>
/// <remarks>
/// The answer for a registered address is the same as for a new one. The account, its
/// profile, the journal entry of the registration and the event that removes the account
/// when its address stays unconfirmed are written in one transaction.
/// </remarks>
public sealed class RegistrationService
{
    /// <summary>The scheduled event that removes an account whose address was not confirmed in time.</summary>
    public const string UnconfirmedExpiryEventType = "account.unconfirmed_expiry";

    /// <summary>The error code of a repeated password that differs from the password.</summary>
    public const string PasswordMismatch = "password_mismatch";

    /// <summary>The error code of an address of a temporary mailbox.</summary>
    public const string EmailDomainBlocked = "email_domain_blocked";

    /// <summary>The sector of the account events, which belong to no sector of the map.</summary>
    public const int AccountEventSector = 0;

    private readonly AetherisDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly NicknameRules _nicknameRules;
    private readonly ConfirmationMailer _mailer;
    private readonly ICaptchaVerifier _captcha;
    private readonly IEventScheduler _scheduler;
    private readonly IGameClock _clock;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="userManager">The account manager.</param>
    /// <param name="nicknameRules">The nickname rules.</param>
    /// <param name="mailer">The confirmation mailer.</param>
    /// <param name="captcha">The captcha check.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock of the real time.</param>
    public RegistrationService(
        AetherisDbContext dbContext,
        UserManager<IdentityUser> userManager,
        NicknameRules nicknameRules,
        ConfirmationMailer mailer,
        ICaptchaVerifier captcha,
        IEventScheduler scheduler,
        IGameClock clock,
        SecuritySettings settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(nicknameRules);
        ArgumentNullException.ThrowIfNull(mailer);
        ArgumentNullException.ThrowIfNull(captcha);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _userManager = userManager;
        _nicknameRules = nicknameRules;
        _mailer = mailer;
        _captcha = captcha;
        _scheduler = scheduler;
        _clock = clock;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Registers an account.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="ipAddress">The remote IP address.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The result.</returns>
    public async Task<RegistrationResult> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        int recentRegistrations = await CountRecentRegistrationsAsync(ipAddress, now, cancellationToken).ConfigureAwait(false);

        if (recentRegistrations >= _settings.RegistrationIpLimit)
        {
            return new RegistrationResult(RegistrationOutcome.LimitReached, Empty);
        }

        if (_settings.CaptchaEnabled
            && recentRegistrations >= _settings.CaptchaAfterRegistrations
            && !await _captcha.VerifyAsync(request.CaptchaToken, ipAddress, cancellationToken).ConfigureAwait(false))
        {
            return new RegistrationResult(RegistrationOutcome.CaptchaRequired, Empty);
        }

        string email = request.Email!.Trim();
        string nickname = NicknameRules.Normalize(request.Nickname);
        IdentityUser user = new() { UserName = nickname, Email = email };

        Dictionary<string, string[]> errors = await ValidateAsync(request, user, cancellationToken).ConfigureAwait(false);

        if (errors.Count > 0)
        {
            return new RegistrationResult(RegistrationOutcome.Invalid, errors);
        }

        if (await _userManager.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            return new RegistrationResult(RegistrationOutcome.Accepted, Empty);
        }

        await using (IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            IdentityResult created = await _userManager.CreateAsync(user, request.Password!).ConfigureAwait(false);

            if (!created.Succeeded)
            {
                // A parallel registration took the nickname between the check and the insert.
                return new RegistrationResult(RegistrationOutcome.Invalid, new Dictionary<string, string[]>
                {
                    [nameof(RegisterRequest.Nickname)] = [NicknameRules.Taken],
                });
            }

            _dbContext.PlayerProfiles.Add(new PlayerProfileEntity { UserId = user.Id, RegisteredAt = now });
            _dbContext.AccountAccessLog.Add(new AccountAccessLogEntity
            {
                UserId = user.Id,
                Kind = AccountAccessKinds.Registration,
                IpAddress = ipAddress,
                At = now,
            });

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _scheduler.ScheduleAsync(
                new ScheduledEventRequest(
                    AccountEventSector,
                    UnconfirmedExpiryEventType,
                    _clock.GameNow + _settings.UnconfirmedLifetime,
                    JsonSerializer.Serialize(new AccountEventPayload(user.Id)),
                    $"{UnconfirmedExpiryEventType}:{user.Id}"),
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        await _mailer.SendAsync(user, cancellationToken).ConfigureAwait(false);

        return new RegistrationResult(RegistrationOutcome.Accepted, Empty, user.Id);
    }

    private static IReadOnlyDictionary<string, string[]> Empty { get; } = new Dictionary<string, string[]>();

    private async Task<Dictionary<string, string[]>> ValidateAsync(RegisterRequest request, IdentityUser user, CancellationToken cancellationToken)
    {
        Dictionary<string, string[]> errors = [];

        // The repeated password is compared by the client too; the server checks it on its own.
        if (!string.Equals(request.Password, request.PasswordConfirmation, StringComparison.Ordinal))
        {
            errors[nameof(RegisterRequest.PasswordConfirmation)] = [PasswordMismatch];
        }

        List<string> passwordErrors = [];

        foreach (IPasswordValidator<IdentityUser> validator in _userManager.PasswordValidators)
        {
            IdentityResult result = await validator.ValidateAsync(_userManager, user, request.Password).ConfigureAwait(false);
            passwordErrors.AddRange(result.Errors.Select(error => error.Code));
        }

        if (passwordErrors.Count > 0)
        {
            errors[nameof(RegisterRequest.Password)] = [.. passwordErrors.Distinct(StringComparer.Ordinal)];
        }

        List<string> nicknameErrors = await _nicknameRules.ValidateAsync(user.UserName!, null, cancellationToken).ConfigureAwait(false);

        if (nicknameErrors.Count > 0)
        {
            errors[nameof(RegisterRequest.Nickname)] = [.. nicknameErrors];
        }

        string domain = user.Email![(user.Email!.LastIndexOf('@') + 1)..].ToLowerInvariant();

        if (await _dbContext.BlockedEmailDomains.AnyAsync(entry => entry.Domain == domain, cancellationToken).ConfigureAwait(false))
        {
            errors[nameof(RegisterRequest.Email)] = [EmailDomainBlocked];
        }

        return errors;
    }

    private Task<int> CountRecentRegistrationsAsync(string? ipAddress, DateTimeOffset now, CancellationToken cancellationToken)
    {
        DateTimeOffset windowStart = now - _settings.RegistrationIpWindow;

        return _dbContext.AccountAccessLog.CountAsync(
            entry => entry.Kind == AccountAccessKinds.Registration && entry.IpAddress == ipAddress && entry.At >= windowStart,
            cancellationToken);
    }
}

/// <summary>
/// The payload of an account event.
/// </summary>
/// <param name="UserId">The account.</param>
public sealed record AccountEventPayload([property: System.Text.Json.Serialization.JsonPropertyName("user_id")] string UserId);

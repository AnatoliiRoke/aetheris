using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Auth;

/// <summary>
/// Sends the e-mail confirmation link, not more often than
/// <c>security.account.confirmation_resend_minutes</c> (task 00006, decision 2).
/// </summary>
public sealed class ConfirmationMailer
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AetherisDbContext _dbContext;
    private readonly IAccountEmailSender _emailSender;
    private readonly SecuritySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationMailer"/> class.
    /// </summary>
    /// <param name="userManager">The account manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="emailSender">The e-mail sender.</param>
    /// <param name="settings">The security settings.</param>
    /// <param name="timeProvider">The clock.</param>
    public ConfirmationMailer(
        UserManager<IdentityUser> userManager,
        AetherisDbContext dbContext,
        IAccountEmailSender emailSender,
        SecuritySettings settings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(emailSender);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _userManager = userManager;
        _dbContext = dbContext;
        _emailSender = emailSender;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Sends the link unless the last one was sent too recently.
    /// </summary>
    /// <param name="user">The account; its address must not be confirmed yet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="null"/> when the e-mail was sent, otherwise the time left before the next one.</returns>
    public async Task<TimeSpan?> SendAsync(IdentityUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        PlayerProfileEntity? profile = await _dbContext.PlayerProfiles
            .SingleOrDefaultAsync(entry => entry.UserId == user.Id, cancellationToken)
            .ConfigureAwait(false);

        if (profile?.ConfirmationSentAt is { } sentAt && sentAt + _settings.ConfirmationResendInterval > now)
        {
            return sentAt + _settings.ConfirmationResendInterval - now;
        }

        string token = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);
        await _emailSender.SendAsync(
            new AccountEmail(user.Id, user.Email!, AccountEmailKinds.Confirmation, token),
            cancellationToken).ConfigureAwait(false);

        if (profile is not null)
        {
            profile.ConfirmationSentAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return null;
    }
}

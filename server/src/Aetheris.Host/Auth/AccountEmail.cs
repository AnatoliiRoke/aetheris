namespace Aetheris.Host.Auth;

/// <summary>
/// The kinds of account e-mails.
/// </summary>
public static class AccountEmailKinds
{
    /// <summary>The confirmation of a new e-mail address.</summary>
    public const string Confirmation = "email_confirmation";

    /// <summary>The link of a password reset.</summary>
    public const string PasswordReset = "password_reset";
}

/// <summary>
/// An e-mail of the account flows.
/// </summary>
/// <param name="UserId">The identifier of the account.</param>
/// <param name="Address">The e-mail address.</param>
/// <param name="Kind">The kind of the e-mail, one of <see cref="AccountEmailKinds"/>.</param>
/// <param name="Token">The confirmation or reset token the e-mail carries.</param>
public sealed record AccountEmail(string UserId, string Address, string Kind, string Token);

/// <summary>
/// Sends the e-mails of the account flows.
/// </summary>
public interface IAccountEmailSender
{
    /// <summary>
    /// Sends one e-mail.
    /// </summary>
    /// <param name="email">The e-mail.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the e-mail is handed over.</returns>
    Task SendAsync(AccountEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// The placeholder sender until the mail delivery task: it only logs that an e-mail would
/// be sent.
/// </summary>
/// <remarks>
/// The log entry names the account by its identifier. It never contains the address or the
/// token (task 00002, section 2.7).
/// </remarks>
public sealed class LoggingAccountEmailSender : IAccountEmailSender
{
    private readonly ILogger<LoggingAccountEmailSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingAccountEmailSender"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public LoggingAccountEmailSender(ILogger<LoggingAccountEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendAsync(AccountEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        _logger.LogInformation(
            "Account message {MessageKind} for account {UserId} is not delivered: no mail service is configured yet.",
            email.Kind,
            email.UserId);

        return Task.CompletedTask;
    }
}

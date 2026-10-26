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

/// <summary>
/// The sender of the development environment: it writes every e-mail with its link into a
/// file of a local mailbox folder, so the account flows can be tried by hand
/// (task 00006, decision 12).
/// </summary>
/// <remarks>
/// The log entry names only the kind, the account and the file; the address and the token
/// stay in the file outside the repository and never reach the log (task 00002,
/// section 2.7). The folder is <c>Mail:DevelopmentMailbox</c>, by default
/// <c>aetheris-mail</c> in the temporary folder of the system. The links point to the first
/// client origin, or to <c>Mail:LinkBaseUrl</c>.
/// </remarks>
public sealed class DevelopmentMailboxEmailSender : IAccountEmailSender
{
    /// <summary>The configuration key of the mailbox folder.</summary>
    public const string MailboxKey = "Mail:DevelopmentMailbox";

    /// <summary>The configuration key of the address the links point to.</summary>
    public const string LinkBaseUrlKey = "Mail:LinkBaseUrl";

    private readonly string _mailbox;
    private readonly string _linkBaseUrl;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DevelopmentMailboxEmailSender> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevelopmentMailboxEmailSender"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="origins">The client origins.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DevelopmentMailboxEmailSender(
        IConfiguration configuration,
        Security.ClientOrigins origins,
        TimeProvider timeProvider,
        ILogger<DevelopmentMailboxEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _mailbox = configuration[MailboxKey] ?? Path.Combine(Path.GetTempPath(), "aetheris-mail");
        _linkBaseUrl = (configuration[LinkBaseUrlKey] ?? origins.Origins.FirstOrDefault() ?? string.Empty).TrimEnd('/');
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(AccountEmail email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        string page = email.Kind == AccountEmailKinds.PasswordReset ? "reset-password" : "confirm-email";
        string link = $"{_linkBaseUrl}/{page}?userId={Uri.EscapeDataString(email.UserId)}&token={Uri.EscapeDataString(email.Token)}";
        string fileName = $"{_timeProvider.GetUtcNow():yyyyMMdd-HHmmssfff}-{email.Kind}-{Guid.NewGuid():N}.txt";

        Directory.CreateDirectory(_mailbox);
        await File.WriteAllTextAsync(
            Path.Combine(_mailbox, fileName),
            $"To: {email.Address}{Environment.NewLine}Kind: {email.Kind}{Environment.NewLine}Link: {link}{Environment.NewLine}",
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Account message {MessageKind} for account {UserId} written to the development mailbox as {MailFile}.",
            email.Kind,
            email.UserId,
            fileName);
    }
}

using Aetheris.Mail.Services;

namespace Aetheris.Mail;

/// <summary>
/// The implementation of <see cref="IMailModule"/>.
/// </summary>
/// <remarks>
/// The module keeps the letters in its own tables, so it works on the context of the request
/// (task 00060).
/// </remarks>
public sealed class MailModule : IMailModule
{
    private readonly MailSendService _send;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailModule"/> class.
    /// </summary>
    /// <param name="send">The letters that the players and the game write.</param>
    public MailModule(MailSendService send, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _send = send;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public string ModuleName => "Mail";

    /// <inheritdoc />
    public Task<long?> SendSystemAsync(
        string playerId,
        string categoryCode,
        string textKey,
        string? parameters,
        string? systemKindCode = null,
        CancellationToken cancellationToken = default) =>
        _unitOfWork.RunAsync(() => _send.SendSystemLetterAsync(playerId, categoryCode, textKey, parameters, systemKindCode, cancellationToken), cancellationToken);
}

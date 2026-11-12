using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Mail.Api;
using Aetheris.Mail.Services;
using FluentValidation;

namespace Aetheris.Mail.Api;

/// <summary>
/// Writes a letter of a player to another player (task 00060).
/// </summary>
public sealed class SendMailCommand : Command<SendMailCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/letters";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.letter_send";

    private readonly MailSendService _send;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendMailCommand"/> class.
    /// </summary>
    /// <param name="send">The letters of the players.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public SendMailCommand(MailSendService send, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _send = send;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.ReceiverNickname).NotNull().NotEmpty();
        rules.RuleFor(request => request.Subject).NotNull();
        rules.RuleFor(request => request.Body).NotNull();
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long messageId = await _unitOfWork.RunAsync(
            () => _send.SendPlayerLetterAsync(
                context.PlayerId,
                request.ReceiverNickname!,
                request.Subject ?? string.Empty,
                request.Body ?? string.Empty,
                cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { id = messageId }, MailJson.Options);
    }

    /// <summary>
    /// The request: the receiver, the subject and the text of the letter.
    /// </summary>
    /// <param name="ReceiverNickname">The nickname of the player who receives the letter.</param>
    /// <param name="Subject">The subject of the letter.</param>
    /// <param name="Body">The text of the letter.</param>
    public sealed record Request(
        [property: JsonPropertyName("receiver_nickname")] string? ReceiverNickname,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("body")] string? Body);
}

/// <summary>
/// Writes a draft of a letter or changes the one that is named (task 00060).
/// </summary>
public sealed class SaveMailDraftCommand : Command<SaveMailDraftCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/drafts";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.draft_save";

    private readonly MailSendService _send;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveMailDraftCommand"/> class.
    /// </summary>
    /// <param name="send">The letters of the players.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public SaveMailDraftCommand(MailSendService send, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _send = send;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.DraftId).NotNull().GreaterThanOrEqualTo(0);
        rules.RuleFor(request => request.Subject).NotNull();
        rules.RuleFor(request => request.Body).NotNull();
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        MailDraftDto draft = await _unitOfWork.RunAsync(
            () => _send.SaveDraftAsync(
                context.PlayerId,
                request.DraftId!.Value,
                request.ReceiverNickname,
                request.Subject ?? string.Empty,
                request.Body ?? string.Empty,
                cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(draft, MailJson.Options);
    }

    /// <summary>
    /// The request: the draft to change and the text of the letter.
    /// </summary>
    /// <param name="DraftId">The draft to change, or zero for a new draft.</param>
    /// <param name="ReceiverNickname">The nickname of the receiver, or <see langword="null"/>.</param>
    /// <param name="Subject">The subject of the draft.</param>
    /// <param name="Body">The text of the draft.</param>
    public sealed record Request(
        [property: JsonPropertyName("draft_id")] long? DraftId,
        [property: JsonPropertyName("receiver_nickname")] string? ReceiverNickname,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("body")] string? Body);
}

/// <summary>
/// Removes a draft of a letter (task 00060).
/// </summary>
public sealed class RemoveMailDraftCommand : Command<RemoveMailDraftCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/drafts/remove";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.draft_remove";

    private readonly MailSendService _send;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveMailDraftCommand"/> class.
    /// </summary>
    /// <param name="send">The letters of the players.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public RemoveMailDraftCommand(MailSendService send, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _send = send;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.DraftId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool removed = await _unitOfWork.RunAsync(
            () => _send.RemoveDraftAsync(context.PlayerId, request.DraftId!.Value, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { removed }, MailJson.Options);
    }

    /// <summary>
    /// The request: the draft to remove.
    /// </summary>
    /// <param name="DraftId">The draft.</param>
    public sealed record Request([property: JsonPropertyName("draft_id")] long? DraftId);
}

/// <summary>
/// Marks every letter of a folder of the player as read (task 00060).
/// </summary>
public sealed class ReadAllMailCommand : Command<ReadAllMailCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/read-all";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.read_all";

    private readonly MailService _mail;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReadAllMailCommand"/> class.
    /// </summary>
    /// <param name="mail">The mail of the player.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public ReadAllMailCommand(MailService mail, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _mail = mail;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FolderCode).NotNull();
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int marked = await _unitOfWork.RunAsync(
            () => _mail.MarkAllReadAsync(context.PlayerId, request.FolderCode, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { marked }, MailJson.Options);
    }

    /// <summary>
    /// The request: the folder whose letters are marked.
    /// </summary>
    /// <param name="FolderCode">The folder, or an empty text for every folder.</param>
    public sealed record Request([property: JsonPropertyName("folder_code")] string? FolderCode);
}

/// <summary>
/// Saves a letter of the player or takes it back from the saved ones (task 00060).
/// </summary>
public sealed class ToggleMailSavedCommand : Command<ToggleMailSavedCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/letters/save";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.letter_save";

    private readonly MailService _mail;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToggleMailSavedCommand"/> class.
    /// </summary>
    /// <param name="mail">The mail of the player.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public ToggleMailSavedCommand(MailService mail, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _mail = mail;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.MessageId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool? saved = await _unitOfWork.RunAsync(
            () => _mail.ToggleSavedAsync(context.PlayerId, request.MessageId!.Value, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { saved }, MailJson.Options);
    }

    /// <summary>
    /// The request: the letter to save.
    /// </summary>
    /// <param name="MessageId">The letter.</param>
    public sealed record Request([property: JsonPropertyName("message_id")] long? MessageId);
}

/// <summary>
/// Moves a letter of the player to the bin or takes it back from the bin (task 00060).
/// </summary>
public sealed class ToggleMailDeletedCommand : Command<ToggleMailDeletedCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/letters/delete";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.letter_delete";

    private readonly MailService _mail;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToggleMailDeletedCommand"/> class.
    /// </summary>
    /// <param name="mail">The mail of the player.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public ToggleMailDeletedCommand(MailService mail, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _mail = mail;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.MessageId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool? deleted = await _unitOfWork.RunAsync(
            () => _mail.ToggleDeletedAsync(context.PlayerId, request.MessageId!.Value, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { deleted }, MailJson.Options);
    }

    /// <summary>
    /// The request: the letter to move to the bin.
    /// </summary>
    /// <param name="MessageId">The letter.</param>
    public sealed record Request([property: JsonPropertyName("message_id")] long? MessageId);
}

/// <summary>
/// Empties the bin of the player (task 00060).
/// </summary>
public sealed class EmptyMailTrashCommand : Command<EmptyMailTrashCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/trash/empty";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.trash_empty";

    private readonly MailService _mail;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmptyMailTrashCommand"/> class.
    /// </summary>
    /// <param name="mail">The mail of the player.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public EmptyMailTrashCommand(MailService mail, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(mail);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _mail = mail;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        int removed = await _unitOfWork.RunAsync(
            () => _mail.EmptyTrashAsync(context.PlayerId, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { removed }, MailJson.Options);
    }

    /// <summary>
    /// The request: the command takes nothing.
    /// </summary>
    public sealed record Request;
}

/// <summary>
/// Complains about a letter of another player (task 00060).
/// </summary>
public sealed class ComplainAboutMailCommand : Command<ComplainAboutMailCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/mail/complaints";

    /// <summary>The action code of the command.</summary>
    public const string Code = "mail.complaint_create";

    private readonly MailSendService _send;
    private readonly MailUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComplainAboutMailCommand"/> class.
    /// </summary>
    /// <param name="send">The letters of the players.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    public ComplainAboutMailCommand(MailSendService send, MailUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _send = send;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.MessageId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Reason).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long complaintId = await _unitOfWork.RunAsync(
            () => _send.ComplainAsync(
                context.PlayerId,
                request.MessageId!.Value,
                request.Reason ?? string.Empty,
                cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { id = complaintId }, MailJson.Options);
    }

    /// <summary>
    /// The request: the letter and the reason of the complaint.
    /// </summary>
    /// <param name="MessageId">The letter.</param>
    /// <param name="Reason">The reason of the complaint.</param>
    public sealed record Request(
        [property: JsonPropertyName("message_id")] long? MessageId,
        [property: JsonPropertyName("reason")] string? Reason);
}

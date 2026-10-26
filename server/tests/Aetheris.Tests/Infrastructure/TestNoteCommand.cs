using Aetheris.Core.Commands;
using Aetheris.Host.Commands;
using FluentValidation;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// The example of a new protected request, added by one class and one registration line
/// (task 00002, acceptance criterion 10).
/// </summary>
/// <remarks>
/// The command writes a note on a player object. It declares only what is particular to
/// it: the route, the validation rule and the target object. Authentication, the rate
/// limit, the size limit, the ownership check, the request identifier, the transaction and
/// the change journal come from the command pipeline.
/// </remarks>
public sealed class TestNoteCommand : Command<TestNoteCommand.Request, TestNoteCommand.Response>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/test/note";

    /// <summary>
    /// The largest length of a note.
    /// </summary>
    public const int MaxTextLength = 32;

    private readonly IChangeJournal _journal;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestNoteCommand"/> class.
    /// </summary>
    /// <param name="journal">The change journal.</param>
    public TestNoteCommand(IChangeJournal journal)
    {
        _journal = journal;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => "test.note";

    /// <inheritdoc />
    public override bool RequiresOnboarding => false;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        rules.RuleFor(request => request.PlayerId).NotEmpty();
        rules.RuleFor(request => request.Text).NotEmpty().MaximumLength(MaxTextLength);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) =>
        new(PlayerObjectResolver.Type, request.PlayerId!);

    /// <inheritdoc />
    public override Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        _journal.Record(new ChangeRecord(
            PlayerObjectResolver.Type,
            request.PlayerId!,
            "note",
            null,
            request.Text,
            nameof(TestNoteCommand)));

        return Task.FromResult(new Response(request.Text!));
    }

    /// <summary>
    /// The request of the command.
    /// </summary>
    /// <param name="PlayerId">The player object the note is written on.</param>
    /// <param name="Text">The note.</param>
    public sealed record Request(string? PlayerId, string? Text);

    /// <summary>
    /// The response of the command.
    /// </summary>
    /// <param name="Text">The stored note.</param>
    public sealed record Response(string Text);
}

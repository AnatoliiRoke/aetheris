using Aetheris.Core.Commands;
using Aetheris.Host.Security;
using FluentValidation;

namespace Aetheris.Host.Commands;

/// <summary>
/// The neutral example command: returns the message it got and the number of the execution.
/// </summary>
/// <remarks>
/// It shows the whole command pipeline without any game rule: a signed-in player, the
/// command rate limit, a validation rule that reads its limit from the configuration, and
/// the request identifier. A repeated request with the same identifier returns the same
/// execution number, because the command did not run again.
/// </remarks>
public sealed class EchoCommand : Command<EchoCommand.Request, EchoCommand.Response>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/test/echo";

    private static long _executions;

    private readonly SecuritySettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="EchoCommand"/> class.
    /// </summary>
    /// <param name="settings">The settings that hold the message length limit.</param>
    public EchoCommand(SecuritySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => "test.echo";

    /// <inheritdoc />
    /// <remarks>The neutral example of the core needs no finished onboarding.</remarks>
    public override bool RequiresOnboarding => false;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.Message)
            .NotEmpty()
            .MaximumLength(_settings.EchoMaxLength);
    }

    /// <inheritdoc />
    public override Task<Response> ExecuteAsync(
        Request request,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        long execution = Interlocked.Increment(ref _executions);

        return Task.FromResult(new Response(request.Message ?? string.Empty, execution));
    }

    /// <summary>
    /// The request of the echo command.
    /// </summary>
    /// <param name="Message">The message to echo.</param>
    public sealed record Request(string? Message);

    /// <summary>
    /// The response of the echo command.
    /// </summary>
    /// <param name="Message">The echoed message.</param>
    /// <param name="Execution">The number of the execution, increasing with every real execution.</param>
    public sealed record Response(string Message, long Execution);
}

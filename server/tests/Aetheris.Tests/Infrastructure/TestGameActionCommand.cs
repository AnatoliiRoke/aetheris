using Aetheris.Core.Commands;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// A neutral stand-in for a game action, such as a construction or a flight, that the
/// account gate must stop (task 00006, decisions 2 and 9).
/// </summary>
/// <remarks>
/// It keeps the defaults of a game command: it needs a confirmed e-mail address unless its
/// action code is listed in the database, and it needs the finished onboarding.
/// </remarks>
public sealed class TestGameActionCommand : Command<TestGameActionCommand.Request, TestGameActionCommand.Response>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/test/game-action";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "test.game_action";

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override Task<Response> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new Response("done"));

    /// <summary>
    /// The request of the command.
    /// </summary>
    public sealed record Request;

    /// <summary>
    /// The response of the command.
    /// </summary>
    /// <param name="Status">The outcome.</param>
    public sealed record Response(string Status);
}

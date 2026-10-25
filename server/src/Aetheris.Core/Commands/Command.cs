using Aetheris.Core.Security;
using FluentValidation;

namespace Aetheris.Core.Commands;

/// <summary>
/// The part of a command that the host needs to publish it as an endpoint.
/// </summary>
public interface ICommandDefinition
{
    /// <summary>
    /// Gets the route of the command, for example <c>/api/v1/test/echo</c>.
    /// </summary>
    string Route { get; }

    /// <summary>
    /// Gets the authorization policy, one of <see cref="SecurityPolicies"/>.
    /// </summary>
    string Policy { get; }

    /// <summary>
    /// Gets the rate limit group, one of <see cref="RateLimitGroups"/>.
    /// </summary>
    string RateLimitGroup { get; }
}

/// <summary>
/// A player command: an intent sent by the client, checked and executed by the server.
/// </summary>
/// <typeparam name="TRequest">The request body. It carries the intent only, never a result.</typeparam>
/// <typeparam name="TResponse">The response body.</typeparam>
/// <remarks>
/// <para>
/// This is the single pipeline of the server for state-changing requests
/// (task 00002, section 1a). A command declares what it needs and the host enforces it, in
/// this order: authentication and the <see cref="Policy"/>, the
/// <see cref="RateLimitGroup"/>, the size limit of the body, the validation rules from
/// <see cref="DefineRules"/>, the ownership of the <see cref="GetTarget"/> object, and the
/// request identifier that makes a repeated request a no-op. <see cref="ExecuteAsync"/>
/// runs inside a database transaction.
/// </para>
/// <para>
/// A new command is one class derived from this one and one registration line,
/// <c>services.AddCommand&lt;TCommand&gt;()</c>.
/// </para>
/// </remarks>
public abstract class Command<TRequest, TResponse> : ICommandDefinition
    where TRequest : class
{
    /// <inheritdoc />
    public abstract string Route { get; }

    /// <inheritdoc />
    public virtual string Policy => SecurityPolicies.Player;

    /// <inheritdoc />
    public virtual string RateLimitGroup => RateLimitGroups.Command;

    /// <summary>
    /// Declares the validation rules of the request.
    /// </summary>
    /// <param name="rules">The validator to add the rules to.</param>
    /// <remarks>
    /// The rules are built for every request, so a limit read from the configuration takes
    /// effect at once.
    /// </remarks>
    public virtual void DefineRules(AbstractValidator<TRequest> rules)
    {
    }

    /// <summary>
    /// Names the object the command acts on, so that the host can check that the player
    /// owns it.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <returns>The target object, or <see langword="null"/> when the command has no target.</returns>
    public virtual OwnedObjectReference? GetTarget(TRequest request) => null;

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="request">The validated request.</param>
    /// <param name="context">The player and the request identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The response sent to the client.</returns>
    public abstract Task<TResponse> ExecuteAsync(
        TRequest request,
        CommandContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// The object a command acts on.
/// </summary>
/// <param name="ObjectType">The object type, answered by an <see cref="IOwnedObjectResolver"/>.</param>
/// <param name="ObjectId">The identifier of the object.</param>
public sealed record OwnedObjectReference(string ObjectType, string ObjectId);

/// <summary>
/// The context of one command execution.
/// </summary>
/// <param name="PlayerId">The identifier of the player who sent the command.</param>
/// <param name="RequestId">The request identifier sent by the client.</param>
public sealed record CommandContext(string PlayerId, string RequestId);

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Research.Api;
using Aetheris.Research.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Research.Api;

/// <summary>
/// Puts a level of a technology into the queue of the researches of a player (task 00012,
/// decisions 5 and 5a).
/// </summary>
/// <remarks>
/// The client names the body the research starts from, the technology and the level; the
/// server checks every rule and takes the cost in the transaction of the command. The answer
/// is the tree of the technologies and the queue after the change.
/// </remarks>
public sealed class EnqueueResearchCommand : Command<EnqueueResearchCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/research/enqueue";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "research.enqueue";

    private readonly ResearchQueueService _queue;
    private readonly ResearchTreeBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueResearchCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the researches.</param>
    /// <param name="builder">The builder of the tree of the technologies.</param>
    public EnqueueResearchCommand(ResearchQueueService queue, ResearchTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(builder);

        _queue = queue;
        _builder = builder;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.PlanetId).NotNull();
        rules.RuleFor(request => request.TechnologyId).NotNull();
        rules.RuleFor(request => request.TargetLevel).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new(PlanetOwnerResolver.Type, request.PlanetId!.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ResearchTreeSnapshot snapshot = await _queue
            .EnqueueAsync(
                context.PlayerId,
                request.PlanetId!.Value,
                request.TechnologyId!.Value,
                request.TargetLevel!.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(_builder.Build(snapshot), ResearchJson.Options);
    }

    /// <summary>
    /// The request: the body the research starts from, the technology and the level.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="TechnologyId">The technology.</param>
    /// <param name="TargetLevel">The level to research.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("technology_id")] int? TechnologyId,
        [property: JsonPropertyName("target_level")] int? TargetLevel);
}

/// <summary>
/// Removes a record from the queue of the researches of a player and returns its whole cost
/// (task 00012, decision 5a).
/// </summary>
public sealed class CancelResearchCommand : Command<CancelResearchCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/research/cancel";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "research.cancel";

    private readonly ResearchQueueService _queue;
    private readonly ResearchTreeBuilder _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelResearchCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the researches.</param>
    /// <param name="builder">The builder of the tree of the technologies.</param>
    public CancelResearchCommand(ResearchQueueService queue, ResearchTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(builder);

        _queue = queue;
        _builder = builder;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.QueueItemId).NotNull();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ResearchTreeSnapshot snapshot = await _queue
            .CancelAsync(context.PlayerId, request.QueueItemId!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(_builder.Build(snapshot), ResearchJson.Options);
    }

    /// <summary>
    /// The request: the record of the queue.
    /// </summary>
    /// <param name="QueueItemId">The record.</param>
    public sealed record Request(
        [property: JsonPropertyName("queue_item_id")] int? QueueItemId);
}

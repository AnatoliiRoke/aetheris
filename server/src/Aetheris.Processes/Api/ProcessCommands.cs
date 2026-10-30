using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Core.Errors;
using Aetheris.Processes.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Processes.Api;

/// <summary>
/// Puts an order of a recipe in the queue of a building (task 00014, decision 1).
/// </summary>
/// <remarks>
/// The client names the body, the building, the recipe and the numbers of the order; the
/// server checks every rule and takes the cost in the transaction of the command. The answer
/// is the building after the change.
/// </remarks>
public sealed class EnqueueProcessCommand : Command<EnqueueProcessCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/processes/enqueue";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "processes.enqueue";

    private readonly ProcessQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueProcessCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the processes.</param>
    public EnqueueProcessCommand(ProcessQueueService queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.PlanetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.PlanetBuildingId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.RecipeId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Quantity).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.SourceResourceTypeId).GreaterThan(0).When(request => request.SourceResourceTypeId is not null);
        rules.RuleFor(request => request.TargetResourceTypeId).GreaterThan(0).When(request => request.TargetResourceTypeId is not null);
        rules.RuleFor(request => request.PartTypeId).GreaterThan(0).When(request => request.PartTypeId is not null);
        rules.RuleFor(request => request.PartConditionPercent).GreaterThanOrEqualTo(0).When(request => request.PartConditionPercent is not null);
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

        ProcessBuildingDto building = await _queue
            .EnqueueAsync(
                context.PlayerId,
                request.PlanetId!.Value,
                request.PlanetBuildingId!.Value,
                request.RecipeId!.Value,
                new ProcessOrderRequest(
                    request.Quantity!.Value,
                    request.SourceResourceTypeId,
                    request.TargetResourceTypeId,
                    request.PartTypeId,
                    request.PartConditionPercent),
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(building, ProcessesJson.Options);
    }

    /// <summary>
    /// The request: the body, the building, the recipe and the numbers of the order.
    /// </summary>
    /// <param name="PlanetId">The body the process runs on.</param>
    /// <param name="PlanetBuildingId">The building the process runs in.</param>
    /// <param name="RecipeId">The recipe of the order.</param>
    /// <param name="Quantity">How many units the order holds.</param>
    /// <param name="SourceResourceTypeId">The resource the conversion takes.</param>
    /// <param name="TargetResourceTypeId">The resource the conversion gives.</param>
    /// <param name="PartTypeId">The part the process works on.</param>
    /// <param name="PartConditionPercent">The condition of the part the process works on, in per cent.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("planet_building_id")] long? PlanetBuildingId,
        [property: JsonPropertyName("recipe_id")] int? RecipeId,
        [property: JsonPropertyName("quantity")] long? Quantity,
        [property: JsonPropertyName("source_resource_type_id")] int? SourceResourceTypeId = null,
        [property: JsonPropertyName("target_resource_type_id")] int? TargetResourceTypeId = null,
        [property: JsonPropertyName("part_type_id")] int? PartTypeId = null,
        [property: JsonPropertyName("part_condition_percent")] int? PartConditionPercent = null);
}

/// <summary>
/// Removes an order from the queue of a building and returns its cost (task 00014,
/// decision 1).
/// </summary>
/// <remarks>
/// The kind of the process says whether the cost comes back: the device of the terraforming
/// is lost when the order is cancelled.
/// </remarks>
public sealed class CancelProcessCommand : Command<CancelProcessCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/processes/cancel";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "processes.cancel";

    private readonly ProcessQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelProcessCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the processes.</param>
    public CancelProcessCommand(ProcessQueueService queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.PlanetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.PlanetBuildingId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.OrderId).NotNull().GreaterThan(0);
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

        ProcessBuildingDto building = await _queue
            .CancelAsync(
                context.PlayerId,
                request.PlanetId!.Value,
                request.PlanetBuildingId!.Value,
                request.OrderId!.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(building, ProcessesJson.Options);
    }

    /// <summary>
    /// The request: the body, the building and the order.
    /// </summary>
    /// <param name="PlanetId">The body the process runs on.</param>
    /// <param name="PlanetBuildingId">The building the process runs in.</param>
    /// <param name="OrderId">The order.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("planet_building_id")] long? PlanetBuildingId,
        [property: JsonPropertyName("order_id")] long? OrderId);
}

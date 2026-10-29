using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Production.Api;
using Aetheris.Production.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Production.Api;

/// <summary>
/// Puts a batch of a recipe into the queue of a factory (task 00013, decision 2.3).
/// </summary>
/// <remarks>
/// The client names the body, the factory, the recipe and the number of units; the server
/// checks every rule and takes the cost in the transaction of the command. The answer is
/// the factory after the change.
/// </remarks>
public sealed class EnqueueProductionCommand : Command<EnqueueProductionCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/production/enqueue";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "production.enqueue";

    private readonly ProductionQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueProductionCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the factories.</param>
    public EnqueueProductionCommand(ProductionQueueService queue)
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

        rules.RuleFor(request => request.PlanetId).NotNull();
        rules.RuleFor(request => request.PlanetBuildingId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.RecipeId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Quantity).NotNull().GreaterThan(0);
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

        ProductionFactoryDto factory = await _queue
            .EnqueueAsync(
                context.PlayerId,
                request.PlanetId!.Value,
                request.PlanetBuildingId!.Value,
                request.RecipeId!.Value,
                request.Quantity!.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(factory, ProductionJson.Options);
    }

    /// <summary>
    /// The request: the body, the factory, the recipe and the number of units.
    /// </summary>
    /// <param name="PlanetId">The body the factory stands on.</param>
    /// <param name="PlanetBuildingId">The factory.</param>
    /// <param name="RecipeId">The recipe.</param>
    /// <param name="Quantity">How many units the batch holds.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("planet_building_id")] long? PlanetBuildingId,
        [property: JsonPropertyName("recipe_id")] int? RecipeId,
        [property: JsonPropertyName("quantity")] long? Quantity);
}

/// <summary>
/// Removes a batch from the queue of a factory and returns its whole cost (task 00013,
/// decision 2.3).
/// </summary>
public sealed class CancelProductionCommand : Command<CancelProductionCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/production/cancel";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "production.cancel";

    private readonly ProductionQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelProductionCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the factories.</param>
    public CancelProductionCommand(ProductionQueueService queue)
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

        rules.RuleFor(request => request.PlanetId).NotNull();
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

        ProductionFactoryDto factory = await _queue
            .CancelAsync(
                context.PlayerId,
                request.PlanetId!.Value,
                request.PlanetBuildingId!.Value,
                request.OrderId!.Value,
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(factory, ProductionJson.Options);
    }

    /// <summary>
    /// The request: the body, the factory and the batch.
    /// </summary>
    /// <param name="PlanetId">The body the factory stands on.</param>
    /// <param name="PlanetBuildingId">The factory.</param>
    /// <param name="OrderId">The batch.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("planet_building_id")] long? PlanetBuildingId,
        [property: JsonPropertyName("order_id")] long? OrderId);
}

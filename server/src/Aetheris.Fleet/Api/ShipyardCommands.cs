using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Fleet.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Fleet.Api;

/// <summary>
/// Puts a project into the shipyard queue of a body and pays for it (task 00039).
/// </summary>
public sealed class EnqueueShipyardCommand : Command<EnqueueShipyardCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/shipyard/enqueue";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.shipyard_enqueue";

    private readonly ShipyardQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueShipyardCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the shipyard.</param>
    public EnqueueShipyardCommand(ShipyardQueueService queue)
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
        rules.RuleFor(request => request.DesignId).NotNull();
        rules.RuleFor(request => request.Quantity).GreaterThan(0).When(request => request.Quantity is not null);
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

        ShipyardViewDto view = await _queue.EnqueueAsync(
            context.PlayerId,
            request.PlanetId!.Value,
            request.PlanetBuildingId,
            request.DesignId!.Value,
            request.Quantity,
            cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the body, the shipyard, the project and how many ships.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="PlanetBuildingId">The shipyard, or <see langword="null"/> to take the shipyard of the body.</param>
    /// <param name="DesignId">The project.</param>
    /// <param name="Quantity">How many ships to assemble. <see langword="null"/> means one, so an older client still queues one ship.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("planet_building_id")] long? PlanetBuildingId,
        [property: JsonPropertyName("design_id")] long? DesignId,
        [property: JsonPropertyName("quantity")] long? Quantity);
}

/// <summary>
/// Removes a record from the shipyard queue and returns everything it paid (task 00039).
/// </summary>
public sealed class CancelShipyardCommand : Command<CancelShipyardCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/shipyard/cancel";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.shipyard_cancel";

    private readonly ShipyardQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelShipyardCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the shipyard.</param>
    public CancelShipyardCommand(ShipyardQueueService queue)
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

        rules.RuleFor(request => request.OrderId).NotNull();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShipyardViewDto view = await _queue.CancelAsync(context.PlayerId, request.OrderId!.Value, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the record of the queue.
    /// </summary>
    /// <param name="OrderId">The record.</param>
    public sealed record Request(
        [property: JsonPropertyName("order_id")] long? OrderId);
}

/// <summary>
/// Takes a finished ship apart on the shipyard of its body (task 00039).
/// </summary>
public sealed class DisassembleShipCommand : Command<DisassembleShipCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/ship/disassemble";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.ship_disassemble";

    private readonly ShipyardQueueService _queue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisassembleShipCommand"/> class.
    /// </summary>
    /// <param name="queue">The queue of the shipyard.</param>
    public DisassembleShipCommand(ShipyardQueueService queue)
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

        rules.RuleFor(request => request.ShipId).NotNull();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShipyardViewDto view = await _queue.DisassembleAsync(context.PlayerId, request.ShipId!.Value, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the ship.
    /// </summary>
    /// <param name="ShipId">The ship.</param>
    public sealed record Request(
        [property: JsonPropertyName("ship_id")] long? ShipId);
}

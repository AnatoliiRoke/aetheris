using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Core.Errors;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using FluentValidation;

namespace Aetheris.Fleet.Api;

/// <summary>
/// Writes a flight plan for a fleet of the player (task 00050). The fleet has no plan yet.
/// </summary>
public sealed class CreateFlightPlanCommand : Command<CreateFlightPlanCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/flight-plans";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.flight_plan_create";

    private readonly FlightPlanService _plans;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFlightPlanCommand"/> class.
    /// </summary>
    /// <param name="plans">The service of the flight plans.</param>
    public CreateFlightPlanCommand(FlightPlanService plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        _plans = plans;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Orders).NotNull();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FlightPlanDto plan = await _plans
            .CreateAsync(context.PlayerId, request.FleetId!.Value, FlightPlanOrderMapper.ToInputs(request.Orders!), cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(plan, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet and the orders of the plan.
    /// </summary>
    /// <param name="FleetId">The fleet the plan is made for.</param>
    /// <param name="Orders">The orders of the plan, in the order they are carried out in.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("orders")] IReadOnlyList<OrderRequest>? Orders);

    /// <summary>
    /// One order of the plan as the player sends it.
    /// </summary>
    /// <param name="TypeCode">The type of the order.</param>
    /// <param name="TargetKind">The kind of the target of the order.</param>
    /// <param name="TargetSectorId">The sector of the target.</param>
    /// <param name="TargetSystemId">The system of the target.</param>
    /// <param name="TargetObjectId">The object of the target.</param>
    /// <param name="TargetX">The first axis of the target.</param>
    /// <param name="TargetY">The second axis of the target.</param>
    /// <param name="TargetZ">The third axis of the target.</param>
    /// <param name="DurationSeconds">How long the order lasts, in game seconds.</param>
    /// <param name="Parameters">The parameters of the order as a document.</param>
    public sealed record OrderRequest(
        [property: JsonPropertyName("type_code")] string? TypeCode,
        [property: JsonPropertyName("target_kind")] string? TargetKind,
        [property: JsonPropertyName("target_sector_id")] int? TargetSectorId,
        [property: JsonPropertyName("target_system_id")] long? TargetSystemId,
        [property: JsonPropertyName("target_object_id")] long? TargetObjectId,
        [property: JsonPropertyName("target_x")] decimal? TargetX,
        [property: JsonPropertyName("target_y")] decimal? TargetY,
        [property: JsonPropertyName("target_z")] decimal? TargetZ,
        [property: JsonPropertyName("duration_seconds")] long? DurationSeconds,
        [property: JsonPropertyName("parameters")] string? Parameters);
}

/// <summary>
/// Changes the orders of the flight plan of a fleet (task 00050).
/// </summary>
public sealed class SaveFlightPlanCommand : Command<SaveFlightPlanCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/flight-plans/save";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.flight_plan_save";

    private readonly FlightPlanService _plans;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveFlightPlanCommand"/> class.
    /// </summary>
    /// <param name="plans">The service of the flight plans.</param>
    public SaveFlightPlanCommand(FlightPlanService plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        _plans = plans;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Orders).NotNull();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FlightPlanDto plan = await _plans
            .SaveAsync(context.PlayerId, request.FleetId!.Value, FlightPlanOrderMapper.ToInputs(request.Orders!), cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(plan, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet and the orders of the plan.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="Orders">The orders of the plan, in the order they are carried out in.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("orders")] IReadOnlyList<CreateFlightPlanCommand.OrderRequest>? Orders);
}

/// <summary>
/// Deletes the flight plan of a fleet (task 00050).
/// </summary>
public sealed class DeleteFlightPlanCommand : Command<DeleteFlightPlanCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/flight-plans/delete";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.flight_plan_delete";

    private readonly FlightPlanService _plans;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteFlightPlanCommand"/> class.
    /// </summary>
    /// <param name="plans">The service of the flight plans.</param>
    public DeleteFlightPlanCommand(FlightPlanService plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        _plans = plans;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _plans.DeleteAsync(context.PlayerId, request.FleetId!.Value, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(new { deleted = true }, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId);
}

/// <summary>
/// Stops the flight plan of a fleet: the fleet waits where it is (task 00050).
/// </summary>
public sealed class StopFlightPlanCommand : Command<StopFlightPlanCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/flight-plans/stop";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.flight_plan_stop";

    private readonly FlightPlanService _plans;

    /// <summary>
    /// Initializes a new instance of the <see cref="StopFlightPlanCommand"/> class.
    /// </summary>
    /// <param name="plans">The service of the flight plans.</param>
    public StopFlightPlanCommand(FlightPlanService plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        _plans = plans;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FlightPlanDto plan = await _plans
            .StopAsync(context.PlayerId, request.FleetId!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(plan, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId);
}

/// <summary>
/// Starts the flight plan of a fleet (task 00050). The orders themselves are carried out by the
/// tasks to come; this command only puts the plan under way.
/// </summary>
public sealed class ExecuteFlightPlanCommand : Command<ExecuteFlightPlanCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/flight-plans/execute";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.flight_plan_execute";

    private readonly FlightPlanService _plans;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecuteFlightPlanCommand"/> class.
    /// </summary>
    /// <param name="plans">The service of the flight plans.</param>
    public ExecuteFlightPlanCommand(FlightPlanService plans)
    {
        ArgumentNullException.ThrowIfNull(plans);

        _plans = plans;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FlightPlanDto plan = await _plans
            .ExecuteAsync(context.PlayerId, request.FleetId!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(plan, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId);
}

/// <summary>
/// Turns the orders of a request into the orders the service writes (task 00050).
/// </summary>
public static class FlightPlanOrderMapper
{
    /// <summary>
    /// Turns the orders of the request into the orders of the service. An order names no type or
    /// a type that does not exist is refused, and so is a target that is named but not given.
    /// </summary>
    /// <param name="orders">The orders of the request.</param>
    /// <returns>The orders of the service.</returns>
    public static IReadOnlyList<FlightPlanOrderInput> ToInputs(IReadOnlyList<CreateFlightPlanCommand.OrderRequest> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);

        List<FlightPlanOrderInput> inputs = [];

        foreach (CreateFlightPlanCommand.OrderRequest order in orders)
        {
            string type = (order.TypeCode ?? string.Empty).Trim().ToLowerInvariant();

            if (!FlightOrderTypes.All.Contains(type))
            {
                throw new GameException(FleetErrorCodes.FlightOrderTypeUnknown, "The order names a type that does not exist.");
            }

            string kind = (order.TargetKind ?? FlightTargetKinds.None).Trim().ToLowerInvariant();

            if (!FlightTargetKinds.All.Contains(kind))
            {
                throw new GameException(FleetErrorCodes.FlightOrderTargetUnknown, "The order names a kind of target that does not exist.");
            }

            long systemId = order.TargetSystemId ?? 0;
            long objectId = order.TargetObjectId ?? 0;

            // A target of a system or of an object names it by its identifier: without one the
            // order would point at nothing, so it is refused instead of being written.
            if (kind == FlightTargetKinds.System && systemId <= 0)
            {
                throw new GameException(FleetErrorCodes.FlightOrderTargetUnknown, "The order names a system but does not say which one.");
            }

            if ((kind == FlightTargetKinds.Object || kind == FlightTargetKinds.Planet) && objectId <= 0)
            {
                throw new GameException(FleetErrorCodes.FlightOrderTargetUnknown, "The order names an object but does not say which one.");
            }

            inputs.Add(new FlightPlanOrderInput(
                type,
                kind,
                order.TargetSectorId ?? 0,
                systemId,
                objectId,
                order.TargetX ?? 0m,
                order.TargetY ?? 0m,
                order.TargetZ ?? 0m,
                Math.Max(0L, order.DurationSeconds ?? 0L),
                string.IsNullOrWhiteSpace(order.Parameters) ? "{}" : order.Parameters!));
        }

        return inputs;
    }
}

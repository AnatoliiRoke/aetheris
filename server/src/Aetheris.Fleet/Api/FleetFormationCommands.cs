using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Fleet.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Fleet.Api;

/// <summary>
/// Forms a fleet of free ships that stand on one body of the player (task 00040). The body is the home body of the fleet.
/// </summary>
public sealed class CreateFleetCommand : Command<CreateFleetCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.create";

    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateFleetCommand"/> class.
    /// </summary>
    /// <param name="fleets">The service of the fleets.</param>
    public CreateFleetCommand(FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(fleets);

        _fleets = fleets;
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
        rules.RuleFor(request => request.Name).NotNull().NotEmpty();
        rules.RuleFor(request => request.ShipIds).NotNull().NotEmpty();
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

        FleetsViewDto view = await _fleets
            .CreateAsync(context.PlayerId, request.PlanetId!.Value, request.Name!, request.ShipIds!, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the body, the name and the ships.
    /// </summary>
    /// <param name="PlanetId">The body the ships stand on.</param>
    /// <param name="Name">The name of the fleet.</param>
    /// <param name="ShipIds">The ships. The fleet needs at least one.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("ship_ids")] IReadOnlyList<long>? ShipIds);
}

/// <summary>
/// Puts free ships into a fleet (task 00040). The ships stand on the body of the fleet.
/// </summary>
public sealed class AddFleetShipsCommand : Command<AddFleetShipsCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/add-ships";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.add_ship";

    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddFleetShipsCommand"/> class.
    /// </summary>
    /// <param name="fleets">The service of the fleets.</param>
    public AddFleetShipsCommand(FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(fleets);

        _fleets = fleets;
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
        rules.RuleFor(request => request.ShipIds).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetsViewDto view = await _fleets
            .AddShipsAsync(context.PlayerId, request.FleetId!.Value, request.ShipIds!, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet and the ships.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="ShipIds">The ships.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("ship_ids")] IReadOnlyList<long>? ShipIds);
}

/// <summary>
/// Takes ships out of a fleet (task 00040). They stay on the body as free ships; the last ship of a fleet stays.
/// </summary>
public sealed class RemoveFleetShipsCommand : Command<RemoveFleetShipsCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/remove-ships";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.remove_ship";

    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveFleetShipsCommand"/> class.
    /// </summary>
    /// <param name="fleets">The service of the fleets.</param>
    public RemoveFleetShipsCommand(FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(fleets);

        _fleets = fleets;
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
        rules.RuleFor(request => request.ShipIds).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetsViewDto view = await _fleets
            .RemoveShipsAsync(context.PlayerId, request.FleetId!.Value, request.ShipIds!, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet and the ships.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="ShipIds">The ships.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("ship_ids")] IReadOnlyList<long>? ShipIds);
}

/// <summary>
/// Moves ships between two fleets of the player that stand on the same body (task 00040).
/// </summary>
public sealed class TransferFleetShipsCommand : Command<TransferFleetShipsCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/transfer-ships";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.transfer_ship";

    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransferFleetShipsCommand"/> class.
    /// </summary>
    /// <param name="fleets">The service of the fleets.</param>
    public TransferFleetShipsCommand(FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(fleets);

        _fleets = fleets;
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
        rules.RuleFor(request => request.TargetFleetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.ShipIds).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetsViewDto view = await _fleets
            .TransferShipsAsync(context.PlayerId, request.FleetId!.Value, request.TargetFleetId!.Value, request.ShipIds!, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the two fleets and the ships.
    /// </summary>
    /// <param name="FleetId">The fleet the ships leave.</param>
    /// <param name="TargetFleetId">The fleet the ships join.</param>
    /// <param name="ShipIds">The ships.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("target_fleet_id")] long? TargetFleetId,
        [property: JsonPropertyName("ship_ids")] IReadOnlyList<long>? ShipIds);
}

/// <summary>
/// Renames a fleet (task 00040).
/// </summary>
public sealed class RenameFleetCommand : Command<RenameFleetCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/rename";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.rename";

    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameFleetCommand"/> class.
    /// </summary>
    /// <param name="fleets">The service of the fleets.</param>
    public RenameFleetCommand(FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(fleets);

        _fleets = fleets;
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
        rules.RuleFor(request => request.Name).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        FleetsViewDto view = await _fleets
            .RenameAsync(context.PlayerId, request.FleetId!.Value, request.Name!, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet and the new name.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="Name">The new name.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("name")] string? Name);
}

/// <summary>
/// Disbands a fleet (task 00040). Its ships stay on the body as free ships.
/// </summary>
public sealed class DissolveFleetCommand : Command<DissolveFleetCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/dissolve";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.dissolve";

    private readonly FleetFormationService _fleets;

    /// <summary>
    /// Initializes a new instance of the <see cref="DissolveFleetCommand"/> class.
    /// </summary>
    /// <param name="fleets">The service of the fleets.</param>
    public DissolveFleetCommand(FleetFormationService fleets)
    {
        ArgumentNullException.ThrowIfNull(fleets);

        _fleets = fleets;
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

        FleetsViewDto view = await _fleets
            .DissolveAsync(context.PlayerId, request.FleetId!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(view, FleetJson.Options);
    }

    /// <summary>
    /// The request: the fleet.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    public sealed record Request(
        [property: JsonPropertyName("fleet_id")] long? FleetId);
}

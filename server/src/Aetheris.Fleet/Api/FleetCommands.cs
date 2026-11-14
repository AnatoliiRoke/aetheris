using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Fleet.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Fleet.Api;

/// <summary>
/// Creates a project of a ship (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// The project belongs to the player, so it is available on every body of the civilization
/// (decision 6); the body in the request is the one the project is made on.
/// </remarks>
public sealed class CreateShipDesignCommand : Command<CreateShipDesignCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/designs";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.design_create";

    private readonly ShipDesignService _designs;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateShipDesignCommand"/> class.
    /// </summary>
    /// <param name="designs">The service of the projects of the ships.</param>
    public CreateShipDesignCommand(ShipDesignService designs)
    {
        ArgumentNullException.ThrowIfNull(designs);

        _designs = designs;
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
        rules.RuleFor(request => request.HullPartTypeId).NotNull().GreaterThan(0);
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

        ShipDesignDto design = await _designs
            .CreateAsync(
                context.PlayerId,
                request.PlanetId!.Value,
                request.Name!,
                request.HullPartTypeId!.Value,
                request.HasRaceBonusSlot ?? false,
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(design, FleetJson.Options);
    }

    /// <summary>
    /// The request: the body, the name and the hull of the project.
    /// </summary>
    /// <param name="PlanetId">The body the project is made on.</param>
    /// <param name="Name">The name of the project.</param>
    /// <param name="HullPartTypeId">The hull of the project.</param>
    /// <param name="HasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("hull_part_type_id")] int? HullPartTypeId,
        [property: JsonPropertyName("has_race_bonus_slot")] bool? HasRaceBonusSlot = null);
}

/// <summary>
/// Renames a project of a ship (task 00038, section 3.2).
/// </summary>
public sealed class RenameShipDesignCommand : Command<RenameShipDesignCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/designs/rename";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.design_rename";

    private readonly ShipDesignService _designs;

    /// <summary>
    /// Initializes a new instance of the <see cref="RenameShipDesignCommand"/> class.
    /// </summary>
    /// <param name="designs">The service of the projects of the ships.</param>
    public RenameShipDesignCommand(ShipDesignService designs)
    {
        ArgumentNullException.ThrowIfNull(designs);

        _designs = designs;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.DesignId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Name).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShipDesignDto design = await _designs
            .RenameAsync(context.PlayerId, request.DesignId!.Value, request.Name!, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(design, FleetJson.Options);
    }

    /// <summary>
    /// The request: the project and its new name.
    /// </summary>
    /// <param name="DesignId">The project.</param>
    /// <param name="Name">The new name of the project.</param>
    public sealed record Request(
        [property: JsonPropertyName("design_id")] long? DesignId,
        [property: JsonPropertyName("name")] string? Name);
}

/// <summary>
/// Saves the hull and the slots of a project of a ship (task 00038, section 3.2).
/// </summary>
/// <remarks>
/// The server is the only source of truth about the rules of the constructor: the number of
/// the slots, the mandatory components, the energy, the role and the belonging of every
/// module. The client shows the reason of a refusal.
/// </remarks>
public sealed class SaveShipDesignCommand : Command<SaveShipDesignCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/designs/save";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.design_save";

    private readonly ShipDesignService _designs;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveShipDesignCommand"/> class.
    /// </summary>
    /// <param name="designs">The service of the projects of the ships.</param>
    public SaveShipDesignCommand(ShipDesignService designs)
    {
        ArgumentNullException.ThrowIfNull(designs);

        _designs = designs;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.DesignId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.HullPartTypeId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Slots).NotNull();
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShipDesignDto design = await _designs
            .SaveAsync(
                context.PlayerId,
                request.DesignId!.Value,
                request.HullPartTypeId!.Value,
                request.HasRaceBonusSlot ?? false,
                [.. (request.Slots ?? []).Select(slot => new ShipDesignSlotInput(
                    slot.SlotIndex ?? 0,
                    slot.PartTypeId ?? 0,
                    slot.MarkLevel ?? 0,
                    slot.HasRaceBonus ?? false,
                    slot.PowerPriority))],
                cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(design, FleetJson.Options);
    }

    /// <summary>
    /// The request: the project, its hull and its slots.
    /// </summary>
    /// <param name="DesignId">The project.</param>
    /// <param name="HullPartTypeId">The hull of the project.</param>
    /// <param name="HasRaceBonusSlot">Whether the project takes the extra slot of the race.</param>
    /// <param name="Slots">The slots of the project.</param>
    public sealed record Request(
        [property: JsonPropertyName("design_id")] long? DesignId,
        [property: JsonPropertyName("hull_part_type_id")] int? HullPartTypeId,
        [property: JsonPropertyName("has_race_bonus_slot")] bool? HasRaceBonusSlot,
        [property: JsonPropertyName("slots")] IReadOnlyList<SlotRequest>? Slots);

    /// <summary>
    /// A slot of a project as the client sends it.
    /// </summary>
    /// <param name="SlotIndex">The number of the slot, from zero.</param>
    /// <param name="PartTypeId">The part in the slot.</param>
    /// <param name="MarkLevel">The mark of the part in the slot.</param>
    /// <param name="HasRaceBonus">Whether the part in the slot carries the bonus of the race that made it.</param>
    /// <param name="PowerPriority">The shutdown order of the slot, or <see langword="null"/> for the order of the group.</param>
    public sealed record SlotRequest(
        [property: JsonPropertyName("slot_index")] int? SlotIndex,
        [property: JsonPropertyName("part_type_id")] int? PartTypeId,
        [property: JsonPropertyName("mark_level")] int? MarkLevel,
        [property: JsonPropertyName("has_race_bonus")] bool? HasRaceBonus,
        [property: JsonPropertyName("power_priority")] int? PowerPriority);
}

/// <summary>
/// Archives a project of a ship: it is kept, so a ship already built of it does not lose its
/// link (task 00023, task 00038, section 3.2).
/// </summary>
public sealed class ArchiveShipDesignCommand : Command<ArchiveShipDesignCommand.Request, JsonElement>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/designs/archive";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.design_archive";

    private readonly ShipDesignService _designs;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArchiveShipDesignCommand"/> class.
    /// </summary>
    /// <param name="designs">The service of the projects of the ships.</param>
    public ArchiveShipDesignCommand(ShipDesignService designs)
    {
        ArgumentNullException.ThrowIfNull(designs);

        _designs = designs;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.DesignId).NotNull().GreaterThan(0);
    }

    /// <inheritdoc />
    public override OwnedObjectReference? GetTarget(Request request) => null;

    /// <inheritdoc />
    public override async Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ShipDesignDto design = await _designs
            .ArchiveAsync(context.PlayerId, request.DesignId!.Value, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(design, FleetJson.Options);
    }

    /// <summary>
    /// The request: the project.
    /// </summary>
    /// <param name="DesignId">The project.</param>
    public sealed record Request([property: JsonPropertyName("design_id")] long? DesignId);
}

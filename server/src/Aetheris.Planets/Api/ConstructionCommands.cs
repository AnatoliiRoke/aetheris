using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Core.Time;
using Aetheris.Planets.Services;
using Aetheris.World;
using FluentValidation;

namespace Aetheris.Planets.Api;

/// <summary>
/// Puts the next level of a building into the construction queue of a body (task 00011,
/// decisions 5 and 6).
/// </summary>
/// <remarks>
/// The client only names the body and the building; the server finds the level, checks every
/// rule and takes the cost in the transaction of the command. The answer is the body after the
/// change, in the snake_case form of the reads.
/// </remarks>
public sealed class EnqueueConstructionCommand : Command<EnqueueConstructionCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/planets/construction/enqueue";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "planets.construction_enqueue";

    private readonly ConstructionService _construction;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnqueueConstructionCommand"/> class.
    /// </summary>
    /// <param name="construction">The changes of a body.</param>
    /// <param name="clock">The game clock.</param>
    public EnqueueConstructionCommand(ConstructionService construction, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(clock);

        _construction = construction;
        _clock = clock;
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
        rules.RuleFor(request => request.BuildingTypeId).NotNull();
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

        PlanetOverviewDto overview = await _construction
            .EnqueueAsync(request.PlanetId!.Value, request.BuildingTypeId!.Value, _clock.GameNow, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(overview, PlanetsJson.Options);
    }

    /// <summary>
    /// The request: the body and the building.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="BuildingTypeId">The building type.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("building_type_id")] int? BuildingTypeId);
}

/// <summary>
/// Removes a record from the construction queue of a body and returns its whole cost
/// (task 00011, decision 6).
/// </summary>
public sealed class CancelConstructionCommand : Command<CancelConstructionCommand.Request, JsonElement>
{
    /// <summary>
    /// The route of the command.
    /// </summary>
    public const string RoutePath = "/api/v1/planets/construction/cancel";

    /// <summary>
    /// The action code of the command.
    /// </summary>
    public const string Code = "planets.construction_cancel";

    private readonly ConstructionService _construction;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CancelConstructionCommand"/> class.
    /// </summary>
    /// <param name="construction">The changes of a body.</param>
    /// <param name="clock">The game clock.</param>
    public CancelConstructionCommand(ConstructionService construction, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(clock);

        _construction = construction;
        _clock = clock;
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
        rules.RuleFor(request => request.QueueItemId).NotNull();
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

        PlanetOverviewDto overview = await _construction
            .CancelAsync(request.PlanetId!.Value, request.QueueItemId!.Value, _clock.GameNow, cancellationToken)
            .ConfigureAwait(false);

        return JsonSerializer.SerializeToElement(overview, PlanetsJson.Options);
    }

    /// <summary>
    /// The request: the body and the queue record.
    /// </summary>
    /// <param name="PlanetId">The body.</param>
    /// <param name="QueueItemId">The queue record.</param>
    public sealed record Request(
        [property: JsonPropertyName("planet_id")] long? PlanetId,
        [property: JsonPropertyName("queue_item_id")] long? QueueItemId);
}

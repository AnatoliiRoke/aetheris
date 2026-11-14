using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Core.Commands;
using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Api;

/// <summary>
/// One item of a load or an unload of a fleet (task 00073).
/// </summary>
/// <param name="Kind">The kind of the item, one of <see cref="CargoItemKinds"/>.</param>
/// <param name="Id">The resource or the kind of the part.</param>
/// <param name="MarkLevel">The mark level of a part; zero for a resource.</param>
/// <param name="HasRaceBonus">Whether a part carries the bonus of its race; always <see langword="false"/> for a resource.</param>
/// <param name="Amount">How much of the resource, or how many parts; above zero.</param>
public sealed record CargoItemRequest(
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("mark_level")] int? MarkLevel = null,
    [property: JsonPropertyName("has_race_bonus")] bool? HasRaceBonus = null,
    [property: JsonPropertyName("amount")] long? Amount = null);

/// <summary>
/// The base of the commands that load and unload the hold of a fleet at once (task 00073).
/// </summary>
/// <remarks>
/// The commands call the same service as the orders of the plan do (task 00057), so the rules of
/// the room, the weight and the stock are the same and there is one copy of them only.
/// </remarks>
/// <typeparam name="TRequest">The request of the command.</typeparam>
public abstract class FleetCargoCommand<TRequest> : Command<TRequest, JsonElement>
    where TRequest : FleetCargoCommand<TRequest>.CargoRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FleetCargoCommand{TRequest}"/> class.
    /// </summary>
    /// <param name="cargo">The load and the unload of the holds.</param>
    /// <param name="holds">The holds of the fleet.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="reports">The reports of the fleets, for a load that was short.</param>
    protected FleetCargoCommand(
        FleetCargoService cargo,
        FleetHoldService holds,
        FleetUnitOfWork unitOfWork,
        IGameClock clock,
        FleetReportService reports)
    {
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(reports);

        Cargo = cargo;
        Holds = holds;
        UnitOfWork = unitOfWork;
        Clock = clock;
        Reports = reports;
    }

    /// <summary>Gets the load and the unload of the holds.</summary>
    protected FleetCargoService Cargo { get; }

    /// <summary>Gets the holds of the fleet.</summary>
    protected FleetHoldService Holds { get; }

    /// <summary>Gets the unit of the transaction.</summary>
    protected FleetUnitOfWork UnitOfWork { get; }

    /// <summary>Gets the game clock.</summary>
    protected IGameClock Clock { get; }

    /// <summary>Gets the reports of the fleets.</summary>
    protected FleetReportService Reports { get; }

    /// <summary>
    /// Loads the items into the holds of the fleet, or puts them out of them.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="context">The context of the command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The state of the holds after the change.</returns>
    protected async Task<JsonElement> ApplyAsync(
        CargoRequest request,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IReadOnlyList<CargoItem> items = request.ToItems();

        return await UnitOfWork.RunAsync(
            async () =>
            {
                await FleetLocks.LockPlayerAsync(HoldsContext, context.PlayerId, cancellationToken).ConfigureAwait(false);

                FleetEntity fleet = await HoldsContext.Fleets
                    .SingleOrDefaultAsync(entry => entry.Id == request.FleetId!.Value && entry.PlayerId == context.PlayerId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new GameException(FleetErrorCodes.FleetUnknown, "The fleet does not exist.");

                CargoOutcome outcome = IsLoad
                    ? await Cargo.LoadAsync(fleet, items, Clock.GameNow, cancellationToken).ConfigureAwait(false)
                    : await Cargo.UnloadAsync(fleet, items, Clock.GameNow, cancellationToken).ConfigureAwait(false);

                if (!outcome.Done)
                {
                    throw new GameException(outcome.Code!, "The hold of the fleet did not change.");
                }

                // The command loads what it can, as the order of a plan does (author's rule, 14.11.2026). A
                // command the player pressed that loads nothing at all is refused with the reason, so the
                // window tells why the hold did not change; a load that took a part writes the report of
                // the rest.
                if (IsLoad && !outcome.LoadedAny)
                {
                    bool noRoom = outcome.Shortfalls.Any(entry => entry.ReasonCode == CargoShortfallReasons.Room);

                    throw new GameException(
                        noRoom ? FleetErrorCodes.CargoOverflow : FleetErrorCodes.CargoShort,
                        noRoom ? "The holds have no room for any item of the load." : "The stock of the body has none of the items of the load.");
                }

                if (outcome.Shortfalls.Count > 0)
                {
                    await Reports.WriteLoadShortReportAsync(fleet, outcome.Shortfalls, cancellationToken).ConfigureAwait(false);
                }

                FleetHoldDto hold = await Holds.ReadForPlayerAsync(context.PlayerId, fleet.Id, cancellationToken).ConfigureAwait(false);

                return JsonSerializer.SerializeToElement(hold, FleetJson.Options);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the context of the fleet module, so the fleets of the player are locked as the other
    /// commands do.
    /// </summary>
    protected abstract FleetDbContext HoldsContext { get; }

    /// <summary>Gets whether the command loads the hold or puts the cargo out of it.</summary>
    protected abstract bool IsLoad { get; }

    /// <summary>
    /// The request of a command that loads or unloads the hold of a fleet.
    /// </summary>
    public abstract record CargoRequest(
        [property: JsonPropertyName("fleet_id")] long? FleetId,
        [property: JsonPropertyName("items")] IReadOnlyList<CargoItemRequest>? Items)
    {
        /// <summary>
        /// Turns the items of the request into the items of a load or an unload.
        /// </summary>
        /// <returns>The items.</returns>
        /// <exception cref="GameException">An item is not well formed.</exception>
        public IReadOnlyList<CargoItem> ToItems()
        {
            List<CargoItem> items = [];

            foreach (CargoItemRequest item in Items ?? [])
            {
                if (item.Kind is null
                    || item.Id is null
                    || item.Amount is null
                    || item.Amount.Value <= 0
                    || (item.Kind != CargoItemKinds.Resource && item.Kind != CargoItemKinds.Part))
                {
                    throw new GameException(FleetErrorCodes.CargoItemsInvalid, "The request names nothing to carry, or an amount that is not above zero.");
                }

                if (item.Kind == CargoItemKinds.Resource)
                {
                    items.Add(new CargoItem(item.Kind, item.Id.Value, 0, false, item.Amount.Value));

                    continue;
                }

                int mark = item.MarkLevel ?? 1;

                if (mark <= 0)
                {
                    throw new GameException(FleetErrorCodes.CargoItemsInvalid, "The mark level of a part is not above zero.");
                }

                items.Add(new CargoItem(item.Kind, item.Id.Value, mark, item.HasRaceBonus ?? false, item.Amount.Value));
            }

            if (items.Count == 0)
            {
                throw new GameException(FleetErrorCodes.CargoItemsInvalid, "The request names nothing to carry, or an amount that is not above zero.");
            }

            return items;
        }
    }
}

/// <summary>
/// Loads the resources and the modules of the body into the holds of a fleet (task 00073).
/// </summary>
/// <remarks>
/// The command is the shortcut of the order of a plan: the same rules and the same service (task 00057).
/// The fleet must stand on a body of its owner. Like the order, it loads what the stock has and what the
/// holds take and reports the rest; only a load that takes nothing at all is refused, with the reason.
/// </remarks>
public sealed class FleetCargoLoadCommand : FleetCargoCommand<FleetCargoLoadCommand.Request>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/cargo-load";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.cargo_load";

    private readonly FleetDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetCargoLoadCommand"/> class.
    /// </summary>
    /// <param name="cargo">The load and the unload of the holds.</param>
    /// <param name="holds">The holds of the fleet.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="reports">The reports of the fleets.</param>
    public FleetCargoLoadCommand(
        FleetCargoService cargo,
        FleetHoldService holds,
        FleetUnitOfWork unitOfWork,
        IGameClock clock,
        FleetDbContext dbContext,
        FleetReportService reports)
        : base(cargo, holds, unitOfWork, clock, reports)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    protected override FleetDbContext HoldsContext => _dbContext;

    /// <inheritdoc />
    protected override bool IsLoad => true;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Items).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken) =>
        ApplyAsync(request, context, cancellationToken);

    /// <summary>
    /// The request: the fleet and what to load.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="Items">What to load.</param>
    public sealed record Request(long? FleetId, IReadOnlyList<CargoItemRequest>? Items) : CargoRequest(FleetId, Items);
}

/// <summary>
/// Puts the resources and the modules of the hold of a fleet back onto the body it stands on
/// (task 00073).
/// </summary>
/// <remarks>
/// The command is the shortcut of the order of a plan: the same rules, the same service and the
/// same refusals (task 00057). The fleet must stand on a body of its owner.
/// </remarks>
public sealed class FleetCargoUnloadCommand : FleetCargoCommand<FleetCargoUnloadCommand.Request>
{
    /// <summary>The route of the command.</summary>
    public const string RoutePath = "/api/v1/fleet/fleets/cargo-unload";

    /// <summary>The action code of the command.</summary>
    public const string Code = "fleet.cargo_unload";

    private readonly FleetDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetCargoUnloadCommand"/> class.
    /// </summary>
    /// <param name="cargo">The load and the unload of the holds.</param>
    /// <param name="holds">The holds of the fleet.</param>
    /// <param name="unitOfWork">The unit of the transaction.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="reports">The reports of the fleets.</param>
    public FleetCargoUnloadCommand(
        FleetCargoService cargo,
        FleetHoldService holds,
        FleetUnitOfWork unitOfWork,
        IGameClock clock,
        FleetDbContext dbContext,
        FleetReportService reports)
        : base(cargo, holds, unitOfWork, clock, reports)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public override string Route => RoutePath;

    /// <inheritdoc />
    public override string ActionCode => Code;

    /// <inheritdoc />
    protected override FleetDbContext HoldsContext => _dbContext;

    /// <inheritdoc />
    protected override bool IsLoad => false;

    /// <inheritdoc />
    public override void DefineRules(AbstractValidator<Request> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        rules.RuleFor(request => request.FleetId).NotNull().GreaterThan(0);
        rules.RuleFor(request => request.Items).NotNull().NotEmpty();
    }

    /// <inheritdoc />
    public override Task<JsonElement> ExecuteAsync(Request request, CommandContext context, CancellationToken cancellationToken) =>
        ApplyAsync(request, context, cancellationToken);

    /// <summary>
    /// The request: the fleet and what to put out.
    /// </summary>
    /// <param name="FleetId">The fleet.</param>
    /// <param name="Items">What to unload.</param>
    public sealed record Request(long? FleetId, IReadOnlyList<CargoItemRequest>? Items) : CargoRequest(FleetId, Items);
}

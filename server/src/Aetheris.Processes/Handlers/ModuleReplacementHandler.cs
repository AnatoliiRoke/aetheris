using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the replacement of a module: the damaged module comes off the ship on the
/// orbit and a new one from the storage of the body takes its place (task 00014, decision 8).
/// </summary>
/// <remarks>
/// <para>
/// The order takes the new modules from the storage when it enters the queue. A unit takes
/// the worst damaged module of the kind off the ship, puts it to the storage as a damaged
/// part and puts a new one in its place. The replacement is quicker than the repair of the
/// whole ship by the time of its recipe, which is data.
/// </para>
/// <para>
/// Stub: not offered until the fleet and its parts exist (author decision 31.10.2026); archived
/// in the seed. The logic below stays for the future: restoring the kind and its recipe in the
/// admin panel offers the process again.
/// </para>
/// </remarks>
public sealed class ModuleReplacementHandler : IProcessHandler
{
    private readonly IShipsOnOrbit _ships;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleReplacementHandler"/> class.
    /// </summary>
    /// <param name="ships">The ships on the orbit of the bodies.</param>
    public ModuleReplacementHandler(IShipsOnOrbit ships)
    {
        ArgumentNullException.ThrowIfNull(ships);

        _ships = ships;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.ModuleReplacement;

    /// <inheritdoc />
    public string PartSubject => ProcessPartSubjects.New;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId)
        {
            return [new ProcessReason(ProcessErrorCodes.PartMissing)];
        }

        ShipOnOrbit? ship = await _ships.FindAsync(context.PlayerId, context.PlanetId, cancellationToken).ConfigureAwait(false);

        if (ship is null)
        {
            return [new ProcessReason(ProcessErrorCodes.NoShipOnOrbit)];
        }

        // Every unit replaces one damaged module of the kind the order names.
        if (Damaged(ship, partTypeId).Count() < context.Quantity)
        {
            return [new ProcessReason(ProcessErrorCodes.ModuleNotOnShip)];
        }

        return [];
    }

    /// <inheritdoc />
    public Task<ProcessInput> InputAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The new modules come from the storage of the body.
        return Task.FromResult(new ProcessInput(
            new Dictionary<int, long>(),
            [new ProcessPartInput(context.Request.PartTypeId!.Value, PartCondition.Full, context.Quantity)],
            decimal.One));
    }

    /// <inheritdoc />
    public async Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId)
        {
            return ProcessResult.Empty;
        }

        ShipOnOrbit? ship = await _ships.FindAsync(context.PlayerId, context.PlanetId, cancellationToken).ConfigureAwait(false);
        ShipPart? removed = ship is null ? null : Damaged(ship, partTypeId).FirstOrDefault();

        if (removed is null)
        {
            // The ship left or lost the module while the order waited, so the new module goes
            // back to the storage and nothing is lost.
            PartStackTaken? installed = ProcessHandlerRules.StackOfUnit(ProcessHandlerRules.NamedStacks(context, PartCondition.Full), context.UnitIndex);

            return installed is null
                ? ProcessResult.Empty
                : new ProcessResult(new Dictionary<int, long>(), [new PartStackAdded(installed.PartTypeId, installed.MarkLevel, installed.HasRaceBonus, installed.ConditionPercent, 1)]);
        }

        // The module that comes off goes to the storage as a damaged part, so it is not lost
        // and can be repaired later (task 00014, decision 8).
        return new ProcessResult(
            new Dictionary<int, long>(),
            [new PartStackAdded(removed.PartTypeId, removed.MarkLevel, removed.HasRaceBonus, removed.ConditionPercent, 1)]);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        PartStackTaken? installed = ProcessHandlerRules.StackOfUnit(ProcessHandlerRules.NamedStacks(context, PartCondition.Full), context.UnitIndex);
        PartStackAdded? removed = result.Parts.FirstOrDefault(part => part.ConditionPercent < PartCondition.Full);

        if (installed is null || removed is null)
        {
            return;
        }

        await _ships.ReplaceAsync(
            context.PlayerId,
            context.PlanetId,
            new ShipPart(removed.PartTypeId, removed.MarkLevel, removed.HasRaceBonus, removed.ConditionPercent),
            new ShipPart(installed.PartTypeId, installed.MarkLevel, installed.HasRaceBonus, PartCondition.Full),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The damaged modules of a kind on a ship, the worst one first.
    /// </summary>
    /// <param name="ship">The ship.</param>
    /// <param name="partTypeId">The kind of the module.</param>
    /// <returns>The damaged modules.</returns>
    private static IEnumerable<ShipPart> Damaged(ShipOnOrbit ship, int partTypeId) =>
        ship.Parts
            .Where(part => part.PartTypeId == partTypeId && part.ConditionPercent < PartCondition.Full)
            .OrderBy(part => part.ConditionPercent);
}

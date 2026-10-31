using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the repair of a ship: the workshop on the orbit repairs the hull and every
/// module of the ship with one order (task 00014, decision 8).
/// </summary>
/// <remarks>
/// <para>
/// The cost and the time of the repair are the sum over the parts of the ship, each in
/// proportion to the condition it lost: the cost is the share of the cost of a new part the
/// parameters of the kind give at the level of the workshop. The ship does not fly while the
/// repair runs; the ships arrive with the task of the flights, until then the ship is the
/// stub of <see cref="IShipsOnOrbit"/>.
/// </para>
/// <para>
/// Stub: not offered until the fleet and its parts exist (author decision 31.10.2026); archived
/// in the seed. The logic below stays for the future: restoring the kind and its recipe in the
/// admin panel offers the process again.
/// </para>
/// </remarks>
public sealed class ShipRepairHandler : IProcessHandler
{
    private readonly IShipsOnOrbit _ships;
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipRepairHandler"/> class.
    /// </summary>
    /// <param name="ships">The ships on the orbit of the bodies.</param>
    /// <param name="parts">The storage of the parts of the production module, for the cost of a part.</param>
    public ShipRepairHandler(IShipsOnOrbit ships, PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(parts);

        _ships = ships;
        _parts = parts;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.ShipRepair;

    /// <inheritdoc />
    public string PartSubject => ProcessPartSubjects.None;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // One order repairs the whole ship.
        if (context.Quantity != 1)
        {
            return [new ProcessReason(ProcessErrorCodes.QuantityInvalid)];
        }

        ShipOnOrbit? ship = await _ships.FindAsync(context.PlayerId, context.PlanetId, cancellationToken).ConfigureAwait(false);

        if (ship is null)
        {
            return [new ProcessReason(ProcessErrorCodes.NoShipOnOrbit)];
        }

        if (!ship.Parts.Any(part => part.ConditionPercent < PartCondition.Full))
        {
            return [new ProcessReason(ProcessErrorCodes.NothingToRepair)];
        }

        if (ProcessHandlerRules.BuildingShare(context.Catalog, context.ProcessType.Id, context.BuildingLevel) is null)
        {
            return [new ProcessReason(ProcessErrorCodes.ParamMissing)];
        }

        return [];
    }

    /// <inheritdoc />
    public async Task<ProcessInput> InputAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        ShipOnOrbit ship = await _ships.FindAsync(context.PlayerId, context.PlanetId, cancellationToken).ConfigureAwait(false)
            ?? new ShipOnOrbit([]);
        IReadOnlyDictionary<int, PartFacts> facts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);
        decimal share = ProcessHandlerRules.BuildingShare(context.Catalog, context.ProcessType.Id, context.BuildingLevel) ?? decimal.Zero;

        // The cost and the time are the sum over the parts, each in proportion to the
        // condition it lost.
        Dictionary<int, decimal> cost = [];
        decimal work = decimal.Zero;

        foreach (ShipPart part in ship.Parts.Where(part => part.ConditionPercent < PartCondition.Full))
        {
            decimal lost = ProcessUnits.Lost(part.ConditionPercent, PartCondition.Full);
            work += lost;

            foreach ((int resourceTypeId, decimal amount) in facts.GetValueOrDefault(part.PartTypeId)?.ResourceCost ?? new Dictionary<int, decimal>())
            {
                cost[resourceTypeId] = cost.GetValueOrDefault(resourceTypeId) + (amount * share * lost);
            }
        }

        return new ProcessInput(
            cost.Where(entry => entry.Value > decimal.Zero).ToDictionary(entry => entry.Key, entry => (long)decimal.Ceiling(entry.Value)),
            [],
            work);
    }

    /// <inheritdoc />
    public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        return Task.FromResult(ProcessResult.Empty);
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The hull and every module of the ship get their full condition back.
        return _ships.RepairAsync(context.PlayerId, context.PlanetId, PartCondition.Full, cancellationToken);
    }
}

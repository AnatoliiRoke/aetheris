using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the repair of a module that was taken off a ship: the repair shop of the
/// body gives the damaged part of the storage its full condition back (task 00014,
/// decision 8).
/// </summary>
/// <remarks>
/// <para>
/// The order takes the damaged parts from the storage when it enters the queue, with the cost
/// of the repair: the share of the cost of a new part the parameters of the kind give at the
/// level of the shop, in proportion to the condition the part lost. The time of a unit is in
/// the same proportion. A unit puts the part back with its full condition.
/// </para>
/// <para>
/// Stub: not offered until the fleet and its parts exist (author decision 31.10.2026); archived
/// in the seed. The logic below stays for the future: restoring the kind and its recipe in the
/// admin panel offers the process again.
/// </para>
/// </remarks>
public sealed class ModuleRepairHandler : IProcessHandler
{
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleRepairHandler"/> class.
    /// </summary>
    /// <param name="parts">The storage of the parts of the production module.</param>
    public ModuleRepairHandler(PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        _parts = parts;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.ModuleRepair;

    /// <inheritdoc />
    public string PartSubject => ProcessPartSubjects.Damaged;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId
            || !(await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false)).ContainsKey(partTypeId))
        {
            return [new ProcessReason(ProcessErrorCodes.PartMissing)];
        }

        if (context.Request.PartConditionPercent is not (>= 0 and < PartCondition.Full))
        {
            return [new ProcessReason(ProcessErrorCodes.PartNotDamaged)];
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

        int partTypeId = context.Request.PartTypeId!.Value;
        int condition = context.Request.PartConditionPercent!.Value;
        decimal lost = ProcessUnits.Lost(condition, PartCondition.Full);
        decimal share = ProcessHandlerRules.BuildingShare(context.Catalog, context.ProcessType.Id, context.BuildingLevel) ?? decimal.Zero;
        PartFacts? facts = (await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false)).GetValueOrDefault(partTypeId);

        // The cost is the share of the cost of a new part for every part of the batch, in
        // proportion to the condition the part lost.
        Dictionary<int, long> cost = [];

        foreach ((int resourceTypeId, decimal amount) in facts?.ResourceCost ?? new Dictionary<int, decimal>())
        {
            long total = (long)decimal.Ceiling(amount * share * lost * context.Quantity);

            if (total > 0)
            {
                cost[resourceTypeId] = total;
            }
        }

        return new ProcessInput(cost, [new ProcessPartInput(partTypeId, condition, context.Quantity)], lost);
    }

    /// <inheritdoc />
    public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        if (context.Request.PartConditionPercent is not { } condition
            || ProcessHandlerRules.StackOfUnit(ProcessHandlerRules.NamedStacks(context, condition), context.UnitIndex) is not { } stack)
        {
            return Task.FromResult(ProcessResult.Empty);
        }

        // The repaired part goes back to the storage with its mark and the bonus of its race.
        return Task.FromResult(new ProcessResult(
            new Dictionary<int, long>(),
            [new PartStackAdded(stack.PartTypeId, stack.MarkLevel, stack.HasRaceBonus, PartCondition.Full, 1)]));
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The engine puts the repaired part into the storage, so nothing else changes.
        return Task.CompletedTask;
    }
}

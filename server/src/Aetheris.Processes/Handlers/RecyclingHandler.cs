using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the recycling: the factory takes the parts of the storage apart and returns
/// a share of their resources (task 00014, decision 3 and section 11).
/// </summary>
/// <remarks>
/// <para>
/// The order takes the parts it names from the storage when it enters the queue. A unit
/// returns the share of the cost of a part by the recipe that makes it, the produced
/// materials included; the share comes from the parameters of the kind by the level of the
/// factory and is multiplied by the condition of the part (task 00014, decision 7). There are
/// no bonuses of the artifacts.
/// </para>
/// <para>
/// A part or a module has no crew, so taking it apart returns none: the crew comes aboard only
/// when a ship is assembled, and the recycling of a whole ship is a process of its own that this
/// task does not hold (task 00014, section 11). A part that is made of other parts is taken
/// apart alone: the parts inside it stay in the storage as items of their own, and only the
/// resources of its own recipe come back.
/// </para>
/// </remarks>
public sealed class RecyclingHandler : IProcessHandler
{
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecyclingHandler"/> class.
    /// </summary>
    /// <param name="parts">The storage of the parts of the production module.</param>
    public RecyclingHandler(PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        _parts = parts;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.Recycling;

    /// <inheritdoc />
    public string PartSubject => ProcessPartSubjects.Any;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId
            || !(await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false)).ContainsKey(partTypeId))
        {
            return [new ProcessReason(ProcessErrorCodes.PartMissing)];
        }

        if (context.Request.PartConditionPercent is < 0 or > PartCondition.Full)
        {
            return [new ProcessReason(ProcessErrorCodes.PartMissing)];
        }

        if (ProcessHandlerRules.BuildingShare(context.Catalog, context.ProcessType.Id, context.BuildingLevel) is null)
        {
            return [new ProcessReason(ProcessErrorCodes.ParamMissing)];
        }

        return [];
    }

    /// <inheritdoc />
    public Task<ProcessInput> InputAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The parts are taken from the storage for the whole batch when the order enters the
        // queue, in the condition the player named.
        return Task.FromResult(new ProcessInput(
            new Dictionary<int, long>(),
            [new ProcessPartInput(context.Request.PartTypeId!.Value, Condition(context.Request), context.Quantity)],
            decimal.One));
    }

    /// <inheritdoc />
    public async Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId
            || !(await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false)).TryGetValue(partTypeId, out PartFacts? facts))
        {
            return ProcessResult.Empty;
        }

        decimal share = ProcessHandlerRules.BuildingShare(context.Catalog, context.ProcessType.Id, context.BuildingLevel) ?? decimal.Zero;
        List<PartStackTaken> stacks = [.. ProcessHandlerRules.NamedStacks(context, Condition(context.Request))];

        // The return of the whole batch is counted first and split into its units, so the
        // rounding of one unit is never lost.
        Dictionary<int, long> resources = [];

        foreach ((int resourceTypeId, decimal cost) in facts.ResourceCost)
        {
            decimal total = stacks.Sum(stack => stack.Quantity * cost * share * ProcessUnits.Kept(stack.ConditionPercent, PartCondition.Full));
            long amount = ProcessUnits.Portion(total, context.Quantity, context.UnitIndex);

            if (amount > 0)
            {
                resources[resourceTypeId] = amount;
            }
        }

        return new ProcessResult(resources, []);
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The parts were taken from the storage when the order entered the queue, so the
        // handler changes nothing else when a unit is ready.
        return Task.CompletedTask;
    }

    /// <summary>
    /// The condition of the parts the order names: a new part when the player named none.
    /// </summary>
    /// <param name="request">What the player asked for.</param>
    /// <returns>The condition, in per cent.</returns>
    private static int Condition(ProcessOrderRequest request) => request.PartConditionPercent ?? PartCondition.Full;
}

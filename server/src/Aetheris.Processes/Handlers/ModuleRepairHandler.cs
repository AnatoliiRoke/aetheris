using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the repair of a module that was taken off a ship: the repair shop gives the
/// damaged part its full condition back (task 00014, decision 8).
/// </summary>
/// <remarks>
/// The cost and the time of the repair are proportional to the condition the part lost. The
/// part stays on the body, only its condition changes.
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
    public Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        List<ProcessReason> reasons = [];

        if (context.Request.PartTypeId is null)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.PartMissing));
        }

        return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
    }

    /// <inheritdoc />
    public async Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId)
        {
            return ProcessResult.Empty;
        }

        IReadOnlyList<PartStackInfo> stacks = await _parts.ReadAsync(
            context.PlanetId,
            [partTypeId],
            cancellationToken).ConfigureAwait(false);

        // The cost of the repair is proportional to the condition the part lost.
        Dictionary<int, long> resources = [];

        foreach (PartStackInfo stack in stacks)
        {
            if (stack.ConditionPercent >= PartCondition.Full)
            {
                continue;
            }

            decimal lost = (ProcessNumbers.FullConditionPercent - stack.ConditionPercent) / ProcessNumbers.PercentDivisor;

            foreach (ProcessIngredientInfo ingredient in context.Recipe.IngredientIds)
            {
                if (ingredient.ResourceTypeId is not { } resourceTypeId)
                {
                    continue;
                }

                long amount = (long)decimal.Floor(ingredient.Amount * lost * stack.Quantity);

                if (amount > 0)
                {
                    resources[resourceTypeId] = resources.GetValueOrDefault(resourceTypeId) + amount;
                }
            }
        }

        return new ProcessResult(resources, []);
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The condition of the part is changed by the engine when the order is made: the
        // damaged part is taken from the storage and the new one takes its place.
        return Task.CompletedTask;
    }
}

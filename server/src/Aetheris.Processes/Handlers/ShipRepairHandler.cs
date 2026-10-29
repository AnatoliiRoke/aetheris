using Aetheris.Processes.Services;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the repair of a ship: the workshop repairs the hull and every module of
/// the ship with one order (task 00014, decision 8).
/// </summary>
/// <remarks>
/// The cost and the time of the repair are the sum of the parts of the ship, proportional to
/// the condition it lost. The ship does not fly while the repair runs.
/// </remarks>
public sealed class ShipRepairHandler : IProcessHandler
{
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipRepairHandler"/> class.
    /// </summary>
    /// <param name="parts">The storage of the parts of the production module.</param>
    public ShipRepairHandler(PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        _parts = parts;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.ShipRepair;

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The ship stands on the orbit of the body; the real ship arrives with the task of
        // the flights, so the check looks at the stub of the recipe.
        if (string.IsNullOrEmpty(context.Request.ShipStub) && string.IsNullOrEmpty(context.Recipe.ShipStub))
        {
            return Task.FromResult<IReadOnlyList<ProcessReason>>([new ProcessReason(ProcessErrorCodes.NoShipOnOrbit)]);
        }

        return Task.FromResult<IReadOnlyList<ProcessReason>>([]);
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

        // The repair costs the resources of the parts that are damaged; the cost is
        // proportional to the condition the part lost.
        Dictionary<int, long> resources = [];

        foreach (PartStackInfo stack in stacks)
        {
            if (stack.ConditionPercent >= ProcessNumbers.FullConditionPercent)
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

        // The condition of the parts of the ship is changed by the module of the ships, which
        // does not exist yet; the handler leaves the parts as they are.
        return Task.CompletedTask;
    }
}

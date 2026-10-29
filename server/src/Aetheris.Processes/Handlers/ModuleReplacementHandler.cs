using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the replacement of a module: the damaged module comes off the ship and a
/// new one from the storage takes its place (task 00014, decision 8).
/// </summary>
/// <remarks>
/// The replacement is quicker than the repair of the whole ship. The module that comes off
/// goes to the storage as a damaged part, so it can be repaired later.
/// </remarks>
public sealed class ModuleReplacementHandler : IProcessHandler
{
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleReplacementHandler"/> class.
    /// </summary>
    /// <param name="parts">The storage of the parts of the production module.</param>
    public ModuleReplacementHandler(PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        _parts = parts;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.ModuleReplacement;

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

        if (string.IsNullOrEmpty(context.Request.ShipStub) && string.IsNullOrEmpty(context.Recipe.ShipStub))
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.NoShipOnOrbit));
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

        // The new module comes from the storage, so the cost of the order is the part of the
        // recipe that names it.
        Dictionary<int, long> resources = [];

        foreach (PartStackInfo stack in stacks)
        {
            foreach (ProcessIngredientInfo ingredient in context.Recipe.IngredientIds)
            {
                if (ingredient.ResourceTypeId is not { } resourceTypeId)
                {
                    continue;
                }

                long amount = ingredient.Amount * stack.Quantity;

                if (amount > 0)
                {
                    resources[resourceTypeId] = resources.GetValueOrDefault(resourceTypeId) + amount;
                }
            }
        }

        return new ProcessResult(resources, []);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.PartTypeId is not { } partTypeId)
        {
            return;
        }

        // The module that comes off the ship goes to the storage as a damaged part, so it
        // is not lost and can be repaired later (task 00014, decision 8).
        IReadOnlyList<PartStackInfo> stacks = await _parts.ReadAsync(
            context.PlanetId,
            [partTypeId],
            cancellationToken).ConfigureAwait(false);

        PartStackInfo? damaged = stacks.FirstOrDefault(stack => stack.ConditionPercent < PartCondition.Full);

        if (damaged is null)
        {
            return;
        }

        Dictionary<(int PartTypeId, bool HasRaceBonus), long> parts = new()
        {
            [(damaged.PartTypeId, damaged.HasRaceBonus)] = damaged.Quantity,
        };

        await _parts.GiveBackAsync(context.PlanetId, parts, damaged.ConditionPercent, cancellationToken).ConfigureAwait(false);
    }
}

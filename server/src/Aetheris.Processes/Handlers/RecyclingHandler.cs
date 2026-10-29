using Aetheris.Processes.Services;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the recycling: the factory takes a part apart and returns a share of its
/// resources (task 00014, decision 3).
/// </summary>
/// <remarks>
/// <para>
/// The share comes from the parameters of the kind of the process and grows with the level of
/// the factory. The share of a part is multiplied by its condition, so a damaged part gives
/// less.
/// </para>
/// <para>
/// The crew of the part comes back in full and waits in the state of the population; the
/// population task settles it. There are no bonuses of the artifacts.
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

        if (stacks.Count == 0)
        {
            return ProcessResult.Empty;
        }

        // The share of the factory comes from the parameters of the kind of the process: the
        // base share grows with the level of the factory.
        decimal share = Share(context);

        Dictionary<int, long> resources = [];
        long crew = 0;

        foreach (PartStackInfo stack in stacks)
        {
            // The share of a part is multiplied by its condition, so a damaged part gives
            // less (task 00014, decision 7).
            decimal condition = stack.ConditionPercent / ProcessNumbers.PercentDivisor;
            decimal partShare = share * condition;

            foreach (ProcessIngredientInfo ingredient in context.Recipe.IngredientIds)
            {
                if (ingredient.ResourceTypeId is not { } resourceTypeId)
                {
                    continue;
                }

                long amount = (long)decimal.Floor(ingredient.Amount * partShare * stack.Quantity);

                if (amount > 0)
                {
                    resources[resourceTypeId] = resources.GetValueOrDefault(resourceTypeId) + amount;
                }
            }
        }

        // The crew of the part comes back in full (task 00014, decision 3).
        crew = Crew(context);

        return new ProcessResult(resources, [], crew);
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The part is taken from the storage by the engine, so the handler changes nothing
        // else when the process finishes.
        return Task.CompletedTask;
    }

    /// <summary>
    /// The share of the resources the factory returns, by the level of the factory.
    /// </summary>
    /// <param name="context">The order and the catalog.</param>
    /// <returns>The share, a number between zero and one.</returns>
    private static decimal Share(ProcessApplyContext context)
    {
        ProcessParamInfo? scaling = context.Catalog
            .ParamsOfSource(context.ProcessType.Id, ProcessLevelSources.Building, null)
            .FirstOrDefault(parameter => string.Equals(parameter.ParamKind, ProcessParamKinds.Scaling, StringComparison.Ordinal));

        if (scaling is null)
        {
            return 0m;
        }

        // The share grows with the level of the factory: every level above the first one adds
        // the same step to the base share.
        int levelsAbove = Math.Max(context.BuildingLevel - scaling.Level, 0);
        decimal share = scaling.Value + (scaling.Value * levelsAbove * ProcessNumbers.ShareStep);

        return Math.Clamp(share, 0m, 1m);
    }

    /// <summary>
    /// The crew that comes back with the parts.
    /// </summary>
    /// <param name="context">The order and the catalog.</param>
    /// <returns>The crew.</returns>
    private static long Crew(ProcessApplyContext context)
    {
        long crew = 0;

        foreach (ProcessIngredientInfo ingredient in context.Recipe.IngredientIds)
        {
            if (ingredient.PartTypeId is not { } partTypeId)
            {
                continue;
            }

            // The crew of a part is the attribute of the part; the part itself is not known
            // to the handler, so the ingredient carries the number of the crew.
            _ = partTypeId;

            crew += ingredient.Amount;
        }

        return crew;
    }
}

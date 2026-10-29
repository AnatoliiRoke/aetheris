using Aetheris.Processes.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the conversion of matter: one raw material becomes another of the same
/// group (task 00014, decision 4).
/// </summary>
/// <remarks>
/// The groups and the exceptions are in the data, so the author decides which resources may
/// be converted: the ether and the produced goods are not converted. The percentage of the
/// output comes from the level of the technology, and the level of the building only makes
/// the process faster.
/// </remarks>
public sealed class MatterConversionHandler : IProcessHandler
{
    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.MatterConversion;

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        List<ProcessReason> reasons = [];

        if (context.Request.SourceResourceTypeId is null || context.Request.TargetResourceTypeId is null)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.SourceGroupMismatch));

            return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
        }

        if (context.Request.SourceResourceTypeId == context.Request.TargetResourceTypeId)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.SameSourceAndTarget));

            return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
        }

        // The recipe names the group of the source and the group of the target, so the
        // exception of the ether and the produced goods lives in the data.
        if (context.Recipe.SourceGroupId is not { } sourceGroupId
            || context.Recipe.TargetGroupId is not { } targetGroupId)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.SourceGroupMismatch));

            return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
        }

        // The source must be in the group the recipe takes, so a raw material of another
        // group is refused before anything is spent.
        if (!context.ResourceGroups.TryGetValue(context.Request.SourceResourceTypeId!.Value, out int sourceGroup)
            || sourceGroup != sourceGroupId)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.SourceGroupMismatch));

            return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
        }

        // The target must be in the group the recipe gives, so the conversion cannot make
        // a raw material of another group.
        if (!context.ResourceGroups.TryGetValue(context.Request.TargetResourceTypeId!.Value, out int targetGroup)
            || targetGroup != targetGroupId)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.SourceGroupMismatch));

            return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
        }

        return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
    }

    /// <inheritdoc />
    public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        if (context.Request.SourceResourceTypeId is not { } sourceResourceTypeId
            || context.Request.TargetResourceTypeId is not { } targetResourceTypeId)
        {
            return Task.FromResult(ProcessResult.Empty);
        }

        // The percentage of the output comes from the level of the technology, so the
        // technology the player learned decides how much of the raw material is left.
        decimal percentage = Percentage(context);

        if (percentage <= 0m)
        {
            return Task.FromResult(ProcessResult.Empty);
        }

        Dictionary<int, long> resources = [];

        foreach (ProcessIngredientInfo ingredient in context.Recipe.IngredientIds)
        {
            if (ingredient.ResourceTypeId is not { } resourceTypeId)
            {
                continue;
            }

            long amount = (long)decimal.Floor(ingredient.Amount * percentage * context.Quantity);

            if (amount > 0)
            {
                resources[targetResourceTypeId] = resources.GetValueOrDefault(targetResourceTypeId) + amount;
            }
        }

        _ = sourceResourceTypeId;

        return Task.FromResult(new ProcessResult(resources, []));
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The result of the conversion goes to the balance of the body, so the engine does
        // the change and the handler has nothing more to do.
        return Task.CompletedTask;
    }

    /// <summary>
    /// The percentage of the raw material that is left after the conversion, by the level of
    /// the technology.
    /// </summary>
    /// <param name="context">The order and the catalog.</param>
    /// <returns>The percentage, a number between zero and one.</returns>
    private static decimal Percentage(ProcessApplyContext context)
    {
        ProcessParamInfo? parameter = context.Catalog
            .ParamsOfSource(context.ProcessType.Id, ProcessLevelSources.Technology, null)
            .FirstOrDefault(entry => string.Equals(entry.ParamKind, ProcessParamKinds.ValueByLevel, StringComparison.Ordinal));

        if (parameter is null)
        {
            return 0m;
        }

        // The technology the recipe needs has its own level; the higher the level, the more
        // of the raw material is left.
        int level = context.TechnologyLevels
            .Where(entry => entry.Key == parameter.LevelTargetId)
            .Select(entry => entry.Value)
            .FirstOrDefault();

        if (level <= 0)
        {
            return 0m;
        }

        int levelsAbove = Math.Max(level - parameter.Level, 0);
        decimal step = parameter.ValueMax is { } maximum
            ? (maximum - parameter.Value) / ProcessNumbers.ConversionLevels
            : parameter.Value * ProcessNumbers.ConversionStep;

        decimal percentage = parameter.Value + (step * levelsAbove);

        return Math.Clamp(percentage, 0m, 1m);
    }
}

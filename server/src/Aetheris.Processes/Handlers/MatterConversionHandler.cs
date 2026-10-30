using Aetheris.Processes.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the conversion of matter: one raw material becomes another of the same
/// group (task 00014, decision 4).
/// </summary>
/// <remarks>
/// The groups and the exceptions are in the data, so the author decides which resources may
/// be converted: the ether and the produced goods are not in a group a recipe converts. The
/// order takes the source for the whole batch, and the output is the source times the
/// percentage of the level of the technology the parameters of the kind name; the level of
/// the building only makes the process faster.
/// </remarks>
public sealed class MatterConversionHandler : IProcessHandler
{
    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.MatterConversion;

    /// <inheritdoc />
    public string PartSubject => ProcessPartSubjects.None;

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        return Task.FromResult<IReadOnlyList<ProcessReason>>(Check(context));
    }

    /// <inheritdoc />
    public Task<ProcessInput> InputAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The source is paid for the whole batch when the order enters the queue.
        return Task.FromResult(new ProcessInput(
            new Dictionary<int, long> { [context.Request.SourceResourceTypeId!.Value] = context.Quantity },
            [],
            decimal.One));
    }

    /// <inheritdoc />
    public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        if (context.Request.TargetResourceTypeId is not { } targetResourceTypeId)
        {
            return Task.FromResult(ProcessResult.Empty);
        }

        // Every unit is one unit of the source; the units together give the source of the
        // batch times the percentage, so the rounding of one unit is never lost.
        decimal percentage = Percentage(context.Catalog, context.ProcessType.Id, context.TechnologyLevels);
        long amount = ProcessUnits.Portion(context.Quantity * percentage, context.Quantity, context.UnitIndex);

        return Task.FromResult(amount > 0
            ? new ProcessResult(new Dictionary<int, long> { [targetResourceTypeId] = amount }, [])
            : ProcessResult.Empty);
    }

    /// <inheritdoc />
    public Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        // The result of the conversion goes to the balance of the body, so the engine does
        // the change and the handler has nothing more to do.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Checks the source and the target of the conversion and the level of the technology.
    /// </summary>
    /// <param name="context">The order.</param>
    /// <returns>The reasons the order is refused.</returns>
    private static List<ProcessReason> Check(ProcessCheckContext context)
    {
        if (context.Request.SourceResourceTypeId is not { } sourceId
            || context.Request.TargetResourceTypeId is not { } targetId)
        {
            return [new ProcessReason(ProcessErrorCodes.SourceGroupMismatch)];
        }

        if (sourceId == targetId)
        {
            return [new ProcessReason(ProcessErrorCodes.SameSourceAndTarget)];
        }

        // The recipe names the group of the source and the group of the target, so a raw
        // material of another group, the ether or a produced good is refused before anything
        // is spent.
        if (context.Recipe.SourceGroupId is not { } sourceGroupId
            || !context.ResourceGroups.TryGetValue(sourceId, out int sourceGroup)
            || sourceGroup != sourceGroupId)
        {
            return [new ProcessReason(ProcessErrorCodes.SourceGroupMismatch)];
        }

        if (context.Recipe.TargetGroupId is not { } targetGroupId
            || !context.ResourceGroups.TryGetValue(targetId, out int targetGroup)
            || targetGroup != targetGroupId)
        {
            return [new ProcessReason(ProcessErrorCodes.TargetGroupMismatch)];
        }

        // Without the technology the conversion would only burn the source, so the order is
        // refused and says which technology it waits for.
        if (Percentage(context.Catalog, context.ProcessType.Id, context.RequirementContext.TechnologyLevels) <= decimal.Zero)
        {
            int? technologyId = context.Catalog.TargetOf(context.ProcessType.Id, ProcessParamKinds.ValueByLevel, ProcessLevelSources.Technology);

            if (technologyId is null)
            {
                return [new ProcessReason(ProcessErrorCodes.ParamMissing)];
            }

            return
            [
                new ProcessReason(
                    ProcessErrorCodes.RequirementTechnology,
                    technologyId.Value,
                    FirstLevel(context.Catalog, context.ProcessType.Id, technologyId.Value),
                    context.RequirementContext.TechnologyNameKeys.GetValueOrDefault(technologyId.Value)),
            ];
        }

        return [];
    }

    /// <summary>
    /// The percentage of the raw material that is left after the conversion, by the level of
    /// the technology the parameters of the kind name.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <param name="technologyLevels">The levels of the technologies of the player.</param>
    /// <returns>The percentage, a number between zero and one.</returns>
    private static decimal Percentage(ProcessCatalog catalog, int processTypeId, IReadOnlyDictionary<int, int> technologyLevels)
    {
        if (catalog.TargetOf(processTypeId, ProcessParamKinds.ValueByLevel, ProcessLevelSources.Technology) is not { } technologyId)
        {
            return decimal.Zero;
        }

        ProcessParamInfo? step = catalog.ParamAt(
            processTypeId,
            ProcessParamKinds.ValueByLevel,
            ProcessLevelSources.Technology,
            technologyId,
            technologyLevels.GetValueOrDefault(technologyId));

        return step is null ? decimal.Zero : Math.Clamp(step.Value, decimal.Zero, decimal.One);
    }

    /// <summary>
    /// The first level of the technology that gives an output.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <param name="technologyId">The technology.</param>
    /// <returns>The level.</returns>
    private static int FirstLevel(ProcessCatalog catalog, int processTypeId, int technologyId) =>
        catalog.Parameters
            .Where(parameter => !parameter.IsArchived
                && parameter.ProcessTypeId == processTypeId
                && parameter.LevelTargetId == technologyId
                && string.Equals(parameter.ParamKind, ProcessParamKinds.ValueByLevel, StringComparison.Ordinal)
                && string.Equals(parameter.LevelSource, ProcessLevelSources.Technology, StringComparison.Ordinal)
                && parameter.Value > decimal.Zero)
            .Select(parameter => parameter.Level)
            .DefaultIfEmpty()
            .Min();
}

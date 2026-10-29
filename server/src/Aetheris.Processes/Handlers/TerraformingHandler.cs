using Aetheris.Processes.Services;
using Aetheris.World;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the terraforming: a device on the orbit of a body makes the deposits of the
/// body rich (task 00014, decision 2).
/// </summary>
/// <remarks>
/// The device is one shot: it is spent when the process finishes, and it is lost when the
/// order is cancelled, so the kind of the process does not refund its input. The type of the
/// body, its size and its buildings do not change; only the coefficient of the deposits does.
/// </remarks>
public sealed class TerraformingHandler : IProcessHandler
{
    /// <summary>
    /// The reason why the deposits of a body become rich, in the journal of the world.
    /// </summary>
    public const string Reason = "terraforming";

    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerraformingHandler"/> class.
    /// </summary>
    /// <param name="world">The world module, for the deposits of a body.</param>
    public TerraformingHandler(IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.Terraforming;

    /// <inheritdoc />
    public Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        List<ProcessReason> reasons = [];

        // The process runs on the colony of the player: the body must belong to them.
        if (context.PlanetId <= 0)
        {
            reasons.Add(new ProcessReason(ProcessErrorCodes.PlanetUnknown));
        }

        return Task.FromResult<IReadOnlyList<ProcessReason>>(reasons);
    }

    /// <inheritdoc />
    public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        return Task.FromResult(ProcessResult.Empty);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The range of the richness comes from the parameters of the kind of the process, so
        // the author changes it in the admin panel and not in the code.
        ProcessParamInfo? range = context.Catalog
            .ParamsOfSource(context.ProcessType.Id, ProcessLevelSources.Building, null)
            .FirstOrDefault(parameter => string.Equals(parameter.ParamKind, ProcessParamKinds.Range, StringComparison.Ordinal));

        if (range is null)
        {
            return;
        }

        // The coefficient of the deposits of a rich body is the middle of the range, so every
        // deposit of the body has the same value.
        decimal coefficient = range.ValueMax is { } maximum
            ? (range.Value + maximum) / ProcessNumbers.RangeDivisor
            : range.Value;

        await _world.SetDepositCoefficientAsync(
            context.PlanetId,
            coefficient,
            context.PlayerId,
            Reason,
            cancellationToken).ConfigureAwait(false);
    }
}

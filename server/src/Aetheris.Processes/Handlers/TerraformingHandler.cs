using Aetheris.Processes.Services;
using Aetheris.Production.Services;
using Aetheris.World;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The handler of the terraforming: a ship on the orbit turns a device on, and the deposits of
/// the body become rich (task 00014, decision 2).
/// </summary>
/// <remarks>
/// <para>
/// The device is a part the recipe takes from the storage of the body, so the engine pays it
/// when the order enters the queue. It is one shot: it is spent when the process finishes,
/// and it is lost when the order is cancelled, since the kind of the process does not refund
/// its input.
/// </para>
/// <para>
/// The multiplier of the deposits comes from the parameters of the kind by the mark level of
/// the device and must lie in the rich range the parameters name. The type of the body, its
/// size and its buildings do not change; only the coefficients of the deposits do.
/// </para>
/// </remarks>
public sealed class TerraformingHandler : IProcessHandler
{
    /// <summary>
    /// The reason why the deposits of a body become rich, in the journal of the world.
    /// </summary>
    public const string Reason = "terraforming";

    private readonly IWorldModule _world;
    private readonly IShipsOnOrbit _ships;
    private readonly PartStockService _parts;

    /// <summary>
    /// Initializes a new instance of the <see cref="TerraformingHandler"/> class.
    /// </summary>
    /// <param name="world">The world module, for the deposits of a body.</param>
    /// <param name="ships">The ships on the orbit of the bodies.</param>
    /// <param name="parts">The storage of the parts of the production module.</param>
    public TerraformingHandler(IWorldModule world, IShipsOnOrbit ships, PartStockService parts)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(parts);

        _world = world;
        _ships = ships;
        _parts = parts;
    }

    /// <inheritdoc />
    public string HandlerCode => ProcessHandlerCodes.Terraforming;

    /// <inheritdoc />
    public string PartSubject => ProcessPartSubjects.None;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // One device makes one body rich once.
        if (context.Quantity != 1)
        {
            return [new ProcessReason(ProcessErrorCodes.QuantityInvalid)];
        }

        // The ship that turns the device on stands on the orbit of the body.
        if (await _ships.FindAsync(context.PlayerId, context.PlanetId, cancellationToken).ConfigureAwait(false) is null)
        {
            return [new ProcessReason(ProcessErrorCodes.NoShipOnOrbit)];
        }

        if (Device(context.Recipe) is not { } deviceId
            || !(await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false)).TryGetValue(deviceId, out PartFacts? device))
        {
            return [new ProcessReason(ProcessErrorCodes.DeviceMissing)];
        }

        if (Coefficient(context.Catalog, context.ProcessType.Id, device.MarkLevel) is null)
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

        // The device is an ingredient of the recipe, so the engine takes it with the rest of
        // the ingredients.
        return Task.FromResult(ProcessInput.None);
    }

    /// <inheritdoc />
    public Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = cancellationToken;

        return Task.FromResult(ProcessResult.Empty);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        int? deviceId = Device(context.Recipe);
        PartStackTaken? device = context.TakenParts.FirstOrDefault(stack => stack.PartTypeId == deviceId);

        if (device is null
            || Coefficient(context.Catalog, context.ProcessType.Id, device.MarkLevel) is not { } coefficient)
        {
            return;
        }

        await _world.SetDepositCoefficientAsync(
            context.PlanetId,
            coefficient,
            context.PlayerId,
            Reason,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The device of the recipe: the first part it takes.
    /// </summary>
    /// <param name="recipe">The recipe.</param>
    /// <returns>The device, or <see langword="null"/> when the recipe takes no part.</returns>
    private static int? Device(ProcessRecipeInfo recipe) =>
        recipe.IngredientIds.FirstOrDefault(ingredient => ingredient.PartTypeId is not null)?.PartTypeId;

    /// <summary>
    /// The multiplier of the deposits a device of a mark level gives, when it lies in the rich
    /// range: above the lowest value of the range and not above the highest one.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <param name="markLevel">The mark level of the device.</param>
    /// <returns>The multiplier, or <see langword="null"/> when the data hold none in the range.</returns>
    private static decimal? Coefficient(ProcessCatalog catalog, int processTypeId, int markLevel)
    {
        ProcessParamInfo? value = catalog.ParamAt(processTypeId, ProcessParamKinds.ValueByLevel, ProcessLevelSources.Part, null, markLevel);
        ProcessParamInfo? range = catalog.ParamAt(processTypeId, ProcessParamKinds.Range, ProcessLevelSources.Part, null, markLevel);

        if (value is null || range?.ValueMax is not { } highest)
        {
            return null;
        }

        return value.Value > range.Value && value.Value <= highest ? value.Value : null;
    }
}

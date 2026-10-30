using Aetheris.Processes.Services;
using Aetheris.Production.Services;

namespace Aetheris.Processes.Handlers;

/// <summary>
/// The rules the handlers of the parts share: the share of the building and the part a unit
/// works on (task 00014).
/// </summary>
internal static class ProcessHandlerRules
{
    /// <summary>
    /// The share the building gives at its level: the return of the recycling or the cost of a
    /// repair as a part of the cost of a new part.
    /// </summary>
    /// <param name="catalog">The catalog of the processes.</param>
    /// <param name="processTypeId">The kind of the process.</param>
    /// <param name="buildingLevel">The level of the building the process runs in.</param>
    /// <returns>The share, or <see langword="null"/> when the data hold no step for the level.</returns>
    public static decimal? BuildingShare(ProcessCatalog catalog, int processTypeId, int buildingLevel) =>
        catalog.ParamAt(processTypeId, ProcessParamKinds.ValueByLevel, ProcessLevelSources.Building, null, buildingLevel) is { } step
            ? Math.Clamp(step.Value, decimal.Zero, decimal.One)
            : null;

    /// <summary>
    /// The stack a unit of the order works on: the first units take the first stacks, as the
    /// order took them.
    /// </summary>
    /// <param name="stacks">The stacks the order took, in their order.</param>
    /// <param name="unitIndex">The unit, counted from one.</param>
    /// <returns>The stack, or <see langword="null"/> when the order took fewer parts.</returns>
    public static PartStackTaken? StackOfUnit(IEnumerable<PartStackTaken> stacks, long unitIndex)
    {
        long passed = 0;

        foreach (PartStackTaken stack in stacks)
        {
            passed += stack.Quantity;

            if (unitIndex <= passed)
            {
                return stack;
            }
        }

        return null;
    }

    /// <summary>
    /// The stacks of the part the order names, among everything the order took.
    /// </summary>
    /// <param name="context">The order.</param>
    /// <param name="conditionPercent">The condition of the part the order names.</param>
    /// <returns>The stacks.</returns>
    public static IEnumerable<PartStackTaken> NamedStacks(ProcessApplyContext context, int conditionPercent) =>
        context.TakenParts.Where(stack => stack.PartTypeId == context.Request.PartTypeId && stack.ConditionPercent == conditionPercent);
}

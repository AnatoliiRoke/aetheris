namespace Aetheris.Processes.Services;

/// <summary>
/// The handler of a kind of process: it checks what the order needs, computes its result
/// and applies it (task 00014, decision 6).
/// </summary>
/// <remarks>
/// <para>
/// The engine of the processes knows no concrete process. A kind of a process names its
/// handler by a logical code, and the engine looks the handler up in the container of
/// dependencies by that code. A new process is therefore a class of a handler and rows in
/// the tables, without a change of the engine and without a change of the client.
/// </para>
/// <para>
/// The three steps of the handler are called inside the transaction of the order: the check
/// before the cost is paid, the result when the unit is ready, and the refund when the order
/// is cancelled.
/// </para>
/// </remarks>
public interface IProcessHandler
{
    /// <summary>
    /// Gets the logical code of the handler. The kind of a process names its handler by this
    /// code.
    /// </summary>
    string HandlerCode { get; }

    /// <summary>
    /// Checks what the order needs before its cost is paid: the goals of the order, the
    /// resources and the parts it takes, the levels it needs.
    /// </summary>
    /// <param name="context">The order and everything the check looks at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The reasons the order is refused, empty when it is allowed.</returns>
    Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Computes what one unit of the order gives.
    /// </summary>
    /// <param name="context">The order and everything the result looks at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The result of one unit of the order.</returns>
    Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the result of one unit of the order in the transaction of the order.
    /// </summary>
    /// <param name="context">The order, its result and everything the change looks at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the result is applied.</returns>
    Task ApplyAsync(ProcessApplyContext context, CancellationToken cancellationToken);
}

/// <summary>
/// A reason why an order of a process is refused, sent to the client as a localisation key
/// (task 00014).
/// </summary>
/// <param name="Code">The code of the reason.</param>
/// <param name="TargetId">What is required, when the reason names it.</param>
/// <param name="TargetLevel">The level that is required.</param>
/// <param name="TargetNameKey">The key of the name of what is required.</param>
public sealed record ProcessReason(string Code, int TargetId = 0, int TargetLevel = 0, string? TargetNameKey = null);

/// <summary>
/// What one unit of an order of a process gives: the resources and the parts that go to the
/// body, and the changes the handler makes on its own (task 00014).
/// </summary>
/// <param name="Resources">The resources that go to the balance of the body.</param>
/// <param name="Parts">The parts that go to the storage of the body.</param>
/// <param name="Crew">The crew that came back and waits to be settled by the population task.</param>
/// <param name="EndsProcess">Whether the unit ends the whole order, for the processes that make one thing at a time.</param>
public sealed record ProcessResult(
    IReadOnlyDictionary<int, long> Resources,
    IReadOnlyList<ProcessPartResult> Parts,
    long Crew = 0,
    bool EndsProcess = false)
{
    /// <summary>
    /// Gets a result with nothing in it.
    /// </summary>
    public static ProcessResult Empty { get; } = new(
        new Dictionary<int, long>(),
        []);
}

/// <summary>
/// A part that goes to the storage of the body: the part and how many of them.
/// </summary>
/// <param name="PartTypeId">The part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="ConditionPercent">The condition of the part in per cent.</param>
/// <param name="Quantity">How many parts.</param>
public sealed record ProcessPartResult(
    int PartTypeId,
    int MarkLevel,
    bool HasRaceBonus,
    int ConditionPercent,
    long Quantity);

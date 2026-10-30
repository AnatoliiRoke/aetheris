using Aetheris.Production.Services;

namespace Aetheris.Processes.Services;

/// <summary>
/// The handler of a kind of process: it checks what the order needs, names what the order
/// takes, computes the result of a unit and applies it (task 00014, decision 6).
/// </summary>
/// <remarks>
/// <para>
/// The engine of the processes knows no concrete process. A kind of a process names its
/// handler by a logical code, and the engine looks the handler up in the container of
/// dependencies by that code. A new process is therefore a class of a handler and rows in
/// the tables, without a change of the engine and without a change of the client.
/// </para>
/// <para>
/// The steps of the handler are called inside the transaction of the order: the check and the
/// input before the cost is paid, the result and its application when a unit is ready. The
/// engine pays the input, keeps it for the refund and returns it when the order is cancelled.
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
    /// Gets the part an order of the process names, one of <see cref="ProcessPartSubjects"/>, so
    /// the client offers the right parts of the storage without knowing the process.
    /// </summary>
    string PartSubject { get; }

    /// <summary>
    /// Checks what the order needs before its cost is paid: the goals of the order, the
    /// resources and the parts it names, the levels it needs.
    /// </summary>
    /// <param name="context">The order and everything the check looks at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The reasons the order is refused, empty when it is allowed.</returns>
    Task<IReadOnlyList<ProcessReason>> CheckAsync(ProcessCheckContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Names what the whole order takes on top of the ingredients of its recipe: the resource
    /// that is converted, the part that is taken apart or repaired, the cost of a repair.
    /// </summary>
    /// <param name="context">The order and everything the input looks at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The input of the whole order.</returns>
    Task<ProcessInput> InputAsync(ProcessCheckContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Computes what one unit of the order gives.
    /// </summary>
    /// <param name="context">The order, its unit and everything the result looks at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The result of the unit.</returns>
    Task<ProcessResult> ComputeAsync(ProcessApplyContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the changes of one unit of the order that are not resources or parts of the
    /// body, in the transaction of the order: the deposits of the body, the ship on its orbit.
    /// </summary>
    /// <param name="context">The order, its unit and everything the change looks at.</param>
    /// <param name="result">The result of the unit.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the change is applied.</returns>
    Task ApplyAsync(ProcessApplyContext context, ProcessResult result, CancellationToken cancellationToken);
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
/// What a whole order of a process takes on top of the ingredients of its recipe (task 00014).
/// </summary>
/// <param name="Resources">The resources, by the resource.</param>
/// <param name="Parts">The parts of the storage.</param>
/// <param name="WorkFactor">The multiplier of the time of a unit: the repair of a part that lost a half of its condition takes a half of the time.</param>
public sealed record ProcessInput(
    IReadOnlyDictionary<int, long> Resources,
    IReadOnlyList<ProcessPartInput> Parts,
    decimal WorkFactor)
{
    /// <summary>
    /// Gets an input with nothing in it and the full time of a unit.
    /// </summary>
    public static ProcessInput None { get; } = new(new Dictionary<int, long>(), [], decimal.One);
}

/// <summary>
/// A part a whole order takes from the storage of the body.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="ConditionPercent">The condition of the parts, in per cent.</param>
/// <param name="Quantity">How many parts.</param>
public sealed record ProcessPartInput(int PartTypeId, int ConditionPercent, long Quantity);

/// <summary>
/// What one unit of an order of a process gives: the resources and the parts that go to the
/// body and the crew that comes back (task 00014).
/// </summary>
/// <param name="Resources">The resources that go to the balance of the body.</param>
/// <param name="Parts">The parts that go to the storage of the body.</param>
/// <param name="Crew">The crew that came back and waits to be settled by the population task.</param>
/// <param name="EndsProcess">Whether the unit ends the whole order, for the processes that make one thing at a time.</param>
public sealed record ProcessResult(
    IReadOnlyDictionary<int, long> Resources,
    IReadOnlyList<PartStackAdded> Parts,
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

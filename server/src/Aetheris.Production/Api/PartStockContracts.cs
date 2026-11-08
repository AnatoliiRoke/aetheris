namespace Aetheris.Production.Api;

/// <summary>
/// The storage of the parts of a body, for the modules that fit those parts (task 00039).
/// </summary>
/// <remarks>
/// The storage belongs to the production module. Another module locks it, reads it and moves
/// the stacks through this contract, in the transaction it already holds.
/// </remarks>
public interface IPartStock
{
    /// <summary>
    /// Locks the storage of the parts of a body for the rest of the transaction, creating it
    /// when the body has none yet.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the storage is locked.</returns>
    Task LockAsync(long planetId, int sectorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes new parts of exact stacks. A stack with the bonus of a race and a stack without
    /// it are never mixed.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="needed">The stacks to take.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the stacks are taken.</returns>
    Task TakeMarkedAsync(long planetId, IReadOnlyList<ShipPartRequest> needed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts the parts into the storage of a body, each on the stack of its own kind, mark,
    /// bonus and condition.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="parts">The parts to put in.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are in the storage.</returns>
    Task AddAsync(long planetId, IReadOnlyList<ShipPartStack> parts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the stacks of the parts of a body that are of the given kinds.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="partTypeIds">The kinds of the parts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks of the parts.</returns>
    Task<IReadOnlyList<ShipPartStack>> ReadAsync(
        long planetId,
        IReadOnlyCollection<int> partTypeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the facts of the parts: the mark, the name and the cost of one part by the recipe
    /// that makes it.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts of every part, by the part.</returns>
    Task<IReadOnlyDictionary<int, PartRecipeFacts>> GetPartFactsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The condition of a new part, in per cent (task 00013, decision 2.13).
/// </summary>
public static class PartConditions
{
    /// <summary>The condition of a new part, in per cent.</summary>
    public const int Full = 100;
}

/// <summary>
/// A stack of new parts named by the type, the mark and the bonus of the race.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="Quantity">How many parts.</param>
public sealed record ShipPartRequest(int PartTypeId, int MarkLevel, bool HasRaceBonus, long Quantity);

/// <summary>
/// A stack of the parts of a body.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="ConditionPercent">The condition of the part in per cent.</param>
/// <param name="Quantity">How many parts the stack holds.</param>
public sealed record ShipPartStack(int PartTypeId, int MarkLevel, bool HasRaceBonus, int ConditionPercent, long Quantity);

/// <summary>
/// The facts of a part another module works with.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="Code">The neutral code of the part.</param>
/// <param name="NameKey">The key of the name of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="IsArchived">Whether the part is archived.</param>
/// <param name="ResourceCost">The resources of one part by the recipe that makes it, by the resource.</param>
/// <param name="IsTerraformingDevice">Whether the part is a one-shot device of the terraforming.</param>
/// <param name="Weight">The weight of one part in tonnes.</param>
public sealed record PartRecipeFacts(
    int PartTypeId,
    string Code,
    string NameKey,
    int MarkLevel,
    bool IsArchived,
    IReadOnlyDictionary<int, decimal> ResourceCost,
    bool IsTerraformingDevice = false,
    decimal Weight = 0m);

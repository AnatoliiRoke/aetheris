using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The storage of the parts of a body: taking the parts a process needs and putting the
/// parts back (task 00014).
/// </summary>
/// <remarks>
/// The storage of the parts belongs to the production module, so another module that works
/// with the parts asks this service and never reaches into the tables (documentation/tech/01,
/// section 8).
/// </remarks>
public sealed class PartStockService
{
    private readonly ProductionDbContext _dbContext;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartStockService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="clock">The clock of the game.</param>
    public PartStockService(ProductionDbContext dbContext, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _clock = clock;
    }

    /// <summary>
    /// Takes the parts of the given kinds from the storage of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="needed">The parts by their type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks the parts came from, so they can be given back.</returns>
    /// <exception cref="GameException">The body lacks the parts.</exception>
    public async Task<IReadOnlyList<PartStackTaken>> TakeAsync(
        long planetId,
        IReadOnlyDictionary<int, long> needed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(needed);

        if (needed.Count == 0)
        {
            return [];
        }

        PartStorageEntity? storage = await _dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        if (storage is null)
        {
            throw new GameException(ProductionErrorCodes.PartMissing, "The body has no storage of the parts.");
        }

        int[] partTypeIds = [.. needed.Keys];
        List<PartStockEntity> stacks = await _dbContext.PartStocks
            .Where(stack => stack.PlanetId == planetId
                && partTypeIds.Contains(stack.PartTypeId)
                && stack.ConditionPercent == PartCondition.Full)
            .OrderBy(stack => stack.HasRaceBonus)
            .ThenBy(stack => stack.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<int, long> left = new(needed);
        Dictionary<(int PartTypeId, bool HasRaceBonus), long> taken = [];

        foreach (PartStockEntity stack in stacks)
        {
            long amount = Math.Min(stack.Quantity, left[stack.PartTypeId]);

            if (amount <= 0)
            {
                continue;
            }

            stack.Quantity -= amount;
            left[stack.PartTypeId] -= amount;
            taken[(stack.PartTypeId, stack.HasRaceBonus)] = taken.GetValueOrDefault((stack.PartTypeId, stack.HasRaceBonus)) + amount;
        }

        if (left.Values.Any(missing => missing > 0))
        {
            throw new GameException(ProductionErrorCodes.PartMissing, "The body lacks the parts.");
        }

        foreach (PartStockEntity stack in stacks.Where(stack => stack.Quantity == 0))
        {
            _dbContext.PartStocks.Remove(stack);
        }

        storage.Quantity -= taken.Values.Sum();
        RefreshFull(storage, _clock.GameNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. taken.OrderBy(entry => entry.Key.PartTypeId).ThenBy(entry => entry.Key.HasRaceBonus).Select(entry => new PartStackTaken(
                entry.Key.PartTypeId,
                entry.Key.HasRaceBonus,
                entry.Value))
        ];
    }

    /// <summary>
    /// Puts the parts back into the storage of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="parts">The parts by their type and the bonus of their race.</param>
    /// <param name="conditionPercent">The condition the parts come back with.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are back.</returns>
    public async Task GiveBackAsync(
        long planetId,
        IReadOnlyDictionary<(int PartTypeId, bool HasRaceBonus), long> parts,
        int conditionPercent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count == 0)
        {
            return;
        }

        PartStorageEntity? storage = await _dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        if (storage is null)
        {
            throw new GameException(ProductionErrorCodes.PartMissing, "The body has no storage of the parts.");
        }

        foreach ((int partTypeId, bool hasRaceBonus) in parts.Keys)
        {
            PartStockEntity? stack = await _dbContext.PartStocks
                .Where(entry => entry.PlanetId == planetId
                    && entry.PartTypeId == partTypeId
                    && entry.HasRaceBonus == hasRaceBonus
                    && entry.ConditionPercent == conditionPercent)
                .OrderBy(entry => entry.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (stack is null)
            {
                _dbContext.PartStocks.Add(new PartStockEntity
                {
                    PlanetId = planetId,
                    SectorId = 0,
                    PartTypeId = partTypeId,
                    MarkLevel = 1,
                    HasRaceBonus = hasRaceBonus,
                    ConditionPercent = conditionPercent,
                    Quantity = parts[(partTypeId, hasRaceBonus)],
                });
            }
            else
            {
                stack.Quantity += parts[(partTypeId, hasRaceBonus)];
            }
        }

        storage.Quantity += parts.Values.Sum();
        RefreshFull(storage, _clock.GameNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts the parts into the storage of a body, adding them to the stacks that are already
    /// there.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="parts">The parts to put in.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are in the storage.</returns>
    public async Task AddAsync(
        long planetId,
        IReadOnlyList<PartStackAdded> parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count == 0)
        {
            return;
        }

        PartStorageEntity? storage = await _dbContext.PartStorages
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false);

        if (storage is null)
        {
            throw new GameException(ProductionErrorCodes.PartMissing, "The body has no storage of the parts.");
        }

        foreach (PartStackAdded part in parts)
        {
            PartStockEntity? stack = await _dbContext.PartStocks
                .Where(entry => entry.PlanetId == planetId
                    && entry.PartTypeId == part.PartTypeId
                    && entry.MarkLevel == part.MarkLevel
                    && entry.HasRaceBonus == part.HasRaceBonus
                    && entry.ConditionPercent == part.ConditionPercent)
                .OrderBy(entry => entry.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (stack is null)
            {
                _dbContext.PartStocks.Add(new PartStockEntity
                {
                    PlanetId = planetId,
                    SectorId = 0,
                    PartTypeId = part.PartTypeId,
                    MarkLevel = part.MarkLevel,
                    HasRaceBonus = part.HasRaceBonus,
                    ConditionPercent = part.ConditionPercent,
                    Quantity = part.Quantity,
                });
            }
            else
            {
                stack.Quantity += part.Quantity;
            }
        }

        storage.Quantity += parts.Sum(part => part.Quantity);
        RefreshFull(storage, _clock.GameNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the stacks of the parts of a body that are of the given kinds.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="partTypeIds">The kinds of the parts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks of the parts.</returns>
    public async Task<IReadOnlyList<PartStackInfo>> ReadAsync(
        long planetId,
        IReadOnlyCollection<int> partTypeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(partTypeIds);

        if (partTypeIds.Count == 0)
        {
            return [];
        }

        List<PartStockEntity> stacks = await _dbContext.PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == planetId && partTypeIds.Contains(stack.PartTypeId))
            .OrderBy(stack => stack.PartTypeId)
            .ThenBy(stack => stack.MarkLevel)
            .ThenBy(stack => stack.ConditionPercent)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. stacks.Select(stack => new PartStackInfo(
                stack.PartTypeId,
                stack.MarkLevel,
                stack.HasRaceBonus,
                stack.ConditionPercent,
                stack.Quantity))
        ];
    }

    /// <summary>
    /// Refreshes the moment the storage of the parts is full, so the client knows when the
    /// production stops.
    /// </summary>
    /// <param name="storage">The storage of the parts.</param>
    /// <param name="now">The moment of game time.</param>
    private static void RefreshFull(PartStorageEntity storage, DateTimeOffset now)
    {
        bool full = storage.Quantity > 0 && storage.Quantity >= storage.Capacity;
        storage.FullAt = full ? storage.FullAt ?? now : null;
    }
}

/// <summary>
/// A stack of the parts a process took from the storage of a body.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="Quantity">How many parts were taken.</param>
public sealed record PartStackTaken(int PartTypeId, bool HasRaceBonus, long Quantity);

/// <summary>
/// A stack of the parts that goes into the storage of a body.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="ConditionPercent">The condition of the part in per cent.</param>
/// <param name="Quantity">How many parts.</param>
public sealed record PartStackAdded(int PartTypeId, int MarkLevel, bool HasRaceBonus, int ConditionPercent, long Quantity);

/// <summary>
/// A stack of the parts of a body.
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="ConditionPercent">The condition of the part in per cent.</param>
/// <param name="Quantity">How many parts the stack holds.</param>
public sealed record PartStackInfo(int PartTypeId, int MarkLevel, bool HasRaceBonus, int ConditionPercent, long Quantity);

using Aetheris.Core.Errors;
using Aetheris.Core.Time;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Requirements;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The storage of the parts of a body for the other modules: taking the parts a process needs,
/// putting the parts back and the facts of a part (task 00014).
/// </summary>
/// <remarks>
/// <para>
/// The storage of the parts belongs to the production module, so another module that works
/// with the parts asks this service and never reaches into the tables (documentation/tech/01,
/// section 8).
/// </para>
/// <para>
/// Every change runs in the transaction of the caller and locks the row of the storage of the
/// body first, like the queue of a factory does, so two buildings of one body never spend the
/// same parts twice and the locks are always taken in the same order: the queue of the
/// building, the storage of the parts, the balance of the resources.
/// </para>
/// </remarks>
public sealed class PartStockService
{
    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly IRequirementsModule _requirements;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartStockService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="requirements">The common requirements module, for the catalog.</param>
    /// <param name="clock">The clock of the game.</param>
    public PartStockService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        ProductionCatalogCache catalogCache,
        IRequirementsModule requirements,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _requirements = requirements;
        _clock = clock;
    }

    /// <summary>
    /// Locks the storage of the parts of a body for the rest of the transaction, creating it
    /// when the body has none yet.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the storage is locked.</returns>
    public Task LockAsync(long planetId, int sectorId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                // The lock opens the work of another module on the storage, so the rows a former
                // transaction of the same scope left in the tracker are read anew.
                _dbContext.ChangeTracker.Clear();
                await OpenAsync(planetId, sectorId, cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <summary>
    /// Takes the new parts of the given kinds from the storage of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="needed">The parts by their type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks the parts came from, so they can be given back as they were.</returns>
    /// <exception cref="GameException">The body lacks the parts.</exception>
    public Task<IReadOnlyList<PartStackTaken>> TakeAsync(
        long planetId,
        IReadOnlyDictionary<int, long> needed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(needed);

        return _unitOfWork.RunAsync(
            async () =>
            {
                List<PartStackTaken> taken = [];

                foreach ((int partTypeId, long quantity) in needed.OrderBy(entry => entry.Key))
                {
                    taken.AddRange(await TakeStacksAsync(planetId, partTypeId, PartCondition.Full, quantity, cancellationToken).ConfigureAwait(false));
                }

                return (IReadOnlyList<PartStackTaken>)taken;
            },
            cancellationToken);
    }

    /// <summary>
    /// Takes the parts of one kind and of one condition from the storage of a body: a damaged
    /// part is a row of its own, so the process names the condition it works on.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="partTypeId">The kind of the part.</param>
    /// <param name="conditionPercent">The condition of the parts, in per cent.</param>
    /// <param name="quantity">How many parts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks the parts came from, so they can be given back as they were.</returns>
    /// <exception cref="GameException">The body lacks the parts.</exception>
    public Task<IReadOnlyList<PartStackTaken>> TakeStackAsync(
        long planetId,
        int partTypeId,
        int conditionPercent,
        long quantity,
        CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () => await TakeStacksAsync(planetId, partTypeId, conditionPercent, quantity, cancellationToken).ConfigureAwait(false),
            cancellationToken);

    /// <summary>
    /// Puts the parts into the storage of a body, each to the stack of its own kind, mark, bonus
    /// of the race and condition.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="parts">The parts to put in.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are in the storage.</returns>
    public Task AddAsync(
        long planetId,
        IReadOnlyList<PartStackAdded> parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return _unitOfWork.RunAsync(
            async () =>
            {
                List<PartStackAdded> added = [.. parts.Where(part => part.Quantity > 0)];

                if (added.Count == 0)
                {
                    return true;
                }

                PartStorageEntity storage = await LockedStorageAsync(planetId, cancellationToken).ConfigureAwait(false);

                foreach (PartStackAdded part in added)
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
                        stack = new PartStockEntity
                        {
                            PlanetId = planetId,
                            SectorId = storage.SectorId,
                            PartTypeId = part.PartTypeId,
                            MarkLevel = part.MarkLevel,
                            HasRaceBonus = part.HasRaceBonus,
                            ConditionPercent = part.ConditionPercent,
                            Quantity = 0,
                        };

                        _dbContext.PartStocks.Add(stack);
                    }

                    stack.Quantity += part.Quantity;
                    storage.Quantity += part.Quantity;

                    // Every stack is saved before the next one is looked up, so two parts of one
                    // stack in the same call add to one row.
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }

                RefreshFull(storage, _clock.GameNow);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Puts the parts that were taken back into the storage of a body, as they were.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="parts">The stacks that were taken.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are back.</returns>
    public Task GiveBackAsync(
        long planetId,
        IReadOnlyList<PartStackTaken> parts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parts);

        return AddAsync(
            planetId,
            [.. parts.Select(part => new PartStackAdded(part.PartTypeId, part.MarkLevel, part.HasRaceBonus, part.ConditionPercent, part.Quantity))],
            cancellationToken);
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
    /// Gets the facts of the parts another module works with: the mark, the crew and the cost of
    /// one part by the recipe that makes it (task 00014, decision 3).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts of every part, by the part.</returns>
    public async Task<IReadOnlyDictionary<int, PartFacts>> GetPartFactsAsync(CancellationToken cancellationToken)
    {
        ProductionCatalog catalog = await _catalogCache.GetAsync(_dbContext, _requirements, cancellationToken).ConfigureAwait(false);

        return catalog.Parts.ToDictionary(part => part.Id, part => FactsOf(catalog, part));
    }

    /// <summary>
    /// Builds the facts of a part from the catalog.
    /// </summary>
    /// <param name="catalog">The catalog of the production.</param>
    /// <param name="part">The part.</param>
    /// <returns>The facts of the part.</returns>
    private static PartFacts FactsOf(ProductionCatalog catalog, PartTypeInfo part)
    {
        // The cost of a part is the cost of the recipe that makes it, the one that is offered
        // first; the produced materials are resources too, so they are a part of it.
        RecipeInfo? recipe = catalog.Recipes
            .Where(entry => entry.PartTypeId == part.Id && entry.OutputQuantity > 0)
            .OrderBy(entry => entry.IsArchived)
            .ThenBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Id)
            .FirstOrDefault();

        Dictionary<int, decimal> cost = [];

        if (recipe is not null)
        {
            foreach (IngredientInfo ingredient in recipe.IngredientIds.Where(ingredient => ingredient.ResourceTypeId is not null))
            {
                int resourceTypeId = ingredient.ResourceTypeId!.Value;
                cost[resourceTypeId] = cost.GetValueOrDefault(resourceTypeId) + ((decimal)ingredient.Amount / recipe.OutputQuantity);
            }
        }

        decimal crew = part.Attributes.TryGetValue(PartAttributeKeys.Crew, out PartAttributeInfo? attribute)
            ? attribute.NumberValue ?? decimal.Zero
            : decimal.Zero;

        return new PartFacts(part.Id, part.Code, part.NameKey, part.MarkLevel, part.IsArchived, crew, cost);
    }

    /// <summary>
    /// Takes the parts of one kind and of one condition from the locked storage of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="partTypeId">The kind of the part.</param>
    /// <param name="conditionPercent">The condition of the parts, in per cent.</param>
    /// <param name="quantity">How many parts.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The stacks the parts came from.</returns>
    /// <exception cref="GameException">The body lacks the parts.</exception>
    private async Task<IReadOnlyList<PartStackTaken>> TakeStacksAsync(
        long planetId,
        int partTypeId,
        int conditionPercent,
        long quantity,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return [];
        }

        PartStorageEntity storage = await LockedStorageAsync(planetId, cancellationToken).ConfigureAwait(false);

        // The plain parts go first and the parts with the bonus of the race last, like the
        // factory takes them.
        List<PartStockEntity> stacks = await _dbContext.PartStocks
            .Where(stack => stack.PlanetId == planetId
                && stack.PartTypeId == partTypeId
                && stack.ConditionPercent == conditionPercent
                && stack.Quantity > 0)
            .OrderBy(stack => stack.HasRaceBonus)
            .ThenBy(stack => stack.MarkLevel)
            .ThenBy(stack => stack.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        long left = quantity;
        List<PartStackTaken> taken = [];

        foreach (PartStockEntity stack in stacks)
        {
            long amount = Math.Min(stack.Quantity, left);

            if (amount <= 0)
            {
                continue;
            }

            stack.Quantity -= amount;
            left -= amount;
            taken.Add(new PartStackTaken(stack.PartTypeId, stack.MarkLevel, stack.HasRaceBonus, stack.ConditionPercent, amount));
        }

        if (left > 0)
        {
            throw new GameException(ProductionErrorCodes.PartMissing, "The body lacks the parts.");
        }

        foreach (PartStockEntity stack in stacks.Where(stack => stack.Quantity == 0))
        {
            _dbContext.PartStocks.Remove(stack);
        }

        storage.Quantity -= quantity;
        RefreshFull(storage, _clock.GameNow);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return taken;
    }

    /// <summary>
    /// Creates the storage of a body when it has none and locks its row.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The locked storage.</returns>
    private async Task<PartStorageEntity> OpenAsync(long planetId, int sectorId, CancellationToken cancellationToken)
    {
        // The row is created without a conflict first, so the first parallel commands of a body
        // wait for one row instead of failing on a duplicate key.
        await _dbContext.Database
            .ExecuteSqlAsync($"INSERT INTO part_storage (planet_id, sector_id, capacity, quantity) VALUES ({planetId}, {sectorId}, 0, 0) ON CONFLICT (planet_id) DO NOTHING", cancellationToken)
            .ConfigureAwait(false);

        return await LockedStorageAsync(planetId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Locks the row of the storage of a body and reads it.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The locked storage.</returns>
    /// <exception cref="GameException">The body has no storage of the parts.</exception>
    private async Task<PartStorageEntity> LockedStorageAsync(long planetId, CancellationToken cancellationToken)
    {
        PartStorageEntity? storage = (await _dbContext.PartStorages
            .FromSql($"SELECT * FROM part_storage WHERE planet_id = {planetId} FOR UPDATE")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).SingleOrDefault();

        return storage ?? throw new GameException(ProductionErrorCodes.PartMissing, "The body has no storage of the parts.");
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
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="HasRaceBonus">Whether the part carries the bonus of the race that made it.</param>
/// <param name="ConditionPercent">The condition of the part in per cent.</param>
/// <param name="Quantity">How many parts were taken.</param>
public sealed record PartStackTaken(int PartTypeId, int MarkLevel, bool HasRaceBonus, int ConditionPercent, long Quantity);

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

/// <summary>
/// The facts of a part another module works with (task 00014).
/// </summary>
/// <param name="PartTypeId">The kind of the part.</param>
/// <param name="Code">The neutral code of the part.</param>
/// <param name="NameKey">The key of the name of the part.</param>
/// <param name="MarkLevel">The mark level of the part.</param>
/// <param name="IsArchived">Whether the part is archived.</param>
/// <param name="Crew">The crew the part carries, from its attribute.</param>
/// <param name="ResourceCost">The resources of one part by the recipe that makes it, by the resource.</param>
public sealed record PartFacts(
    int PartTypeId,
    string Code,
    string NameKey,
    int MarkLevel,
    bool IsArchived,
    decimal Crew,
    IReadOnlyDictionary<int, decimal> ResourceCost);

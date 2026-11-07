using Aetheris.Core.Admin;
using Aetheris.Core.Errors;
using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// The parts and the factory queues of a player for the admin panel.
/// </summary>
public sealed class ProductionPlayerAdminService
{
    private const int MinimumConditionPercent = 1;

    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly PartStockService _parts;
    private readonly ProductionQueueService _queue;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionPlayerAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="parts">The stacks of the parts.</param>
    /// <param name="queue">The queues of the factories.</param>
    /// <param name="world">The world, for the owner of the body.</param>
    public ProductionPlayerAdminService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        PartStockService parts,
        ProductionQueueService queue,
        IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _parts = parts;
        _queue = queue;
        _world = world;
    }

    /// <summary>
    /// Reads the stacks, the part catalog and the factory orders of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The parts and the orders.</returns>
    public async Task<AdminProductionView> ReadAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        List<PartStockEntity> stacks = await _dbContext.PartStocks.AsNoTracking()
            .Where(stack => stack.PlanetId == planetId)
            .OrderBy(stack => stack.PartTypeId)
            .ThenBy(stack => stack.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);

        List<ProductionOrderEntity> orders = await _dbContext.ProductionOrders.AsNoTracking()
            .Where(order => order.PlanetId == planetId)
            .OrderBy(order => order.PlanetBuildingId)
            .ThenBy(order => order.Position)
            .ThenBy(order => order.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<int> recipeIds = [.. orders.Select(order => order.RecipeId).Distinct()];
        Dictionary<int, string> recipeNames = await _dbContext.Recipes.AsNoTracking()
            .Where(recipe => recipeIds.Contains(recipe.Id))
            .ToDictionaryAsync(recipe => recipe.Id, recipe => recipe.NameKey, cancellationToken)
            .ConfigureAwait(false);

        return new AdminProductionView(
            [.. stacks.Select(stack => new AdminPartStackRow(
                stack.Id,
                stack.PartTypeId,
                parts.GetValueOrDefault(stack.PartTypeId)?.NameKey ?? string.Empty,
                stack.MarkLevel,
                stack.HasRaceBonus,
                stack.ConditionPercent,
                stack.Quantity))],
            [.. parts.Values
                .OrderBy(part => part.PartTypeId)
                .Select(part => new AdminPartTypeRow(part.PartTypeId, part.NameKey, part.MarkLevel, part.IsArchived))],
            [.. orders.Select(order => new AdminProductionOrderRow(
                order.Id,
                order.PlanetBuildingId,
                order.RecipeId,
                recipeNames.GetValueOrDefault(order.RecipeId, string.Empty),
                order.Quantity,
                order.ProducedQuantity,
                order.Position,
                order.FinishesAt))]);
    }

    /// <summary>
    /// Sets the quantity of a stack. A quantity of zero keeps the row.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="stackId">The stack.</param>
    /// <param name="quantity">The quantity.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The quantity before the change and the quantity after it.</returns>
    public Task<(long Before, long After)> SetQuantityAsync(
        string playerId,
        long planetId,
        long stackId,
        long quantity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (quantity < 0)
        {
            throw new GameException(PlayerAdminCodes.AmountNegative, "The quantity of a stack cannot be negative.");
        }

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetFacts facts = await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);
                await _parts.LockAsync(facts.PlanetId, facts.SectorId, cancellationToken).ConfigureAwait(false);

                PartStockEntity stack = await _dbContext.PartStocks
                    .FirstOrDefaultAsync(entry => entry.Id == stackId && entry.PlanetId == planetId, cancellationToken)
                    .ConfigureAwait(false)
                    ?? throw new GameException(GameErrorCodes.NotFound, "The stack does not exist on the body.");

                long before = stack.Quantity;
                stack.Quantity = quantity;

                return (before, quantity);
            },
            cancellationToken);
    }

    /// <summary>
    /// Adds parts to the stack of their kind, mark, bonus and condition. The mark is the mark of the part.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="partTypeId">The part.</param>
    /// <param name="quantity">How many parts to add.</param>
    /// <param name="hasRaceBonus">Whether the parts carry the bonus of the race.</param>
    /// <param name="conditionPercent">The condition in per cent, or <see langword="null"/> for a new part.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the parts are in the storage.</returns>
    public async Task GrantAsync(
        string playerId,
        long planetId,
        int partTypeId,
        long quantity,
        bool hasRaceBonus,
        int? conditionPercent,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (quantity <= 0)
        {
            throw new GameException(PlayerAdminCodes.AmountNegative, "The quantity of a grant has to be positive.");
        }

        int condition = conditionPercent ?? PartCondition.Full;

        if (condition < MinimumConditionPercent || condition > PartCondition.Full)
        {
            throw new GameException(PlayerAdminCodes.LevelOutOfRange, "The condition of a part is between 1 and 100.");
        }

        await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<int, PartFacts> parts = await _parts.GetPartFactsAsync(cancellationToken).ConfigureAwait(false);
        PartFacts part = parts.GetValueOrDefault(partTypeId)
            ?? throw new GameException(GameErrorCodes.NotFound, "The part does not exist.");

        await _parts.AddAsync(
            planetId,
            [new PartStackAdded(part.PartTypeId, part.MarkLevel, hasRaceBonus, condition, quantity)],
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels a factory order and returns the cost of the units it has not made.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="orderId">The order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the order is cancelled.</returns>
    public async Task CancelOrderAsync(string playerId, long planetId, long orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        ProductionOrderEntity order = await _dbContext.ProductionOrders.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == orderId && entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new GameException(GameErrorCodes.NotFound, "The order does not exist on the body.");

        await _queue.CancelAsync(playerId, planetId, order.PlanetBuildingId, orderId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PlanetFacts> RequireOwnerAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(GameErrorCodes.NotFound, "The body is not a colony of the player.");
        }

        return facts;
    }
}

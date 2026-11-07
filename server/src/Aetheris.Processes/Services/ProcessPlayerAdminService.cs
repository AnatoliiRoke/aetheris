using Aetheris.Core.Errors;
using Aetheris.Processes.Api;
using Aetheris.Processes.Data;
using Aetheris.Processes.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Services;

/// <summary>
/// The process queues of a player for the admin panel.
/// </summary>
public sealed class ProcessPlayerAdminService
{
    private readonly ProcessesDbContext _dbContext;
    private readonly ProcessQueueService _queue;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessPlayerAdminService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="queue">The queues of the processes.</param>
    /// <param name="world">The world, for the owner of the body.</param>
    public ProcessPlayerAdminService(ProcessesDbContext dbContext, ProcessQueueService queue, IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _queue = queue;
        _world = world;
    }

    /// <summary>
    /// Reads the process orders of a body.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The orders.</returns>
    public async Task<IReadOnlyList<AdminProcessOrderRow>> ReadAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        List<ProcessOrderEntity> orders = await _dbContext.ProcessOrders.AsNoTracking()
            .Where(order => order.PlanetId == planetId)
            .OrderBy(order => order.PlanetBuildingId)
            .ThenBy(order => order.Position)
            .ThenBy(order => order.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<int> recipeIds = [.. orders.Select(order => order.ProcessRecipeId).Distinct()];
        Dictionary<int, string> recipeNames = await _dbContext.ProcessRecipes.AsNoTracking()
            .Where(recipe => recipeIds.Contains(recipe.Id))
            .ToDictionaryAsync(recipe => recipe.Id, recipe => recipe.NameKey, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. orders.Select(order => new AdminProcessOrderRow(
                order.Id,
                order.PlanetBuildingId,
                order.ProcessRecipeId,
                recipeNames.GetValueOrDefault(order.ProcessRecipeId, string.Empty),
                order.Quantity,
                order.ProducedQuantity,
                order.Position,
                order.FinishesAt)),
        ];
    }

    /// <summary>
    /// Cancels a process order. Some kinds do not refund the input.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="orderId">The order.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the order is cancelled.</returns>
    public async Task CancelAsync(string playerId, long planetId, long orderId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await RequireOwnerAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        ProcessOrderEntity order = await _dbContext.ProcessOrders.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == orderId && entry.PlanetId == planetId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new GameException(GameErrorCodes.NotFound, "The order does not exist on the body.");

        await _queue.CancelAsync(playerId, planetId, order.PlanetBuildingId, orderId, cancellationToken).ConfigureAwait(false);
    }

    private async Task RequireOwnerAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(GameErrorCodes.NotFound, "The body is not a colony of the player.");
        }
    }
}

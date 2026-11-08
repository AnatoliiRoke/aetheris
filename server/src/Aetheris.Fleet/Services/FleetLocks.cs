using Aetheris.Fleet.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The row locks of the fleets (task 00040).
/// </summary>
internal static class FleetLocks
{
    /// <summary>
    /// Locks the fleets of a player until the end of the transaction: the row of the player is
    /// made when it is missing and then taken for update.
    /// </summary>
    /// <remarks>
    /// The lock is the first one a fleet command takes. The dismantling of a ship takes it after
    /// the lock of the queue of its body, and no fleet command takes the queue of a body, so the
    /// two orders never wait on each other in a circle.
    /// </remarks>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the row is locked.</returns>
    public static async Task LockPlayerAsync(FleetDbContext dbContext, string playerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await dbContext.Database
            .ExecuteSqlAsync(
                $"INSERT INTO fleet_player_state (player_id) VALUES ({playerId}) ON CONFLICT (player_id) DO NOTHING",
                cancellationToken)
            .ConfigureAwait(false);

        await dbContext.Database
            .ExecuteSqlAsync($"SELECT 1 FROM fleet_player_state WHERE player_id = {playerId} FOR UPDATE", cancellationToken)
            .ConfigureAwait(false);
    }
}

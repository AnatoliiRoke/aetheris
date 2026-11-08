namespace Aetheris.Economy.Services;

/// <summary>
/// The upkeep of the ships of a civilization, written by the fleet and read by the economy
/// (task 00039).
/// </summary>
/// <remarks>
/// The fleet calls <see cref="SetPerHourAsync"/> after it has brought the credits to the
/// moment with the old rate. The economy reads the row itself when it counts, so this contract
/// is only the write.
/// </remarks>
public interface IShipUpkeepLedger
{
    /// <summary>
    /// Sets the credits the ships of a player cost per hour.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="perHour">The rate. Zero clears the charge.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rate is stored.</returns>
    Task SetPerHourAsync(string playerId, decimal perHour, CancellationToken cancellationToken = default);
}

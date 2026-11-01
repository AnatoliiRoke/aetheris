using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Production.Services;

/// <summary>
/// The neutral code of the race of a player, which a capsule of inhabitants is checked
/// against (task 00031).
/// </summary>
internal static class PlayerRaceCode
{
    /// <summary>
    /// Reads the code of the race of a player.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The code of the race, or <see langword="null"/> when the player has no civilization.</returns>
    internal static async Task<string?> ReadAsync(IWorldModule world, string playerId, CancellationToken cancellationToken)
    {
        CivilizationSummary? civilization = await world.GetCivilizationAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (civilization is null)
        {
            return null;
        }

        IReadOnlyList<RaceDto> races = await world.GetRacesAsync(cancellationToken).ConfigureAwait(false);

        return races.FirstOrDefault(race => race.Id == civilization.RaceId)?.Code;
    }
}

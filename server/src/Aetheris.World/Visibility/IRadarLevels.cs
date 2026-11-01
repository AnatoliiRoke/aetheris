namespace Aetheris.World.Visibility;

/// <summary>
/// The levels of the radars of the bodies of a player: the contract the module of the buildings
/// fills, so the world never reads its tables (task 00034).
/// </summary>
/// <remarks>
/// A radar is a building with a range that grows with its level. The world turns the level into a
/// range and the range into what the player sees; where the level comes from is the business of
/// the implementation. While the test stub of the radar is on
/// (<see cref="Configuration.WorldConfigKeys.RadarStubEnabled"/>) this contract is not asked.
/// </remarks>
public interface IRadarLevels
{
    /// <summary>
    /// Gets the levels of the radars of the bodies.
    /// </summary>
    /// <param name="playerId">The owner of the bodies.</param>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the radar by body; a body without a radar is not in the answer or has zero.</returns>
    Task<IReadOnlyDictionary<long, int>> GetLevelsAsync(string playerId, IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken);
}

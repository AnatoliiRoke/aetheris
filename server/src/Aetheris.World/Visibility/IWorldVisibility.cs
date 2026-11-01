namespace Aetheris.World.Visibility;

/// <summary>
/// What a player sees of the map.
/// </summary>
/// <param name="SeesEverything">Whether the whole map is open, for example in the development mode.</param>
/// <param name="VisibleObjectIds">The celestial objects that are open.</param>
/// <param name="VisibleSystemIds">The star systems that are open.</param>
/// <param name="RadarCircles">The ranges of the radars of the player, for the map; none until the radar is built (task 00034).</param>
public sealed record VisibilityScope(
    bool SeesEverything,
    IReadOnlySet<long> VisibleObjectIds,
    IReadOnlySet<long> VisibleSystemIds,
    IReadOnlyList<RadarCircle>? RadarCircles = null)
{
    /// <summary>
    /// Tells whether an object is open.
    /// </summary>
    /// <param name="objectId">The celestial object.</param>
    /// <returns><see langword="true"/> when the player sees it.</returns>
    public bool SeesObject(long objectId) => SeesEverything || VisibleObjectIds.Contains(objectId);

    /// <summary>
    /// Tells whether a system is open.
    /// </summary>
    /// <param name="systemId">The star system.</param>
    /// <returns><see langword="true"/> when the player sees it.</returns>
    public bool SeesSystem(long systemId) => SeesEverything || VisibleSystemIds.Contains(systemId);
}

/// <summary>
/// The range of the radar of a body of the player.
/// </summary>
/// <param name="SectorId">The sector the body lies in.</param>
/// <param name="PlanetId">The body.</param>
/// <param name="X">The x coordinate of the body within the sector.</param>
/// <param name="Y">The y coordinate of the body within the sector.</param>
/// <param name="Z">The z coordinate of the body within the sector.</param>
/// <param name="Radius">The radius of the range.</param>
public sealed record RadarCircle(int SectorId, long PlanetId, double X, double Y, double Z, double Radius);

/// <summary>
/// The place where the radar connects to the map (task 00004, section 2.7; decisions 9, 13, 16).
/// </summary>
/// <remarks>
/// A player sees their own bodies and what the radars of their bodies opened (task 00034): the
/// radar is a building with a range by level, and <see cref="RadarVisibility"/> turns the level
/// into what is open.
/// </remarks>
public interface IWorldVisibility
{
    /// <summary>
    /// Gets what a player sees.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The open part of the map.</returns>
    Task<VisibilityScope> GetScopeAsync(string playerId, CancellationToken cancellationToken);
}

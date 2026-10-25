namespace Aetheris.World.Visibility;

/// <summary>
/// What a player sees of the map.
/// </summary>
/// <param name="SeesEverything">Whether the whole map is open, for example in the development mode.</param>
/// <param name="VisibleObjectIds">The celestial objects that are open.</param>
/// <param name="VisibleSystemIds">The star systems that are open.</param>
public sealed record VisibilityScope(
    bool SeesEverything,
    IReadOnlySet<long> VisibleObjectIds,
    IReadOnlySet<long> VisibleSystemIds)
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
/// The place where the radar connects to the map (task 00004, section 2.7; decisions 9, 13, 16).
/// </summary>
/// <remarks>
/// A player sees only what a radar opened. The radar is a building with a range by level and
/// arrives with its own task; it replaces the registration of this interface. Until then a
/// player sees only their own bodies.
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

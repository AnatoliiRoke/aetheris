using Aetheris.World.Visibility;

namespace Aetheris.Planets.Services;

/// <summary>
/// The levels of the radars of the bodies of a player for the world (task 00034): the level of the
/// building of the class <see cref="BuildingClasses.Sensor"/> on each body. The world turns the
/// level into a range and the range into what the player sees; the class and the level are data.
/// </summary>
public sealed class PlanetsRadarLevels : IRadarLevels
{
    private readonly PlanetQueryService _queries;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetsRadarLevels"/> class.
    /// </summary>
    /// <param name="queries">The reads of the bodies.</param>
    public PlanetsRadarLevels(PlanetQueryService queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        _queries = queries;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, int>> GetLevelsAsync(string playerId, IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(planetIds);

        IReadOnlyList<PlanetBuildingLevel> levels = await _queries.GetBuildingLevelsAsync(BuildingClasses.Sensor, playerId, cancellationToken).ConfigureAwait(false);

        return levels
            .Where(entry => planetIds.Contains(entry.PlanetId) && entry.Level > 0)
            .ToDictionary(entry => entry.PlanetId, entry => entry.Level);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, decimal>> GetRangeBonusesAsync(string playerId, IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(planetIds);

        IReadOnlyDictionary<long, decimal> bonuses = await _queries
            .GetBuildingEffectValuesAsync(BuildingClasses.Sensor, BuildingEffectTypes.RadarRangeBonus, playerId, cancellationToken)
            .ConfigureAwait(false);

        return bonuses
            .Where(entry => planetIds.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }
}

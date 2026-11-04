using Aetheris.Research.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// Reads the levels of the technologies of a player once for a request: the planet, the economy,
/// the production and the research all ask for them, and a request used to read the same rows
/// again for each of them (task 00036, section 6.3).
/// </summary>
/// <remarks>
/// <para>
/// The reader lives as long as the request and shares its context, so every save of the context
/// forgets what was read. A learned technology is written through the context only.
/// </para>
/// <para>
/// A technology is researched from a body, so every body has its own level of it. The reader
/// gives both: the level of a body, and the level of the civilization, which is the largest of
/// the levels of its bodies.
/// </para>
/// </remarks>
public sealed class PlayerTechnologyLevelsReader
{
    private readonly ResearchDbContext _dbContext;
    private readonly Dictionary<string, IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>>> _bodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<int, int>> _levels = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerTechnologyLevelsReader"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the request.</param>
    public PlayerTechnologyLevelsReader(ResearchDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
        _dbContext.SavedChanges += (_, _) => Clear();
        _dbContext.SaveChangesFailed += (_, _) => Clear();
    }

    /// <summary>
    /// Gets the levels of the technologies of the civilization of a player: the largest level of
    /// every body of the player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels by technology.</returns>
    public async Task<IReadOnlyDictionary<int, int>> GetAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        if (_levels.TryGetValue(playerId, out IReadOnlyDictionary<int, int>? known))
        {
            return known;
        }

        IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>> bodies = await BodiesAsync(playerId, cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> levels = [];

        foreach (KeyValuePair<int, int> entry in bodies.Values.SelectMany(body => body))
        {
            levels[entry.Key] = Math.Max(levels.GetValueOrDefault(entry.Key), entry.Value);
        }

        _levels[playerId] = levels;

        return levels;
    }

    /// <summary>
    /// Gets the levels of the technologies of one body of a player: the levels its own research
    /// gave it.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels by technology.</returns>
    public async Task<IReadOnlyDictionary<int, int>> GetAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>> bodies = await BodiesAsync(playerId, cancellationToken).ConfigureAwait(false);

        return bodies.GetValueOrDefault(planetId) ?? new Dictionary<int, int>();
    }

    /// <summary>
    /// Gets the levels of the technologies of several bodies of a player at once: the levels
    /// every body has learned by its own research.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels by technology of every body; a body without a technology is not in the answer.</returns>
    public async Task<IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>>> GetAsync(
        string playerId,
        IReadOnlyCollection<long> planetIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(planetIds);

        if (planetIds.Count == 0)
        {
            return new Dictionary<long, IReadOnlyDictionary<int, int>>();
        }

        IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>> bodies = await BodiesAsync(playerId, cancellationToken).ConfigureAwait(false);

        return bodies.Where(entry => planetIds.Contains(entry.Key)).ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    /// <summary>
    /// Reads the learned technologies of every body of a player in one query, once for the
    /// request: the level of the civilization and the levels of the bodies come from the same rows
    /// (code audit of 04.11.2026).
    /// </summary>
    private async Task<IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>>> BodiesAsync(string playerId, CancellationToken cancellationToken)
    {
        if (_bodies.TryGetValue(playerId, out IReadOnlyDictionary<long, IReadOnlyDictionary<int, int>>? known))
        {
            return known;
        }

        var rows = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .Select(entry => new { entry.PlanetId, entry.TechnologyId, entry.Level })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<long, IReadOnlyDictionary<int, int>> bodies = rows
            .GroupBy(row => row.PlanetId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, int>)group.ToDictionary(row => row.TechnologyId, row => row.Level));

        _bodies[playerId] = bodies;

        return bodies;
    }

    private void Clear()
    {
        _bodies.Clear();
        _levels.Clear();
    }
}

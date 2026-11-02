using Aetheris.Research.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// Reads the levels of the technologies of a player once for a request: the planet, the economy,
/// the production and the research all ask for them, and a request used to read the same rows
/// again for each of them (task 00036, section 6.3).
/// </summary>
/// <remarks>
/// The reader lives as long as the request and shares its context, so every save of the context
/// forgets what was read. A learned technology is written through the context only.
/// </remarks>
public sealed class PlayerTechnologyLevelsReader
{
    private readonly ResearchDbContext _dbContext;
    private readonly Dictionary<string, IReadOnlyDictionary<int, int>> _levels = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerTechnologyLevelsReader"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    public PlayerTechnologyLevelsReader(ResearchDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
        _dbContext.SavedChanges += (_, _) => _levels.Clear();
        _dbContext.SaveChangesFailed += (_, _) => _levels.Clear();
    }

    /// <summary>
    /// Gets the levels of the technologies of a player.
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

        Dictionary<int, int> levels = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);

        _levels[playerId] = levels;

        return levels;
    }
}

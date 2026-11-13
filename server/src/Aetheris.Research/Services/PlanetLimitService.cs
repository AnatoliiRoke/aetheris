using Aetheris.Core.Configuration;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;

namespace Aetheris.Research.Services;

/// <summary>
/// The largest number of bodies a player may have (task 00073).
/// </summary>
public interface IPlanetLimit
{
    /// <summary>
    /// Gets the largest number of bodies a player may have at once, the home body among them.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The limit: the value of the effect of the highest level, or the setting without the technology.</returns>
    Task<int> LimitAsync(string playerId, CancellationToken cancellationToken);
}

/// <summary>
/// The limit of the bodies of a player, read from the technologies it has learned (task 00073).
/// </summary>
/// <remarks>
/// <para>
/// The technology of the colonial administration gives the limit as an absolute value of its
/// level, so the biggest level the player has learned wins. A technology is researched on a
/// body, so the levels of every body of the player are read and the biggest one is taken.
/// </para>
/// <para>
/// A player without the technology keeps the limit of the configuration
/// (<c>research.colony.base_limit</c>), so the number of bodies of a new player is a parameter
/// of the game and not a number in the code.
/// </para>
/// </remarks>
public sealed class PlanetLimitService : IPlanetLimit
{
    private readonly ResearchDbContext _dbContext;
    private readonly TechnologyCatalogCache _catalogCache;
    private readonly PlayerTechnologyLevelsReader _levelsReader;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetLimitService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    /// <param name="levelsReader">The levels of the technologies of a player, read once for a request.</param>
    /// <param name="config">The game configuration.</param>
    public PlanetLimitService(
        ResearchDbContext dbContext,
        TechnologyCatalogCache catalogCache,
        PlayerTechnologyLevelsReader levelsReader,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(levelsReader);
        ArgumentNullException.ThrowIfNull(config);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
        _levelsReader = levelsReader;
        _config = config;
    }

    /// <inheritdoc />
    public async Task<int> LimitAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        int baseLimit = ResearchSettings.From(_config).ColonyBaseLimit;

        IReadOnlyDictionary<int, int> levels = await _levelsReader.GetAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (levels.Count == 0)
        {
            return baseLimit;
        }

        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        int limit = baseLimit;

        foreach ((int technologyId, int level) in levels)
        {
            TechnologyInfo? technology = catalog.FindTechnology(technologyId);

            if (technology is null)
            {
                continue;
            }

            foreach (TechnologyEffectInfo effect in technology.Levels.GetValueOrDefault(level)?.Effects ?? [])
            {
                if (effect.EffectType != TechnologyEffectTypes.PlanetLimit)
                {
                    continue;
                }

                // The value of the level is absolute: a level with a smaller value than the one
                // already found must not lower the limit of the player.
                int value = decimal.ToInt32(decimal.Truncate(effect.Value));
                if (value > limit)
                {
                    limit = value;
                }
            }
        }

        return limit;
    }
}

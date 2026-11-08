using Aetheris.Research.Data;

namespace Aetheris.Research.Services;

/// <summary>
/// The multiplier a learned technology gives the assembly time of a ship (task 00039).
/// </summary>
/// <remarks>
/// The effect is its own, so a technology that shortens construction, production or research
/// does not shorten the assembly. No such technology leaves the multiplier at one.
/// </remarks>
public interface IShipyardTime
{
    /// <summary>
    /// Gets the multiplier the technologies of one body give the assembly.
    /// </summary>
    /// <param name="playerId">The player, or <see langword="null"/> for a body without an owner.</param>
    /// <param name="planetId">The body the shipyard stands on.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The multiplier. One changes nothing. It is never negative.</returns>
    Task<decimal> MultiplierAsync(string? playerId, long planetId, CancellationToken cancellationToken);
}

/// <summary>
/// The multiplier of the assembly, read from the technologies of the body (task 00039).
/// </summary>
public sealed class ShipyardTimeService : IShipyardTime
{
    private readonly ResearchDbContext _dbContext;
    private readonly TechnologyCatalogCache _catalogCache;
    private readonly PlayerTechnologyLevelsReader _levelsReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipyardTimeService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    /// <param name="levelsReader">The levels of the technologies of a player, read once for a request.</param>
    public ShipyardTimeService(
        ResearchDbContext dbContext,
        TechnologyCatalogCache catalogCache,
        PlayerTechnologyLevelsReader levelsReader)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(levelsReader);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
        _levelsReader = levelsReader;
    }

    /// <inheritdoc />
    public async Task<decimal> MultiplierAsync(string? playerId, long planetId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return decimal.One;
        }

        IReadOnlyDictionary<int, int> levels = await _levelsReader.GetAsync(playerId, planetId, cancellationToken).ConfigureAwait(false);

        if (levels.Count == 0)
        {
            return decimal.One;
        }

        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        decimal assemblyTime = decimal.One;

        foreach ((int technologyId, int level) in levels)
        {
            TechnologyInfo? technology = catalog.FindTechnology(technologyId);

            if (technology is null)
            {
                continue;
            }

            foreach (TechnologyEffectInfo effect in technology.Levels.GetValueOrDefault(level)?.Effects ?? [])
            {
                if (effect.EffectType != TechnologyEffectTypes.ShipyardTime)
                {
                    continue;
                }

                // An empty multiplier changes nothing; a negative one is broken data, which must
                // never make the assembly instant.
                if (effect.Value < 0)
                {
                    throw new InvalidOperationException(
                        $"The technology {technology.Code} has a negative multiplier of the assembly time at level {level}.");
                }

                if (effect.Value > 0)
                {
                    assemblyTime *= effect.Value;
                }
            }
        }

        return assemblyTime > 0m ? assemblyTime : decimal.One;
    }
}

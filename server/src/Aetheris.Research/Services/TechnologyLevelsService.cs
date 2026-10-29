using Aetheris.Planets.Services;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// The technologies of a player: the levels they have learned and the bonuses they give
/// (task 00012, decision 5).
/// </summary>
/// <remarks>
/// The knowledge belongs to the player, so the bonuses apply to every body of the
/// civilization. The bonuses of the technologies of a branch are multiplied, so a level of a
/// technology that raises a parameter by five percent raises it by five percent more.
/// </remarks>
public sealed class TechnologyLevelsService : ITechnologyLevels
{
    private readonly ResearchDbContext _dbContext;
    private readonly TechnologyCatalogCache _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="TechnologyLevelsService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    public TechnologyLevelsService(ResearchDbContext dbContext, TechnologyCatalogCache catalogCache)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, int>> GetLevelsAsync(string? playerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return new Dictionary<int, int>();
        }

        return await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<PlanetBonuses> GetBonusesAsync(string? playerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return PlanetBonuses.None;
        }

        IReadOnlyDictionary<int, int> levels = await GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (levels.Count == 0)
        {
            return PlanetBonuses.None;
        }

        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);

        decimal metalProduction = decimal.One;
        decimal crystalProduction = decimal.One;
        decimal constructionTime = decimal.One;
        decimal storageCapacity = decimal.One;
        decimal energyOutput = decimal.One;
        decimal populationMax = decimal.One;

        foreach ((int technologyId, int level) in levels)
        {
            TechnologyInfo? technology = catalog.FindTechnology(technologyId);

            if (technology is null)
            {
                continue;
            }

            foreach (TechnologyEffectInfo effect in technology.Levels.GetValueOrDefault(level)?.Effects ?? [])
            {
                switch (effect.EffectType)
                {
                    case TechnologyEffectTypes.MetalProduction:
                        metalProduction *= effect.Value;
                        break;
                    case TechnologyEffectTypes.CrystalProduction:
                        crystalProduction *= effect.Value;
                        break;
                    case TechnologyEffectTypes.ConstructionTime:
                        constructionTime *= effect.Value;
                        break;
                    case TechnologyEffectTypes.StorageCapacity:
                        storageCapacity *= effect.Value;
                        break;
                    case TechnologyEffectTypes.EnergyOutput:
                        energyOutput *= effect.Value;
                        break;
                    case TechnologyEffectTypes.PopulationMax:
                        populationMax *= effect.Value;
                        break;
                    default:
                        // The effect opens a subject and changes no parameter of a body.
                        break;
                }
            }
        }

        return new PlanetBonuses(
            metalProduction,
            crystalProduction,
            constructionTime,
            storageCapacity,
            energyOutput,
            populationMax);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, string>> GetTechnologyNameKeysAsync(CancellationToken cancellationToken)
    {
        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);

        return catalog.Technologies.ToDictionary(technology => technology.Id, technology => technology.NameKey);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<int>> GetTechnologyIdsAsync(CancellationToken cancellationToken) =>
        new HashSet<int>(await _dbContext.Technologies.AsNoTracking().Select(technology => technology.Id).ToListAsync(cancellationToken).ConfigureAwait(false));
}

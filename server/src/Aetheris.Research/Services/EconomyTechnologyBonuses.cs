using Aetheris.Economy.Services;
using Aetheris.Research.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// The bonuses the technologies of a player give the economy (task 00015).
/// </summary>
/// <remarks>
/// The economy declares the contract and the research module implements it, so the research
/// module does not depend on the economy and the economy does not depend on the research
/// module.
/// </remarks>
public sealed class EconomyTechnologyBonuses : IEconomyTechnologyBonuses
{
    private readonly ResearchDbContext _dbContext;
    private readonly TechnologyCatalogCache _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyTechnologyBonuses"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The cache of the catalog of the technologies.</param>
    public EconomyTechnologyBonuses(ResearchDbContext dbContext, TechnologyCatalogCache catalogCache)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
    }

    /// <inheritdoc />
    public async Task<EconomyBonuses> GetBonusesAsync(string? playerId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return new EconomyBonuses();
        }

        Dictionary<int, int> levels = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);

        if (levels.Count == 0)
        {
            return new EconomyBonuses();
        }

        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);

        decimal populationGrowth = decimal.One;
        decimal creditIncome = decimal.One;

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
                    case TechnologyEffectTypes.PopulationGrowthBonus:
                        populationGrowth *= effect.Value;
                        break;
                    case TechnologyEffectTypes.CreditIncomeBonus:
                        creditIncome *= effect.Value;
                        break;
                    default:
                        // The other effects change no parameter of the economy; the technologies
                        // that raise the largest population are counted by the bodies module.
                        break;
                }
            }
        }

        return new EconomyBonuses(populationGrowth, creditIncome);
    }
}

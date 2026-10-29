using Aetheris.Research.Data;
using Aetheris.Research.Services;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// The multipliers the technologies give the production (task 00013, decision 14).
/// </summary>
/// <remarks>
/// Every speed has its own multiplier, so the technology that shortens the construction time
/// does not shorten the production time (documentation/tech/09, section 1). The multiplier of
/// the production is less than one for a faster production, like the multiplier of the
/// construction time.
/// </remarks>
/// <param name="ProductionTimeMultiplier">The multiplier of the time of one unit of a recipe.</param>
public sealed record ProductionMultipliers(decimal ProductionTimeMultiplier)
{
    /// <summary>Gets the multipliers without technologies: nothing changes.</summary>
    public static ProductionMultipliers None { get; } = new(decimal.One);
}

/// <summary>
/// The multipliers of the production of a player (task 00013, decision 14).
/// </summary>
public interface IProductionMultipliers
{
    /// <summary>
    /// Gets the multipliers the technologies of a player give the production.
    /// </summary>
    /// <param name="playerId">The player, or <see langword="null"/> for a body without an owner.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The multipliers.</returns>
    Task<ProductionMultipliers> GetMultipliersAsync(string? playerId, CancellationToken cancellationToken);
}

/// <summary>
/// The multipliers of the production, read from the technologies of the player
/// (task 00013, decision 14).
/// </summary>
public sealed class ProductionMultipliersService : IProductionMultipliers
{
    private readonly ResearchDbContext _dbContext;
    private readonly TechnologyCatalogCache _catalogCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionMultipliersService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    public ProductionMultipliersService(ResearchDbContext dbContext, TechnologyCatalogCache catalogCache)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
    }

    /// <inheritdoc />
    public async Task<ProductionMultipliers> GetMultipliersAsync(string? playerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(playerId))
        {
            return ProductionMultipliers.None;
        }

        IReadOnlyDictionary<int, int> levels = await _dbContext.PlayerTechnologies.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId)
            .ToDictionaryAsync(entry => entry.TechnologyId, entry => entry.Level, cancellationToken)
            .ConfigureAwait(false);

        if (levels.Count == 0)
        {
            return ProductionMultipliers.None;
        }

        TechnologyCatalog catalog = await _catalogCache.GetAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        decimal productionTime = decimal.One;

        foreach ((int technologyId, int level) in levels)
        {
            TechnologyInfo? technology = catalog.FindTechnology(technologyId);

            if (technology is null)
            {
                continue;
            }

            foreach (TechnologyEffectInfo effect in technology.Levels.GetValueOrDefault(level)?.Effects ?? [])
            {
                if (effect.EffectType != TechnologyEffectTypes.ProductionTime)
                {
                    continue;
                }

                // An empty multiplier changes nothing; a negative one is broken data, which must
                // never make the production instant (task 00013, decision 14).
                if (effect.Value < 0)
                {
                    throw new InvalidOperationException(
                        $"The technology {technology.Code} has a negative multiplier of the production time at level {level}.");
                }

                if (effect.Value > 0)
                {
                    productionTime *= effect.Value;
                }
            }
        }

        return new ProductionMultipliers(productionTime);
    }
}

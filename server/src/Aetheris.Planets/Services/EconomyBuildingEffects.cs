using Aetheris.Economy.Services;
using Aetheris.Planets.Data;
using Aetheris.Requirements;
using Aetheris.World;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The effects of the buildings of a body for the module of the economy (task 00015).
/// </summary>
/// <remarks>
/// <para>
/// The economy declares the contract and the bodies module implements it, so the bodies
/// module does not depend on the economy and the economy does not depend on the bodies
/// module. This is the pattern of the project: the contract is declared by the module that
/// reads the data and implemented by the module that owns it (see <c>ITechnologyLevels</c>).
/// </para>
/// <para>
/// The implementation reads the buildings of a body itself and not through the service of
/// the reads of a body, because that service takes the service of the construction, and the
/// construction pays the credits through the economy: the objects would make a circle.
/// </para>
/// </remarks>
public sealed class EconomyBuildingEffects : IEconomyBuildingEffects
{
    private readonly PlanetsDbContext _dbContext;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyBuildingEffects"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The cache of the catalog of the buildings.</param>
    /// <param name="world">The world, for the deposits of the bodies.</param>
    /// <param name="requirements">The common requirements module.</param>
    public EconomyBuildingEffects(
        PlanetsDbContext dbContext,
        BuildingCatalogCache catalogCache,
        IWorldModule world,
        IRequirementsModule requirements)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(requirements);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
        _world = world;
        _requirements = requirements;
    }

    /// <inheritdoc />
    public async Task<long> GetEffectTotalAsync(long planetId, string effectType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(effectType);

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);

        Dictionary<int, int> levels = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == planetId)
            .ToDictionaryAsync(building => building.BuildingTypeId, building => building.Level, cancellationToken)
            .ConfigureAwait(false);

        return PlanetEconomy.Effects(catalog, levels)
            .Where(entry => string.Equals(entry.Effect.EffectType, effectType, StringComparison.Ordinal))
            .Sum(entry => entry.Effect.Value);
    }
}

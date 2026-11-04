using Aetheris.Planets.Data;
using Aetheris.Requirements;
using Aetheris.World;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// What the buildings of every body of a player give the whole civilization (task 00028).
/// </summary>
/// <remarks>
/// <para>
/// The science centre and the design bureau of a player give their places and their speed to
/// every body of the player, so their levels are read for all the bodies at once and the
/// largest level of each kind is used. A body without such a building gives nothing.
/// </para>
/// <para>
/// The reading is one query of the buildings of the bodies of the owner, so the answer costs the
/// same however many bodies the player has.
/// </para>
/// </remarks>
public sealed class OwnerBuildingEffects
{
    private readonly PlanetsDbContext _dbContext;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerBuildingEffects"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The cache of the catalog of the buildings.</param>
    /// <param name="world">The world, for the bodies of the player.</param>
    /// <param name="requirements">The common requirements module.</param>
    public OwnerBuildingEffects(
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

    /// <summary>
    /// Gets what the science centres give at a level the caller has read already: only the
    /// cached catalog is read.
    /// </summary>
    /// <param name="researchCenterLevel">The largest level of the science centres of the player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The places and the speed of the research.</returns>
    public async Task<OwnerBuildingBonus> ResearchCenterAsync(int researchCenterLevel, CancellationToken cancellationToken)
    {
        if (researchCenterLevel <= 0)
        {
            return OwnerBuildingBonus.Empty;
        }

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> levels = catalog.Types
            .Where(type => string.Equals(type.BuildingClass, BuildingClasses.ResearchCenter, StringComparison.Ordinal))
            .ToDictionary(type => type.Id, _ => researchCenterLevel);

        return new OwnerBuildingBonus(
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.ResearchQueueSlots),
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.ResearchSpeedBonus));
    }

    /// <summary>
    /// Gets what the buildings of every body of a player give the whole civilization.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The effects of the owner.</returns>
    public async Task<OwnerBuildingBonus> GetAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);

        // Every type of these classes counts, so a second building of a class added by the
        // administrator works like the first one.
        string[] ownerClasses = [BuildingClasses.ResearchCenter, BuildingClasses.DesignBureau, BuildingClasses.Spaceport];
        List<int> typeIds = [.. catalog.Types
            .Where(type => ownerClasses.Contains(type.BuildingClass, StringComparer.Ordinal))
            .Select(type => type.Id)];

        if (typeIds.Count == 0)
        {
            return OwnerBuildingBonus.Empty;
        }

        // The buildings of the bodies of the owner are read in one query: the state of a body
        // names its owner (code audit of 04.11.2026).
        Dictionary<int, int> levels = (await _dbContext.PlanetBuildings.AsNoTracking()
                .Where(building => typeIds.Contains(building.BuildingTypeId))
                .Join(
                    _dbContext.PlanetStates.AsNoTracking().Where(state => state.OwnerPlayerId == playerId),
                    building => building.PlanetId,
                    state => state.PlanetId,
                    (building, state) => new { building.BuildingTypeId, building.Level })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .GroupBy(building => building.BuildingTypeId)
            .ToDictionary(group => group.Key, group => group.Max(building => building.Level));

        // Every effect is read from the levels of the types of the classes only, each at the largest
        // level the owner has, so an effect of the same type on another building never leaks in.
        return new OwnerBuildingBonus(
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.ResearchQueueSlots),
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.ResearchSpeedBonus),
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.DesignQueueSlots),
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.HullClassLimit),
            PlanetEconomy.EffectMax(catalog, levels, BuildingEffectTypes.FleetLimit));
    }
}


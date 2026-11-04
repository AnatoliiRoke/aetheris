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
/// The implementation reads the buildings of the bodies itself and not through the service of
/// the reads of a body, because that service takes the service of the construction, and the
/// construction pays the credits through the economy: the objects would make a circle.
/// </para>
/// <para>
/// The largest population is counted here with the same function the overview of a body uses
/// (<see cref="PlanetEconomy.PopulationMax"/>) and with the bonus of the technologies of the
/// owner of the body, so the bodies module and the economy show one number.
/// </para>
/// </remarks>
public sealed class EconomyBuildingEffects : IEconomyBuildingEffects
{
    private readonly PlanetsDbContext _dbContext;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IRequirementsModule _requirements;
    private readonly ITechnologyLevels _technologies;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyBuildingEffects"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="catalogCache">The cache of the catalog of the buildings.</param>
    /// <param name="world">The world, for the deposits of the bodies.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="technologies">The technologies of the players.</param>
    public EconomyBuildingEffects(
        PlanetsDbContext dbContext,
        BuildingCatalogCache catalogCache,
        IWorldModule world,
        IRequirementsModule requirements,
        ITechnologyLevels technologies)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(technologies);

        _dbContext = dbContext;
        _catalogCache = catalogCache;
        _world = world;
        _requirements = requirements;
        _technologies = technologies;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<long, PlanetEconomyEffects>> GetAsync(
        IReadOnlyCollection<long> planetIds,
        CancellationToken cancellationToken = default) =>
        ComputeAsync(planetIds, null, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<long, PlanetEconomyEffects>> GetAsync(
        IReadOnlyCollection<long> planetIds,
        string ownerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerPlayerId);

        return ComputeAsync(planetIds, ownerPlayerId, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<long, PlanetEconomyEffects>> ComputeAsync(
        IReadOnlyCollection<long> planetIds,
        string? knownOwnerPlayerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planetIds);

        if (planetIds.Count == 0)
        {
            return new Dictionary<long, PlanetEconomyEffects>();
        }

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);

        // The buildings and the owners of all the bodies are read at once, and the bonuses of the
        // technologies once for every owner. A caller that knows the owner of the bodies says so,
        // and the owners are not read at all.
        Dictionary<long, Dictionary<int, int>> levelsByPlanet = (await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => planetIds.Contains(building.PlanetId))
            .Select(building => new { building.PlanetId, building.BuildingTypeId, building.Level })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .GroupBy(building => building.PlanetId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(building => building.BuildingTypeId, building => building.Level));

        Dictionary<long, string?> owners = knownOwnerPlayerId is not null
            ? []
            : await _dbContext.PlanetStates.AsNoTracking()
                .Where(state => planetIds.Contains(state.PlanetId))
                .ToDictionaryAsync(state => state.PlanetId, state => state.OwnerPlayerId, cancellationToken)
                .ConfigureAwait(false);

        Dictionary<(string OwnerPlayerId, long PlanetId), PlanetBonuses> bonusesByBody = [];
        Dictionary<long, PlanetEconomyEffects> effects = [];

        // The technologies are researched from a body, so the bonuses of the technologies are the
        // ones of that body, and every body of a player has its own. The levels of all the bodies
        // are read at once.
        IReadOnlyDictionary<long, PlanetBonuses> bonusesByPlanet = knownOwnerPlayerId is null
            ? new Dictionary<long, PlanetBonuses>()
            : await _technologies.GetBonusesAsync(knownOwnerPlayerId, planetIds, cancellationToken).ConfigureAwait(false);

        foreach (long planetId in planetIds)
        {
            string? ownerPlayerId = knownOwnerPlayerId ?? owners.GetValueOrDefault(planetId);
            string ownerKey = ownerPlayerId ?? string.Empty;

            if (!bonusesByBody.TryGetValue((ownerKey, planetId), out PlanetBonuses? bonuses))
            {
                bonuses = knownOwnerPlayerId is null
                    ? await _technologies.GetBonusesAsync(ownerPlayerId, planetId, cancellationToken).ConfigureAwait(false)
                    : bonusesByPlanet.GetValueOrDefault(planetId) ?? PlanetBonuses.None;
                bonusesByBody[(ownerKey, planetId)] = bonuses;
            }

            Dictionary<int, int> levels = levelsByPlanet.GetValueOrDefault(planetId) ?? [];

            effects[planetId] = new PlanetEconomyEffects(
                PlanetEconomy.PopulationMax(catalog, levels, bonuses),
                PlanetEconomy.Effects(catalog, levels)
                    .Where(entry => string.Equals(entry.Effect.EffectType, BuildingEffectTypes.PopulationGrowthBonus, StringComparison.Ordinal))
                    .Sum(entry => entry.Effect.Value),
                PlanetEconomy.Effects(catalog, levels)
                    .Where(entry => string.Equals(entry.Effect.EffectType, BuildingEffectTypes.CreditIncomeBonus, StringComparison.Ordinal))
                    .Sum(entry => entry.Effect.Value));
        }

        return effects;
    }
}

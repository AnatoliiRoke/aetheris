using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The reads of a body for its owner (task 00011, section 3.1).
/// </summary>
/// <remarks>
/// The balance is computed for the moment of the read and not written. A construction that is
/// due but not completed yet, because the scheduler has not woken up, is completed first, so
/// the player never sees a finished timer on an old level.
/// </remarks>
public sealed class PlanetQueryService
{
    private readonly PlanetsDbContext _dbContext;
    private readonly ConstructionService _construction;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IEconomyModule _economy;
    private readonly ITechnologyLevels _technologies;
    private readonly IRequirementsModule _requirements;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="construction">The changes of a body.</param>
    /// <param name="catalogCache">The building catalog.</param>
    /// <param name="world">The world, for the owner of a body.</param>
    /// <param name="technologies">The technologies of the players.</param>
    /// <param name="requirements">The common requirements module.</param>
    /// <param name="clock">The game clock.</param>
    public PlanetQueryService(
        PlanetsDbContext dbContext,
        ConstructionService construction,
        BuildingCatalogCache catalogCache,
        IWorldModule world,
        IEconomyModule economy,
        ITechnologyLevels technologies,
        IRequirementsModule requirements,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _construction = construction;
        _catalogCache = catalogCache;
        _world = world;
        _economy = economy;
        _technologies = technologies;
        _requirements = requirements;
        _clock = clock;
    }

    /// <summary>
    /// Gets a body of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The body, or <see langword="null"/> when the player does not own it or it is not a colony.</returns>
    public async Task<PlanetOverviewDto?> GetAsync(string playerId, long planetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            return null;
        }

        DateTimeOffset now = _clock.GameNow;

        // The queue is read with the body, so a construction that is due is told by the same rows
        // and no separate question is asked (task 00036, section 6.3).
        PlanetRead? read = await _construction.ReadWithPopulationAsync(planetId, now, facts, cancellationToken).ConfigureAwait(false);

        if (read is { HasDueConstruction: true })
        {
            await _construction.CompleteDueAsync(planetId, now, cancellationToken).ConfigureAwait(false);
            read = await _construction.ReadWithPopulationAsync(planetId, now, facts, cancellationToken).ConfigureAwait(false);
        }

        if (read is null)
        {
            return null;
        }

        return Enrich(facts, read.Overview, read.Population);
    }

    /// <summary>
    /// Adds the facts of the other modules to the answer of a command on a body, so the answer
    /// the client takes in place of the body is as full as a read (task 00020). The command
    /// has checked the owner already.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="overview">The answer of the command.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The answer with the facts of the other modules.</returns>
    public async Task<PlanetOverviewDto> EnrichOwnedAsync(long planetId, PlanetOverviewDto overview, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(overview);

        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts?.OwnerPlayerId is not { } ownerPlayerId)
        {
            return overview;
        }

        // The population belongs to the economy module; a body without a population yet has
        // none, and the client shows a placeholder instead of a made-up number.
        PopulationFacts? population = await _economy.GetPopulationAsync(ownerPlayerId, planetId, cancellationToken).ConfigureAwait(false);

        return Enrich(facts, overview, population);
    }

    /// <summary>
    /// Adds to the answer of a body the facts that the other modules own: the name and the
    /// place of the body from the world, the population from the economy (task 00020).
    /// </summary>
    /// <param name="facts">The facts of the body from the world.</param>
    /// <param name="overview">The answer of the bodies module.</param>
    /// <param name="population">The population of the body from the economy.</param>
    /// <returns>The answer with the facts of the other modules.</returns>
    private static PlanetOverviewDto Enrich(
        PlanetFacts facts,
        PlanetOverviewDto overview,
        PopulationFacts? population)
    {
        return overview with
        {
            Name = facts.Name,
            // The type is linked by its identifier, so the client reads the name and the picture
            // of the type from the reference table of the world (documentation/tech/09, 3.3).
            TypeId = facts.ObjectTypeId,
            Coordinates = new PlanetCoordinatesDto(facts.SectorId, facts.SystemId, facts.Orbit, facts.SystemName, facts.SectorName),
            Population = population?.Amount ?? 0,
            PopulationGrowthPerHour = population?.GrowthPerHour ?? 0,
            TimeToMaximum = FormatTimeToMaximum(population?.TimeToMaximum),
            DepositRichness = [.. facts.Deposits.Select(deposit => new DepositRichnessDto(deposit.ResourceTypeId, deposit.Coefficient))],
        };
    }

    /// <summary>
    /// The time to the largest population as the server sends it: `[-][d.]hh:mm:ss`.
    /// </summary>
    /// <param name="time">The time span.</param>
    /// <returns>The text of the time, or <see langword="null"/> when there is no span.</returns>
    private static string? FormatTimeToMaximum(TimeSpan? time)
    {
        if (time is not { } span)
        {
            return null;
        }

        bool negative = span < TimeSpan.Zero;
        TimeSpan absolute = negative ? span.Negate() : span;

        string text = absolute.Days > 0
            ? $"{absolute.Days}.{absolute.Hours:00}:{absolute.Minutes:00}:{absolute.Seconds:00}"
            : $"{absolute.Hours:00}:{absolute.Minutes:00}:{absolute.Seconds:00}";

        return negative ? $"-{text}" : text;
    }

    /// <summary>
    /// Checks the requirements of a subject on a body: whether it is available and why not
    /// (task 00011, decision 3).
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The answer.</returns>
    public async Task<AvailabilityDto> CheckAsync(
        string subjectType,
        int subjectId,
        int level,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        Dictionary<int, int> levels = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == planetId)
            .ToDictionaryAsync(building => building.BuildingTypeId, building => building.Level, cancellationToken)
            .ConfigureAwait(false);

        RequirementContext context = new(
            levels,
            (facts?.Deposits ?? []).Select(deposit => deposit.ResourceTypeId).ToHashSet(),
            await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false));

        // The building catalog keeps the requirements of the building types; the requirements
        // of the other subjects are read from the common module, and the same check applies.
        IReadOnlyList<UnmetRequirement> unmet = string.Equals(subjectType, RequirementSubjectTypes.BuildingType, StringComparison.Ordinal)
            ? RequirementChecker.Check(catalog, subjectType, subjectId, level, context)
            : RequirementChecker.Check(
                (await _requirements.GetAsync(subjectType, subjectId, level, cancellationToken).ConfigureAwait(false))
                    .Select(requirement => new RequirementInfo(
                        requirement.Id,
                        subjectType,
                        subjectId,
                        level,
                        requirement.RequirementType,
                        requirement.TargetId,
                        requirement.TargetLevel)),
                context);

        IReadOnlyDictionary<int, string> technologyNameKeys = await _technologies.GetTechnologyNameKeysAsync(cancellationToken).ConfigureAwait(false);

        return new AvailabilityDto(unmet.Count == 0, PlanetOverviewBuilder.Requirements(catalog, unmet, technologyNameKeys: technologyNameKeys));
    }

    /// <summary>
    /// Gets the levels of the buildings of a body by type.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The largest level of a building by its type.</returns>
    public async Task<IReadOnlyDictionary<int, int>> GetPlanetBuildingLevelsAsync(long planetId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == planetId)
            .Select(building => new { building.BuildingTypeId, building.Level })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(building => building.BuildingTypeId)
            .ToDictionary(group => group.Key, group => group.Max(building => building.Level));
    }

    /// <summary>
    /// Gets the level of the building of a class on a body (task 00012, decision 6).
    /// </summary>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, or <see langword="null"/> when the level of any body is wanted.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the building, or zero when the body has no building of the class.</returns>
    public async Task<int> GetBuildingLevelAsync(
        string buildingClass,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildingClass);

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);
        BuildingTypeInfo? type = catalog.FindByClass(buildingClass);

        if (type is null)
        {
            return 0;
        }

        if (playerId is not null)
        {
            PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

            if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
            {
                return 0;
            }
        }

        return await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == planetId && building.BuildingTypeId == type.Id)
            .Select(building => (int?)building.Level)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? 0;
    }

    /// <summary>
    /// Finds the identifier of a building type by its neutral code (task 00012).
    /// </summary>
    /// <param name="code">The neutral code of the building type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier, or <see langword="null"/> when the code is unknown.</returns>
    public Task<int?> FindBuildingTypeIdAsync(string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return _dbContext.BuildingTypes.AsNoTracking()
            .Where(type => type.Code == code)
            .Select(type => (int?)type.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds the identifier of a building type by its class.
    /// </summary>
    /// <param name="buildingClass">The class of the building type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier, or <see langword="null"/> when no building has the class.</returns>
    public Task<int?> FindBuildingTypeIdByClassAsync(string buildingClass, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildingClass);

        return _dbContext.BuildingTypes.AsNoTracking()
            .Where(type => type.BuildingClass == buildingClass)
            .OrderBy(type => type.Id)
            .Select(type => (int?)type.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets every body of a player with the level of its building of a class (task 00012,
    /// decisions 5 and 5a).
    /// </summary>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bodies of the player in the order of their identifiers.</returns>
    /// <remarks>
    /// The bodies come from the state rows of the module and the owner is confirmed by the
    /// world, so a body the player has lost is never listed.
    /// </remarks>
    public async Task<IReadOnlyList<PlanetBuildingLevel>> GetBuildingLevelsAsync(
        string buildingClass,
        string playerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildingClass);

        return (await GetBuildingLevelsAsync([buildingClass], playerId, cancellationToken).ConfigureAwait(false))[buildingClass];
    }

    /// <summary>
    /// Gets every body of a player with the level of its building of each of several classes, from
    /// one read of the bodies and one read of the buildings (task 00036, section 6.3).
    /// </summary>
    /// <param name="buildingClasses">The classes of the buildings, each one of <see cref="BuildingClasses"/>.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bodies of the player in the order of their identifiers, by class.</returns>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<PlanetBuildingLevel>>> GetBuildingLevelsAsync(
        IReadOnlyCollection<string> buildingClasses,
        string playerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildingClasses);
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);
        Dictionary<string, int?> typeIdByClass = buildingClasses
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(buildingClass => buildingClass, buildingClass => catalog.FindByClass(buildingClass)?.Id, StringComparer.Ordinal);

        // The bodies the state rows of the module list, confirmed by the owner the world names:
        // one query of the owned bodies of the world replaces the facts of every body.
        HashSet<long> stateIds = [.. await _dbContext.PlanetStates.AsNoTracking()
            .Where(state => state.OwnerPlayerId == playerId)
            .Select(state => state.PlanetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)];
        IReadOnlyList<OwnedPlanet> owned = await _world.GetOwnedPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false);
        List<OwnedPlanet> colonies = [.. owned.Where(planet => stateIds.Contains(planet.PlanetId))];
        List<long> planetIds = [.. colonies.Select(planet => planet.PlanetId)];
        List<int> typeIds = [.. typeIdByClass.Values.Where(typeId => typeId is not null).Select(typeId => typeId!.Value).Distinct()];

        var rows = typeIds.Count == 0 || planetIds.Count == 0
            ? []
            : await _dbContext.PlanetBuildings.AsNoTracking()
                .Where(building => planetIds.Contains(building.PlanetId) && typeIds.Contains(building.BuildingTypeId))
                .Select(building => new { building.PlanetId, building.BuildingTypeId, building.Level })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        Dictionary<string, IReadOnlyList<PlanetBuildingLevel>> result = new(StringComparer.Ordinal);

        foreach ((string buildingClass, int? typeId) in typeIdByClass)
        {
            Dictionary<long, int> levels = rows
                .Where(row => row.BuildingTypeId == typeId)
                .GroupBy(row => row.PlanetId)
                .ToDictionary(group => group.Key, group => group.Last().Level);

            result[buildingClass] = [.. colonies.Select(planet => new PlanetBuildingLevel(planet.PlanetId, planet.Name, levels.GetValueOrDefault(planet.PlanetId)))];
        }

        return result;
    }

    /// <summary>
    /// Gets the localisation keys of the names of the building types (task 00012, decision 9).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The keys by building type.</returns>
    public async Task<IReadOnlyDictionary<int, string>> GetBuildingNameKeysAsync(CancellationToken cancellationToken)
    {
        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);

        return catalog.Types.ToDictionary(type => type.Id, type => type.NameKey);
    }

    /// <summary>
    /// Gets the sum of one effect of the buildings of a body at their levels: the room of the
    /// storage of the parts, for example (task 00013, decision 9).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="effectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum of the values of the effect, or zero when no building has it.</returns>
    public async Task<long> GetBuildingEffectTotalAsync(long planetId, string effectType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(effectType);

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> levels = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == planetId)
            .ToDictionaryAsync(building => building.BuildingTypeId, building => building.Level, cancellationToken)
            .ConfigureAwait(false);

        // The effects counted here (room, limits) are whole numbers; a fraction is cut.
        return (long)decimal.Floor(PlanetEconomy.Effects(catalog, levels)
            .Where(entry => string.Equals(entry.Effect.EffectType, effectType, StringComparison.Ordinal))
            .Sum(entry => entry.Effect.Value));
    }

    /// <summary>
    /// Gets the sum of one effect of the buildings of several bodies at once, in one read of the
    /// buildings and not one read for every body (task 00029).
    /// </summary>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="effectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum by body; a body without the effect has zero.</returns>
    public async Task<IReadOnlyDictionary<long, long>> GetBuildingEffectTotalsAsync(
        IReadOnlyCollection<long> planetIds,
        string effectType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planetIds);
        ArgumentException.ThrowIfNullOrEmpty(effectType);

        Dictionary<long, long> totals = planetIds.Distinct().ToDictionary(planetId => planetId, _ => 0L);

        if (totals.Count == 0)
        {
            return totals;
        }

        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, _requirements, cancellationToken).ConfigureAwait(false);
        var rows = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => planetIds.Contains(building.PlanetId))
            .Select(building => new { building.PlanetId, building.BuildingTypeId, building.Level })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var group in rows.GroupBy(row => row.PlanetId))
        {
            Dictionary<int, int> levels = group.ToDictionary(row => row.BuildingTypeId, row => row.Level);

            totals[group.Key] = (long)decimal.Floor(PlanetEconomy.Effects(catalog, levels)
                .Where(entry => string.Equals(entry.Effect.EffectType, effectType, StringComparison.Ordinal))
                .Sum(entry => entry.Effect.Value));
        }

        return totals;
    }

    /// <summary>
    /// Gets the level of a building type on a body of a player.
    /// </summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the building, or zero when the body has no building of the type.</returns>
    public async Task<int> GetBuildingLevelByTypeAsync(
        int buildingTypeId,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        IQueryable<PlanetBuildingEntity> buildings = _dbContext.PlanetBuildings
            .Where(building => building.PlanetId == planetId && building.BuildingTypeId == buildingTypeId);

        if (playerId is not null)
        {
            buildings = buildings.Where(building => _dbContext.PlanetStates
                .Any(state => state.PlanetId == building.PlanetId && state.OwnerPlayerId == playerId));
        }

        return await buildings.AsNoTracking()
            .Select(building => building.Level)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the level of one building of a body and the type it is of.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The building, or a record with zero level when the body has none.</returns>
    public async Task<PlanetBuildingLevelById> GetBuildingLevelByIdAsync(
        long planetId,
        long planetBuildingId,
        string? playerId,
        CancellationToken cancellationToken)
    {
        IQueryable<PlanetBuildingEntity> buildings = _dbContext.PlanetBuildings
            .Where(building => building.PlanetId == planetId && building.Id == planetBuildingId);

        if (playerId is not null)
        {
            buildings = buildings.Where(building => _dbContext.PlanetStates
                .Any(state => state.PlanetId == building.PlanetId && state.OwnerPlayerId == playerId));
        }

        PlanetBuildingEntity? building = await buildings.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return building is null
            ? new PlanetBuildingLevelById(planetBuildingId, 0, 0)
            : new PlanetBuildingLevelById(building.Id, building.BuildingTypeId, building.Level);
    }

    /// <summary>
    /// Gets the buildings of a body that are of the given types.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="buildingTypeIds">The types of the buildings to look for.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The buildings of the body that are of the given types.</returns>
    public async Task<IReadOnlyList<PlanetBuildingLevelById>> GetBuildingsByTypesAsync(
        long planetId,
        IReadOnlyCollection<int> buildingTypeIds,
        string? playerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buildingTypeIds);

        if (buildingTypeIds.Count == 0)
        {
            return [];
        }

        IQueryable<PlanetBuildingEntity> buildings = _dbContext.PlanetBuildings
            .Where(building => building.PlanetId == planetId && buildingTypeIds.Contains(building.BuildingTypeId));

        if (playerId is not null)
        {
            buildings = buildings.Where(building => _dbContext.PlanetStates
                .Any(state => state.PlanetId == building.PlanetId && state.OwnerPlayerId == playerId));
        }

        List<PlanetBuildingEntity> rows = await buildings.AsNoTracking()
            .OrderBy(building => building.BuildingTypeId)
            .ThenBy(building => building.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(building => new PlanetBuildingLevelById(building.Id, building.BuildingTypeId, building.Level))
        ];
    }

    /// <summary>
    /// Forgets the building catalog kept in memory.
    /// </summary>
    public void InvalidateCatalog() => _catalogCache.Invalidate();
}

/// <summary>
/// A body of a player with the level of its building of a class (task 00012, decision 5).
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="Name">The name of the body.</param>
/// <param name="Level">The level of the building, or zero when the body has none.</param>
public sealed record PlanetBuildingLevel(long PlanetId, string? Name, int Level);

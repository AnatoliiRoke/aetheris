using Aetheris.Core.Time;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
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
    private readonly ITechnologyLevels _technologies;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="construction">The changes of a body.</param>
    /// <param name="catalogCache">The building catalog.</param>
    /// <param name="world">The world, for the owner of a body.</param>
    /// <param name="technologies">The technologies of the players.</param>
    /// <param name="clock">The game clock.</param>
    public PlanetQueryService(
        PlanetsDbContext dbContext,
        ConstructionService construction,
        BuildingCatalogCache catalogCache,
        IWorldModule world,
        ITechnologyLevels technologies,
        IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(technologies);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _construction = construction;
        _catalogCache = catalogCache;
        _world = world;
        _technologies = technologies;
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

        bool due = await _dbContext.QueueItems.AsNoTracking()
            .AnyAsync(item => item.PlanetId == planetId && item.FinishesAt != null && item.FinishesAt <= now, cancellationToken)
            .ConfigureAwait(false);

        if (due)
        {
            await _construction.CompleteDueAsync(planetId, now, cancellationToken).ConfigureAwait(false);
        }

        return await _construction.ReadAsync(planetId, now, cancellationToken).ConfigureAwait(false);
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
        BuildingCatalog catalog = await _catalogCache.GetAsync(_dbContext, _world, cancellationToken).ConfigureAwait(false);
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        Dictionary<int, int> levels = await _dbContext.PlanetBuildings.AsNoTracking()
            .Where(building => building.PlanetId == planetId)
            .ToDictionaryAsync(building => building.BuildingTypeId, building => building.Level, cancellationToken)
            .ConfigureAwait(false);

        RequirementContext context = new(
            levels,
            (facts?.Deposits ?? []).Select(deposit => deposit.ResourceTypeId).ToHashSet(),
            await _technologies.GetLevelsAsync(playerId, cancellationToken).ConfigureAwait(false));

        IReadOnlyList<UnmetRequirement> unmet = RequirementChecker.Check(catalog, subjectType, subjectId, level, context);

        return new AvailabilityDto(unmet.Count == 0, PlanetOverviewBuilder.Requirements(catalog, unmet));
    }
}

using Aetheris.Core.Errors;
using Aetheris.Core.Text;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The first step of the game: a player chooses a race and names the home body, gets the
/// start body and the start package of the race (task 00006, decisions 9 and 11).
/// </summary>
/// <remarks>
/// <para>
/// Everything happens in one transaction: the start body, its name, the civilization, the
/// start resources and the start building. A player has one civilization; a second call is
/// refused, so the race never changes and the package is never given twice.
/// </para>
/// <para>
/// The start resources come from <c>race_start_resource</c>, each up to
/// <c>world.civilization.start_storage_capacity</c>, and go to <see cref="IHomePlanetStore"/>,
/// the balance of the body (task 00011). The start building of the race goes to
/// <see cref="IStartBuildingPlacer"/>.
/// </para>
/// </remarks>
public sealed class CivilizationService
{
    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly StartPlanetAllocator _allocator;
    private readonly WorldReferenceRepository _reference;
    private readonly IStartBuildingPlacer _buildingPlacer;
    private readonly IHomePlanetStore _homeStore;
    private readonly ITextModeration _moderation;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="CivilizationService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="allocator">The start body allocator.</param>
    /// <param name="reference">The parameters.</param>
    /// <param name="buildingPlacer">The placer of the start building.</param>
    /// <param name="homeStore">The store of the amounts of a home body.</param>
    /// <param name="moderation">The restricted words check.</param>
    /// <param name="timeProvider">The clock.</param>
    public CivilizationService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        StartPlanetAllocator allocator,
        WorldReferenceRepository reference,
        IStartBuildingPlacer buildingPlacer,
        IHomePlanetStore homeStore,
        ITextModeration moderation,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(buildingPlacer);
        ArgumentNullException.ThrowIfNull(homeStore);
        ArgumentNullException.ThrowIfNull(moderation);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _allocator = allocator;
        _reference = reference;
        _buildingPlacer = buildingPlacer;
        _homeStore = homeStore;
        _moderation = moderation;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Gets the races in the order of the list.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The races.</returns>
    public Task<List<RaceDto>> GetRacesAsync(CancellationToken cancellationToken) =>
        _dbContext.Races.AsNoTracking()
            .OrderBy(race => race.SortOrder)
            .ThenBy(race => race.Id)
            .Select(race => new RaceDto(
                race.Id,
                race.Code,
                race.NameKey,
                race.DescriptionKey,
                race.MoodKey,
                race.TraitKeys,
                race.ImageKey,
                race.ColorKey,
                race.PreferredObjectTypeId))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Gets the civilization of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The civilization, or <see langword="null"/> before the first step.</returns>
    public Task<CivilizationSummary?> GetAsync(string playerId, CancellationToken cancellationToken) =>
        (from civilization in _dbContext.Civilizations.AsNoTracking()
         join race in _dbContext.Races on civilization.RaceId equals race.Id
         join planet in _dbContext.Planets on civilization.PlanetId equals planet.Id
         where civilization.PlayerId == playerId
         select new CivilizationSummary(
             race.Id,
             race.Code,
             race.NameKey,
             planet.Id,
             planet.Name,
             planet.SystemId,
             planet.SectorId,
             civilization.FoundedAt))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Gets the home bodies of every civilization with the start building of the race.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The home bodies.</returns>
    public Task<List<CivilizationHome>> GetHomesAsync(CancellationToken cancellationToken) =>
        (from civilization in _dbContext.Civilizations.AsNoTracking()
         join race in _dbContext.Races on civilization.RaceId equals race.Id
         orderby civilization.PlanetId
         select new CivilizationHome(
             civilization.PlanetId,
             civilization.SectorId,
             civilization.PlayerId,
             race.StartBuildingTypeId,
             race.StartBuildingLevel))
        .ToListAsync(cancellationToken);

    /// <summary>
    /// Takes the amounts of a body out of the old <c>planet_storage</c> table of task 00006:
    /// they are returned and removed, so a second call returns nothing.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The amounts by resource.</returns>
    public Task<IReadOnlyDictionary<int, long>> TakeLegacyStorageAsync(long planetId, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync<IReadOnlyDictionary<int, long>>(
            async () =>
            {
                Dictionary<int, long> amounts = await _dbContext.Storage.AsNoTracking()
                    .Where(item => item.PlanetId == planetId)
                    .ToDictionaryAsync(item => item.ResourceTypeId, item => item.Amount, cancellationToken)
                    .ConfigureAwait(false);

                await _dbContext.Storage.Where(item => item.PlanetId == planetId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
                await _dbContext.StartBuildingGrants.Where(item => item.PlanetId == planetId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

                return amounts;
            },
            cancellationToken);

    /// <summary>
    /// Sets the start building of every race that has none yet.
    /// </summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of races changed.</returns>
    public Task<int> AssignStartBuildingAsync(int buildingTypeId, int level, CancellationToken cancellationToken) =>
        _dbContext.Races
            .Where(race => race.StartBuildingTypeId == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(race => race.StartBuildingTypeId, buildingTypeId)
                    .SetProperty(race => race.StartBuildingLevel, level),
                cancellationToken);

    /// <summary>
    /// Creates the civilization of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="raceId">The chosen race.</param>
    /// <param name="homeName">The name of the home body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The civilization.</returns>
    public Task<CivilizationSummary> FoundAsync(string playerId, int raceId, string homeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(homeName);

        return _unitOfWork.RunAsync(() => FoundInTransactionAsync(playerId, raceId, homeName.Trim(), cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Releases everything a player holds in the world, for the deletion of the account: the
    /// bodies become free again, and the civilization, the storage and the start grant go.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the player is released.</returns>
    public Task ReleaseAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(() => ReleaseInTransactionAsync(playerId, cancellationToken), cancellationToken);
    }

    private async Task<CivilizationSummary> FoundInTransactionAsync(string playerId, int raceId, string homeName, CancellationToken cancellationToken)
    {
        // Two calls of one player wait for each other; the allocation takes the same lock.
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({playerId}, 0))", cancellationToken)
            .ConfigureAwait(false);

        if (await _dbContext.Civilizations.AnyAsync(civilization => civilization.PlayerId == playerId, cancellationToken).ConfigureAwait(false))
        {
            throw new GameException(WorldErrorCodes.CivilizationExists, "The player already has a civilization.");
        }

        RaceEntity race = await _dbContext.Races.AsNoTracking()
            .Include(entry => entry.StartResourceIds)
            .SingleOrDefaultAsync(entry => entry.Id == raceId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(WorldErrorCodes.RaceUnknown, "The race does not exist.");

        WorldSettings settings = _reference.Settings;
        await ValidateHomeNameAsync(homeName, settings, cancellationToken).ConfigureAwait(false);

        StartPlanetResult start = await _allocator
            .AllocateInTransactionAsync(playerId, race.PreferredObjectTypeId, cancellationToken)
            .ConfigureAwait(false);

        PlanetEntity planet = await _dbContext.Planets
            .SingleAsync(entry => entry.Id == start.PlanetId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        planet.Name = homeName;
        _dbContext.Civilizations.Add(new PlayerCivilizationEntity
        {
            PlayerId = playerId,
            RaceId = race.Id,
            PlanetId = planet.Id,
            SectorId = planet.SectorId,
            FoundedAt = now,
        });

        Dictionary<int, long> startResources = race.StartResourceIds
            .Where(entry => entry.Amount > 0)
            .ToDictionary(entry => entry.ResourceTypeId, entry => Math.Min(entry.Amount, settings.StartStorageCapacity));

        await _homeStore
            .WriteStartResourcesAsync(playerId, planet.Id, planet.SectorId, startResources, cancellationToken)
            .ConfigureAwait(false);

        if (race.StartBuildingTypeId is { } buildingTypeId)
        {
            await _buildingPlacer
                .PlaceAsync(planet.Id, planet.SectorId, buildingTypeId, race.StartBuildingLevel, cancellationToken)
                .ConfigureAwait(false);
        }

        return new CivilizationSummary(race.Id, race.Code, race.NameKey, planet.Id, planet.Name, planet.SystemId, planet.SectorId, now);
    }

    private async Task ValidateHomeNameAsync(string homeName, WorldSettings settings, CancellationToken cancellationToken)
    {
        List<string> errors = [];

        if (homeName.Length < settings.HomeNameMinLength)
        {
            errors.Add(WorldErrorCodes.HomeNameTooShort);
        }

        if (homeName.Length > settings.HomeNameMaxLength)
        {
            errors.Add(WorldErrorCodes.HomeNameTooLong);
        }

        if (homeName.Length > 0 && !settings.HomeNameCharacters.IsMatch(homeName))
        {
            errors.Add(WorldErrorCodes.HomeNameInvalidCharacters);
        }

        if (errors.Count == 0 && !await _moderation.IsAllowedAsync(homeName, cancellationToken).ConfigureAwait(false))
        {
            errors.Add(WorldErrorCodes.HomeNameRestricted);
        }

        if (errors.Count > 0)
        {
            throw new GameException(GameErrorCodes.ValidationFailed, "The name of the home body is not accepted.", new Dictionary<string, object?>
            {
                ["planet_name"] = errors.ToArray(),
            });
        }
    }

    private async Task<bool> ReleaseInTransactionAsync(string playerId, CancellationToken cancellationToken)
    {
        List<PlanetEntity> planets = await _dbContext.Planets
            .Where(planet => planet.OwnerPlayerId == playerId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<long> planetIds = [.. planets.Select(planet => planet.Id)];

        StartPlanetAllocationEntity? allocation = await _dbContext.StartAllocations
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId, cancellationToken).ConfigureAwait(false);

        await _dbContext.Civilizations.Where(entry => entry.PlayerId == playerId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Storage.Where(entry => planetIds.Contains(entry.PlanetId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.StartBuildingGrants.Where(entry => planetIds.Contains(entry.PlanetId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _homeStore.ReleaseAsync(planetIds, cancellationToken).ConfigureAwait(false);

        foreach (PlanetEntity planet in planets)
        {
            planet.OwnerPlayerId = null;
            planet.Name = null;
        }

        if (allocation is not null)
        {
            // The newcomer no longer counts against the limit of the system.
            long systemId = await _dbContext.Planets.AsNoTracking()
                .Where(planet => planet.Id == allocation.PlanetId)
                .Select(planet => planet.SystemId)
                .SingleAsync(cancellationToken).ConfigureAwait(false);

            await _dbContext.Systems
                .Where(system => system.Id == systemId && system.NewbieCount > 0)
                .ExecuteUpdateAsync(setters => setters.SetProperty(system => system.NewbieCount, system => system.NewbieCount - 1), cancellationToken)
                .ConfigureAwait(false);

            _dbContext.StartAllocations.Remove(allocation);
        }

        return true;
    }
}

using Aetheris.Core.Time;
using Aetheris.Economy.Services;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheris.Planets.Services;

/// <summary>
/// Writes the start package of a new civilization into the bodies module (task 00011,
/// section 3.1): the state of the home body and its balance, and the start building.
/// </summary>
/// <remarks>
/// The world calls both extension points in the transaction of the civilization step; the
/// changes here join it. The start resources come first, so the state row exists when the
/// start building is placed. The home body is a colony from this moment, so its row of the
/// population is written in the same transaction through the contract of the economy module
/// (task 00015); a later colonization writes the row the same way.
/// </remarks>
public sealed class PlanetStartPackage : IHomePlanetStore, IStartBuildingPlacer
{
    private readonly PlanetsDbContext _dbContext;
    private readonly PlanetsUnitOfWork _unitOfWork;
    private readonly IGameClock _clock;
    private readonly IColonyRegistry _colonies;
    private readonly ILogger<PlanetStartPackage> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetStartPackage"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="colonies">The rows of the population of the colonies.</param>
    /// <param name="logger">The logger.</param>
    public PlanetStartPackage(
        PlanetsDbContext dbContext,
        PlanetsUnitOfWork unitOfWork,
        IGameClock clock,
        IColonyRegistry colonies,
        ILogger<PlanetStartPackage> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(colonies);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _colonies = colonies;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task WriteStartResourcesAsync(
        string playerId,
        long planetId,
        int sectorId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureStateAsync(planetId, sectorId, playerId, cancellationToken).ConfigureAwait(false);
                await AddResourcesAsync(planetId, sectorId, amounts, cancellationToken).ConfigureAwait(false);

                // The body is a colony of the player from now on: its population is counted from here.
                await _colonies.FoundAsync(playerId, planetId, sectorId, cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task PlaceAsync(long planetId, int sectorId, int buildingTypeId, int level, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureStateAsync(planetId, sectorId, null, cancellationToken).ConfigureAwait(false);
                await PlaceBuildingAsync(planetId, sectorId, buildingTypeId, level, cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task ReleaseAsync(IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planetIds);

        // The buildings, the queue, the balance and the storage states go with the state row, and
        // the population goes with the colony.
        return _unitOfWork.RunAsync(
            async () =>
            {
                await _colonies.ReleaseAsync(planetIds, cancellationToken).ConfigureAwait(false);

                return await _dbContext.PlanetStates.Where(state => planetIds.Contains(state.PlanetId)).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>
    /// Creates the state row of a body when it is missing.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="playerId">The owner, if known.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The state row.</returns>
    public async Task<PlanetStateEntity> EnsureStateAsync(long planetId, int sectorId, string? playerId, CancellationToken cancellationToken)
    {
        PlanetStateEntity? state = _dbContext.PlanetStates.Local.FirstOrDefault(entry => entry.PlanetId == planetId)
            ?? await _dbContext.PlanetStates.SingleOrDefaultAsync(entry => entry.PlanetId == planetId, cancellationToken).ConfigureAwait(false);

        if (state is null)
        {
            state = new PlanetStateEntity
            {
                PlanetId = planetId,
                SectorId = sectorId,
                OwnerPlayerId = playerId,
                ResourcesUpdatedAt = _clock.GameNow,
            };

            _dbContext.PlanetStates.Add(state);
        }
        else if (playerId is not null)
        {
            state.OwnerPlayerId = playerId;
        }

        return state;
    }

    /// <summary>
    /// Adds amounts to the balance of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="amounts">The amounts by resource.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are added.</returns>
    public async Task AddResourcesAsync(long planetId, int sectorId, IReadOnlyDictionary<int, long> amounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        List<PlanetResourceEntity> rows = await _dbContext.PlanetResources
            .Where(row => row.PlanetId == planetId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach ((int resourceTypeId, long amount) in amounts.Where(entry => entry.Value > 0))
        {
            PlanetResourceEntity? row = rows.FirstOrDefault(entry => entry.ResourceTypeId == resourceTypeId);

            if (row is null)
            {
                _dbContext.PlanetResources.Add(new PlanetResourceEntity { PlanetId = planetId, SectorId = sectorId, ResourceTypeId = resourceTypeId, Amount = amount });
            }
            else
            {
                row.Amount += amount;
            }
        }
    }

    /// <summary>
    /// Places a building of a level on a body; an existing building is never lowered.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the building is placed.</returns>
    public async Task PlaceBuildingAsync(long planetId, int sectorId, int buildingTypeId, int level, CancellationToken cancellationToken)
    {
        if (!await _dbContext.BuildingTypes.AnyAsync(type => type.Id == buildingTypeId, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogWarning("The start building type {BuildingTypeId} does not exist; the body {PlanetId} gets no start building.", buildingTypeId, planetId);

            return;
        }

        PlanetBuildingEntity? building = await _dbContext.PlanetBuildings
            .SingleOrDefaultAsync(entry => entry.PlanetId == planetId && entry.BuildingTypeId == buildingTypeId, cancellationToken)
            .ConfigureAwait(false);

        if (building is null)
        {
            _dbContext.PlanetBuildings.Add(new PlanetBuildingEntity { PlanetId = planetId, SectorId = sectorId, BuildingTypeId = buildingTypeId, Level = level });
        }
        else
        {
            building.Level = Math.Max(building.Level, level);
        }
    }
}

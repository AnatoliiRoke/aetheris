using Aetheris.World.Data;
using Aetheris.World.Data.Entities;

namespace Aetheris.World.Services;

/// <summary>
/// Places the start building of a new civilization (task 00006, decision 11).
/// </summary>
/// <remarks>
/// This is the extension point for the buildings module, which does not exist yet. It is
/// called in the transaction of the civilization step, once per civilization, with the
/// building type and the level of the race. The default, <see cref="PendingStartBuildingPlacer"/>,
/// keeps the grant in <c>start_building_grant</c>, so the buildings module can build it
/// when it arrives; that module then registers its own placer.
/// </remarks>
public interface IStartBuildingPlacer
{
    /// <summary>
    /// Places the start building.
    /// </summary>
    /// <param name="planetId">The home body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the building is placed or kept.</returns>
    Task PlaceAsync(long planetId, int sectorId, int buildingTypeId, int level, CancellationToken cancellationToken);
}

/// <summary>
/// The default placer until the buildings module exists: it keeps the grant.
/// </summary>
public sealed class PendingStartBuildingPlacer : IStartBuildingPlacer
{
    private readonly WorldDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="PendingStartBuildingPlacer"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="timeProvider">The clock.</param>
    public PendingStartBuildingPlacer(WorldDbContext dbContext, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task PlaceAsync(long planetId, int sectorId, int buildingTypeId, int level, CancellationToken cancellationToken)
    {
        _dbContext.StartBuildingGrants.Add(new StartBuildingGrantEntity
        {
            PlanetId = planetId,
            SectorId = sectorId,
            BuildingTypeId = buildingTypeId,
            Level = level,
            GrantedAt = _timeProvider.GetUtcNow(),
        });

        return Task.CompletedTask;
    }
}

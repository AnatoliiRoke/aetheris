using Aetheris.World.Data;
using Aetheris.World.Data.Entities;

namespace Aetheris.World.Services;

/// <summary>
/// Keeps the amounts of a home body: the start resources of a new civilization and their
/// removal when the player leaves (task 00011).
/// </summary>
/// <remarks>
/// This is the extension point for the module that owns the balance of the bodies. The world
/// calls it in the transaction of the civilization step and of the release of a player. The
/// default, <see cref="LegacyHomePlanetStore"/>, writes the old <c>planet_storage</c> table of
/// task 00006; the module of the balance registers its own store instead.
/// </remarks>
public interface IHomePlanetStore
{
    /// <summary>
    /// Writes the start resources of a new civilization.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The home body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="amounts">The amounts by resource, already capped by the start storage limit.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the amounts are written.</returns>
    Task WriteStartResourcesAsync(
        string playerId,
        long planetId,
        int sectorId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes everything kept for the bodies of a player who leaves.
    /// </summary>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the data is removed.</returns>
    Task ReleaseAsync(IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken);
}

/// <summary>
/// The store of task 00006: the start resources go to <c>planet_storage</c>.
/// </summary>
public sealed class LegacyHomePlanetStore : IHomePlanetStore
{
    private readonly WorldDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyHomePlanetStore"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    public LegacyHomePlanetStore(WorldDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
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

        foreach ((int resourceTypeId, long amount) in amounts)
        {
            _dbContext.Storage.Add(new PlanetStorageEntity
            {
                SectorId = sectorId,
                PlanetId = planetId,
                ResourceTypeId = resourceTypeId,
                Amount = amount,
            });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Data;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Economy.Services;

/// <summary>
/// The row of the population of a colony: it appears with the colony and goes with it
/// (task 00015, section 3.1).
/// </summary>
/// <remarks>
/// The module that makes a colony (the founding of a home body, a later colonization) calls
/// this contract in the transaction that makes it, so the population of a body is counted from
/// the moment the body became a colony. The contract is small and has no dependency on the
/// world, so the module that implements the extension points of the world can use it without
/// making a circle.
/// </remarks>
public interface IColonyRegistry
{
    /// <summary>
    /// Writes the row of the population of a new colony; a colony that has the row already keeps it.
    /// </summary>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the row is written.</returns>
    Task FoundAsync(string playerId, long planetId, int sectorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the rows of the population of the bodies that stop being colonies.
    /// </summary>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rows are removed.</returns>
    Task ReleaseAsync(IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes and removes the rows of the population of the colonies (task 00015).
/// </summary>
public sealed class ColonyRegistry : IColonyRegistry
{
    private readonly EconomyDbContext _dbContext;
    private readonly EconomyUnitOfWork _unitOfWork;
    private readonly IGameConfig _config;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColonyRegistry"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="config">The configuration of the game.</param>
    /// <param name="clock">The game clock.</param>
    public ColonyRegistry(EconomyDbContext dbContext, EconomyUnitOfWork unitOfWork, IGameConfig config, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _config = config;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task FoundAsync(string playerId, long planetId, int sectorId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureAsync(playerId, [new OwnedPlanet(planetId, sectorId)], cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(planetIds);

        return _unitOfWork.RunAsync(
            () => _dbContext.PlanetPopulations.Where(row => planetIds.Contains(row.PlanetId)).ExecuteDeleteAsync(cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// Writes the rows of the population of the bodies of a player that have none, inside the
    /// current transaction.
    /// </summary>
    /// <param name="playerId">The owner of the bodies.</param>
    /// <param name="planets">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    /// <remarks>
    /// The row is written with <c>INSERT ... ON CONFLICT</c>, so two calls that meet the same
    /// body at once (the founding and the first read of the colony) write one row, and the
    /// owner of the world is the owner of the row: a body that changed hands keeps its population
    /// and gets its new owner.
    /// </remarks>
    internal async Task EnsureAsync(string playerId, IReadOnlyCollection<OwnedPlanet> planets, CancellationToken cancellationToken)
    {
        if (planets.Count == 0)
        {
            return;
        }

        DateTimeOffset now = EconomyTime.Normalize(_clock.GameNow);
        long initial = EconomySettings.From(_config).PopulationInitial;

        foreach (OwnedPlanet planet in planets.OrderBy(entry => entry.PlanetId))
        {
            await _dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO planet_population (planet_id, sector_id, owner_player_id, amount, amount_fraction, updated_at)
                VALUES ({planet.PlanetId}, {planet.SectorId}, {playerId}, {initial}, 0, {now})
                ON CONFLICT (planet_id) DO UPDATE
                SET owner_player_id = EXCLUDED.owner_player_id
                WHERE planet_population.owner_player_id IS DISTINCT FROM EXCLUDED.owner_player_id
                """,
                cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// The game time of the economy.
/// </summary>
internal static class EconomyTime
{
    /// <summary>
    /// Cuts a moment to the microseconds the database keeps, so a moment read back equals the
    /// moment written.
    /// </summary>
    /// <param name="moment">The moment.</param>
    /// <returns>The moment without the ticks below a microsecond.</returns>
    public static DateTimeOffset Normalize(DateTimeOffset moment) =>
        moment.AddTicks(-(moment.Ticks % TimeSpan.TicksPerMicrosecond));
}

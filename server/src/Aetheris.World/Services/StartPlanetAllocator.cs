using Aetheris.Core.Errors;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aetheris.World.Services;

/// <summary>
/// Gives a newcomer a free start body (task 00004, section 2.4, rule 5).
/// </summary>
/// <remarks>
/// <para>
/// The procedure is atomic: it runs in one transaction, and one body is never given to two
/// players. A player gets one start body; a repeated call returns the same one.
/// </para>
/// <para>
/// The body has the start type of the configuration, lies in a sector for newcomers (or, when
/// those are full, in any sector), and its system holds fewer newcomers than the limit.
/// Systems that already have newcomers come first, so newcomers start close to each other.
/// A system is locked while a newcomer is placed in it; parallel calls skip the locked system
/// and take the next one instead of waiting.
/// </para>
/// <para>
/// The procedure cannot deadlock (task 00021). Every attempt runs under a savepoint; a system
/// that turns out to have no free body, and any system a lock query kept without returning it,
/// is released by rolling the savepoint back. A call that has to wait therefore holds no
/// system, and the calls that wait line up behind one lock, so at most one of them waits for a
/// system at a time and nobody it waits for is waiting itself.
/// </para>
/// </remarks>
public sealed class StartPlanetAllocator
{
    /// <summary>
    /// The savepoint of one attempt to take a body in a system.
    /// </summary>
    private const string AttemptSavepoint = "start_planet_attempt";

    /// <summary>
    /// The name of the lock that lines up the calls waiting for a system. The two-key form of
    /// the lock keeps it apart from the lock of a player.
    /// </summary>
    private const string WaitLockName = "start_planet_wait";

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly WorldReferenceRepository _reference;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartPlanetAllocator"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="reference">The parameters.</param>
    /// <param name="timeProvider">The clock.</param>
    public StartPlanetAllocator(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        WorldReferenceRepository reference,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _reference = reference;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Allocates the start body of a player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="preferredObjectTypeId">
    /// The body type the race of the player prefers (task 00006, decision 9). A free body of
    /// this type is taken first; without one, the start type of the configuration.
    /// </param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The start body.</returns>
    public Task<StartPlanetResult> AllocateAsync(string playerId, int? preferredObjectTypeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return _unitOfWork.RunAsync(() => AllocateInTransactionAsync(playerId, preferredObjectTypeId, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Allocates the start body inside a unit of work that the caller already runs, for a
    /// larger change of the world such as the civilization step.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="preferredObjectTypeId">The preferred body type, if any.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The start body.</returns>
    public Task<StartPlanetResult> AllocateInTransactionAsync(string playerId, int? preferredObjectTypeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return AllocateCoreAsync(playerId, preferredObjectTypeId, cancellationToken);
    }

    private async Task<StartPlanetResult> AllocateCoreAsync(string playerId, int? preferredObjectTypeId, CancellationToken cancellationToken)
    {
        WorldSettings settings = _reference.Settings;

        // Two calls for the same player wait for each other here; calls for different
        // players do not.
        await _dbContext.Database
            .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({playerId}, 0))", cancellationToken)
            .ConfigureAwait(false);

        StartPlanetAllocationEntity? existing = await _dbContext.StartAllocations.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PlayerId == playerId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            PlanetEntity owned = await _dbContext.Planets.AsNoTracking()
                .SingleAsync(planet => planet.Id == existing.PlanetId, cancellationToken).ConfigureAwait(false);

            return new StartPlanetResult(owned.Id, owned.CelestialObjectId, owned.SystemId, owned.SectorId, true);
        }

        // The start type is a flag of the data, not a code in the configuration (task 00022,
        // decision 5); with several flagged types the oldest one wins.
        int startTypeId = await _dbContext.ObjectTypes.AsNoTracking()
            .Where(type => type.IsStartDefault && type.IsColonizable)
            .OrderBy(type => type.Id)
            .Select(type => (int?)type.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(WorldErrorCodes.StartTypeUnknown, "No colonizable body type is marked as the start type.");

        List<int> typeIds = [startTypeId];

        if (preferredObjectTypeId is { } preferred
            && preferred != startTypeId
            && await _dbContext.ObjectTypes.AsNoTracking()
                .AnyAsync(type => type.Id == preferred && type.IsColonizable, cancellationToken).ConfigureAwait(false))
        {
            typeIds.Insert(0, preferred);
        }

        foreach ((int typeId, bool newbieSectors) in typeIds.SelectMany(typeId => new[] { (typeId, true), (typeId, false) }))
        {
            PlanetEntity? planet = await TakePlanetAsync(newbieSectors, typeId, settings.MaxNewbiesPerSystem, cancellationToken)
                .ConfigureAwait(false);

            if (planet is null)
            {
                continue;
            }

            planet.OwnerPlayerId = playerId;
            _dbContext.StartAllocations.Add(new StartPlanetAllocationEntity
            {
                PlayerId = playerId,
                PlanetId = planet.Id,
                SectorId = planet.SectorId,
                AllocatedAt = _timeProvider.GetUtcNow(),
            });

            return new StartPlanetResult(planet.Id, planet.CelestialObjectId, planet.SystemId, planet.SectorId, false);
        }

        throw new GameException(WorldErrorCodes.NoStartPlanet, "No free start body is left; the galaxy needs a new sector.");
    }

    private async Task<PlanetEntity?> TakePlanetAsync(bool newbieSectors, int startTypeId, int maxNewbies, CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("The start body is allocated only inside a transaction.");

        List<long> skipped = [];

        while (true)
        {
            long[] excluded = [.. skipped];

            await transaction.CreateSavepointAsync(AttemptSavepoint, cancellationToken).ConfigureAwait(false);

            // Parallel calls skip a system that another call holds. Only when every candidate
            // is held does the call wait for one, so a free body is never missed.
            StarSystemEntity? system = await LockSystemAsync(newbieSectors, startTypeId, maxNewbies, excluded, wait: false, cancellationToken)
                .ConfigureAwait(false);

            if (system is null)
            {
                // A lock query may lock a system that no longer matches once the change of
                // another call is committed, keep it and go on to the next row, so even an empty
                // answer can leave a system locked. Such locks are let go before waiting. The
                // waiting calls then queue up one by one, and the others never wait for a
                // system, so no cycle can close.
                await transaction.RollbackToSavepointAsync(AttemptSavepoint, cancellationToken).ConfigureAwait(false);
                await _dbContext.Database
                    .ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({WaitLockName}), 0)", cancellationToken)
                    .ConfigureAwait(false);

                system = await LockSystemAsync(newbieSectors, startTypeId, maxNewbies, excluded, wait: true, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (system is null)
            {
                await transaction.ReleaseSavepointAsync(AttemptSavepoint, cancellationToken).ConfigureAwait(false);

                return null;
            }

            PlanetEntity? planet = (await _dbContext.Planets
                .FromSql(
                    $"""
                    SELECT p.*
                    FROM planet p
                    JOIN celestial_object o ON o.id = p.celestial_object_id
                    WHERE p.system_id = {system.Id}
                      AND p.owner_player_id IS NULL
                      AND o.object_type_id = {startTypeId}
                    ORDER BY o.orbit, p.id
                    LIMIT 1
                    FOR UPDATE OF p SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();

            if (planet is null || system.NewbieCount >= maxNewbies)
            {
                // The lock query sees the free bodies as they were before it waited, so the
                // system may have been filled meanwhile. Holding it while waiting for the next
                // one could close a cycle with another call, so the lock is let go here.
                await transaction.RollbackToSavepointAsync(AttemptSavepoint, cancellationToken).ConfigureAwait(false);
                await transaction.ReleaseSavepointAsync(AttemptSavepoint, cancellationToken).ConfigureAwait(false);
                _dbContext.Entry(system).State = EntityState.Detached;

                if (planet is not null)
                {
                    _dbContext.Entry(planet).State = EntityState.Detached;
                }

                skipped.Add(system.Id);
                continue;
            }

            await transaction.ReleaseSavepointAsync(AttemptSavepoint, cancellationToken).ConfigureAwait(false);
            system.NewbieCount++;

            return planet;
        }
    }

    private async Task<StarSystemEntity?> LockSystemAsync(
        bool newbieSectors,
        int startTypeId,
        int maxNewbies,
        long[] excluded,
        bool wait,
        CancellationToken cancellationToken)
    {
        IQueryable<StarSystemEntity> query = wait
            ? _dbContext.Systems.FromSql(
                $"""
                SELECT s.*
                FROM star_system s
                JOIN sector sec ON sec.id = s.sector_id
                WHERE sec.is_newbie = {newbieSectors}
                  AND s.newbie_count < {maxNewbies}
                  AND s.id <> ALL({excluded})
                  AND EXISTS (
                      SELECT 1
                      FROM planet p
                      JOIN celestial_object o ON o.id = p.celestial_object_id
                      WHERE p.system_id = s.id
                        AND p.owner_player_id IS NULL
                        AND o.object_type_id = {startTypeId})
                ORDER BY s.newbie_count DESC, s.sector_id, s.number
                LIMIT 1
                FOR UPDATE OF s
                """)
            : _dbContext.Systems.FromSql(
                $"""
                SELECT s.*
                FROM star_system s
                JOIN sector sec ON sec.id = s.sector_id
                WHERE sec.is_newbie = {newbieSectors}
                  AND s.newbie_count < {maxNewbies}
                  AND s.id <> ALL({excluded})
                  AND EXISTS (
                      SELECT 1
                      FROM planet p
                      JOIN celestial_object o ON o.id = p.celestial_object_id
                      WHERE p.system_id = s.id
                        AND p.owner_player_id IS NULL
                        AND o.object_type_id = {startTypeId})
                ORDER BY s.newbie_count DESC, s.sector_id, s.number
                LIMIT 1
                FOR UPDATE OF s SKIP LOCKED
                """);

        return (await query.ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }
}

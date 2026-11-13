using Aetheris.Core.Errors;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Claims a free body for a player (task 00073).
/// </summary>
/// <remarks>
/// <para>
/// A body is claimed by the fleet that colonizes it: the body must be free, colonizable and of a
/// type that suits the race of the player. The row of the body is locked for the time of the
/// transaction, so two fleets that fly to the same body cannot take it twice: the second one is
/// refused with the code of a body that is taken.
/// </para>
/// <para>
/// The types that suit a race are the data of the table <c>race_colonizable_type</c>, so the
/// administrator may give a race another type without a change of the code.
/// </para>
/// </remarks>
public sealed class PlanetClaimService
{
    private readonly WorldDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetClaimService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    public PlanetClaimService(WorldDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <summary>
    /// Claims a free body for a player.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="raceId">The race of the player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The body, as it is now.</returns>
    /// <exception cref="GameException">The body is taken, is not colonizable or does not suit the race.</exception>
    public async Task<PlanetEntity> ClaimAsync(
        long planetId,
        string playerId,
        int raceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        // The row of the body is locked, so a fleet that flies to the same body waits for the
        // first one to finish.
        PlanetEntity? planet = await _dbContext.Planets
            .FromSqlInterpolated($"SELECT * FROM planet WHERE id = {planetId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (planet is null)
        {
            throw new GameException(WorldErrorCodes.PlanetNotFound, "The body does not exist.");
        }

        if (planet.OwnerPlayerId is not null)
        {
            throw new GameException(WorldErrorCodes.PlanetTaken, "The body belongs to another player already.");
        }

        // The type of the body lies on its celestial object (task 00073).
        var type = await (
                from entry in _dbContext.Objects.AsNoTracking()
                where entry.Id == planet.CelestialObjectId
                join objectType in _dbContext.ObjectTypes.AsNoTracking() on entry.ObjectTypeId equals objectType.Id
                select new { objectType.Id, objectType.IsColonizable })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (type is null || !type.IsColonizable)
        {
            throw new GameException(WorldErrorCodes.PlanetNotColonizable, "The body is not of a colonizable type.");
        }

        bool suits = await _dbContext.RaceColonizableTypes.AsNoTracking()
            .AnyAsync(entry => entry.RaceId == raceId && entry.ObjectTypeId == type.Id, cancellationToken)
            .ConfigureAwait(false);

        if (!suits)
        {
            throw new GameException(WorldErrorCodes.PlanetRaceUnsuitable, "The body does not suit the race of the player.");
        }

        planet.OwnerPlayerId = playerId;
        planet.Version++;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return planet;
    }

    /// <summary>
    /// Checks that a body suits the race of a player, without taking it.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="raceId">The race of the player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether the body suits the race.</returns>
    public async Task<bool> SuitsRaceAsync(long planetId, int raceId, CancellationToken cancellationToken = default)
    {
        int? typeId = await _dbContext.Planets.AsNoTracking()
            .Where(entry => entry.Id == planetId)
            .Select(entry => (int?)entry.CelestialObjectId)
            .SelectMany(objectId => _dbContext.Objects.AsNoTracking()
                .Where(entry => entry.Id == objectId)
                .Select(entry => (int?)entry.ObjectTypeId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (typeId is null)
        {
            return false;
        }

        return await _dbContext.RaceColonizableTypes.AsNoTracking()
            .AnyAsync(entry => entry.RaceId == raceId && entry.ObjectTypeId == typeId.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}

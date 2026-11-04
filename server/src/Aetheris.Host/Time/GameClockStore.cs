using Aetheris.Core.Time;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Time;

/// <summary>
/// Keeps the anchor of the game clock in the database, so a restart of the server never moves
/// the game time back (code audit of 04.11.2026).
/// </summary>
/// <remarks>
/// The server puts the clock on the stored anchor when it starts; a first start stores the
/// anchor the clock has. A change of the multiplier stores the new anchor in the same
/// transaction as its audit record, and the clock takes it after the commit.
/// </remarks>
public static class GameClockStore
{
    /// <summary>
    /// Puts the clock on the stored anchor, or stores the anchor of the clock when there is none.
    /// </summary>
    /// <param name="dbContext">The main context.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the clock is restored.</returns>
    public static async Task RestoreAsync(AetherisDbContext dbContext, GameClock clock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(clock);

        GameClockStateEntity? state = await dbContext.GameClockStates.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == GameClockStateEntity.SingleRowId, cancellationToken)
            .ConfigureAwait(false);

        if (state is not null)
        {
            clock.Restore(state.RealAnchor, state.GameAnchor, state.Multiplier);

            return;
        }

        DateTimeOffset now = clock.UtcNow;
        (DateTimeOffset realAnchor, DateTimeOffset gameAnchor) = clock.AnchorAt(now);

        dbContext.GameClockStates.Add(new GameClockStateEntity
        {
            Id = GameClockStateEntity.SingleRowId,
            RealAnchor = realAnchor,
            GameAnchor = gameAnchor,
            Multiplier = clock.Multiplier,
            UpdatedAt = now,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the anchor of a change of the multiplier into the context; the caller saves it.
    /// </summary>
    /// <param name="dbContext">The main context.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="multiplier">The new multiplier.</param>
    /// <param name="now">The real time of the change.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the row is staged.</returns>
    public static async Task StageAsync(
        AetherisDbContext dbContext,
        GameClock clock,
        double multiplier,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(clock);

        (DateTimeOffset realAnchor, DateTimeOffset gameAnchor) = clock.AnchorAt(now);

        GameClockStateEntity? state = await dbContext.GameClockStates
            .SingleOrDefaultAsync(row => row.Id == GameClockStateEntity.SingleRowId, cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            state = new GameClockStateEntity { Id = GameClockStateEntity.SingleRowId };
            dbContext.GameClockStates.Add(state);
        }

        state.RealAnchor = realAnchor;
        state.GameAnchor = gameAnchor;
        state.Multiplier = multiplier;
        state.UpdatedAt = now;
    }
}

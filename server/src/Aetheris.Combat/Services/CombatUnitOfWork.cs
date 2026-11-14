using Aetheris.Combat.Data;
using Aetheris.Core.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Combat.Services;

/// <summary>
/// Runs a change of the combat module in the transaction of the request.
/// </summary>
/// <remarks>
/// Inside a command the transaction already exists and the context of the module joins it.
/// Called on its own, the unit begins its own transaction and commits it.
/// </remarks>
public sealed class CombatUnitOfWork
{
    private readonly CombatDbContext _dbContext;
    private readonly IDatabaseSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CombatUnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="session">The connection of the request.</param>
    public CombatUnitOfWork(CombatDbContext dbContext, IDatabaseSession session)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(session);

        _dbContext = dbContext;
        _session = session;
    }

    /// <summary>
    /// Runs the work in a transaction and saves the context of the module.
    /// </summary>
    /// <typeparam name="T">The outcome type.</typeparam>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome of the work.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        IDatabaseTransaction? owned = _session.CurrentTransaction is null
            ? await _session.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        if (owned is not null)
        {
            _dbContext.ChangeTracker.Clear();
        }

        try
        {
            await _dbContext.Database.UseTransactionAsync(_session.CurrentTransaction, cancellationToken).ConfigureAwait(false);

            T outcome = await work().ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (owned is not null)
            {
                await owned.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return outcome;
        }
        catch
        {
            _dbContext.ChangeTracker.Clear();

            throw;
        }
        finally
        {
            await _dbContext.Database.UseTransactionAsync(null, CancellationToken.None).ConfigureAwait(false);

            if (owned is not null)
            {
                await owned.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

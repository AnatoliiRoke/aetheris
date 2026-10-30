using Aetheris.Core.Data;
using Aetheris.Economy.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Economy.Services;

/// <summary>
/// The transaction of the economy module: it joins the transaction of the request or opens
/// its own (task 00015).
/// </summary>
/// <remarks>
/// <para>
/// The order of the locks is fixed, so two commands of one civilization never wait for each other
/// in a circle: the rows of the population of its bodies are locked first, by the identifier of the
/// body, and then its balances, by the identifier of the currency. Every change of a balance locks
/// the populations too, since it counts the income of the bodies into the balance; a reader locks
/// nothing. A module that holds a lock of its own (the state of a body, the queue of the research)
/// takes it before it calls the economy, so the economy is always the last lock of a request.
/// </para>
/// <para>
/// A change inside a transaction that already exists joins it. The unit is not meant to be
/// nested: a piece of work does not start another unit.
/// </para>
/// </remarks>
public sealed class EconomyUnitOfWork
{
    private readonly EconomyDbContext _dbContext;
    private readonly IDatabaseSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyUnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="session">The session of the database.</param>
    public EconomyUnitOfWork(EconomyDbContext dbContext, IDatabaseSession session)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(session);

        _dbContext = dbContext;
        _session = session;
    }

    /// <summary>
    /// Runs a piece of work in the transaction of the module.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="work">The work to run.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The result of the work.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        IDatabaseTransaction? owned = _session.CurrentTransaction is null
            ? await _session.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;

        if (owned is not null)
        {
            // A unit with its own transaction reads its rows anew: a row the scheduler
            // changed since the last unit of the same scope would otherwise come back with
            // its old values from the tracker.
            _dbContext.ChangeTracker.Clear();
        }

        try
        {
            await _dbContext.Database.UseTransactionAsync(_session.CurrentTransaction, cancellationToken).ConfigureAwait(false);

            T result = await work().ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (owned is not null)
            {
                await owned.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch
        {
            // A refused change leaves nothing behind for the next change of the same scope.
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

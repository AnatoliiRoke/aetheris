using Aetheris.Core.Data;
using Aetheris.Processes.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Services;

/// <summary>
/// The transaction of the processes module: it joins the transaction of the request or opens
/// its own (task 00014, section 3.1).
/// </summary>
/// <remarks>
/// The order of the locks is fixed, so two commands of two bodies never wait for each other:
/// the queue of the building is locked first, then the balance of the resources.
/// </remarks>
public sealed class ProcessesUnitOfWork
{
    private readonly ProcessesDbContext _dbContext;
    private readonly IDatabaseSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessesUnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="session">The session of the database.</param>
    public ProcessesUnitOfWork(ProcessesDbContext dbContext, IDatabaseSession session)
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
            // A unit with its own transaction reads its rows anew: a row the scheduler changed
            // since the last unit of the same scope would otherwise come back with its old
            // values from the tracker, and the old quantity would be written over the new one.
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

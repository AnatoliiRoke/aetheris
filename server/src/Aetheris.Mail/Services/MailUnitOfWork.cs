using Aetheris.Core.Data;
using Aetheris.Mail.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Mail.Services;

/// <summary>
/// The unit of work of the mail module: the letters are written in a transaction (task 00060).
/// </summary>
/// <remarks>
/// The unit shares the transaction of the request when there is one, so a letter and the box of
/// the receiver are written together, and it opens one when the work comes from outside a request.
/// </remarks>
public sealed class MailUnitOfWork
{
    private readonly MailDbContext _dbContext;
    private readonly IDatabaseSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailUnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="session">The connection of the request.</param>
    public MailUnitOfWork(MailDbContext dbContext, IDatabaseSession session)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(session);

        _dbContext = dbContext;
        _session = session;
    }

    /// <summary>
    /// Runs the work in a transaction and saves the context of the module.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="work">The work.</param>
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
            // A unit with its own transaction reads its rows anew: a row another change wrote
            // since the last unit of the same scope would otherwise come back with its old
            // values from the tracker.
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

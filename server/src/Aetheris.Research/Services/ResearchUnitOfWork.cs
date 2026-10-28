using Aetheris.Core.Data;
using Aetheris.Research.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// Runs a change of the research module in the transaction of the request.
/// </summary>
/// <remarks>
/// Inside a command or a scheduled event the transaction already exists and the context of
/// the module joins it, so the queue, the scheduled events and the stored response are
/// committed together. Called on its own the unit begins its own transaction and commits it.
/// </remarks>
public sealed class ResearchUnitOfWork
{
    private readonly ResearchDbContext _dbContext;
    private readonly IDatabaseSession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchUnitOfWork"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="session">The connection of the request.</param>
    public ResearchUnitOfWork(ResearchDbContext dbContext, IDatabaseSession session)
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

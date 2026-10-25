using System.Data.Common;
using Aetheris.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aetheris.Host.Data;

/// <summary>
/// The connection and the transaction of the request, owned by <see cref="AetherisDbContext"/>.
/// </summary>
/// <remarks>
/// The command pipeline and the scheduler begin their transactions on the main context. The
/// database contexts of the modules use the same connection and join that transaction, so
/// a request commits or rolls back as one.
/// </remarks>
public sealed class EfDatabaseSession : IDatabaseSession
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfDatabaseSession"/> class.
    /// </summary>
    /// <param name="dbContext">The main database context of the request.</param>
    public EfDatabaseSession(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public DbConnection Connection => _dbContext.Database.GetDbConnection();

    /// <inheritdoc />
    public DbTransaction? CurrentTransaction => _dbContext.Database.CurrentTransaction?.GetDbTransaction();

    /// <inheritdoc />
    public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        IDbContextTransaction transaction = await _dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        return new Transaction(transaction);
    }

    private sealed class Transaction : IDatabaseTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public Transaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken) => _transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}

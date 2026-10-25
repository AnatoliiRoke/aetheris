using System.Data.Common;

namespace Aetheris.Core.Data;

/// <summary>
/// The database connection of the current request, shared by the host and the modules.
/// </summary>
/// <remarks>
/// A module keeps its tables in its own database context, but a change of a module and a
/// change of the core (a scheduled event, the stored response of a command) must be committed
/// together. Every context of a request therefore works on this one connection and joins
/// its <see cref="CurrentTransaction"/>.
/// </remarks>
public interface IDatabaseSession
{
    /// <summary>
    /// Gets the connection of the request.
    /// </summary>
    DbConnection Connection { get; }

    /// <summary>
    /// Gets the open transaction of the request, or <see langword="null"/> when there is none.
    /// </summary>
    DbTransaction? CurrentTransaction { get; }

    /// <summary>
    /// Begins a transaction on the connection of the request.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The transaction; disposing it without a commit rolls it back.</returns>
    Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A transaction begun through <see cref="IDatabaseSession"/>.
/// </summary>
public interface IDatabaseTransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the transaction is committed.</returns>
    Task CommitAsync(CancellationToken cancellationToken);
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Processes.Data;

/// <summary>
/// The database of the processes module: its own context and its own migrations, written
/// into the common history of the server (task 00014).
/// </summary>
public static class ProcessesDatabase
{
    /// <summary>
    /// Configures the context of the module for a connection.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="connection">The connection of the request.</param>
    /// <returns>The same options.</returns>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connection);

        return options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(ProcessesDbContext).Assembly.FullName));
    }

    /// <summary>
    /// Applies the migrations of the module.
    /// </summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the migrations are applied.</returns>
    public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        DbContextOptionsBuilder<ProcessesDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(ProcessesDbContext).Assembly.FullName));

        await using ProcessesDbContext context = new(options.Options);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}

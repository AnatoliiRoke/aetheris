using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Data;

/// <summary>
/// The options and the migrations of the context of the bodies module.
/// </summary>
public static class PlanetsDatabase
{
    /// <summary>
    /// Configures the context on a shared connection.
    /// </summary>
    /// <param name="options">The options builder.</param>
    /// <param name="connection">The connection of the request.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// The migration history lives in the common <c>__EFMigrationsHistory</c> table, like the
    /// world one; the identifiers of the migrations differ, so the sets never collide.
    /// </remarks>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connection);

        return options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(PlanetsDbContext).Assembly.FullName));
    }

    /// <summary>
    /// Applies the migrations of the module.
    /// </summary>
    /// <param name="connectionString">The connection string of an account that may change the schema.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the schema is up to date.</returns>
    public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        DbContextOptionsBuilder<PlanetsDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(PlanetsDbContext).Assembly.FullName));

        await using PlanetsDbContext context = new(options.Options);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}

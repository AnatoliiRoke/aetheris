using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Data;

/// <summary>
/// The options and the migrations of the world context.
/// </summary>
public static class WorldDatabase
{
    /// <summary>
    /// Configures the world context on a shared connection.
    /// </summary>
    /// <param name="options">The options builder.</param>
    /// <param name="connection">The connection of the request.</param>
    /// <returns>The same builder.</returns>
    /// <remarks>
    /// The migration history lives in the common <c>__EFMigrationsHistory</c> table; the
    /// identifiers of the world migrations differ from those of the host, so the two sets
    /// never collide.
    /// </remarks>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connection);

        return options.UseNpgsql(connection, npgsql => npgsql.MigrationsAssembly(typeof(WorldDbContext).Assembly.FullName));
    }

    /// <summary>
    /// Applies the world migrations.
    /// </summary>
    /// <param name="connectionString">The connection string of an account that may change the schema.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the schema is up to date.</returns>
    public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        DbContextOptionsBuilder<WorldDbContext> options = new();
        options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(WorldDbContext).Assembly.FullName));

        await using WorldDbContext context = new(options.Options);
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}

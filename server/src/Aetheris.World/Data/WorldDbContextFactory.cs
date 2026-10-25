using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.World.Data;

/// <summary>
/// Creates the world context for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code. The server applies the
/// migrations itself at start (<see cref="WorldDatabase.MigrateAsync"/>).
/// </remarks>
public sealed class WorldDbContextFactory : IDesignTimeDbContextFactory<WorldDbContext>
{
    /// <inheritdoc />
    public WorldDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<WorldDbContext> options = new();
        WorldDatabase.Configure(options, new NpgsqlConnection());

        return new WorldDbContext(options.Options);
    }
}

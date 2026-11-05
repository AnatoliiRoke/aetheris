using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Fleet.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class FleetDbContextFactory : IDesignTimeDbContextFactory<FleetDbContext>
{
    /// <inheritdoc />
    public FleetDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<FleetDbContext> options = new();
        FleetDatabase.Configure(options, new NpgsqlConnection());

        return new FleetDbContext(options.Options);
    }
}

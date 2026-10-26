using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Planets.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class PlanetsDbContextFactory : IDesignTimeDbContextFactory<PlanetsDbContext>
{
    /// <inheritdoc />
    public PlanetsDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<PlanetsDbContext> options = new();
        PlanetsDatabase.Configure(options, new NpgsqlConnection());

        return new PlanetsDbContext(options.Options);
    }
}

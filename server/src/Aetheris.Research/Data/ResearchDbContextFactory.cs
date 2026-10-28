using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Research.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class ResearchDbContextFactory : IDesignTimeDbContextFactory<ResearchDbContext>
{
    /// <inheritdoc />
    public ResearchDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ResearchDbContext> options = new();
        ResearchDatabase.Configure(options, new NpgsqlConnection());

        return new ResearchDbContext(options.Options);
    }
}

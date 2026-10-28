using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Requirements.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class RequirementsDbContextFactory : IDesignTimeDbContextFactory<RequirementsDbContext>
{
    /// <inheritdoc />
    public RequirementsDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<RequirementsDbContext> options = new();
        RequirementsDatabase.Configure(options, new NpgsqlConnection());

        return new RequirementsDbContext(options.Options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Production.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class ProductionDbContextFactory : IDesignTimeDbContextFactory<ProductionDbContext>
{
    /// <inheritdoc />
    public ProductionDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<ProductionDbContext> options = new();
        ProductionDatabase.Configure(options, new NpgsqlConnection());

        return new ProductionDbContext(options.Options);
    }
}

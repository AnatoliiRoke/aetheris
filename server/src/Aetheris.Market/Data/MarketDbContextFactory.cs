using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Aetheris.Market.Data;

/// <summary>
/// Creates the context of the module for <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// Adding a migration does not open a connection, so the factory uses an unconfigured
/// connection: no connection string is stored in the code.
/// </remarks>
public sealed class MarketDbContextFactory : IDesignTimeDbContextFactory<MarketDbContext>
{
    /// <inheritdoc />
    public MarketDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<MarketDbContext> options = new();
        MarketDatabase.Configure(options, new NpgsqlConnection());

        return new MarketDbContext(options.Options);
    }
}

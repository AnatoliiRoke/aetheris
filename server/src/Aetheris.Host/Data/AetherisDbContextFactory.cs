using Aetheris.Host.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aetheris.Host.Data;

/// <summary>
/// Creates a database context for EF Core design-time operations such as
/// <c>dotnet ef migrations add</c>.
/// </summary>
/// <remarks>
/// The connection string is taken from the same environment variable as the running
/// server, so that migrations can be created without a live database.
/// </remarks>
public sealed class AetherisDbContextFactory : IDesignTimeDbContextFactory<AetherisDbContext>
{
    /// <inheritdoc />
    public AetherisDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable(
            Configuration.DatabaseOptions.ConnectionStringEnvironmentVariable)
            ?? "Host=localhost;Port=5432;Database=aetheris_dev;Username=postgres;Password=postgres";

        DbContextOptionsBuilder<AetherisDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(AetherisDbContext).Assembly.FullName));

        return new AetherisDbContext(optionsBuilder.Options);
    }
}

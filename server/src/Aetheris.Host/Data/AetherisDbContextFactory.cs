using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aetheris.Host.Data;

/// <summary>
/// Creates a database context for EF Core design-time operations such as
/// <c>dotnet ef migrations add</c> and <c>dotnet ef database update</c>.
/// </summary>
/// <remarks>
/// The connection string is resolved exactly like the running server does, so a design-time
/// command always targets the same database. No connection string is hardcoded here: the
/// server refuses to start without one, and so does this factory.
/// </remarks>
public sealed class AetherisDbContextFactory : IDesignTimeDbContextFactory<AetherisDbContext>
{
    /// <inheritdoc />
    public AetherisDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // The design-time tools change the schema, so they prefer the migration account.
        string connectionString = DatabaseOptions.GetMigrationConnectionString(configuration)
            ?? DatabaseOptions.GetConnectionString(configuration);

        DbContextOptionsBuilder<AetherisDbContext> optionsBuilder = new();
        AetherisDbContextSetup.Configure(optionsBuilder, connectionString);

        return new AetherisDbContext(optionsBuilder.Options);
    }
}

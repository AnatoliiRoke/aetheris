using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Test host for the Aetheris server, backed by the local <c>aetheris_test</c> database.
/// </summary>
/// <remarks>
/// The connection string comes from the same sources as the running server: the
/// environment variable or the git-ignored <c>appsettings.Local.json</c> file, so that the
/// connection details are never stored in the repository (task 00001, section 2.3).
/// </remarks>
public sealed class AetherisTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// The connection string used by the tests.
    /// </summary>
    /// <remarks>
    /// It is resolved exactly like the server does, so the tests always target the same
    /// database as the running application. The database name is replaced with the test
    /// one, so the tests can never touch the development data.
    /// </remarks>
    public static string ConnectionString { get; } = BuildTestConnectionString();

    /// <summary>
    /// Gets the name of the test database.
    /// </summary>
    public string DatabaseName { get; } = "aetheris_test";

    /// <summary>
    /// Gets the connection string used by the tests.
    /// </summary>
    public static string TestConnectionString => ConnectionString;

    private static string BuildTestConnectionString()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string resolved = DatabaseOptions.GetConnectionString(configuration);

        // Point the connection at the test database, whatever the configured one is.
        var builder = new NpgsqlConnectionStringBuilder(resolved) { Database = "aetheris_test" };

        return builder.ConnectionString;
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = ConnectionString,
                ["ConnectionStrings:Default"] = ConnectionString,
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDatabaseRegistrations(services);

            services.AddDbContext<AetherisDbContext>(options =>
                options.UseNpgsql(ConnectionString));

            // The background reload timer would compete with the tests for the database.
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        });
    }

    /// <summary>
    /// Creates the test database schema and clears it, so that every run starts from a
    /// known empty state.
    /// </summary>
    /// <returns>A task that completes when the schema is ready and the tables are empty.</returns>
    public async ValueTask InitializeAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        await dbContext.Database.MigrateAsync();

        await ResetDatabaseAsync(scope.ServiceProvider);
    }

    /// <summary>
    /// Clears the test database, seeds the configuration again and reloads it, so that a
    /// test starts from the same state as a freshly started server.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <returns>A task that completes when the database is ready.</returns>
    public static async Task ResetDatabaseAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();

        await ClearDatabaseAsync(dbContext);
        await services.GetRequiredService<ConfigSeedService>().SeedAsync();
        await services.GetRequiredService<ConfigReloader>().ReloadAsync();
    }

    /// <summary>
    /// Removes all rows from every table of the test database.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <returns>A task that completes when the database is empty.</returns>
    /// <remarks>
    /// TRUNCATE is used instead of dropping and recreating the database: it is fast, keeps
    /// the connection settings intact and does not require elevated rights. The statement
    /// is built inside PostgreSQL from <c>pg_tables</c> and quoted with
    /// <c>quote_ident</c>, so no table name is ever passed through string interpolation.
    /// </remarks>
    public static async Task ClearDatabaseAsync(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        await dbContext.Database
            .ExecuteSqlRawAsync(
                """
                DO $$
                DECLARE
                    statement text;
                BEGIN
                    SELECT format(
                        'TRUNCATE TABLE %s RESTART IDENTITY CASCADE',
                        string_agg(quote_ident(tablename), ', '))
                    INTO statement
                    FROM pg_tables
                    WHERE schemaname = 'public'
                      AND tablename <> '__EFMigrationsHistory';

                    IF statement IS NOT NULL THEN
                        EXECUTE statement;
                    END IF;
                END
                $$;
                """)
            .ConfigureAwait(false);
    }

    private static void RemoveDatabaseRegistrations(IServiceCollection services)
    {
        List<ServiceDescriptor> descriptors = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(DbContextOptions<AetherisDbContext>)
                || (descriptor.ServiceType == typeof(DbContextOptions)
                    && descriptor.ServiceType.FullName?.Contains("AetherisDbContext") == true))
            .ToList();

        foreach (ServiceDescriptor descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }
}

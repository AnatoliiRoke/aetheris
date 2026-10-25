using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Test host for the Aetheris server, backed by the local <c>aetheris_test</c> database.
/// </summary>
/// <remarks>
/// The connection string comes from the same environment variable as the running server,
/// so that the connection details are never stored in the repository
/// (task 00001, section 2.3).
/// </remarks>
public sealed class AetherisTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable(DatabaseOptions.ConnectionStringEnvironmentVariable)
        ?? "Host=localhost;Port=5432;Database=aetheris_test;Username=postgres;Password=postgres";

    /// <summary>
    /// Initializes a new instance of the <see cref="AetherisTestFactory"/> class.
    /// </summary>
    public AetherisTestFactory()
    {
        // The test database name is fixed so that the connection string can point at it.
        DatabaseName = "aetheris_test";
    }

    /// <summary>
    /// Gets the name of the test database.
    /// </summary>
    public string DatabaseName { get; }

    /// <summary>
    /// Gets the connection string used by the tests.
    /// </summary>
    public static string TestConnectionString => ConnectionString;

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
    /// Creates the test database schema.
    /// </summary>
    /// <returns>A task that completes when the schema is ready.</returns>
    public async Task InitializeAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Cleans the test database between test runs.
    /// </summary>
    /// <returns>A task that completes when the database is clean.</returns>
    public async Task DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
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

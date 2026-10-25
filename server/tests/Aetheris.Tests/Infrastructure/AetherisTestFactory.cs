using Aetheris.Core.Commands;
using Aetheris.Core.Security;
using Aetheris.Host.Auth;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Serilog.Core;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Test host for the Aetheris server, backed by the local <c>aetheris_test</c> database.
/// </summary>
/// <remarks>
/// The connection string comes from the same sources as the running server: the
/// environment variable or the git-ignored <c>appsettings.Local.json</c> file, so that the
/// connection details are never stored in the repository (task 00001, section 2.3).
/// </remarks>
public class AetherisTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
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
    /// The origin of the test client, the only origin allowed by CORS in the tests.
    /// </summary>
    public const string ClientOrigin = "https://client.test";

    /// <summary>
    /// The limits that the tests run with. The tests send many sign-in requests from one
    /// address, so the rate limits are raised; the rate limit test lowers them again for
    /// itself.
    /// </summary>
    private static readonly Dictionary<string, string> TestLimits = new(StringComparer.Ordinal)
    {
        [SecurityKeys.RateLimitPermits(RateLimitGroups.Ip)] = "100000",
        [SecurityKeys.RateLimitPermits(RateLimitGroups.Auth)] = "10000",
        [SecurityKeys.RateLimitPermits(RateLimitGroups.Command)] = "100000",
        [SecurityKeys.RateLimitPermits(RateLimitGroups.Admin)] = "10000",
        [SecurityKeys.AlertFailedSignInThreshold] = "100000",
    };

    /// <summary>
    /// Gets the e-mails the server sent, so that the tests can read the tokens.
    /// </summary>
    public CapturingEmailSender Emails { get; } = new();

    /// <summary>
    /// Gets the log events the server wrote, so that the tests can check their content.
    /// </summary>
    public CapturingLogSink Logs { get; } = new();

    /// <summary>
    /// Gets the hosting environment of the test host.
    /// </summary>
    protected virtual string EnvironmentName => "Development";

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

        builder.UseEnvironment(EnvironmentName);

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = ConnectionString,
                ["ConnectionStrings:Default"] = ConnectionString,

                // The migrations of the test host always go to the test database, even
                // when a migration account is configured for the development database.
                [DatabaseOptions.MigrationConnectionStringKey] = ConnectionString,
                [ClientOrigins.ConfigurationKey + ":0"] = ClientOrigin,
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDatabaseRegistrations(services);

            services.AddDbContext<AetherisDbContext>(options =>
                AetherisDbContextSetup.Configure(options, ConnectionString));

            // The background reload timer would compete with the tests for the database.
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(Emails);
            services.AddSingleton<ILogEventSink>(Logs);

            // The example of a new protected request: one class and this one line
            // (task 00002, acceptance criterion 10).
            services.AddCommand<TestNoteCommand>();
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

        foreach ((string key, string value) in TestLimits)
        {
            await SetParameterAsync(services, key, value, reload: false);
        }

        await services.GetRequiredService<ConfigReloader>().ReloadAsync();
    }

    /// <summary>
    /// Writes a configuration parameter and, by default, reloads the configuration.
    /// </summary>
    /// <param name="services">The scoped service provider.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The new value.</param>
    /// <param name="reload">Whether to reload the configuration at once.</param>
    /// <returns>The previous value, or <see langword="null"/> when the parameter did not exist.</returns>
    public static async Task<string?> SetParameterAsync(IServiceProvider services, string key, string value, bool reload = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        AetherisDbContext dbContext = services.GetRequiredService<AetherisDbContext>();
        ConfigParameterEntity? parameter = await dbContext.ConfigParameters.SingleOrDefaultAsync(entry => entry.Key == key);
        string? previous = parameter?.Value;

        if (parameter is null)
        {
            dbContext.ConfigParameters.Add(new ConfigParameterEntity
            {
                Key = key,
                Value = value,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = "test",
            });
        }
        else
        {
            parameter.Value = value;
            parameter.Version++;
            parameter.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync();

        if (reload)
        {
            await services.GetRequiredService<ConfigReloader>().ReloadAsync();
        }

        return previous;
    }

    /// <summary>
    /// Creates a client that talks HTTPS to the test server and keeps cookies, like a browser.
    /// </summary>
    /// <returns>The client.</returns>
    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

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

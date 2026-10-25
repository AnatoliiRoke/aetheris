using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Data;

/// <summary>
/// The options of the database context, shared by the server and the test host, so that
/// both use the same interceptors.
/// </summary>
public static class AetherisDbContextSetup
{
    /// <summary>
    /// The interceptor that keeps the audit log append-only. It is stateless, so one
    /// instance serves every context.
    /// </summary>
    private static readonly AppendOnlyAuditInterceptor AuditInterceptor = new();

    /// <summary>
    /// Configures the context options for a connection string.
    /// </summary>
    /// <param name="options">The options builder.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The same builder.</returns>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        return options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AetherisDbContext).Assembly.FullName))
            .AddInterceptors(AuditInterceptor);
    }
}

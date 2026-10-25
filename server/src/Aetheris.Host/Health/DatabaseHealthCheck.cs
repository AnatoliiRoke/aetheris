using Aetheris.Host.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Aetheris.Host.Health;

/// <summary>
/// Health check that verifies the database connection.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public DatabaseHealthCheck(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // The query goes through the context, so the connection is taken from the pool
            // and returned to it. Taking the connection with GetDbConnection and disposing
            // it would close a connection that the context still owns.
            await _dbContext.Database
                .ExecuteSqlRawAsync("SELECT 1", cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                "Database connection is available.",
                new Dictionary<string, object> { ["database"] = _dbContext.Database.GetDbConnection().Database });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database connection is not available.",
                exception);
        }
    }
}

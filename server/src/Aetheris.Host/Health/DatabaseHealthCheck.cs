using System.Data.Common;
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
            await using DbConnection connection = _dbContext.Database.GetDbConnection();

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                "Database connection is available.",
                new Dictionary<string, object> { ["database"] = connection.Database });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "Database connection is not available.",
                exception);
        }
    }
}

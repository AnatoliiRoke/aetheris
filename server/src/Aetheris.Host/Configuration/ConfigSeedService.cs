using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Configuration;

/// <summary>
/// Adds the initial configuration parameters to a database that does not have them yet.
/// </summary>
/// <remarks>
/// The seed is idempotent: a parameter that already exists is never overwritten, so an
/// administrator who changed a value keeps the change. The seed runs after the migrations
/// and before the configuration is loaded for the first time, so the rules always
/// find a value.
/// </remarks>
public sealed class ConfigSeedService
{
    private readonly AetherisDbContext _dbContext;
    private readonly ILogger<ConfigSeedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigSeedService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="logger">The logger.</param>
    public ConfigSeedService(AetherisDbContext dbContext, ILogger<ConfigSeedService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Inserts the missing configuration parameters.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of parameters that were inserted.</returns>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<(string Key, string Value)> parameters = ConfigSeed.GetParameters();

        List<string> existingKeys = await _dbContext.ConfigParameters
            .AsNoTracking()
            .Select(parameter => parameter.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missing = parameters
            .Where(parameter => !existingKeys.Contains(parameter.Key, StringComparer.Ordinal))
            .ToList();

        if (missing.Count == 0)
        {
            return 0;
        }

        foreach ((string key, string value) in missing)
        {
            _dbContext.ConfigParameters.Add(new ConfigParameterEntity
            {
                Key = key,
                Value = value,
                Version = 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = "seed",
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Seeded {Count} configuration parameters: {Keys}.",
            missing.Count,
            string.Join(", ", missing.Select(parameter => parameter.Key)));

        return missing.Count;
    }
}

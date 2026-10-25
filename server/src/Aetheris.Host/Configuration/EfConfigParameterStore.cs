using Aetheris.Core.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Host.Configuration;

/// <summary>
/// Reads configuration parameters from PostgreSQL using Entity Framework Core.
/// </summary>
public sealed class EfConfigParameterStore : IConfigParameterStore
{
    private readonly AetherisDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfConfigParameterStore"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public EfConfigParameterStore(AetherisDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KeyValuePair<string, ConfigParameter>>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        List<ConfigParameterEntity> rows = await _dbContext.ConfigParameters
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(row => new KeyValuePair<string, ConfigParameter>(row.Key, row.ToDomain()))
            .ToList();
    }
}

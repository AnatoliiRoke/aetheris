using Aetheris.Core.Admin;
using Aetheris.Core.Text;
using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.World.Data;
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
/// <para>
/// The starting value of a missing parameter can be replaced for one environment through the
/// <c>ConfigSeed:Overrides</c> section, for example to switch the second factor of the
/// administrators off on a developer machine (task 00007, decision 6). An existing value is
/// never touched by an override either.
/// </para>
/// </remarks>
public sealed class ConfigSeedService
{
    /// <summary>
    /// The configuration section with the starting values of one environment.
    /// </summary>
    public const string OverridesSection = "ConfigSeed:Overrides";

    private readonly AetherisDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigSeedService> _logger;
    private readonly IEnumerable<IAdminReferenceService> _references;
    private readonly ILocalizationTexts _texts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigSeedService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="references">The reference kinds of the modules, for their permissions and the texts of their labels.</param>
    /// <param name="texts">The texts of the game data.</param>
    public ConfigSeedService(
        AetherisDbContext dbContext,
        IConfiguration configuration,
        ILogger<ConfigSeedService> logger,
        IEnumerable<IAdminReferenceService> references,
        ILocalizationTexts texts)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(texts);

        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _references = references;
        _texts = texts;
    }

    /// <summary>
    /// Inserts the missing configuration parameters.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of parameters that were inserted.</returns>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        // The lists of the account rules are seeded with the parameters, so a fresh database
        // and a cleared test database get both (task 00006).
        await AccountListsSeed.SeedAsync(_dbContext, cancellationToken).ConfigureAwait(false);
        await SeedReferenceKindsAsync(cancellationToken).ConfigureAwait(false);

        IConfigurationSection overrides = _configuration.GetSection(OverridesSection);
        IReadOnlyList<(string Key, string Value)> parameters = ConfigSeed.GetParameters()
            .Select(parameter => (parameter.Key, overrides[parameter.Key] ?? parameter.Value))
            .ToList();

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

    /// <summary>
    /// Adds the permissions of the reference kinds of the modules to the seed of the permissions,
    /// and the missing texts of their labels (task 00023). A text changed in the admin panel is
    /// kept.
    /// </summary>
    private async Task SeedReferenceKindsAsync(CancellationToken cancellationToken)
    {
        List<IAdminReferenceService> references = [.. _references];
        List<SeedAdminPermission> permissions =
        [
            .. references
                .SelectMany(reference => reference.Descriptors)
                .SelectMany(descriptor => descriptor.Permissions.Screens.Select(code => new SeedAdminPermission(code, AdminPermissionKinds.Screen))
                    .Concat(descriptor.Permissions.Actions.Select(code => new SeedAdminPermission(code, AdminPermissionKinds.Action)))),
        ];

        await AdminAccessSeed.SeedAsync(_dbContext, permissions, cancellationToken).ConfigureAwait(false);

        int added = await _texts.AddMissingAsync(references.SelectMany(reference => reference.LabelTexts), SeedTexts.Author, cancellationToken)
            .ConfigureAwait(false);

        if (added > 0)
        {
            _texts.Invalidate();
        }
    }
}

using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.Production.Data;
using Aetheris.Requirements;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Production.Services;

/// <summary>
/// Prepares the module: fills the tables of the production with the starting data
/// (task 00013, decision 2.12).
/// </summary>
/// <remarks>
/// Every step changes nothing on a repeated start. The seed runs only while the tables are
/// empty, so it never overwrites a change of the admin panel (task 00023, decision 7).
/// </remarks>
public sealed class ProductionBootstrapService
{
    private readonly ProductionDbContext _dbContext;
    private readonly ProductionUnitOfWork _unitOfWork;
    private readonly ProductionCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly ILocalizationTexts _texts;
    private readonly ICodeSequence _codes;
    private readonly IRequirementsModule _requirements;
    private readonly ITechnologyLevels _technologies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductionBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the production.</param>
    /// <param name="world">The world, for the resources and the races.</param>
    /// <param name="planets">The bodies module, for the factories of the recipes.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="requirements">The common requirements module.</param>
    public ProductionBootstrapService(
        ProductionDbContext dbContext,
        ProductionUnitOfWork unitOfWork,
        ProductionCatalogCache catalogCache,
        IWorldModule world,
        IPlanetsModule planets,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IRequirementsModule requirements,
        ITechnologyLevels technologies)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(technologies);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _world = world;
        _planets = planets;
        _texts = texts;
        _codes = codes;
        _requirements = requirements;
        _technologies = technologies;
    }

    /// <summary>
    /// Runs the steps.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    public async Task<ProductionBootstrapResult> EnsureAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RaceDto> races = await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyDictionary<string, int> technologyIds = await _technologies.GetTechnologyIdsByCodeAsync(cancellationToken).ConfigureAwait(false);

        bool seeded = await _unitOfWork.RunAsync(
            () => ProductionReferenceSeed.SeedAsync(_dbContext, resources, races, _planets, _texts, _codes, _requirements, technologyIds, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        _catalogCache.Invalidate();

        return new ProductionBootstrapResult(seeded);
    }
}

/// <summary>
/// What the preparation of the module did (task 00013).
/// </summary>
/// <param name="Seeded">Whether the starting data was written.</param>
public sealed record ProductionBootstrapResult(bool Seeded);

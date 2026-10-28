using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Requirements;
using Aetheris.Research.Data;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Research.Services;

/// <summary>
/// Prepares the module: fills the technology tables of a fresh database (task 00012).
/// </summary>
/// <remarks>
/// Every step changes nothing on a repeated start. The seed runs only while the technology
/// table is empty, so it never overwrites a change.
/// </remarks>
public sealed class ResearchBootstrapService
{
    private readonly ResearchDbContext _dbContext;
    private readonly ResearchUnitOfWork _unitOfWork;
    private readonly TechnologyCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IGameConfig _config;
    private readonly ILocalizationTexts _texts;
    private readonly ICodeSequence _codes;
    private readonly IPlanetsModule _planets;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction.</param>
    /// <param name="catalogCache">The catalog of the technologies.</param>
    /// <param name="world">The world, for the resources.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="planets">The bodies module, for the buildings a technology opens.</param>
    /// <param name="requirements">The common requirements module.</param>
    public ResearchBootstrapService(
        ResearchDbContext dbContext,
        ResearchUnitOfWork unitOfWork,
        TechnologyCatalogCache catalogCache,
        IWorldModule world,
        IGameConfig config,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IPlanetsModule planets,
        IRequirementsModule requirements)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(requirements);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _world = world;
        _config = config;
        _texts = texts;
        _codes = codes;
        _planets = planets;
        _requirements = requirements;
    }

    /// <summary>
    /// Runs the steps.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    public async Task<ResearchBootstrapResult> EnsureAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);

        bool seeded = await _unitOfWork.RunAsync(
            () => ResearchReferenceSeed.SeedAsync(_dbContext, resources, _texts, _codes, _planets, _requirements, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        _catalogCache.Invalidate();

        return new ResearchBootstrapResult(seeded ? 1 : 0);
    }
}

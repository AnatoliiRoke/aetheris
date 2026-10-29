using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets;
using Aetheris.Processes.Data;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.World;
using Aetheris.World.Api;

namespace Aetheris.Processes.Services;

/// <summary>
/// Prepares the module: fills the tables of the processes with the starting data (task
/// 00014, decision 2.12).
/// </summary>
/// <remarks>
/// Every step changes nothing on a repeated start. The seed runs only while the tables are
/// empty, so it never overwrites a change of the admin panel (task 00023, decision 7).
/// </remarks>
public sealed class ProcessesBootstrapService
{
    private readonly ProcessesDbContext _dbContext;
    private readonly ProcessesUnitOfWork _unitOfWork;
    private readonly ProcessCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IPlanetsModule _planets;
    private readonly ILocalizationTexts _texts;
    private readonly ICodeSequence _codes;
    private readonly IRequirementsModule _requirements;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessesBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="catalogCache">The catalog of the processes.</param>
    /// <param name="world">The world, for the resources and the groups.</param>
    /// <param name="planets">The bodies module, for the buildings the processes run in.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    /// <param name="requirements">The common requirements module.</param>
    public ProcessesBootstrapService(
        ProcessesDbContext dbContext,
        ProcessesUnitOfWork unitOfWork,
        ProcessCatalogCache catalogCache,
        IWorldModule world,
        IPlanetsModule planets,
        ILocalizationTexts texts,
        ICodeSequence codes,
        IRequirementsModule requirements)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(requirements);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _catalogCache = catalogCache;
        _world = world;
        _planets = planets;
        _texts = texts;
        _codes = codes;
        _requirements = requirements;
    }

    /// <summary>
    /// Runs the steps.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    public async Task<ProcessesBootstrapResult> EnsureAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RaceDto> races = await _world.GetRacesAsync(cancellationToken).ConfigureAwait(false);

        bool seeded = await _unitOfWork.RunAsync(
            () => ProcessReferenceSeed.SeedAsync(_dbContext, resources, races, _planets, _texts, _codes, _requirements, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        _catalogCache.Invalidate();

        return new ProcessesBootstrapResult(seeded);
    }
}

/// <summary>
/// What the preparation of the module did (task 00014).
/// </summary>
/// <param name="Seeded">Whether the starting data was written.</param>
public sealed record ProcessesBootstrapResult(bool Seeded);

using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Text;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// What the start of the module did.
/// </summary>
/// <param name="ReferenceSeeded">Whether the building tables were filled.</param>
/// <param name="RacesAssigned">The races that got the start building.</param>
/// <param name="AdoptedPlanets">The home bodies of the civilizations created before the module.</param>
public sealed record PlanetsBootstrapResult(bool ReferenceSeeded, int RacesAssigned, int AdoptedPlanets);

/// <summary>
/// Prepares the module at start: the building tables, the start building of the races, and
/// the civilizations created before the module (task 00011, section 3.1).
/// </summary>
/// <remarks>
/// Every step changes nothing on a repeated start. A civilization of task 00006 has no state
/// in the module: it gets one, the start building of its race, and the amounts from the old
/// storage of the world.
/// </remarks>
public sealed class PlanetsBootstrapService
{
    private readonly PlanetsDbContext _dbContext;
    private readonly PlanetsUnitOfWork _unitOfWork;
    private readonly PlanetStartPackage _startPackage;
    private readonly BuildingCatalogCache _catalogCache;
    private readonly IWorldModule _world;
    private readonly IGameConfig _config;
    private readonly ILocalizationTexts _texts;
    private readonly ICodeSequence _codes;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetsBootstrapService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction.</param>
    /// <param name="startPackage">The start package writer.</param>
    /// <param name="catalogCache">The building catalog.</param>
    /// <param name="world">The world.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="texts">The texts of the game data.</param>
    /// <param name="codes">The counters of the neutral codes.</param>
    public PlanetsBootstrapService(
        PlanetsDbContext dbContext,
        PlanetsUnitOfWork unitOfWork,
        PlanetStartPackage startPackage,
        BuildingCatalogCache catalogCache,
        IWorldModule world,
        IGameConfig config,
        ILocalizationTexts texts,
        ICodeSequence codes)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(startPackage);
        ArgumentNullException.ThrowIfNull(catalogCache);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(codes);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _startPackage = startPackage;
        _catalogCache = catalogCache;
        _world = world;
        _config = config;
        _texts = texts;
        _codes = codes;
    }

    /// <summary>
    /// Runs the steps.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    public async Task<PlanetsBootstrapResult> EnsureAsync(CancellationToken cancellationToken)
    {
        ResourceCatalog resources = await _world.GetResourceCatalogAsync(cancellationToken).ConfigureAwait(false);
        bool seeded = await _unitOfWork.RunAsync(() => PlanetsReferenceSeed.SeedAsync(_dbContext, resources, _texts, _codes, cancellationToken), cancellationToken).ConfigureAwait(false);

        _catalogCache.Invalidate();

        if (seeded)
        {
            _texts.Invalidate();
        }

        // The start building is a flag of the data (task 00022, decision 5); with several
        // flagged types the oldest one wins.
        PlanetsSettings settings = PlanetsSettings.From(_config);
        int? startTypeId = await _dbContext.BuildingTypes.AsNoTracking()
            .Where(type => type.IsStartBuilding)
            .OrderBy(type => type.Id)
            .Select(type => (int?)type.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        int assigned = startTypeId is { } typeId
            ? await _world.AssignStartBuildingAsync(typeId, settings.StartBuildingLevel, cancellationToken).ConfigureAwait(false)
            : 0;

        int adopted = 0;

        foreach (CivilizationHome home in await _world.GetCivilizationHomesAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await _dbContext.PlanetStates.AnyAsync(state => state.PlanetId == home.PlanetId, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            IReadOnlyDictionary<int, long> legacy = await _world.TakeLegacyStorageAsync(home.PlanetId, cancellationToken).ConfigureAwait(false);

            await _unitOfWork.RunAsync(
                async () =>
                {
                    await _startPackage.EnsureStateAsync(home.PlanetId, home.SectorId, home.PlayerId, cancellationToken).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await _startPackage.AddResourcesAsync(home.PlanetId, home.SectorId, legacy, cancellationToken).ConfigureAwait(false);

                    if (home.StartBuildingTypeId is { } buildingTypeId)
                    {
                        await _startPackage.PlaceBuildingAsync(home.PlanetId, home.SectorId, buildingTypeId, home.StartBuildingLevel, cancellationToken).ConfigureAwait(false);
                    }

                    return true;
                },
                cancellationToken).ConfigureAwait(false);

            adopted++;
        }

        return new PlanetsBootstrapResult(seeded, assigned, adopted);
    }
}

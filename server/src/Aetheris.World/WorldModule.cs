using Aetheris.Core.Configuration;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Extraction;
using Aetheris.World.Services;

namespace Aetheris.World;

/// <summary>
/// The implementation of <see cref="IWorldModule"/>: a thin front of the world services.
/// </summary>
public sealed class WorldModule : IWorldModule
{
    private readonly WorldGenerationService _generation;
    private readonly StartPlanetAllocator _allocator;
    private readonly CivilizationService _civilizations;
    private readonly FieldService _fields;
    private readonly WorldQueryService _queries;
    private readonly GenerationRunService _runs;
    private readonly DepositRichnessService _deposits;
    private readonly IGameConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldModule"/> class.
    /// </summary>
    /// <param name="generation">The generation service.</param>
    /// <param name="allocator">The start body allocator.</param>
    /// <param name="civilizations">The civilization service.</param>
    /// <param name="fields">The field service.</param>
    /// <param name="queries">The read service.</param>
    /// <param name="runs">The generation runs of the admin panel.</param>
    /// <param name="config">The game configuration.</param>
    public WorldModule(
        WorldGenerationService generation,
        StartPlanetAllocator allocator,
        CivilizationService civilizations,
        FieldService fields,
        WorldQueryService queries,
        GenerationRunService runs,
        DepositRichnessService deposits,
        IGameConfig config)
    {
        ArgumentNullException.ThrowIfNull(generation);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(civilizations);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(deposits);
        ArgumentNullException.ThrowIfNull(config);

        _generation = generation;
        _allocator = allocator;
        _civilizations = civilizations;
        _fields = fields;
        _queries = queries;
        _runs = runs;
        _deposits = deposits;
        _config = config;
    }

    /// <inheritdoc />
    public string ModuleName => "World";

    /// <inheritdoc />
    public TimeSpan InSystemTravelTime => WorldSettings.From(_config).InSystemTravelTime;

    /// <inheritdoc />
    public Task<WorldBootstrapResult> EnsureWorldAsync(CancellationToken cancellationToken = default) =>
        _generation.EnsureWorldAsync(cancellationToken);

    /// <inheritdoc />
    public Task<SectorSummary> AddSectorAsync(long? seed, bool isNewbie, string actorId, CancellationToken cancellationToken = default) =>
        _generation.AddSectorAsync(seed, isNewbie, actorId, cancellationToken);

    /// <inheritdoc />
    public Task<int> FailInterruptedGenerationRunsAsync(CancellationToken cancellationToken = default) =>
        _runs.FailInterruptedAsync(cancellationToken);

    /// <inheritdoc />
    public Task<SectorSummary> RegenerateSectorAsync(int sectorId, long? seed, string actorId, CancellationToken cancellationToken = default) =>
        _generation.RegenerateSectorAsync(sectorId, seed, actorId, cancellationToken);

    /// <inheritdoc />
    public Task<SectorStatistics?> GetSectorStatisticsAsync(int sectorId, CancellationToken cancellationToken = default) =>
        _generation.GetStatisticsAsync(sectorId, cancellationToken);

    /// <inheritdoc />
    public Task<StartPlanetResult> AllocateStartPlanetAsync(string playerId, CancellationToken cancellationToken = default) =>
        _allocator.AllocateAsync(playerId, null, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RaceDto>> GetRacesAsync(CancellationToken cancellationToken = default) =>
        await _civilizations.GetRacesAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<CivilizationSummary?> GetCivilizationAsync(string playerId, CancellationToken cancellationToken = default) =>
        _civilizations.GetAsync(playerId, cancellationToken);

    /// <inheritdoc />
    public Task ReleasePlayerAsync(string playerId, CancellationToken cancellationToken = default) =>
        _civilizations.ReleaseAsync(playerId, cancellationToken);

    /// <inheritdoc />
    public Task<ExtractionResult> ExtractFromFieldAsync(long fieldId, long processingMass, long freeHold, CancellationToken cancellationToken = default) =>
        _fields.ExtractAsync(fieldId, processingMass, freeHold, cancellationToken);

    /// <inheritdoc />
    public Task<double?> GetSystemDistanceAsync(long fromSystemId, long toSystemId, CancellationToken cancellationToken = default) =>
        _queries.GetSystemDistanceAsync(fromSystemId, toSystemId, cancellationToken);

    /// <inheritdoc />
    public Task<SystemContentDto?> GetSystemContentAsync(long systemId, CancellationToken cancellationToken = default) =>
        _queries.GetSystemContentAsync(null, systemId, cancellationToken);

    /// <inheritdoc />
    public Task<PlanetFacts?> GetPlanetFactsAsync(long planetId, CancellationToken cancellationToken = default) =>
        _queries.GetPlanetFactsAsync(planetId, cancellationToken);

    /// <inheritdoc />
    public Task<string?> GetObjectTypeCodeAsync(int objectTypeId, CancellationToken cancellationToken = default) =>
        _queries.GetObjectTypeCodeAsync(objectTypeId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<OwnedPlanet>> GetOwnedPlanetsAsync(string playerId, CancellationToken cancellationToken = default) =>
        _queries.GetOwnedPlanetsAsync(playerId, cancellationToken);

    /// <inheritdoc />
    public Task<ResourceCatalog> GetResourceCatalogAsync(CancellationToken cancellationToken = default) =>
        _queries.GetResourceCatalogAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CivilizationHome>> GetCivilizationHomesAsync(CancellationToken cancellationToken = default) =>
        await _civilizations.GetHomesAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, long>> TakeLegacyStorageAsync(long planetId, CancellationToken cancellationToken = default) =>
        _civilizations.TakeLegacyStorageAsync(planetId, cancellationToken);

    /// <inheritdoc />
    public Task<TerraformResult> TerraformAsync(
        long planetId,
        decimal multiplierMin,
        decimal multiplierMax,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        _deposits.TerraformAsync(planetId, multiplierMin, multiplierMax, actorId, reason, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<FreePlanetDto>> GetFreePlanetsByIdsAsync(IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken = default) =>
        _queries.GetFreePlanetsByIdsAsync(planetIds, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<FreePlanetDto>> GetFreePlanetsAsync(string playerId, int sectorId, CancellationToken cancellationToken = default) =>
        _queries.GetFreePlanetsAsync(playerId, sectorId, null, cancellationToken);

    /// <inheritdoc />
    public Task<int> AssignStartBuildingAsync(int buildingTypeId, int level, CancellationToken cancellationToken = default) =>
        _civilizations.AssignStartBuildingAsync(buildingTypeId, level, cancellationToken);
}

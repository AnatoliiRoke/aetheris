using Aetheris.World.Api;
using Aetheris.World.Extraction;

namespace Aetheris.World;

/// <summary>
/// The public contract of the World module (task 00004).
/// </summary>
/// <remarks>
/// The module owns the galaxy, its sectors, star systems, bodies, fields and nebulae, and
/// the rules of their existence. Other modules reach the world only through this interface,
/// never through its tables (documentation/tech/01, section 8).
/// </remarks>
public interface IWorldModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Gets the fixed travel time inside a star system (task 00004, decision 3).
    /// </summary>
    TimeSpan InSystemTravelTime { get; }

    /// <summary>
    /// Creates the galaxy and the initial sectors when they are missing; a repeated call
    /// changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was created.</returns>
    Task<WorldBootstrapResult> EnsureWorldAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a sector at the edge of the galaxy.
    /// </summary>
    /// <param name="seed">The seed, or <see langword="null"/> to derive it.</param>
    /// <param name="isNewbie">Whether newcomers start there.</param>
    /// <param name="actorId">Who adds the sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The new sector.</returns>
    Task<SectorSummary> AddSectorAsync(long? seed, bool isNewbie, string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the generation runs that were running when the server stopped as failed, so a new
    /// run can start (task 00007, decision 3).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of runs marked.</returns>
    Task<int> FailInterruptedGenerationRunsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a sector without owners again.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="seed">The new seed, or <see langword="null"/> to keep the old one.</param>
    /// <param name="actorId">Who starts the action.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sector.</returns>
    Task<SectorSummary> RegenerateSectorAsync(int sectorId, long? seed, string actorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the content of a sector.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The statistics, or <see langword="null"/> when the sector does not exist.</returns>
    Task<SectorStatistics?> GetSectorStatisticsAsync(int sectorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a player a free start body, atomically and once.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The start body.</returns>
    Task<StartPlanetResult> AllocateStartPlanetAsync(string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the races of the world with their identifiers and their codes (task 00013).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The races.</returns>
    Task<IReadOnlyList<RaceDto>> GetRacesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the civilization of a player: the race and the home body chosen at the first step
    /// of the game (task 00006, decision 9).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The civilization, or <see langword="null"/> when the player has not created one yet.</returns>
    Task<CivilizationSummary?> GetCivilizationAsync(string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases everything a player holds in the world, when the account is deleted: the
    /// bodies become free again.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the player is released.</returns>
    Task ReleasePlayerAsync(string playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes one cycle on an asteroid field.
    /// </summary>
    /// <param name="fieldId">The field.</param>
    /// <param name="processingMass">The mass the module processes per cycle.</param>
    /// <param name="freeHold">The free room of the hold.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The yield.</returns>
    Task<ExtractionResult> ExtractFromFieldAsync(long fieldId, long processingMass, long freeHold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the distance between two star systems.
    /// </summary>
    /// <param name="fromSystemId">The first system.</param>
    /// <param name="toSystemId">The second system.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The three-dimensional distance, or <see langword="null"/> when a system is unknown.</returns>
    Task<double?> GetSystemDistanceAsync(long fromSystemId, long toSystemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content of a star system without the visibility filter, for the server itself.
    /// </summary>
    /// <param name="systemId">The system.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The content, or <see langword="null"/>.</returns>
    Task<SystemContentDto?> GetSystemContentAsync(long systemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the facts of a body for another module: the sector, the owner and the deposits.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts, or <see langword="null"/> when the body does not exist.</returns>
    Task<PlanetFacts?> GetPlanetFactsAsync(long planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the resource types and their groups.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    Task<ResourceCatalog> GetResourceCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the home body of every civilization with the start building of its race, so the
    /// module of the bodies can adopt the civilizations created before it.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The home bodies.</returns>
    Task<IReadOnlyList<CivilizationHome>> GetCivilizationHomesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes the amounts of a body out of the storage of task 00006; a second call returns nothing.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The amounts by resource.</returns>
    Task<IReadOnlyDictionary<int, long>> TakeLegacyStorageAsync(long planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terraforms a free body: raises the coefficient of every poorer deposit to the value, marks
    /// the body as terraformed and writes the change in the journal of the world (task 00014,
    /// section 9). A colony and a body terraformed before are refused and change nothing; the
    /// body stays free and gets no owner.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="coefficient">The new coefficient of the deposits.</param>
    /// <param name="actorId">Who terraformed the body.</param>
    /// <param name="reason">Why the deposits changed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome and the number of the deposits that changed.</returns>
    Task<TerraformResult> TerraformAsync(
        long planetId,
        decimal coefficient,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the free bodies of a sector that a player sees and may colonize, at most one page.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The free bodies.</returns>
    Task<IReadOnlyList<FreePlanetDto>> GetFreePlanetsAsync(string playerId, int sectorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the start building of every race that has none yet (task 00011).
    /// </summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of races changed.</returns>
    Task<int> AssignStartBuildingAsync(int buildingTypeId, int level, CancellationToken cancellationToken = default);
}

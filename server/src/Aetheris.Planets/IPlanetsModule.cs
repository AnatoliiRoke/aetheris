using Aetheris.Planets.Api;
using Aetheris.Planets.Services;
using Aetheris.Requirements.Services;

namespace Aetheris.Planets;

/// <summary>
/// The public contract of the Planets module (task 00011): the buildings of the bodies, the
/// common requirement mechanism, the construction queues and the balance of the bodies.
/// </summary>
/// <remarks>
/// Modules communicate only through public interfaces (documentation/tech/01, section 8).
/// The module reads the world through its contract and keeps its own tables.
/// </remarks>
public interface IPlanetsModule
{
    /// <summary>
    /// Gets the module name, used in logs and diagnostics.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Prepares the module: fills the building tables of a fresh database, gives the races
    /// their start building and adopts the civilizations created before the module. A repeated
    /// call changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was done.</returns>
    Task<PlanetsBootstrapResult> EnsureAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a body of a player: buildings, queue, balance, storage and energy.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The body, or <see langword="null"/> when the player does not own it.</returns>
    Task<PlanetOverviewDto?> GetPlanetAsync(string playerId, long planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the levels of the buildings of a body by type, from one read of the buildings and
    /// without the rest of the answer about the body (task 00036, section 6.3). The caller has
    /// checked the owner.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The largest level of a building by its type; a type the body has no building of is not listed.</returns>
    Task<IReadOnlyDictionary<int, int>> GetPlanetBuildingLevelsAsync(long planetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the requirements of a closed subject on a body (task 00011, decision 3).
    /// </summary>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>Whether the subject is available and why not.</returns>
    Task<AvailabilityDto> CheckRequirementsAsync(
        string subjectType,
        int subjectId,
        int level,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the level of the building of a class on a body: the level of the science centre
    /// of a body, for example (task 00012, decision 6).
    /// </summary>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, or <see langword="null"/> when the level of any body is wanted.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the building, or zero when the body has no building of the class.</returns>
    Task<int> GetBuildingLevelAsync(
        string buildingClass,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the value of one effect of the building of a class on a body, at the level the body
    /// has built: the coefficient of the converter of the body, for example.
    /// </summary>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="effectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, or <see langword="null"/> when the level of any body is wanted.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The value of the effect; zero when the body has no such building or the building has no such effect.</returns>
    Task<decimal> GetBuildingEffectValueAsync(
        string buildingClass,
        string effectType,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the values of the effects of one building of a body, at the level it has built: the
    /// coefficient of the converter of the body, for example.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="playerId">The player, or <see langword="null"/> when the level of any body is wanted.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The values of the effects by their type; a building without effects gives nothing.</returns>
    Task<IReadOnlyDictionary<string, decimal>> GetBuildingEffectsAsync(
        long planetId,
        long planetBuildingId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the identifier of a building type by its neutral code.
    /// </summary>
    /// <param name="code">The neutral code of the building type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier, or <see langword="null"/> when the code is unknown.</returns>
    /// <remarks>
    /// The codes come from the data of the other modules, so they are looked up and never
    /// written in the code (documentation/tech/09, section 3.1).
    /// </remarks>
    Task<int?> FindBuildingTypeIdAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the identifier of a building type by its class.
    /// </summary>
    /// <param name="buildingClass">The class of the building type, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifier, or <see langword="null"/> when no building has the class.</returns>
    /// <remarks>
    /// The class comes from the data of the other modules, so it is looked up and never
    /// written in the code (documentation/tech/09, section 3.1).
    /// </remarks>
    Task<int?> FindBuildingTypeIdByClassAsync(string buildingClass, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every body of a player with the level of its building of a class: the bodies a
    /// research can start from, for example (task 00012, decisions 5 and 5a).
    /// </summary>
    /// <param name="buildingClass">The class of the building, one of <see cref="BuildingClasses"/>.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bodies of the player in the order of their identifiers; the level is zero on a body without the building.</returns>
    Task<IReadOnlyList<PlanetBuildingLevel>> GetBuildingLevelsAsync(
        string buildingClass,
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every body of a player with the level of its building of each of several classes, in
    /// one read of the bodies and one read of the buildings.
    /// </summary>
    /// <param name="buildingClasses">The classes of the buildings, each one of <see cref="BuildingClasses"/>.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bodies of the player in the order of their identifiers, by class; the level is zero on a body without the building.</returns>
    Task<IReadOnlyDictionary<string, IReadOnlyList<PlanetBuildingLevel>>> GetBuildingLevelsAsync(
        IReadOnlyCollection<string> buildingClasses,
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets what the science centre of the civilization gives at the largest level the caller has
    /// read already: the places and the speed of the research. Only the cached catalog is read, so
    /// a caller that knows the levels asks the database nothing (code audit of 04.11.2026).
    /// </summary>
    /// <param name="researchCenterLevel">The largest level of the science centres of the player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bonus; only its fields of the research are filled.</returns>
    Task<OwnerBuildingBonus> GetResearchCenterBonusAsync(
        int researchCenterLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets what the buildings of every body of a player give the whole civilization (task
    /// 00035): the places and the speed the building plants, the science centres and the design
    /// bureaus give.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bonus of the owner; nothing changes for a player without such buildings.</returns>
    Task<OwnerBuildingBonus> GetOwnerBuildingBonusAsync(
        string playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the level of a building type on a body of a player: the level of the factory
    /// that makes a part, for example (task 00013, decision 2.7).
    /// </summary>
    /// <param name="buildingTypeId">The building type.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the building, or zero when the body has no building of the type.</returns>
    Task<int> GetBuildingLevelByTypeAsync(
        int buildingTypeId,
        long planetId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the level of one building of a body by the row of the building: the level of the
    /// factory that makes a part, for example (task 00013, decision 2.7).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="planetBuildingId">The building.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The level of the building and the type it is of, or zero when the body has no such building.</returns>
    Task<PlanetBuildingLevelById> GetBuildingLevelByIdAsync(
        long planetId,
        long planetBuildingId,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the buildings of a body that are of the given types, with their rows and their
    /// levels: the factories of a body, for example (task 00013, decision 2.7).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="buildingTypeIds">The types of the buildings to look for.</param>
    /// <param name="playerId">The player, who must own the body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The buildings of the body that are of the given types.</returns>
    Task<IReadOnlyList<PlanetBuildingLevelById>> GetBuildingsByTypesAsync(
        long planetId,
        IReadOnlyCollection<int> buildingTypeIds,
        string? playerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sum of one effect of the buildings of a body at their levels: the room of the
    /// storage of the parts the production module keeps, for example (task 00013, decision 9).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="effectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum of the values of the effect, or zero when no building has it.</returns>
    Task<long> GetBuildingEffectTotalAsync(
        long planetId,
        string effectType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sum of one effect of the buildings of several bodies at once (task 00029), so a
    /// module that needs it for every body of a player does not read the buildings body by body.
    /// </summary>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="effectType">The effect type, one of <see cref="BuildingEffectTypes"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sum by body; a body without the effect has zero.</returns>
    Task<IReadOnlyDictionary<long, long>> GetBuildingEffectTotalsAsync(
        IReadOnlyCollection<long> planetIds,
        string effectType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the localisation keys of the names of the building types, for the modules that
    /// name a building they open (task 00012, decision 9).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The keys by building type.</returns>
    Task<IReadOnlyDictionary<int, string>> GetBuildingNameKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the building catalog kept in memory, so the next read loads it again. A module
    /// that adds requirements to the building types calls it after its change is committed.
    /// </summary>
    void InvalidateCatalog();

    /// <summary>
    /// Founds a colony on a body (task 00073): the state of the body, the same start buildings
    /// that a new civilization gets, and the population of the colony.
    /// </summary>
    /// <remarks>
    /// The buildings are the same as the ones of the registration, because the same method places
    /// them: the types with the flag of the start building and the start building of the race.
    /// </remarks>
    /// <param name="playerId">The owner of the body.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="sectorId">The sector of the body.</param>
    /// <param name="raceBuildingTypeId">The start building of the race.</param>
    /// <param name="raceBuildingLevel">The level of the start building.</param>
    /// <param name="inhabitants">The number of inhabitants of the colony.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the colony is founded.</returns>
    Task FoundColonyAsync(
        string playerId,
        long planetId,
        int sectorId,
        int raceBuildingTypeId,
        int raceBuildingLevel,
        long inhabitants,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A building of a body with its level and its type (task 00013).
/// </summary>
/// <param name="PlanetBuildingId">The building.</param>
/// <param name="BuildingTypeId">The type of the building.</param>
/// <param name="Level">The level of the building.</param>
/// <param name="ProductionSpeedBonus">The share of the production time the level of the building takes off (task 00045); zero for a building without the bonus.</param>
public sealed record PlanetBuildingLevelById(long PlanetBuildingId, int BuildingTypeId, int Level, decimal ProductionSpeedBonus = 0m);

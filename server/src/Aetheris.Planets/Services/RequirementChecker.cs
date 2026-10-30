using Aetheris.Requirements.Services;

namespace Aetheris.Planets.Services;

/// <summary>
/// What a requirement is checked against: the buildings and deposits of the body and the
/// technologies of the player.
/// </summary>
/// <param name="BuildingLevels">The levels of the buildings of the body by type.</param>
/// <param name="DepositResourceIds">The resources with a deposit on the body.</param>
/// <param name="TechnologyLevels">The levels of the technologies of the player by technology.</param>
public sealed record RequirementContext(
    IReadOnlyDictionary<int, int> BuildingLevels,
    IReadOnlySet<int> DepositResourceIds,
    IReadOnlyDictionary<int, int> TechnologyLevels);

/// <summary>A requirement that is not met, with the reason for the client.</summary>
/// <param name="RequirementId">The requirement.</param>
/// <param name="RequirementType">The requirement type.</param>
/// <param name="TargetId">What is required.</param>
/// <param name="TargetLevel">The required level.</param>
/// <param name="ReasonCode">The localisation key of the reason.</param>
public sealed record UnmetRequirement(int RequirementId, string RequirementType, int TargetId, int TargetLevel, string ReasonCode);

/// <summary>
/// The one check of the requirements of every closed subject (task 00011, decision 3): a
/// building type and its levels now, technologies, modules, recipes and ship classes later.
/// </summary>
/// <remarks>
/// A technology that does not exist yet, or that the player has not opened, leaves its
/// requirement unmet; so do an unknown requirement type and a requirement on a building the
/// body does not have.
/// </remarks>
public static class RequirementChecker
{
    /// <summary>
    /// Finds the requirements of a subject level that are not met.
    /// </summary>
    /// <param name="catalog">The catalog with the requirements.</param>
    /// <param name="subjectType">The subject type, one of <see cref="RequirementSubjectTypes"/>.</param>
    /// <param name="subjectId">The subject.</param>
    /// <param name="level">The level of the subject.</param>
    /// <param name="context">The state the requirements are checked against.</param>
    /// <returns>The unmet requirements; empty when the subject is available.</returns>
    public static IReadOnlyList<UnmetRequirement> Check(
        BuildingCatalog catalog,
        string subjectType,
        int subjectId,
        int level,
        RequirementContext context)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return Check(catalog.RequirementsOf(subjectType, subjectId, level), context);
    }

    /// <summary>
    /// Finds the requirements that are not met among the given ones, for a subject whose
    /// requirements the building catalog does not keep (a technology, for example).
    /// </summary>
    /// <param name="requirements">The requirements of the subject level.</param>
    /// <param name="context">The state the requirements are checked against.</param>
    /// <returns>The unmet requirements; empty when the subject is available.</returns>
    public static IReadOnlyList<UnmetRequirement> Check(IEnumerable<RequirementInfo> requirements, RequirementContext context)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(context);

        List<UnmetRequirement> unmet = [];

        foreach (RequirementInfo requirement in requirements)
        {
            string? reason = requirement.RequirementType switch
            {
                RequirementTypes.BuildingLevel => context.BuildingLevels.GetValueOrDefault(requirement.TargetId) >= requirement.TargetLevel
                    ? null
                    : PlanetsErrorCodes.RequirementBuildingLevel,
                RequirementTypes.TechnologyLevel => context.TechnologyLevels.TryGetValue(requirement.TargetId, out int technologyLevel) && technologyLevel >= requirement.TargetLevel
                    ? null
                    : PlanetsErrorCodes.RequirementTechnology,
                RequirementTypes.Deposit => context.DepositResourceIds.Contains(requirement.TargetId)
                    ? null
                    : PlanetsErrorCodes.RequirementDeposit,
                _ => PlanetsErrorCodes.RequirementUnknown,
            };

            if (reason is not null)
            {
                unmet.Add(new UnmetRequirement(requirement.Id, requirement.RequirementType, requirement.TargetId, requirement.TargetLevel, reason));
            }
        }

        return unmet;
    }
}

/// <summary>
/// The technologies of a player, for the requirements and the bonuses. The research task
/// (00012) provides them; until then a player has none.
/// </summary>
public interface ITechnologyLevels
{
    /// <summary>
    /// Gets the levels of the technologies of a player.
    /// </summary>
    /// <param name="playerId">The player, or <see langword="null"/> for a body without an owner.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The levels by technology.</returns>
    Task<IReadOnlyDictionary<int, int>> GetLevelsAsync(string? playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the bonuses the technologies of a player give the bodies.
    /// </summary>
    /// <param name="playerId">The player, or <see langword="null"/> for a body without an owner.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bonuses.</returns>
    Task<PlanetBonuses> GetBonusesAsync(string? playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the technologies that exist, for the check of a requirement of a building in the
    /// admin panel (task 00023).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifiers of the technologies.</returns>
    Task<IReadOnlySet<int>> GetTechnologyIdsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the localisation keys of the names of the technologies, for the modules that name
    /// the technology a subject waits for (task 00013, decision 7).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The keys by technology.</returns>
    Task<IReadOnlyDictionary<int, string>> GetTechnologyNameKeysAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the technologies by their neutral code, for the starting data of the modules that
    /// name a technology (task 00014).
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The identifiers of the technologies by their code.</returns>
    Task<IReadOnlyDictionary<string, int>> GetTechnologyIdsByCodeAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The multipliers the technologies give a body (task 00012, decision 4).
/// </summary>
/// <param name="MetalProductionMultiplier">The multiplier of the production of the metal mines.</param>
/// <param name="CrystalProductionMultiplier">The multiplier of the production of the crystal mines.</param>
/// <param name="ConstructionTimeMultiplier">The multiplier of the construction time.</param>
/// <param name="StorageCapacityMultiplier">The multiplier of the capacity of the storages.</param>
/// <param name="EnergyOutputMultiplier">The multiplier of the energy the buildings produce.</param>
/// <param name="PopulationMaxMultiplier">The multiplier of the largest population of the body.</param>
/// <remarks>
/// Every multiplier is a separate bonus of the technologies, so a technology changes only the
/// parameter it belongs to. A multiplier of one changes nothing.
/// </remarks>
public sealed record PlanetBonuses(
    decimal MetalProductionMultiplier,
    decimal CrystalProductionMultiplier,
    decimal ConstructionTimeMultiplier,
    decimal StorageCapacityMultiplier,
    decimal EnergyOutputMultiplier,
    decimal PopulationMaxMultiplier)
{
    /// <summary>Gets the bonuses of a player without technologies: nothing changes.</summary>
    public static PlanetBonuses None { get; } = new(
        decimal.One,
        decimal.One,
        decimal.One,
        decimal.One,
        decimal.One,
        decimal.One);
}

/// <summary>
/// The technologies until the research task exists: none, so every technology requirement is
/// unmet and the bonuses change nothing.
/// </summary>
public sealed class NoTechnologyLevels : ITechnologyLevels
{
    private static readonly IReadOnlyDictionary<int, int> Empty = new Dictionary<int, int>();

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, int>> GetLevelsAsync(string? playerId, CancellationToken cancellationToken) =>
        Task.FromResult(Empty);

    /// <inheritdoc />
    public Task<PlanetBonuses> GetBonusesAsync(string? playerId, CancellationToken cancellationToken) =>
        Task.FromResult(PlanetBonuses.None);

    /// <inheritdoc />
    public Task<IReadOnlySet<int>> GetTechnologyIdsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<int>>(new HashSet<int>());

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, string>> GetTechnologyNameKeysAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, int>> GetTechnologyIdsByCodeAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
}

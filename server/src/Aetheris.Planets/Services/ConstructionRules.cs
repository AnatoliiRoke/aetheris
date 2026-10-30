using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Requirements.Services;

namespace Aetheris.Planets.Services;

/// <summary>
/// The state of a body at a moment, gathered for the rules and the answer to the client.
/// </summary>
/// <param name="PlanetId">The body.</param>
/// <param name="ExtraQueueSlots">The paid places of the queue.</param>
/// <param name="Levels">The levels of the buildings by type.</param>
/// <param name="Queue">The construction queue in its order.</param>
/// <param name="Amounts">The amounts at <paramref name="Now"/>.</param>
/// <param name="FullAt">The moment every resource group is or will be full.</param>
/// <param name="Deposits">The richness coefficients of the deposits by resource.</param>
/// <param name="TechnologyLevels">The technologies of the owner.</param>
/// <param name="Bonuses">The bonuses of the technologies.</param>
/// <param name="Now">The moment of game time.</param>
public sealed record PlanetFigures(
    long PlanetId,
    int ExtraQueueSlots,
    IReadOnlyDictionary<int, int> Levels,
    IReadOnlyList<QueueItemFigure> Queue,
    IReadOnlyDictionary<int, long> Amounts,
    IReadOnlyDictionary<int, DateTimeOffset?> FullAt,
    IReadOnlyDictionary<int, decimal> Deposits,
    IReadOnlyDictionary<int, int> TechnologyLevels,
    PlanetBonuses Bonuses,
    DateTimeOffset Now)
{
    /// <summary>
    /// Gets the levels once every queued building is done.
    /// </summary>
    /// <returns>The levels by building type.</returns>
    public Dictionary<int, int> QueuedLevels()
    {
        Dictionary<int, int> levels = new(Levels);

        foreach (QueueItemFigure item in Queue)
        {
            levels[item.BuildingTypeId] = Math.Max(levels.GetValueOrDefault(item.BuildingTypeId), item.TargetLevel);
        }

        return levels;
    }

    /// <summary>
    /// Gets the context of the requirements: the buildings built now, not the queued ones.
    /// </summary>
    /// <returns>The context.</returns>
    public RequirementContext RequirementContext() =>
        new(Levels, Deposits.Keys.ToHashSet(), TechnologyLevels);
}

/// <summary>A record of the construction queue.</summary>
/// <param name="Id">The record.</param>
/// <param name="BuildingTypeId">The building type.</param>
/// <param name="TargetLevel">The level the building reaches.</param>
/// <param name="Position">The place in the queue.</param>
/// <param name="DurationSeconds">The construction time.</param>
/// <param name="StartedAt">The start, or <see langword="null"/> while it waits.</param>
/// <param name="FinishesAt">The end, or <see langword="null"/> while it waits.</param>
/// <param name="Costs">The cost paid.</param>
public sealed record QueueItemFigure(
    long Id,
    int BuildingTypeId,
    int TargetLevel,
    int Position,
    long DurationSeconds,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishesAt,
    IReadOnlyDictionary<int, long> Costs);

/// <summary>
/// Whether the next level of a building can be queued, and why not.
/// </summary>
/// <param name="Level">The next level.</param>
/// <param name="Info">The row of the level, or <see langword="null"/> when the table has none.</param>
/// <param name="DurationSeconds">The construction time with the bonus.</param>
/// <param name="ReasonCodes">The reasons it cannot be queued, in the order they are checked.</param>
/// <param name="Unmet">The unmet requirements.</param>
/// <param name="Missing">The resources that are short, by how much.</param>
/// <param name="EnergyAfter">The energy once the queue and this level are done.</param>
public sealed record NextLevelCheck(
    int Level,
    BuildingLevelInfo? Info,
    long DurationSeconds,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<UnmetRequirement> Unmet,
    IReadOnlyDictionary<int, long> Missing,
    EnergyBalance EnergyAfter);

/// <summary>
/// The rules of the construction queue (task 00011, decisions 1–7): the client gets the same
/// reasons the server refuses with.
/// </summary>
public static class ConstructionRules
{
    /// <summary>
    /// Gets the places of the queue of a body: the base places and the paid ones up to their limit.
    /// </summary>
    /// <param name="settings">The parameters.</param>
    /// <param name="extraSlots">The paid places of the body.</param>
    /// <returns>The places.</returns>
    public static int SlotCount(PlanetsSettings settings, int extraSlots)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.QueueBaseSlots + Math.Clamp(extraSlots, 0, settings.QueueMaxExtraSlots);
    }

    /// <summary>
    /// Checks the next level of a building in the order of the rules: requirements, the
    /// largest level, the level row, the limit of the main building, the places of the queue,
    /// the energy and the resources.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="settings">The parameters.</param>
    /// <param name="figures">The state of the body.</param>
    /// <param name="buildingType">The building type.</param>
    /// <returns>The result of the check.</returns>
    public static NextLevelCheck CheckNext(BuildingCatalog catalog, PlanetsSettings settings, PlanetFigures figures, BuildingTypeInfo buildingType)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(figures);
        ArgumentNullException.ThrowIfNull(buildingType);

        Dictionary<int, int> queuedLevels = figures.QueuedLevels();
        int level = queuedLevels.GetValueOrDefault(buildingType.Id) + 1;
        BuildingLevelInfo? info = buildingType.Levels.GetValueOrDefault(level);
        List<string> reasons = [];

        // An archived type keeps the buildings the players have, but no new level of it is
        // offered (task 00023, decision 5).
        if (buildingType.IsArchived)
        {
            reasons.Add(PlanetsErrorCodes.BuildingArchived);
        }

        IReadOnlyList<UnmetRequirement> unmet = RequirementChecker.Check(
            catalog,
            RequirementSubjectTypes.BuildingType,
            buildingType.Id,
            level,
            figures.RequirementContext());

        if (unmet.Count > 0)
        {
            reasons.Add(PlanetsErrorCodes.RequirementUnmet);
        }

        if (level > settings.BuildingMaxLevel)
        {
            reasons.Add(PlanetsErrorCodes.LevelMax);
        }
        else if (info is null)
        {
            reasons.Add(PlanetsErrorCodes.LevelMissing);
        }

        if (PlanetEconomy.LevelCap(catalog, figures.Levels, buildingType) is { } cap && level > cap)
        {
            reasons.Add(PlanetsErrorCodes.LevelCapped);
        }

        if (figures.Queue.Count >= SlotCount(settings, figures.ExtraQueueSlots))
        {
            reasons.Add(PlanetsErrorCodes.QueueFull);
        }

        queuedLevels[buildingType.Id] = level;
        EnergyBalance energyAfter = PlanetEconomy.Energy(catalog, queuedLevels, figures.Bonuses);
        bool consumes = info?.Effects.Any(effect => effect.EffectType == BuildingEffectTypes.EnergyConsumption && effect.Value > 0) ?? false;

        if (consumes && energyAfter.Balance < 0)
        {
            reasons.Add(PlanetsErrorCodes.EnergyShort);
        }

        Dictionary<int, long> missing = [];

        foreach ((int resourceTypeId, long amount) in info?.Costs ?? new Dictionary<int, long>())
        {
            long shortBy = amount - figures.Amounts.GetValueOrDefault(resourceTypeId);

            if (shortBy > 0)
            {
                missing[resourceTypeId] = shortBy;
            }
        }

        if (missing.Count > 0)
        {
            reasons.Add(PlanetsErrorCodes.ResourcesShort);
        }

        long duration = info is null ? 0 : (long)decimal.Ceiling(info.DurationSeconds * figures.Bonuses.ConstructionTimeMultiplier);

        return new NextLevelCheck(level, info, duration, reasons, unmet, missing, energyAfter);
    }
}

/// <summary>
/// Builds the answer about a body from its figures.
/// </summary>
public static class PlanetOverviewBuilder
{
    /// <summary>
    /// Builds the answer.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="settings">The parameters.</param>
    /// <param name="figures">The state of the body.</param>
    /// <param name="timeMultiplier">The game seconds per real second.</param>
    /// <returns>The answer.</returns>
    public static PlanetOverviewDto Build(BuildingCatalog catalog, PlanetsSettings settings, PlanetFigures figures, double timeMultiplier)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(figures);

        Dictionary<int, decimal> rates = PlanetEconomy.ProductionRates(catalog, figures.Levels, figures.Deposits, figures.Bonuses);
        Dictionary<int, long> capacities = PlanetEconomy.Capacities(catalog, figures.Levels, figures.Bonuses);
        Dictionary<int, int> queuedLevels = figures.QueuedLevels();
        EnergyBalance energy = PlanetEconomy.Energy(catalog, figures.Levels, figures.Bonuses);

        List<StorageGroupDto> groups = [];

        foreach (var group in catalog.Groups)
        {
            long stored = figures.Amounts.Where(entry => catalog.GroupOf(entry.Key) == group.Id).Sum(entry => entry.Value);
            long capacity = capacities.GetValueOrDefault(group.Id);
            DateTimeOffset? fullAt = figures.FullAt.GetValueOrDefault(group.Id);

            if (capacity == 0 && stored == 0)
            {
                continue;
            }

            groups.Add(new StorageGroupDto(group.Id, capacity, stored, fullAt is { } moment && moment <= figures.Now, fullAt));
        }

        HashSet<int> fullGroups = [.. groups.Where(group => group.IsFull).Select(group => group.ResourceGroupId)];

        List<PlanetResourceDto> resources = [];

        foreach (var resource in catalog.Resources)
        {
            // A currency of a civilization does not lie on a body, so it is not a resource of
            // the body (task 00015).
            if (resource.ResourceGroupId is not { } resourceGroupId)
            {
                continue;
            }

            long amount = figures.Amounts.GetValueOrDefault(resource.Id);
            decimal rate = fullGroups.Contains(resourceGroupId) ? 0 : rates.GetValueOrDefault(resource.Id);

            if (amount > 0 || rate > 0 || figures.Deposits.ContainsKey(resource.Id))
            {
                resources.Add(new PlanetResourceDto(resource.Id, resourceGroupId, amount, rate));
            }
        }

        List<BuildingDto> buildings = [];

        foreach (BuildingTypeInfo type in catalog.Types)
        {
            int level = figures.Levels.GetValueOrDefault(type.Id);

            // An archived type is shown only on a body that has a building of it or has it in the queue.
            if (type.IsArchived && level == 0 && queuedLevels.GetValueOrDefault(type.Id) == 0)
            {
                continue;
            }
            NextLevelCheck next = ConstructionRules.CheckNext(catalog, settings, figures, type);

            buildings.Add(new BuildingDto(
                type.Id,
                type.Code,
                type.NameKey,
                type.DescriptionKey,
                level,
                queuedLevels.GetValueOrDefault(type.Id),
                settings.BuildingMaxLevel,
                Effects(type.Levels.GetValueOrDefault(level)),
                new NextLevelDto(
                    next.Level,
                    next.Info is null ? null : next.DurationSeconds,
                    Amounts(next.Info?.Costs),
                    Effects(next.Info),
                    next.EnergyAfter.Balance,
                    next.ReasonCodes.Count == 0,
                    next.ReasonCodes,
                    Requirements(catalog, next.Unmet),
                    Amounts(next.Missing))));
        }

        return new PlanetOverviewDto(
            figures.PlanetId,
            figures.Now,
            timeMultiplier,
            resources,
            groups,
            new EnergyDto(energy.Output, energy.Consumption, energy.Balance, PlanetEconomy.Energy(catalog, queuedLevels, figures.Bonuses).Balance),
            new ConstructionQueueDto(
                ConstructionRules.SlotCount(settings, figures.ExtraQueueSlots),
                [
                    .. figures.Queue.Select(item => new QueueItemDto(
                        item.Id,
                        item.BuildingTypeId,
                        item.TargetLevel,
                        item.Position,
                        item.DurationSeconds,
                        item.StartedAt,
                        item.FinishesAt,
                        Amounts(item.Costs))),
                ]),
            buildings,
            PlanetEconomy.PopulationMax(catalog, figures.Levels, figures.Bonuses),
            [.. catalog.Resources.Select(resource => new NamedItemDto(resource.Id, resource.Code, resource.NameKey, resource.ResourceGroupId))],
            [.. catalog.Groups.Select(group => new NamedItemDto(group.Id, group.Code, group.NameKey, null))]);
    }

    /// <summary>
    /// Names the unmet requirements for the client.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="unmet">The unmet requirements.</param>
    /// <returns>The requirements with the names of their targets.</returns>
    public static IReadOnlyList<RequirementDto> Requirements(BuildingCatalog catalog, IReadOnlyList<UnmetRequirement> unmet)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(unmet);

        return
        [
            .. unmet.Select(requirement => new RequirementDto(
                requirement.RequirementId,
                requirement.RequirementType,
                requirement.TargetId,
                requirement.TargetLevel,
                requirement.ReasonCode,
                requirement.RequirementType switch
                {
                    RequirementTypes.BuildingLevel => catalog.FindType(requirement.TargetId)?.NameKey,
                    RequirementTypes.Deposit => catalog.Resources.FirstOrDefault(resource => resource.Id == requirement.TargetId)?.NameKey,
                    _ => null,
                })),
        ];
    }

    private static List<ResourceAmountDto> Amounts(IReadOnlyDictionary<int, long>? amounts) =>
        [.. (amounts ?? new Dictionary<int, long>()).OrderBy(entry => entry.Key).Select(entry => new ResourceAmountDto(entry.Key, entry.Value))];

    private static List<EffectDto> Effects(BuildingLevelInfo? level) =>
        [.. (level?.Effects ?? []).Select(effect => new EffectDto(effect.EffectType, effect.ResourceTypeId, effect.ResourceGroupId, effect.Value))];
}

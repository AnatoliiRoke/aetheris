namespace Aetheris.Planets.Services;

/// <summary>
/// The formulas of a body: production, storage, energy and the level limit (task 00011,
/// decisions 2, 7, 8 and 10).
/// </summary>
/// <remarks>
/// Only the shape of the formulas lives here; every value comes from the building tables, the
/// deposits of the world and the configuration. The functions are pure, so the read of a body
/// and the change of a body compute the same numbers.
/// </remarks>
public static class PlanetEconomy
{
    /// <summary>
    /// Gets the effects of the buildings at their levels.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="levels">The levels by building type.</param>
    /// <returns>The effects with the building type they come from.</returns>
    public static IEnumerable<(int BuildingTypeId, BuildingEffectInfo Effect)> Effects(BuildingCatalog catalog, IReadOnlyDictionary<int, int> levels)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(levels);

        foreach ((int buildingTypeId, int level) in levels)
        {
            if (level <= 0 || catalog.FindType(buildingTypeId)?.Levels.GetValueOrDefault(level) is not { } info)
            {
                continue;
            }

            foreach (BuildingEffectInfo effect in info.Effects)
            {
                yield return (buildingTypeId, effect);
            }
        }
    }

    /// <summary>
    /// Gets the production per hour of every resource: the value of the level times the
    /// richness of the deposit times the bonus of the kind of the resource. A resource without
    /// a deposit is not produced.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="levels">The levels by building type.</param>
    /// <param name="deposits">The richness coefficients by resource.</param>
    /// <param name="bonuses">The bonuses of the technologies.</param>
    /// <returns>The rates by resource.</returns>
    public static Dictionary<int, decimal> ProductionRates(
        BuildingCatalog catalog,
        IReadOnlyDictionary<int, int> levels,
        IReadOnlyDictionary<int, decimal> deposits,
        PlanetBonuses bonuses)
    {
        ArgumentNullException.ThrowIfNull(deposits);
        ArgumentNullException.ThrowIfNull(bonuses);

        Dictionary<int, decimal> rates = [];

        foreach ((_, BuildingEffectInfo effect) in Effects(catalog, levels))
        {
            decimal? multiplier = effect.EffectType switch
            {
                BuildingEffectTypes.MetalProduction => bonuses.MetalProductionMultiplier,
                BuildingEffectTypes.CrystalProduction => bonuses.CrystalProductionMultiplier,
                _ => null,
            };

            if (multiplier is not { } factor
                || effect.ResourceTypeId is not { } resourceTypeId
                || !deposits.TryGetValue(resourceTypeId, out decimal coefficient))
            {
                continue;
            }

            rates[resourceTypeId] = rates.GetValueOrDefault(resourceTypeId) + (effect.Value * coefficient * factor);
        }

        return rates;
    }

    /// <summary>
    /// Gets the storage capacity of every resource group, with the bonus of the technologies. The
    /// capacity of a group is the limit of every resource of the group, not a sum over them.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="levels">The levels by building type.</param>
    /// <param name="bonuses">The bonuses of the technologies.</param>
    /// <returns>The capacities by group.</returns>
    public static Dictionary<int, long> Capacities(
        BuildingCatalog catalog,
        IReadOnlyDictionary<int, int> levels,
        PlanetBonuses bonuses)
    {
        ArgumentNullException.ThrowIfNull(bonuses);

        Dictionary<int, long> capacities = [];

        foreach ((_, BuildingEffectInfo effect) in Effects(catalog, levels))
        {
            if (effect.EffectType == BuildingEffectTypes.StorageCapacity && effect.ResourceGroupId is { } groupId)
            {
                capacities[groupId] = capacities.GetValueOrDefault(groupId) + (long)decimal.Floor(effect.Value * bonuses.StorageCapacityMultiplier);
            }
        }

        return capacities;
    }

    /// <summary>
    /// Gets the energy produced and consumed by the buildings, with the bonus of the
    /// technologies on the produced energy.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="levels">The levels by building type.</param>
    /// <param name="bonuses">The bonuses of the technologies.</param>
    /// <returns>The output, the consumption and the balance.</returns>
    public static EnergyBalance Energy(
        BuildingCatalog catalog,
        IReadOnlyDictionary<int, int> levels,
        PlanetBonuses bonuses)
    {
        ArgumentNullException.ThrowIfNull(bonuses);

        long output = 0;
        long consumption = 0;

        foreach ((_, BuildingEffectInfo effect) in Effects(catalog, levels))
        {
            if (effect.EffectType == BuildingEffectTypes.EnergyOutput)
            {
                output += (long)decimal.Floor(effect.Value * bonuses.EnergyOutputMultiplier);
            }
            else if (effect.EffectType == BuildingEffectTypes.EnergyConsumption)
            {
                consumption += (long)decimal.Floor(effect.Value);
            }
        }

        return new EnergyBalance(output, consumption);
    }

    /// <summary>
    /// Gets the largest population of the body with the bonus of the technologies; the
    /// population task uses it.
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="levels">The levels by building type.</param>
    /// <param name="bonuses">The bonuses of the technologies.</param>
    /// <returns>The largest population.</returns>
    public static long PopulationMax(
        BuildingCatalog catalog,
        IReadOnlyDictionary<int, int> levels,
        PlanetBonuses bonuses)
    {
        ArgumentNullException.ThrowIfNull(bonuses);

        return Effects(catalog, levels)
            .Where(entry => entry.Effect.EffectType == BuildingEffectTypes.PopulationMax)
            .Sum(entry => (long)decimal.Floor(entry.Effect.Value * bonuses.PopulationMaxMultiplier));
    }

    /// <summary>
    /// Gets the largest level a building of a type may have on the body: the limit of the
    /// buildings with a level cap effect. A building with such an effect is not limited by
    /// them (decision 2).
    /// </summary>
    /// <param name="catalog">The building catalog.</param>
    /// <param name="levels">The levels by building type.</param>
    /// <param name="buildingType">The building type.</param>
    /// <returns>The limit, or <see langword="null"/> when the type has none.</returns>
    public static long? LevelCap(BuildingCatalog catalog, IReadOnlyDictionary<int, int> levels, BuildingTypeInfo buildingType)
    {
        ArgumentNullException.ThrowIfNull(buildingType);

        if (buildingType.CapsLevels)
        {
            return null;
        }

        return Effects(catalog, levels)
            .Where(entry => entry.Effect.EffectType == BuildingEffectTypes.LevelCap && entry.BuildingTypeId != buildingType.Id)
            .Select(entry => (long)decimal.Floor(entry.Effect.Value))
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// Brings the amounts of a body from one moment to another at constant rates (decision 8).
    /// </summary>
    /// <param name="catalog">The building catalog, for the groups of the resources.</param>
    /// <param name="amounts">The amounts at <paramref name="from"/>.</param>
    /// <param name="rates">The production per hour by resource.</param>
    /// <param name="capacities">The capacities by resource group; every resource of the group has this capacity of its own.</param>
    /// <param name="from">The moment of the amounts.</param>
    /// <param name="to">The moment to bring them to.</param>
    /// <returns>The amounts at <paramref name="to"/> and the moment every resource is full.</returns>
    /// <remarks>
    /// Every resource has its own limit, the capacity its group gets from the storage (decision of
    /// the author, 31.10.2026): a resource fills at its own rate and stops alone, the other
    /// resources of the group go on. The amounts are whole numbers: the fraction of a unit is
    /// left out, and the resource that fills gets exactly its capacity. An amount over the
    /// capacity (a refund) stays and counts as full. A group with no capacity at all has no
    /// limit: its resources never fill (decision 7.9 of task 00020).
    /// </remarks>
    public static AccumulationResult Accumulate(
        BuildingCatalog catalog,
        IReadOnlyDictionary<int, long> amounts,
        IReadOnlyDictionary<int, decimal> rates,
        IReadOnlyDictionary<int, long> capacities,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(amounts);
        ArgumentNullException.ThrowIfNull(rates);
        ArgumentNullException.ThrowIfNull(capacities);

        Dictionary<int, long> result = new(amounts);
        Dictionary<int, DateTimeOffset?> fullAt = [];
        decimal elapsedTicks = to > from ? (to - from).Ticks : 0;

        foreach (int resourceTypeId in amounts.Keys.Union(rates.Keys).Order())
        {
            if (catalog.GroupOf(resourceTypeId) is not { } groupId)
            {
                continue;
            }

            long stored = amounts.GetValueOrDefault(resourceTypeId);
            long capacity = capacities.GetValueOrDefault(groupId);
            decimal rate = rates.GetValueOrDefault(resourceTypeId);

            // The one rule of a zero capacity (task 00020, section 7.9): a group without a storage
            // has no limit. Its resources are never full and never stop the production; the
            // limit begins with the first storage that gives the group a capacity.
            if (capacity <= 0)
            {
                fullAt[resourceTypeId] = null;

                if (rate > 0)
                {
                    result[resourceTypeId] = stored + (long)decimal.Floor(rate * elapsedTicks / TimeSpan.TicksPerHour);
                }

                continue;
            }

            if (stored >= capacity)
            {
                fullAt[resourceTypeId] = stored > 0 || rate > 0 ? from : null;

                continue;
            }

            if (rate <= 0)
            {
                fullAt[resourceTypeId] = null;

                continue;
            }

            decimal ticksToFull = (capacity - stored) * (decimal)TimeSpan.TicksPerHour / rate;
            fullAt[resourceTypeId] = AddTicks(from, ticksToFull);

            // The resource that fills gets exactly its capacity, so the fraction left out never
            // leaves it short of the limit.
            result[resourceTypeId] = elapsedTicks >= ticksToFull
                ? capacity
                : stored + (long)decimal.Floor(rate * elapsedTicks / TimeSpan.TicksPerHour);
        }

        return new AccumulationResult(result, fullAt);
    }

    private static DateTimeOffset? AddTicks(DateTimeOffset from, decimal ticks)
    {
        decimal whole = decimal.Ceiling(ticks);

        return whole >= (DateTimeOffset.MaxValue - from).Ticks ? null : from.AddTicks((long)whole);
    }
}

/// <summary>The energy of a body (decision 7).</summary>
/// <param name="Output">The energy produced.</param>
/// <param name="Consumption">The energy consumed.</param>
public sealed record EnergyBalance(long Output, long Consumption)
{
    /// <summary>Gets the balance: the output minus the consumption.</summary>
    public long Balance => Output - Consumption;
}

/// <summary>The amounts of a body brought to a moment.</summary>
/// <param name="Amounts">The amounts by resource.</param>
/// <param name="FullAt">The moment every resource is or will be full by resource, or <see langword="null"/> when it does not fill.</param>
public sealed record AccumulationResult(IReadOnlyDictionary<int, long> Amounts, IReadOnlyDictionary<int, DateTimeOffset?> FullAt);

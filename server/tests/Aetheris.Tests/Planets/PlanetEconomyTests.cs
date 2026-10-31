using Aetheris.Planets.Services;
using Aetheris.World.Api;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the formulas of a body without a database (task 00011, criterion 6).
/// </summary>
public sealed class PlanetEconomyTests
{
    private const int Metals = 1;
    private const int Crystals = 2;
    private const int Produced = 3;
    private const int Iron = 10;
    private const int Copper = 11;
    private const int Quartzite = 20;
    private const int Beams = 30;

    private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The production of a mine is the value of its level times the richness of the deposit
    /// times the bonus of the kind of the resource; a resource without a deposit is not
    /// produced.
    /// </summary>
    [Fact]
    public void The_rate_multiplies_the_level_value_by_the_deposit_and_the_bonus()
    {
        BuildingCatalog catalog = Catalog(new BuildingEffectInfo(BuildingEffectTypes.MetalProduction, Iron, null, 60));
        Dictionary<int, int> levels = new() { [1] = 1 };

        Dictionary<int, decimal> rates = PlanetEconomy.ProductionRates(catalog, levels, new Dictionary<int, decimal> { [Iron] = 1.5m }, Bonuses(metalProduction: 2m));
        Dictionary<int, decimal> withoutDeposit = PlanetEconomy.ProductionRates(catalog, levels, new Dictionary<int, decimal> { [Copper] = 1.5m }, Bonuses(metalProduction: 1m));

        Assert.Equal(180m, rates[Iron]);
        Assert.Empty(withoutDeposit);
    }

    /// <summary>
    /// The metals and the crystals have their own bonus, so a technology changes only the
    /// production of the kind of the resource it belongs to.
    /// </summary>
    [Fact]
    public void The_metal_and_the_crystal_bonuses_change_only_their_own_production()
    {
        BuildingCatalog catalog = Catalog(
            new BuildingEffectInfo(BuildingEffectTypes.MetalProduction, Iron, null, 60),
            new BuildingEffectInfo(BuildingEffectTypes.CrystalProduction, Quartzite, null, 40));
        Dictionary<int, int> levels = new() { [1] = 1 };
        Dictionary<int, decimal> deposits = new() { [Iron] = 1m, [Quartzite] = 1m };

        Dictionary<int, decimal> metalOnly = PlanetEconomy.ProductionRates(catalog, levels, deposits, Bonuses(metalProduction: 2m));
        Dictionary<int, decimal> crystalOnly = PlanetEconomy.ProductionRates(catalog, levels, deposits, Bonuses(crystalProduction: 3m));

        Assert.Equal(120m, metalOnly[Iron]);
        Assert.Equal(40m, metalOnly[Quartzite]);
        Assert.Equal(60m, crystalOnly[Iron]);
        Assert.Equal(120m, crystalOnly[Quartzite]);
    }

    /// <summary>
    /// The capacity of the storages, the energy the buildings produce and the largest
    /// population of the body take their own bonus.
    /// </summary>
    [Fact]
    public void The_capacity_the_energy_and_the_population_take_their_own_bonus()
    {
        BuildingCatalog catalog = Catalog(
            new BuildingEffectInfo(BuildingEffectTypes.StorageCapacity, null, Metals, 1000),
            new BuildingEffectInfo(BuildingEffectTypes.EnergyOutput, null, null, 50),
            new BuildingEffectInfo(BuildingEffectTypes.PopulationMax, null, null, 200));
        Dictionary<int, int> levels = new() { [1] = 1 };

        Dictionary<int, long> capacities = PlanetEconomy.Capacities(catalog, levels, Bonuses(storageCapacity: 1.5m));
        EnergyBalance energy = PlanetEconomy.Energy(catalog, levels, Bonuses(energyOutput: 2m));
        long population = PlanetEconomy.PopulationMax(catalog, levels, Bonuses(populationMax: 1.25m));

        Assert.Equal(1500, capacities[Metals]);
        Assert.Equal(100, energy.Output);
        Assert.Equal(250, population);

        // Without the technologies nothing changes.
        Assert.Equal(1000, PlanetEconomy.Capacities(catalog, levels, PlanetBonuses.None)[Metals]);
        Assert.Equal(50, PlanetEconomy.Energy(catalog, levels, PlanetBonuses.None).Output);
        Assert.Equal(200, PlanetEconomy.PopulationMax(catalog, levels, PlanetBonuses.None));
    }

    /// <summary>
    /// The amount grows as time times rate, whole units only.
    /// </summary>
    [Fact]
    public void The_amount_grows_as_time_times_rate()
    {
        BuildingCatalog catalog = Catalog();
        Dictionary<int, decimal> rates = new() { [Iron] = 90m };

        AccumulationResult result = PlanetEconomy.Accumulate(
            catalog,
            new Dictionary<int, long> { [Iron] = 100 },
            rates,
            new Dictionary<int, long> { [Metals] = 1_000_000 },
            Start,
            Start.AddMinutes(150));

        Assert.Equal(100 + 225, result.Amounts[Iron]);
        Assert.Equal(Start.AddHours((1_000_000 - 100) / 90d).Ticks, result.FullAt[Iron]!.Value.Ticks, TimeSpan.FromSeconds(1).Ticks);
    }

    /// <summary>
    /// A resource that fills holds exactly its own capacity and stops alone: the other resources
    /// of its group keep growing up to the same capacity, and another group goes on.
    /// </summary>
    [Fact]
    public void A_full_resource_stops_only_itself_and_holds_its_capacity()
    {
        BuildingCatalog catalog = Catalog();
        Dictionary<int, decimal> rates = new() { [Iron] = 70m, [Copper] = 30m, [Quartzite] = 10m };
        Dictionary<int, long> capacities = new() { [Metals] = 1000, [Crystals] = 10_000 };

        AccumulationResult result = PlanetEconomy.Accumulate(
            catalog,
            new Dictionary<int, long> { [Iron] = 400, [Copper] = 100 },
            rates,
            capacities,
            Start,
            Start.AddHours(24));

        // The iron fills after 600 / 70 hours; the copper of the same group has not filled yet.
        Assert.Equal(1000, result.Amounts[Iron]);
        Assert.Equal(100 + 720, result.Amounts[Copper]);
        Assert.Equal(Start.AddHours(600 / 70d).Ticks, result.FullAt[Iron]!.Value.Ticks, TimeSpan.FromSeconds(1).Ticks);
        Assert.Equal(Start.AddHours(30), result.FullAt[Copper]);
        Assert.Equal(240, result.Amounts[Quartzite]);

        // Later the iron stays and the copper goes on to its own capacity.
        AccumulationResult later = PlanetEconomy.Accumulate(catalog, result.Amounts, rates, capacities, Start.AddHours(24), Start.AddHours(48));

        Assert.Equal(1000, later.Amounts[Iron]);
        Assert.Equal(Start.AddHours(24), later.FullAt[Iron]);
        Assert.Equal(1000, later.Amounts[Copper]);
        Assert.Equal(Start.AddHours(30), later.FullAt[Copper]);
    }

    /// <summary>
    /// The capacity a storage gives is the capacity of every resource of its group: a storage of
    /// five thousand lets five thousand of each resource lie, and the capacities do not add up
    /// over the resources.
    /// </summary>
    [Fact]
    public void Every_resource_of_a_group_has_the_capacity_of_the_storage()
    {
        const long capacity = 5000;
        BuildingCatalog catalog = Catalog(new BuildingEffectInfo(BuildingEffectTypes.StorageCapacity, null, Metals, capacity));
        Dictionary<int, long> capacities = PlanetEconomy.Capacities(catalog, new Dictionary<int, int> { [1] = 1 }, PlanetBonuses.None);

        AccumulationResult result = PlanetEconomy.Accumulate(
            catalog,
            new Dictionary<int, long>(),
            new Dictionary<int, decimal> { [Iron] = 100_000m, [Copper] = 100_000m },
            capacities,
            Start,
            Start.AddHours(24));

        Assert.Equal(capacity, capacities[Metals]);
        Assert.Equal(capacity, result.Amounts[Iron]);
        Assert.Equal(capacity, result.Amounts[Copper]);
    }

    /// <summary>
    /// The moment a resource is full is counted for the resource from its own rate and room, so
    /// two resources of one group have two moments.
    /// </summary>
    [Fact]
    public void The_full_moment_is_counted_for_every_resource()
    {
        AccumulationResult result = PlanetEconomy.Accumulate(
            Catalog(),
            new Dictionary<int, long> { [Iron] = 200, [Copper] = 0 },
            new Dictionary<int, decimal> { [Iron] = 100m, [Copper] = 50m },
            new Dictionary<int, long> { [Metals] = 1000 },
            Start,
            Start);

        Assert.Equal(Start.AddHours(8), result.FullAt[Iron]);
        Assert.Equal(Start.AddHours(20), result.FullAt[Copper]);
    }

    /// <summary>
    /// The fill moment is exact at the edge: a moment before it the resource is not full yet, at
    /// it the resource holds its capacity.
    /// </summary>
    [Fact]
    public void The_edge_of_the_storage_is_exact()
    {
        BuildingCatalog catalog = Catalog();
        Dictionary<int, decimal> rates = new() { [Iron] = 3600m };
        Dictionary<int, long> capacities = new() { [Metals] = 1000 };
        Dictionary<int, long> amounts = new() { [Iron] = 0 };

        AccumulationResult before = PlanetEconomy.Accumulate(catalog, amounts, rates, capacities, Start, Start.AddSeconds(999));
        AccumulationResult edge = PlanetEconomy.Accumulate(catalog, amounts, rates, capacities, Start, Start.AddSeconds(1000));
        AccumulationResult after = PlanetEconomy.Accumulate(catalog, amounts, rates, capacities, Start, Start.AddSeconds(5000));

        Assert.Equal(999, before.Amounts[Iron]);
        Assert.Equal(1000, edge.Amounts[Iron]);
        Assert.Equal(1000, after.Amounts[Iron]);
        Assert.Equal(Start.AddSeconds(1000), before.FullAt[Iron]);
    }

    /// <summary>
    /// The one rule of a zero capacity (task 00020, section 7.9): a produced resource whose group
    /// has no storage is not full and does not stop; it grows by its rate without a limit.
    /// </summary>
    [Fact]
    public void A_produced_resource_without_a_capacity_is_not_full_and_does_not_stop()
    {
        AccumulationResult result = PlanetEconomy.Accumulate(
            Catalog(),
            new Dictionary<int, long> { [Beams] = 40 },
            new Dictionary<int, decimal> { [Beams] = 10m },
            new Dictionary<int, long> { [Metals] = 1000 },
            Start,
            Start.AddHours(5));

        Assert.Equal(90, result.Amounts[Beams]);
        Assert.Null(result.FullAt[Beams]);

        // Nothing stored and nothing made: not full either.
        AccumulationResult idle = PlanetEconomy.Accumulate(
            Catalog(),
            new Dictionary<int, long>(),
            new Dictionary<int, decimal> { [Beams] = 0m },
            new Dictionary<int, long>(),
            Start,
            Start.AddHours(5));

        Assert.Null(idle.FullAt[Beams]);
    }

    /// <summary>
    /// Once the group of the produced resource has a capacity, the resource fills up to it and
    /// is full from that moment.
    /// </summary>
    [Fact]
    public void A_produced_resource_with_a_capacity_fills_up_to_it()
    {
        AccumulationResult result = PlanetEconomy.Accumulate(
            Catalog(),
            new Dictionary<int, long> { [Beams] = 40 },
            new Dictionary<int, decimal> { [Beams] = 10m },
            new Dictionary<int, long> { [Produced] = 100 },
            Start,
            Start.AddHours(24));

        Assert.Equal(100, result.Amounts[Beams]);
        Assert.Equal(Start.AddHours(6), result.FullAt[Beams]);
    }

    /// <summary>
    /// A start balance above the capacity is kept and nothing is added.
    /// </summary>
    [Fact]
    public void A_balance_above_the_capacity_is_kept()
    {
        AccumulationResult result = PlanetEconomy.Accumulate(
            Catalog(),
            new Dictionary<int, long> { [Iron] = 5000 },
            new Dictionary<int, decimal> { [Iron] = 100m },
            new Dictionary<int, long> { [Metals] = 1000 },
            Start,
            Start.AddHours(10));

        Assert.Equal(5000, result.Amounts[Iron]);
        Assert.Equal(Start, result.FullAt[Iron]);
    }

    /// <summary>
    /// The energy is the output minus the consumption; the level cap comes from the buildings
    /// with the cap effect and does not limit them.
    /// </summary>
    [Fact]
    public void Energy_and_the_level_cap_come_from_the_effects()
    {
        BuildingTypeInfo main = Type(1, new BuildingEffectInfo(BuildingEffectTypes.LevelCap, null, null, 3), new BuildingEffectInfo(BuildingEffectTypes.EnergyOutput, null, null, 10));
        BuildingTypeInfo mine = Type(2, new BuildingEffectInfo(BuildingEffectTypes.EnergyConsumption, null, null, 4));
        BuildingCatalog catalog = new([main, mine], [], Resources(), Groups());
        Dictionary<int, int> levels = new() { [1] = 1, [2] = 1 };

        EnergyBalance energy = PlanetEconomy.Energy(catalog, levels, PlanetBonuses.None);

        Assert.Equal(6, energy.Balance);
        Assert.Equal(3, PlanetEconomy.LevelCap(catalog, levels, mine));
        Assert.Null(PlanetEconomy.LevelCap(catalog, levels, main));
        Assert.Equal(0, PlanetEconomy.LevelCap(catalog, new Dictionary<int, int> { [2] = 1 }, mine));
    }

    private static PlanetBonuses Bonuses(
        decimal metalProduction = 1m,
        decimal crystalProduction = 1m,
        decimal constructionTime = 1m,
        decimal storageCapacity = 1m,
        decimal energyOutput = 1m,
        decimal populationMax = 1m) =>
        new(metalProduction, crystalProduction, constructionTime, storageCapacity, energyOutput, populationMax);

    private static BuildingCatalog Catalog(params BuildingEffectInfo[] effects) =>
        new([Type(1, effects)], [], Resources(), Groups());

    private static BuildingTypeInfo Type(int id, params BuildingEffectInfo[] effects) =>
        new(id, "type_" + id, "name", "description", id, BuildingClasses.None, new Dictionary<int, BuildingLevelInfo>
        {
            [1] = new BuildingLevelInfo(1, 60, new Dictionary<int, long>(), effects),
        });

    private static List<ResourceTypeFact> Resources() =>
    [
        new(Iron, "resource_1", "world.resource.resource_1.name", Metals),
        new(Copper, "resource_3", "world.resource.resource_3.name", Metals),
        new(Quartzite, "resource_5", "world.resource.resource_5.name", Crystals),
        new(Beams, "resource_13", "world.resource.resource_13.name", Produced),
    ];

    private static List<ResourceGroupFact> Groups() =>
    [
        new(Metals, "resource_group_1", "world.resource_group.resource_group_1.name"),
        new(Crystals, "resource_group_2", "world.resource_group.resource_group_2.name"),
        new(Produced, "resource_group_6", "world.resource_group.resource_group_6.name"),
    ];
}

using Aetheris.Planets;
using Aetheris.Planets.Data;
using Aetheris.Production.Data;
using Aetheris.Research.Data;
using Aetheris.World.Data;
using Aetheris.World.Generation;
using Aetheris.World.Geometry;

namespace Aetheris.Tests.World;

/// <summary>
/// The balanced economy of the seed (task 00030): the tree can be played to its end, the start
/// buildings, the round numbers, the ceiling of the prices by the warehouse, the minerals of the
/// high levels and the crystals of the asteroid fields. The tests read the seed the way the
/// server does and need no database.
/// </summary>
public sealed class BalancedEconomyTests
{
    private const int FirstMineralBuildingLevel = 10;

    private const int FirstMineralTechnologyLevel = 8;

    private const int EarlyLevels = 4;

    private const int RoundDigits = 2;

    private const int WarehouseFirstCapacity = 5000;

    private const int LargestWarehouseLevel = 20;

    private const double WarehouseGrowth = 1.4;

    private const int FieldSeeds = 400;

    private const decimal CrystalTotalMin = 0.03m;

    private const decimal CrystalTotalMax = 0.07m;

    private const decimal CrystalEachMin = 0.01m;

    private const decimal FieldTotalMin = 0.70m;

    private const decimal FieldTotalMax = 0.95m;

    private const string StorageEffect = "storage_capacity";

    private const string LevelCapEffect = "level_cap";

    private const string ProductionClass = "research_center";

    private const string TechnologyRequirement = "technology_level";

    private const string BuildingRequirement = "building_level";

    private static readonly PlanetsSeedData Planets = PlanetsReferenceSeed.Load();

    private static readonly ResearchSeedData Research = ResearchReferenceSeed.Load();

    private static readonly ReferenceSeedData World = WorldReferenceSeed.Load();

    private static readonly ProductionSeedData Production = ProductionReferenceSeed.Load();

    private static readonly Dictionary<string, string> NameOf = World.Resources.ToDictionary(resource => resource.Code, resource => resource.Name["uk"]);

    /// <summary>
    /// The resources a building pays with the inhabitants of the body (an update of the seed after
    /// task 00030): the people are spent and grow back, so they are no price of the storage and
    /// follow a whole-number curve of their own.
    /// </summary>
    private static readonly HashSet<string> PaidByPopulation = [.. World.Resources.Where(resource => resource.PaidByPopulation).Select(resource => resource.Code)];

    private static readonly Dictionary<string, string> GroupOf = World.Resources.ToDictionary(resource => resource.Code, resource => resource.Group ?? string.Empty);

    /// <summary>
    /// From the start every level of every building and technology can be reached: the play of
    /// the tree with the level cap of the administrative centre, the sciences every level asks
    /// for and the science centre every level of a technology asks for ends with every level
    /// reached, so no circle or dead end remains (item 2 of the task).
    /// </summary>
    [Fact]
    public void Every_level_of_every_building_and_technology_is_reachable_from_the_start()
    {
        string admin = Planets.BuildingTypes.Single(type => type.Effects.Any(effect => effect.Type == LevelCapEffect)).Code;
        string science = Planets.BuildingTypes.Single(type => type.BuildingClass == ProductionClass).Code;
        Dictionary<string, int> buildings = Planets.BuildingTypes.ToDictionary(type => type.Code, type => type.IsStartBuilding ? 1 : 0);
        Dictionary<string, int> technologies = Research.Technologies.ToDictionary(technology => technology.Code, _ => 0);
        bool changed = true;

        while (changed)
        {
            changed = false;

            foreach (SeedBuildingType type in Planets.BuildingTypes)
            {
                int level = buildings[type.Code] + 1;

                if (level > (type.MaxLevel ?? Planets.LevelCount) || (type.Code != admin && level > buildings[admin]))
                {
                    continue;
                }

                if (Satisfied(type.Requirements, level, buildings, technologies))
                {
                    buildings[type.Code] = level;
                    changed = true;
                }
            }

            foreach (SeedTechnology technology in Research.Technologies)
            {
                int level = technologies[technology.Code] + 1;

                if (level > technology.MaxLevel || buildings[science] < technology.ResearchCenterLevel)
                {
                    continue;
                }

                if (Satisfied(technology.Requirements ?? [], level, buildings, technologies))
                {
                    technologies[technology.Code] = level;
                    changed = true;
                }
            }
        }

        Assert.All(Planets.BuildingTypes, type => Assert.Equal(type.MaxLevel ?? Planets.LevelCount, buildings[type.Code]));
        Assert.All(Research.Technologies, technology => Assert.Equal(technology.MaxLevel, technologies[technology.Code]));
    }

    /// <summary>
    /// A new civilization starts with four buildings of the first level: the administrative
    /// centre, the housing, the warehouse and the solar plant (item 4 of the task).
    /// </summary>
    [Fact]
    public void The_start_buildings_are_the_centre_the_housing_the_warehouse_and_the_solar_plant()
    {
        IReadOnlyList<SeedBuildingType> start = [.. Planets.BuildingTypes.Where(type => type.IsStartBuilding)];

        Assert.Equal(4, start.Count);
        Assert.Contains(start, type => type.Effects.Any(effect => effect.Type == LevelCapEffect));
        Assert.Contains(start, type => type.Effects.Any(effect => effect.Type == StorageEffect));
        Assert.Contains(start, type => type.Effects.Any(effect => effect.Type == "energy_output"));
        Assert.Contains(start, type => type.Effects.Any(effect => effect.Type == "population_max") && type.Effects.All(effect => effect.Type != LevelCapEffect));
        Assert.All(start, type => Assert.DoesNotContain(type.Requirements, requirement => requirement.SubjectLevel is null or 1));
    }

    /// <summary>
    /// The warehouse holds 5000 at the first level and grows 1.4 times a level, about three
    /// million at the twentieth (item 5 of the task).
    /// </summary>
    [Fact]
    public void The_warehouse_holds_5000_and_grows_by_1_4()
    {
        SeedEffect storage = Planets.BuildingTypes.SelectMany(type => type.Effects).First(effect => effect.Type == StorageEffect);

        Assert.Equal(WarehouseFirstCapacity, PlanetsReferenceSeed.ValueAt(storage, 1));
        Assert.Equal(WarehouseGrowth, storage.Growth);

        long largest = PlanetsReferenceSeed.ValueAt(storage, LargestWarehouseLevel);

        Assert.InRange(largest, 2_900_000, 3_100_000);
    }

    /// <summary>
    /// No price of a level of a building or a technology is more than a half of the largest
    /// warehouse (item 8 of the task, decision 5.1).
    /// </summary>
    [Fact]
    public void The_largest_price_is_at_most_a_half_of_the_largest_warehouse()
    {
        SeedEffect storage = Planets.BuildingTypes.SelectMany(type => type.Effects).First(effect => effect.Type == StorageEffect);
        long ceiling = PlanetsReferenceSeed.ValueAt(storage, LargestWarehouseLevel) / 2;
        long largest = 0;

        foreach (SeedBuildingType type in Planets.BuildingTypes)
        {
            for (int level = 1; level <= (type.MaxLevel ?? Planets.LevelCount); level++)
            {
                largest = Math.Max(largest, BuildingPrices(type, level).Values.DefaultIfEmpty(0).Max());
            }
        }

        foreach (SeedTechnology technology in Research.Technologies)
        {
            for (int level = 1; level <= technology.MaxLevel; level++)
            {
                largest = Math.Max(largest, TechnologyPrices(technology, level).Values.DefaultIfEmpty(0).Max());
            }
        }

        Assert.InRange(largest, 1, ceiling);
    }

    /// <summary>
    /// The prices, the times and the effects read as round numbers: at most two digits that are
    /// not zero (item 5 of the task).
    /// </summary>
    [Fact]
    public void The_numbers_of_the_seed_are_round()
    {
        List<string> rough = [];

        foreach (SeedBuildingType type in Planets.BuildingTypes)
        {
            for (int level = 1; level <= (type.MaxLevel ?? Planets.LevelCount); level++)
            {
                CheckRound(rough, $"{type.Code} time {level}", PlanetsReferenceSeed.ValueAt(type.DurationSeconds, level));

                foreach ((string resource, long price) in BuildingPrices(type, level))
                {
                    CheckRound(rough, $"{type.Code} {resource} {level}", price);
                }

                foreach (SeedEffect effect in type.Effects.Where(effect => effect.Values is null))
                {
                    CheckRound(rough, $"{type.Code} {effect.Type} {level}", PlanetsReferenceSeed.ValueAt(effect, level));
                }
            }
        }

        foreach (SeedTechnology technology in Research.Technologies)
        {
            for (int level = 1; level <= technology.MaxLevel; level++)
            {
                CheckRound(rough, $"{technology.Code} time {level}", ResearchReferenceSeed.ValueAt(technology.DurationSeconds, level));

                foreach ((string resource, long price) in TechnologyPrices(technology, level))
                {
                    CheckRound(rough, $"{technology.Code} {resource} {level}", price);
                }
            }
        }

        Assert.Empty(rough);
    }

    /// <summary>
    /// The prices and the times never fall with the level (the curves are monotonic).
    /// </summary>
    [Fact]
    public void Prices_and_times_rise_with_the_level()
    {
        foreach (SeedBuildingType type in Planets.BuildingTypes)
        {
            long previousTime = 0;
            Dictionary<string, long> previous = [];

            for (int level = 1; level <= (type.MaxLevel ?? Planets.LevelCount); level++)
            {
                long time = PlanetsReferenceSeed.ValueAt(type.DurationSeconds, level);
                Assert.True(time >= previousTime, $"{type.Code} time falls at level {level}");
                previousTime = time;

                foreach ((string resource, long price) in BuildingPrices(type, level))
                {
                    Assert.True(price >= previous.GetValueOrDefault(resource), $"{type.Code} price of {resource} falls at level {level}");
                    previous[resource] = price;
                }
            }
        }
    }

    /// <summary>
    /// The levels 1 to 4 of a building or a technology ask for ores, building materials and
    /// credits only, so a home planet with any minerals can build every mine in the first days
    /// (item 6 of the task).
    /// </summary>
    [Fact]
    public void The_early_levels_ask_for_ores_materials_and_credits_only()
    {
        HashSet<string> early = [.. World.Resources
            .Where(resource => resource.Group == "resource_group_1" || NameOf[resource.Code] is "Будматеріали" or "Кредити")
            .Select(resource => resource.Code)];

        foreach (SeedBuildingType type in Planets.BuildingTypes)
        {
            for (int level = 1; level <= EarlyLevels; level++)
            {
                Assert.All(BuildingPrices(type, level).Keys, resource => Assert.True(early.Contains(resource), $"{type.Code} level {level} asks for {NameOf[resource]}"));
            }
        }

        foreach (SeedTechnology technology in Research.Technologies)
        {
            for (int level = 1; level <= EarlyLevels; level++)
            {
                Assert.All(TechnologyPrices(technology, level).Keys, resource => Assert.True(early.Contains(resource), $"{technology.Code} level {level} asks for {NameOf[resource]}"));
            }
        }
    }

    /// <summary>
    /// The way out of a mistake (task 00030): a player who spent all the building materials and
    /// all the energy can still build the mines, the plant of the building materials and the
    /// first levels of the solar plant from ores and credits alone, and credits never stop coming.
    /// </summary>
    [Fact]
    public void The_way_out_of_a_mistake_costs_ores_and_credits_only()
    {
        const int WayOutLevels = 3;
        HashSet<string> allowed = [.. World.Resources.Where(resource => resource.Group == "resource_group_1" || NameOf[resource.Code] == "Кредити").Select(resource => resource.Code)];
        string materials = World.Resources.Single(resource => NameOf[resource.Code] == "Будматеріали").Code;
        string materialsFactory = Production.Recipes.Single(recipe => recipe.Resource == materials).Factory!;
        SeedBuildingType plant = Planets.BuildingTypes
            .Where(type => type.Effects.Any(effect => effect.Type == "energy_output"))
            .OrderBy(type => type.Effects.First(effect => effect.Type == "energy_output").Base)
            .First();
        IReadOnlyList<SeedBuildingType> wayOut =
        [
            .. Planets.BuildingTypes.Where(type => type.BuildingClass == "mine"),
            Planets.BuildingTypes.Single(type => type.Code == materialsFactory),
            plant,
        ];

        Assert.All(wayOut, type =>
        {
            for (int level = 1; level <= WayOutLevels; level++)
            {
                Assert.All(BuildingPrices(type, level).Keys, resource => Assert.True(allowed.Contains(resource), $"{type.Code} level {level} asks for {NameOf[resource]}"));
            }
        });
    }

    /// <summary>
    /// The higher levels need minerals: from the tenth level of a building and the eighth of a
    /// technology every level asks for a mineral (item 9 of the task).
    /// </summary>
    [Fact]
    public void The_high_levels_need_minerals()
    {
        foreach (SeedBuildingType type in Planets.BuildingTypes.Where(type => (type.MaxLevel ?? Planets.LevelCount) >= FirstMineralBuildingLevel))
        {
            for (int level = FirstMineralBuildingLevel; level <= (type.MaxLevel ?? Planets.LevelCount); level++)
            {
                Assert.Contains(BuildingPrices(type, level).Keys, resource => GroupOf[resource] == "resource_group_2");
            }
        }

        // A technology priced in a single resource (the matter conversion costs credits only) is
        // outside the rule: the rule is for the technologies that grow a price of several resources.
        foreach (SeedTechnology technology in Research.Technologies.Where(technology => technology.Costs.Count > 1))
        {
            for (int level = FirstMineralTechnologyLevel; level <= technology.MaxLevel; level++)
            {
                Assert.Contains(TechnologyPrices(technology, level).Keys, resource => GroupOf[resource] == "resource_group_2");
            }
        }
    }

    /// <summary>
    /// The demand of the four minerals is level among the buildings and the technologies: the
    /// most asked mineral is asked for by at most a quarter more of them than the least asked one,
    /// and every crystal is asked for by a few, fewer than the minerals (the author, 01.11.2026).
    /// </summary>
    [Fact]
    public void The_demand_of_the_minerals_and_of_the_crystals_is_level()
    {
        Dictionary<string, int> demand = World.Resources.ToDictionary(resource => resource.Code, _ => 0);

        foreach (SeedBuildingType type in Planets.BuildingTypes)
        {
            foreach (string resource in BuildingPrices(type, type.MaxLevel ?? Planets.LevelCount).Keys)
            {
                demand[resource]++;
            }
        }

        foreach (SeedTechnology technology in Research.Technologies)
        {
            foreach (string resource in TechnologyPrices(technology, technology.MaxLevel).Keys)
            {
                demand[resource]++;
            }
        }

        int[] minerals = [.. World.Resources.Where(resource => resource.Group == "resource_group_2").Select(resource => demand[resource.Code])];
        int[] crystals = [.. World.Resources.Where(resource => resource.Group == "resource_group_4").Select(resource => demand[resource.Code])];

        Assert.True(minerals.Max() <= minerals.Min() * 1.25, $"minerals {string.Join('/', minerals)}");
        Assert.True(crystals.Min() >= 5, $"crystals {string.Join('/', crystals)}");
        Assert.True(crystals.Max() <= crystals.Min() * 1.5, $"crystals {string.Join('/', crystals)}");
        Assert.True(crystals.Max() < minerals.Min(), "a crystal is asked for as often as a mineral");
    }

    /// <summary>
    /// The quantities of the four minerals are level: the units to take every building to the top,
    /// the units to take every technology to the top and the units in one of every module differ
    /// by a few per cent from mineral to mineral, each of the three by itself (the author,
    /// 01.11.2026: not one mineral in the buildings and another in the technologies).
    /// </summary>
    [Fact]
    public void The_quantities_of_the_minerals_are_level_among_buildings_technologies_and_modules()
    {
        string[] minerals = [.. World.Resources.Where(resource => resource.Group == "resource_group_2").Select(resource => resource.Code)];
        Dictionary<string, long> buildings = minerals.ToDictionary(code => code, _ => 0L);
        Dictionary<string, long> technologies = minerals.ToDictionary(code => code, _ => 0L);
        Dictionary<string, long> modules = minerals.ToDictionary(code => code, _ => 0L);

        foreach (SeedBuildingType type in Planets.BuildingTypes)
        {
            for (int level = 1; level <= (type.MaxLevel ?? Planets.LevelCount); level++)
            {
                foreach ((string resource, long price) in BuildingPrices(type, level).Where(entry => buildings.ContainsKey(entry.Key)))
                {
                    buildings[resource] += price;
                }
            }
        }

        foreach (SeedTechnology technology in Research.Technologies)
        {
            for (int level = 1; level <= technology.MaxLevel; level++)
            {
                foreach ((string resource, long price) in TechnologyPrices(technology, level).Where(entry => technologies.ContainsKey(entry.Key)))
                {
                    technologies[resource] += price;
                }
            }
        }

        HashSet<string> moduleCodes = [.. Production.Parts.Where(part => part.Code.StartsWith("part_m_", StringComparison.Ordinal)).Select(part => part.Code)];

        foreach (SeedIngredient ingredient in Production.Recipes.Where(recipe => recipe.Part is not null && moduleCodes.Contains(recipe.Part)).SelectMany(recipe => recipe.Ingredients))
        {
            if (ingredient.Resource is not null && modules.ContainsKey(ingredient.Resource))
            {
                modules[ingredient.Resource] += ingredient.Amount;
            }
        }

        const double SpreadBuildings = 1.10;
        const double SpreadTechnologies = 1.20;
        const double SpreadModules = 1.10;

        Assert.True(buildings.Values.Max() <= buildings.Values.Min() * SpreadBuildings, $"buildings {string.Join('/', buildings.Values)}");
        Assert.True(technologies.Values.Max() <= technologies.Values.Min() * SpreadTechnologies, $"technologies {string.Join('/', technologies.Values)}");
        Assert.True(modules.Values.Max() <= modules.Values.Min() * SpreadModules, $"modules {string.Join('/', modules.Values)}");
    }

    /// <summary>
    /// Energy, population, helium-3, dark matter and ether are not paid by a building or a
    /// technology: the first two are balances, the last three are for the modules and the fuel.
    /// </summary>
    [Fact]
    public void Balances_and_cosmic_resources_are_not_in_the_prices_of_buildings_and_technologies()
    {
        HashSet<string> kept = [.. World.Resources.Where(resource => NameOf[resource.Code] is "Енергія" or "Населення" or "Гелій-3" or "Темна матерія" or "Ефір").Select(resource => resource.Code)];
        IEnumerable<string> paid = Planets.BuildingTypes.SelectMany(type => type.Costs.Select(cost => cost.Resource).Where(resource => !PaidByPopulation.Contains(resource)))
            .Concat(Research.Technologies.SelectMany(technology => technology.Costs.Select(cost => cost.Resource)));

        Assert.Empty(paid.Where(kept.Contains).Distinct());
    }

    /// <summary>
    /// Every resource that a player can hold is asked for by something: a building, a technology,
    /// a recipe, or the fuel of a module (item 3 of the task).
    /// </summary>
    [Fact]
    public void Every_stored_resource_has_a_consumer()
    {
        HashSet<string> asked = [.. Planets.BuildingTypes.SelectMany(type => type.Costs.Select(cost => cost.Resource))
            .Concat(Research.Technologies.SelectMany(technology => technology.Costs.Select(cost => cost.Resource)))
            .Concat(Production.Recipes.SelectMany(recipe => recipe.Ingredients.Select(ingredient => ingredient.Resource ?? string.Empty)))];
        string[] balances = ["Енергія", "Населення", "Торгова валюта", "Реальна валюта"];

        Assert.All(
            World.Resources.Where(resource => !balances.Contains(NameOf[resource.Code])),
            resource => Assert.True(asked.Contains(resource.Code), $"{NameOf[resource.Code]} is asked for by nothing"));
    }

    /// <summary>
    /// The cobalt shell stays made of cobalt ore (the author: the cobalt shells are a must).
    /// </summary>
    [Fact]
    public void The_cobalt_shell_is_made_of_cobalt_ore()
    {
        string cobalt = World.Resources.Single(resource => resource.Name["uk"] == "Кобальтова руда").Code;
        SeedPart shell = Production.Parts.Single(part => part.Name["uk"] == "Кобальтовий снаряд");
        SeedRecipe recipe = Production.Recipes.Single(candidate => candidate.Part == shell.Code);

        Assert.Contains(recipe.Ingredients, ingredient => ingredient.Resource == cobalt);
    }

    /// <summary>
    /// Every asteroid field holds the three crystals together, 3 to 7 per cent in all and 1 per
    /// cent at least each, and its resources are 70 to 95 per cent of its mass (items 9 and 10
    /// of the task, decisions of the author of 01.11.2026).
    /// </summary>
    [Fact]
    public void Every_asteroid_field_has_the_three_crystals_and_at_least_seventy_per_cent_of_resources()
    {
        WorldGenerationConfig config = WorldTestData.Config();
        HashSet<int> crystals = WorldTestData.ResourcesOfGroup(config.Reference, "resource_group_4");

        for (int seed = 1; seed <= FieldSeeds; seed++)
        {
            FieldPlan field = SectorGenerator.CreateField(new WorldRandom(seed), config, $"F{seed}", null, new Point3(0, 0, 0));
            IReadOnlyList<ResourceShare> own = [.. field.CompositionIds.Where(share => crystals.Contains(share.ResourceId))];
            decimal total = field.CompositionIds.Sum(share => share.Coefficient);
            decimal crystalTotal = own.Sum(share => share.Coefficient);

            Assert.Equal(crystals.Count, own.Count);
            Assert.All(own, share => Assert.True(share.Coefficient >= CrystalEachMin, $"seed {seed}: a crystal is under 1 per cent"));
            Assert.InRange(crystalTotal, CrystalTotalMin, CrystalTotalMax);
            Assert.InRange(total, FieldTotalMin, FieldTotalMax);
        }
    }

    private static bool Satisfied(
        IReadOnlyList<Aetheris.Planets.SeedRequirement> requirements,
        int level,
        IReadOnlyDictionary<string, int> buildings,
        IReadOnlyDictionary<string, int> technologies)
    {
        foreach (Aetheris.Planets.SeedRequirement requirement in requirements.Where(requirement => (requirement.SubjectLevel ?? 1) <= level))
        {
            if (requirement.Type == TechnologyRequirement && technologies[requirement.Target] < (requirement.TargetLevel ?? 0))
            {
                return false;
            }

            if (requirement.Type == BuildingRequirement && buildings[requirement.Target] < (requirement.TargetLevel ?? 0))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, long> BuildingPrices(SeedBuildingType type, int level) =>
        type.Costs
            .Where(cost => level >= cost.FromLevel && !PaidByPopulation.Contains(cost.Resource))
            .ToDictionary(cost => cost.Resource, cost => PlanetsReferenceSeed.ValueAt(cost, level - cost.FromLevel + 1));

    private static Dictionary<string, long> TechnologyPrices(SeedTechnology technology, int level) =>
        technology.Costs
            .Where(cost => level >= cost.FromLevel)
            .ToDictionary(cost => cost.Resource, cost => ResearchReferenceSeed.ValueAt(cost.Growth, level - cost.FromLevel + 1));

    private static void CheckRound(List<string> rough, string what, long value)
    {
        string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');

        if (digits.Length > RoundDigits)
        {
            rough.Add($"{what} = {value}");
        }
    }
}

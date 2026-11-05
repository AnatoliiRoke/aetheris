using Aetheris.Planets;
using Aetheris.Planets.Data;
using Aetheris.Processes.Data;
using Aetheris.Production.Data;

namespace Aetheris.Tests.Production;

/// <summary>
/// The seed written from the balance files of the author (task 00045): a hull asks for a level
/// of the shipyard, a module for a level of its own factory, no recipe asks for a science, the
/// factories speed up with their level, the bunker protects a share of the storage and the
/// converter converts only the ores and the minerals. The tests read the seed the way the server
/// does and need no database.
/// </summary>
public sealed class BalanceSeedTests
{
    private const string BuildingRequirement = "building_level";

    private const string ProductionClass = "production";

    private const string ProductionSpeedEffect = "production_speed_bonus";

    private const string ProtectedShareEffect = "protected_storage_share";

    private const string BunkerClass = "bunker";

    private const string MatterConversion = "matter_conversion";

    private const decimal SpeedPerLevel = 0.025m;

    private const decimal BunkerFirstShare = 0.5m;

    private const decimal BunkerLastShare = 0.75m;

    private const string SmallTransport = "part_h_21";

    private const string LightLaser = "part_m_13";

    private const string EnergyWeaponFactory = "building_42";

    private const int LightLaserFactoryLevel = 2;

    private static readonly ProductionSeedData Production = ProductionReferenceSeed.Load();

    private static readonly PlanetsSeedData Planets = PlanetsReferenceSeed.Load();

    private static readonly ProcessSeedData Processes = ProcessReferenceSeed.Load();

    /// <summary>
    /// A recipe of a hull or a module asks for nothing but a level of its own factory: the
    /// shipyard for a hull, the factory of the module for a module (task 00045, criterion 2).
    /// </summary>
    [Fact]
    public void A_part_asks_only_for_a_level_of_its_own_factory()
    {
        List<SeedRecipe> partRecipes = [.. Production.Recipes.Where(recipe => recipe.Part is not null)];

        Assert.NotEmpty(partRecipes);
        Assert.All(partRecipes, recipe => Assert.All(
            recipe.Requirements,
            requirement =>
            {
                Assert.Equal(BuildingRequirement, requirement.Type);
                Assert.Equal(recipe.Factory, requirement.Target);
                Assert.True(requirement.TargetLevel > 1, recipe.Code);
            }));
    }

    /// <summary>
    /// The small transport opens with the first level of the shipyard, and the light laser with
    /// the second level of the energy weapon factory, as the ladder of the author says.
    /// </summary>
    [Fact]
    public void The_ladder_of_the_hulls_and_the_modules_reaches_the_seed()
    {
        SeedRecipe transport = Production.Recipes.Single(recipe => recipe.Part == SmallTransport);
        SeedRecipe laser = Production.Recipes.Single(recipe => recipe.Part == LightLaser);

        Assert.Empty(transport.Requirements);
        Aetheris.Production.Data.SeedRequirement requirement = Assert.Single(laser.Requirements);
        Assert.Equal(EnergyWeaponFactory, requirement.Target);
        Assert.Equal(LightLaserFactoryLevel, requirement.TargetLevel);
    }

    /// <summary>
    /// Every factory of the production class speeds the production up by 2.5 per cent a level:
    /// nothing on the first level, 47.5 per cent on the twentieth (task 00045, criterion 3).
    /// </summary>
    [Fact]
    public void Every_factory_speeds_up_with_its_level()
    {
        List<SeedBuildingType> factories = [.. Planets.BuildingTypes.Where(type => type.BuildingClass == ProductionClass)];

        Assert.NotEmpty(factories);
        Assert.All(factories, factory =>
        {
            SeedEffect effect = Assert.Single(factory.Effects, entry => entry.Type == ProductionSpeedEffect);
            int maxLevel = factory.MaxLevel ?? Planets.LevelCount;

            Assert.NotNull(effect.Values);
            Assert.Equal(maxLevel, effect.Values.Count);
            Assert.All(Enumerable.Range(1, maxLevel), level => Assert.Equal(SpeedPerLevel * (level - 1), effect.Values[level - 1]));
        });
    }

    /// <summary>
    /// The bunker protects a share of the storage that grows from a half on the first level to
    /// three quarters on the last one (decision of the author of 05.11.2026).
    /// </summary>
    [Fact]
    public void The_bunker_protects_a_half_to_three_quarters_of_the_storage()
    {
        SeedBuildingType bunker = Planets.BuildingTypes.Single(type => type.BuildingClass == BunkerClass);
        SeedEffect effect = Assert.Single(bunker.Effects, entry => entry.Type == ProtectedShareEffect);

        Assert.NotNull(effect.Values);
        Assert.Equal(BunkerFirstShare, effect.Values[0]);
        Assert.Equal(BunkerLastShare, effect.Values[^1]);
        Assert.Equal(effect.Values.Order(), effect.Values);
    }

    /// <summary>
    /// The converter turns only the raw materials mined on a body into each other: the ores and
    /// the minerals; the fuel has no recipe (decision of the author of 05.11.2026).
    /// </summary>
    [Fact]
    public void The_converter_converts_only_the_ores_and_the_minerals()
    {
        List<SeedProcessRecipe> conversions = [.. Processes.Recipes.Where(recipe => recipe.Type == MatterConversion)];

        Assert.Equal(["resource_group_1", "resource_group_2"], conversions.Select(recipe => recipe.SourceGroup).Order());
        Assert.All(conversions, recipe => Assert.Equal(recipe.SourceGroup, recipe.TargetGroup));
    }
}

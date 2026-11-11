using Aetheris.Fleet.Services;
using Aetheris.Production.Api;
using Aetheris.World.Api;
using Aetheris.World.Extraction;
using Aetheris.World.Generation;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the rules of the extraction of a fleet (task 00050, task 00053, task 00055): the yield of a
/// module is an amount of an hour, a cycle takes its part of it, several modules multiply it, what a
/// module takes and where comes only from the rows of the module, and the place loses the whole
/// processed mass.
/// </summary>
public sealed class FleetExtractionRulesTests
{
    private const int Cycle = 900;
    private const int Hour = 3600;

    // The identifiers of the resources and of the group of the resources of these tests; the fleet knows no
    // code of a resource, only the identifiers the rows of the modules name.
    private const int Deuterium = 12;
    private const int Helium = 13;
    private const int Matter = 14;
    private const int Ether = 15;
    private const int CrystalGroup = 4;
    private static readonly int[] Crystals = [20, 21, 22];

    private static readonly IReadOnlyDictionary<int, IReadOnlyList<int>> NoGroups = new Dictionary<int, IReadOnlyList<int>>();
    private static readonly IReadOnlyDictionary<int, IReadOnlyList<int>> CrystalGroups = new Dictionary<int, IReadOnlyList<int>>
    {
        [CrystalGroup] = Crystals,
    };

    private static readonly ShipModuleExtractionDto[] DrillRows =
        [new(ExtractionPlaceKinds.AsteroidField, ExtractionModes.AllShares, null, null, true)];

    private static readonly ShipModuleExtractionDto[] CrystalRows =
        [new(ExtractionPlaceKinds.AsteroidField, ExtractionModes.GroupEven, null, CrystalGroup, false)];

    private static readonly ShipModuleExtractionDto[] HeliumRows =
        [new(ExtractionPlaceKinds.GasGiant, ExtractionModes.Listed, Helium, null, false)];

    private static readonly ShipModuleExtractionDto[] EtherRows =
    [
        new(ExtractionPlaceKinds.Nebula, ExtractionModes.Listed, Ether, null, false),
        new(ExtractionPlaceKinds.Nebula, ExtractionModes.Listed, Deuterium, null, false),
    ];

    private static readonly ShipModuleExtractionDto[] MatterRows =
    [
        new(ExtractionPlaceKinds.Nebula, ExtractionModes.Listed, Matter, null, false),
        new(ExtractionPlaceKinds.Nebula, ExtractionModes.Listed, Deuterium, null, false),
    ];

    private static FleetExtractionService.ExtractionModules Module(int partTypeId, ShipModuleExtractionDto[] rows, decimal yieldPerHour, int count) =>
        new(partTypeId, yieldPerHour, count, rows);

    private static long AmountOf(IReadOnlyList<ResourceAmount> yields, int resourceId) =>
        yields.Where(amount => amount.ResourceId == resourceId).Sum(amount => amount.Amount);

    /// <summary>
    /// The yield of a module is the amount of an hour: a cycle of a quarter of an hour takes a fourth
    /// of it, and four cycles give the whole hour.
    /// </summary>
    [Fact]
    public void A_cycle_takes_its_part_of_the_yield_of_an_hour()
    {
        Assert.Equal(250L, FleetExtractionService.YieldOfPeriod(1000m, 1, Cycle));
        Assert.Equal(1000L, FleetExtractionService.YieldOfPeriod(1000m, 1, Hour));
        Assert.Equal(1000L, 4 * FleetExtractionService.YieldOfPeriod(1000m, 1, Cycle));
    }

    /// <summary>
    /// A cycle never takes more than its part of the hour, and a short period, a module without a
    /// yield and a fleet without modules take nothing.
    /// </summary>
    [Fact]
    public void A_short_period_a_module_without_a_yield_and_no_modules_take_nothing_or_their_part()
    {
        Assert.Equal(500L, FleetExtractionService.YieldOfPeriod(1000m, 1, Hour / 2));
        Assert.Equal(233L, FleetExtractionService.YieldOfPeriod(1400m, 1, 600));
        Assert.Equal(0L, FleetExtractionService.YieldOfPeriod(1000m, 1, 0));
        Assert.Equal(0L, FleetExtractionService.YieldOfPeriod(0m, 1, Cycle));
        Assert.Equal(0L, FleetExtractionService.CycleMass([], ExtractionPlaceKinds.AsteroidField, Cycle));
    }

    /// <summary>
    /// A module takes from the kinds of the places its rows name, and from no other: the drill rig and the
    /// crystal extractor from a field, the helium extractor from a gas giant, the extractors of ether and of
    /// matter from a nebula.
    /// </summary>
    [Fact]
    public void Every_module_takes_from_the_kinds_of_places_its_rows_name()
    {
        FleetExtractionService.ExtractionModules drill = Module(1, DrillRows, 1400m, 1);
        FleetExtractionService.ExtractionModules crystal = Module(2, CrystalRows, 150m, 1);
        FleetExtractionService.ExtractionModules helium = Module(3, HeliumRows, 1400m, 1);
        FleetExtractionService.ExtractionModules ether = Module(4, EtherRows, 1300m, 1);
        FleetExtractionService.ExtractionModules matter = Module(5, MatterRows, 1100m, 1);

        Assert.True(FleetExtractionService.CanExtract([drill], ExtractionPlaceKinds.AsteroidField));
        Assert.True(FleetExtractionService.CanExtract([crystal], ExtractionPlaceKinds.AsteroidField));
        Assert.False(FleetExtractionService.CanExtract([drill], ExtractionPlaceKinds.Nebula));
        Assert.False(FleetExtractionService.CanExtract([drill, crystal, ether], ExtractionPlaceKinds.GasGiant));
        Assert.True(FleetExtractionService.CanExtract([helium], ExtractionPlaceKinds.GasGiant));
        Assert.False(FleetExtractionService.CanExtract([helium], ExtractionPlaceKinds.AsteroidField));
        Assert.True(FleetExtractionService.CanExtract([ether], ExtractionPlaceKinds.Nebula));
        Assert.True(FleetExtractionService.CanExtract([matter], ExtractionPlaceKinds.Nebula));
        Assert.False(FleetExtractionService.CanExtract([ether], ExtractionPlaceKinds.AsteroidField));
        Assert.False(FleetExtractionService.CanExtract([], ExtractionPlaceKinds.AsteroidField));

        Assert.Equal(
            [ExtractionPlaceKinds.AsteroidField, ExtractionPlaceKinds.GasGiant, ExtractionPlaceKinds.Nebula],
            FleetExtractionService.PlaceKinds([drill, crystal, helium, ether, matter]).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The crystal extractor takes the resources of the group its row names that the field holds, and the
    /// value of the module is split evenly between them.
    /// </summary>
    [Fact]
    public void The_yield_of_a_group_row_is_split_evenly_between_the_resources_of_the_group_the_place_holds()
    {
        CompositionDto[] shares =
        [
            new(1, 7, null, 20, 0.1m), new(2, 7, null, 21, 0.2m), new(3, 7, null, 22, 0.05m), new(4, 7, null, 99, 0.4m),
        ];
        FleetExtractionService.ExtractionModules extractor = Module(2, CrystalRows, 150m, 1);

        IReadOnlyList<ResourceAmount> one = FleetExtractionService.RuleYields([extractor], ExtractionPlaceKinds.AsteroidField, shares, CrystalGroups, Hour);

        Assert.Equal([(20, 50L), (21, 50L), (22, 50L)], one.Select(amount => (amount.ResourceId, amount.Amount)));

        // Two modules give twice as much, and it is split the same way.
        IReadOnlyList<ResourceAmount> two = FleetExtractionService.RuleYields(
            [Module(2, CrystalRows, 150m, 2)],
            ExtractionPlaceKinds.AsteroidField,
            shares,
            CrystalGroups,
            Hour);

        Assert.All(two, amount => Assert.Equal(100L, amount.Amount));

        // A field with one resource of the group gives it everything; a field with none gives nothing.
        Assert.Equal(150L, FleetExtractionService.RuleYields([extractor], ExtractionPlaceKinds.AsteroidField, [shares[0]], CrystalGroups, Hour).Single().Amount);
        Assert.Empty(FleetExtractionService.RuleYields([extractor], ExtractionPlaceKinds.AsteroidField, [shares[3]], CrystalGroups, Hour));
        Assert.Empty(FleetExtractionService.RuleYields([extractor], ExtractionPlaceKinds.AsteroidField, shares, NoGroups, Hour));
    }

    /// <summary>
    /// A gas giant has no composition and no mass: the resource of the row goes to the hold as it is, and
    /// several extractors multiply it.
    /// </summary>
    [Fact]
    public void A_gas_giant_gives_the_named_resource_as_it_is()
    {
        FleetExtractionService.ExtractionModules helium = Module(3, HeliumRows, 1400m, 2);

        IReadOnlyList<ResourceAmount> yields = FleetExtractionService.RuleYields([helium], ExtractionPlaceKinds.GasGiant, null, NoGroups, Cycle);

        Assert.Equal([(Helium, 700L)], yields.Select(amount => (amount.ResourceId, amount.Amount)));
        Assert.Empty(FleetExtractionService.RuleYields([Module(1, DrillRows, 1400m, 1)], ExtractionPlaceKinds.GasGiant, null, NoGroups, Cycle));
    }

    /// <summary>
    /// Only a module whose row takes the mass of the place processes a mass of a field; the other modules
    /// do not, and the mass of a module that takes it is the yield of its period.
    /// </summary>
    [Fact]
    public void Only_a_module_whose_row_takes_the_mass_processes_a_mass_of_a_field()
    {
        FleetExtractionService.ExtractionModules ether = Module(4, EtherRows, 1300m, 1);
        FleetExtractionService.ExtractionModules helium = Module(3, HeliumRows, 1400m, 1);
        FleetExtractionService.ExtractionModules crystal = Module(2, CrystalRows, 150m, 1);
        FleetExtractionService.ExtractionModules drill = Module(1, DrillRows, 1400m, 1);

        Assert.Equal(0L, FleetExtractionService.CycleMass([ether], ExtractionPlaceKinds.Nebula, Cycle));
        Assert.Equal(0L, FleetExtractionService.CycleMass([helium], ExtractionPlaceKinds.GasGiant, Cycle));
        Assert.Equal(0L, FleetExtractionService.CycleMass([ether, helium, crystal], ExtractionPlaceKinds.AsteroidField, Cycle));
        Assert.Equal(350L, FleetExtractionService.CycleMass([ether, helium, crystal, drill], ExtractionPlaceKinds.AsteroidField, Cycle));
        Assert.Equal(1050L, FleetExtractionService.CycleMass([Module(1, DrillRows, 1400m, 3)], ExtractionPlaceKinds.AsteroidField, Cycle));

        // The mass of the drilling is not a yield of the rows: the world splits it by the shares.
        Assert.Empty(FleetExtractionService.RuleYields([drill], ExtractionPlaceKinds.AsteroidField, [new(1, 7, null, 20, 0.5m)], NoGroups, Cycle));
    }

    /// <summary>
    /// The extractor of ether takes the ether and the deuterium of a nebula, each as the yield of the period
    /// times its share in the composition; the extractor of matter takes the dark matter and the deuterium;
    /// together they take the deuterium twice, and a module with no row for a nebula takes nothing from it.
    /// </summary>
    [Fact]
    public void Each_extractor_of_a_nebula_takes_the_resources_of_its_rows_and_the_deuterium_adds_up()
    {
        CompositionDto[] shares =
        [
            new(1, null, 9, Ether, 0.4m), new(2, null, 9, Matter, 0.3m), new(3, null, 9, Deuterium, 0.1m), new(4, null, 9, 99, 0.1m),
        ];
        FleetExtractionService.ExtractionModules etherExtractor = Module(4, EtherRows, 1300m, 2);
        FleetExtractionService.ExtractionModules matterExtractor = Module(5, MatterRows, 1100m, 1);
        FleetExtractionService.ExtractionModules drill = Module(1, DrillRows, 1400m, 1);

        // Two extractors of ether take 650 t a cycle: 40 per cent of it is the ether, 10 per cent the
        // deuterium, and nothing else.
        IReadOnlyList<ResourceAmount> onlyEther = FleetExtractionService.RuleYields([etherExtractor], ExtractionPlaceKinds.Nebula, shares, NoGroups, Cycle);

        Assert.Equal([(Ether, 260L), (Deuterium, 65L)], onlyEther.Select(amount => (amount.ResourceId, amount.Amount)));

        // The extractor of matter takes 275 t a cycle: 30 per cent of it is the dark matter, 10 per cent
        // the deuterium (27 t, rounded down).
        IReadOnlyList<ResourceAmount> onlyMatter = FleetExtractionService.RuleYields([matterExtractor], ExtractionPlaceKinds.Nebula, shares, NoGroups, Cycle);

        Assert.Equal([(Matter, 82L), (Deuterium, 27L)], onlyMatter.Select(amount => (amount.ResourceId, amount.Amount)));

        // Both together take the three resources, and the deuterium of the two adds up.
        IReadOnlyList<ResourceAmount> both = FleetExtractionService.RuleYields([etherExtractor, matterExtractor, drill], ExtractionPlaceKinds.Nebula, shares, NoGroups, Cycle);

        Assert.Equal(3, both.Count);
        Assert.Equal(65L + 27L, AmountOf(both, Deuterium));
        Assert.Empty(FleetExtractionService.RuleYields([drill], ExtractionPlaceKinds.Nebula, shares, NoGroups, Cycle));
    }

    /// <summary>
    /// One extractor of ether and one of matter with the same yield give the deuterium twice as much as
    /// one of them does: the numbers of the deuterium of the two extractors add up.
    /// </summary>
    [Fact]
    public void Two_extractors_of_the_same_yield_give_the_deuterium_twice()
    {
        CompositionDto[] shares = [new(1, null, 9, Ether, 0.4m), new(2, null, 9, Matter, 0.3m), new(3, null, 9, Deuterium, 0.08m)];
        FleetExtractionService.ExtractionModules etherExtractor = Module(4, EtherRows, 1000m, 1);
        FleetExtractionService.ExtractionModules matterExtractor = Module(5, MatterRows, 1000m, 1);

        long one = AmountOf(FleetExtractionService.RuleYields([etherExtractor], ExtractionPlaceKinds.Nebula, shares, NoGroups, Hour), Deuterium);
        long both = AmountOf(FleetExtractionService.RuleYields([etherExtractor, matterExtractor], ExtractionPlaceKinds.Nebula, shares, NoGroups, Hour), Deuterium);

        Assert.Equal(80L, one);
        Assert.Equal(2 * one, both);
    }

    /// <summary>
    /// A new row of the data changes what a module takes without a change of the code: the same module
    /// with one more row for a nebula takes one more resource, and a row for a new kind of place makes the
    /// module able to take from it.
    /// </summary>
    [Fact]
    public void A_new_row_changes_what_a_module_takes_without_a_change_of_the_code()
    {
        CompositionDto[] shares = [new(1, null, 9, Ether, 0.4m), new(2, null, 9, Helium, 0.2m)];
        FleetExtractionService.ExtractionModules before = Module(4, [EtherRows[0]], 1000m, 1);

        Assert.Equal([Ether], FleetExtractionService.RuleYields([before], ExtractionPlaceKinds.Nebula, shares, NoGroups, Hour).Select(amount => amount.ResourceId));
        Assert.False(FleetExtractionService.CanExtract([before], ExtractionPlaceKinds.GasGiant));

        FleetExtractionService.ExtractionModules after = Module(
            4,
            [EtherRows[0], new(ExtractionPlaceKinds.Nebula, ExtractionModes.Listed, Helium, null, false), new(ExtractionPlaceKinds.GasGiant, ExtractionModes.Listed, Helium, null, false)],
            1000m,
            1);

        Assert.Equal([Ether, Helium], FleetExtractionService.RuleYields([after], ExtractionPlaceKinds.Nebula, shares, NoGroups, Hour).Select(amount => amount.ResourceId));
        Assert.True(FleetExtractionService.CanExtract([after], ExtractionPlaceKinds.GasGiant));
        Assert.Equal(1000L, FleetExtractionService.RuleYields([after], ExtractionPlaceKinds.GasGiant, null, NoGroups, Hour).Single().Amount);
    }

    /// <summary>
    /// The example of the author: a field with the shares 0.24 / 0.21 / 0.24 / 0.03 / 0.01 / 0.02 / 0.02
    /// and a processed mass of 1000 give 240 / 210 / 240 / 30 / 10 / 20 / 20, the rest is the waste, and
    /// the field loses the whole 1000, waste included.
    /// </summary>
    [Fact]
    public void The_field_gives_the_example_of_the_author_and_loses_the_whole_processed_mass()
    {
        ResourceShare[] composition =
        [
            new(1, 0.24m), new(2, 0.21m), new(3, 0.24m), new(4, 0.03m), new(5, 0.01m), new(6, 0.02m), new(7, 0.02m),
        ];

        ExtractionResult result = FieldExtraction.Extract(5000, composition, 1000, long.MaxValue);

        Assert.Equal(1000L, result.ProcessedMass);
        Assert.Equal([240L, 210L, 240L, 30L, 10L, 20L, 20L], result.YieldIds.Select(amount => amount.Amount));
        Assert.Equal(230L, result.RockMass);
        Assert.Equal(4000L, result.RemainingMass);
        Assert.False(result.Depleted);

        // The hold got 770 of the 1000: the 230 that is left is the waste, and the field lost all of it.
        Assert.Equal(770L, result.YieldIds.Sum(amount => amount.Amount));
        Assert.Equal(5000L - 1000L, result.RemainingMass);
    }

    /// <summary>
    /// A field with the mass left of one cycle gives the rest and disappears at zero.
    /// </summary>
    [Fact]
    public void A_field_disappears_when_its_mass_reaches_zero()
    {
        ExtractionResult result = FieldExtraction.Extract(300, [new ResourceShare(1, 0.5m)], 1000, long.MaxValue);

        Assert.Equal(300L, result.ProcessedMass);
        Assert.Equal(0L, result.RemainingMass);
        Assert.True(result.Depleted);
    }
}

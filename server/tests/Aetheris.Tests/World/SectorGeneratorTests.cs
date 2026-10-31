using System.Text.Json;
using Aetheris.World.Configuration;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;
using Aetheris.World.Geometry;
using FsCheck;
using FsCheck.Fluent;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the pure sector generator (task 00004, acceptance criteria 1, 2, 3, 4, 11, 12).
/// </summary>
public sealed class SectorGeneratorTests
{
    /// <summary>
    /// The number of seeds of the invariant run (acceptance criterion 2).
    /// </summary>
    private const int InvariantSeeds = 100;

    /// <summary>
    /// The allowed difference between a measured share and the configured one.
    /// </summary>
    private const double ShareTolerance = 0.03;

    /// <summary>
    /// The smallest number of bodies of the distribution run (acceptance criterion 3).
    /// </summary>
    private const int DistributionBodies = 1000;

    private static Config TestConfig => Config.QuickThrowOnFailure.WithMaxTest(InvariantSeeds);

    /// <summary>
    /// The same seed and the same configuration give the same sector; another seed gives another one.
    /// </summary>
    [Fact]
    public void The_same_seed_gives_the_same_sector()
    {
        WorldGenerationConfig config = WorldTestData.Config();

        string first = JsonSerializer.Serialize(SectorGenerator.Generate(42, "S1", config));
        string second = JsonSerializer.Serialize(SectorGenerator.Generate(42, "S1", WorldTestData.Config()));
        string other = JsonSerializer.Serialize(SectorGenerator.Generate(43, "S1", config));

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    /// <summary>
    /// For any seed: every system has a star, the bodies are within the range and on distinct
    /// inner orbits, a body has one kind of crystals (two only rarely) and no ether, gas
    /// giants are not colonizable, the number of systems is within the range, the systems
    /// keep their distance, the sector has enough fields and the map is flat.
    /// </summary>
    [Fact]
    public void The_invariants_hold_for_any_seed()
    {
        WorldGenerationConfig config = WorldTestData.Config();
        WorldSettings settings = config.Settings;
        WorldReferenceData reference = config.Reference;
        HashSet<int> crystals = WorldTestData.ResourcesOfGroup(reference, SeedCodes.MineralGroup);
        int etherId = reference.Resources.Single(resource => resource.Code == SeedCodes.Ether).Id;
        int darkMatterId = reference.Resources.Single(resource => resource.Code == SeedCodes.DarkMatter).Id;
        int heliumId = reference.Resources.Single(resource => resource.Code == SeedCodes.Helium).Id;

        Property property = Prop.ForAll(
            Arb.From(Gen.Choose(0, int.MaxValue)),
            seed =>
            {
                SectorPlan plan = SectorGenerator.Generate(seed, "S1", config);
                List<string> problems = [];

                Check(problems, plan.SystemIds.Count >= settings.SystemsMin && plan.SystemIds.Count <= settings.SystemsMax, "system count");
                Check(problems, plan.AllFields.Count() >= settings.MinFieldsPerSector, "field count");
                Check(problems, plan.NebulaIds.Count == settings.NebulaCount, "nebula count");

                foreach (SystemPlan system in plan.SystemIds)
                {
                    Check(problems, reference.FindType(system.StarTypeId)?.ObjectClass == WorldObjectClasses.Star, "star");
                    Check(problems, system.BodyIds.Count >= settings.BodiesMin && system.BodyIds.Count <= settings.BodiesMax, "body count");
                    Check(problems, system.BodyIds.Select(body => body.Orbit).Distinct().Count() == system.BodyIds.Count, "distinct orbits");
                    Check(problems, system.BodyIds.All(body => body.Orbit >= 1 && body.Orbit < settings.OrbitCount), "inner orbits");
                    Check(problems, system.Field is null || system.Field.Orbit == settings.OrbitCount, "field on the outermost orbit");
                    Check(problems, system.Position.Z == 0d, "flat map");

                    int gasGiants = 0;

                    foreach (BodyPlan body in system.BodyIds)
                    {
                        ReferenceObjectType type = reference.FindType(body.ObjectTypeId)!;
                        int crystalKinds = body.DepositIds.Count(share => crystals.Contains(share.ResourceId));

                        Check(problems, WorldTestData.BodyClasses.Contains(type.ObjectClass), "body class");
                        Check(problems, !body.DepositIds.Any(share => share.ResourceId == etherId || share.ResourceId == darkMatterId), "no ether or dark matter on a body");

                        if (type.ObjectClass == WorldObjectClasses.Planet)
                        {
                            Check(problems, crystalKinds is 1 or 2, "one kind of crystals");
                            Check(problems, !body.DepositIds.Any(share => share.ResourceId == heliumId), "no helium-3 on a planet");
                        }
                        else
                        {
                            gasGiants++;
                            Check(problems, !type.IsColonizable, "gas giant not colonizable");
                        }
                    }

                    Check(problems, gasGiants <= settings.GasGiantMax, "gas giant count");
                }

                for (int first = 0; first < plan.SystemIds.Count; first++)
                {
                    for (int second = first + 1; second < plan.SystemIds.Count; second++)
                    {
                        Check(
                            problems,
                            WorldGeometry.Distance(plan.SystemIds[first].Position, plan.SystemIds[second].Position) >= settings.MinSystemDistance,
                            "system distance");
                    }
                }

                foreach (FieldPlan field in plan.AllFields)
                {
                    Check(problems, field.CompositionIds.Sum(share => share.Coefficient) <= settings.FieldMaxCompositionShare, "rock remains");
                    Check(problems, field.Mass > 0, "field mass");
                }

                return problems.Count == 0;
            });

        property.Check(TestConfig);
    }

    /// <summary>
    /// On more than a thousand bodies the shares of the types and the sizes follow the
    /// weights of the reference data, and two kinds of crystals stay rare.
    /// </summary>
    [Fact]
    public void The_types_and_sizes_follow_the_configured_weights()
    {
        WorldGenerationConfig config = WorldTestData.Config();
        WorldReferenceData reference = config.Reference;
        HashSet<int> crystals = WorldTestData.ResourcesOfGroup(reference, SeedCodes.MineralGroup);

        List<BodyPlan> planets = [];
        List<BodyPlan> bodies = [];

        for (long seed = 1; planets.Count < DistributionBodies; seed++)
        {
            foreach (BodyPlan body in SectorGenerator.Generate(seed, "S1", config).SystemIds.SelectMany(system => system.BodyIds))
            {
                bodies.Add(body);

                if (reference.FindType(body.ObjectTypeId)!.ObjectClass == WorldObjectClasses.Planet)
                {
                    planets.Add(body);
                }
            }
        }

        IReadOnlyList<ReferenceObjectType> planetTypes = reference.TypesOf(WorldObjectClasses.Planet);
        double typeWeight = planetTypes.Sum(type => type.GenerationWeight);

        foreach (ReferenceObjectType type in planetTypes)
        {
            double expected = type.GenerationWeight / typeWeight;
            double actual = planets.Count(body => body.ObjectTypeId == type.Id) / (double)planets.Count;

            Assert.True(Math.Abs(expected - actual) <= ShareTolerance, $"{type.Code}: expected {expected:F3}, got {actual:F3}.");
        }

        double sizeWeight = reference.SizeClasses.Sum(size => size.GenerationWeight);

        foreach (ReferenceSizeClass size in reference.SizeClasses)
        {
            double expected = size.GenerationWeight / sizeWeight;
            double actual = bodies.Count(body => body.SizeClassId == size.Id) / (double)bodies.Count;

            Assert.True(Math.Abs(expected - actual) <= ShareTolerance, $"{size.Code}: expected {expected:F3}, got {actual:F3}.");
            Assert.All(bodies.Where(body => body.SizeClassId == size.Id), body => Assert.InRange(body.Size, size.SizeMin, size.SizeMax));
        }

        double twoCrystals = planets.Count(body => body.DepositIds.Count(share => crystals.Contains(share.ResourceId)) == 2) / (double)planets.Count;
        double configured = reference.RuleGroups.First(group => WorldTestData.GroupOfRules(reference, group) == reference.Groups.Single(entry => entry.Code == SeedCodes.MineralGroup).Id).ExtraPickProbability;

        Assert.True(Math.Abs(twoCrystals - configured) <= ShareTolerance, $"two kinds of crystals: expected {configured:F3}, got {twoCrystals:F3}.");
    }

    /// <summary>
    /// Gas giants are not in every system, and some systems have them.
    /// </summary>
    [Fact]
    public void Gas_giants_appear_in_some_systems_only()
    {
        WorldGenerationConfig config = WorldTestData.Config();
        SectorPlan plan = SectorGenerator.Generate(7, "S1", config);

        int withGasGiants = plan.SystemIds.Count(system => system.BodyIds.Any(body =>
            config.Reference.FindType(body.ObjectTypeId)!.ObjectClass == WorldObjectClasses.GasGiant));

        Assert.InRange(withGasGiants, 1, plan.SystemIds.Count - 1);
    }

    /// <summary>
    /// A changed parameter changes the result, without any change of the code.
    /// </summary>
    [Fact]
    public void A_changed_parameter_changes_the_result()
    {
        SectorPlan plan = SectorGenerator.Generate(5, "S1", WorldTestData.Config(new Dictionary<string, string>
        {
            [WorldConfigKeys.SystemsMin] = "7",
            [WorldConfigKeys.SystemsMax] = "7",
            [WorldConfigKeys.BodiesMin] = "4",
            [WorldConfigKeys.BodiesMax] = "4",
            [WorldConfigKeys.SystemNameTemplate] = "Q{number}",
        }));

        Assert.Equal(7, plan.SystemIds.Count);
        Assert.All(plan.SystemIds, system => Assert.Equal(4, system.BodyIds.Count));
        Assert.Equal("Q1", plan.SystemIds[0].Name);
        Assert.Equal("Q1-" + plan.SystemIds[0].BodyIds[0].Orbit, plan.SystemIds[0].BodyIds[0].Name);
    }

    /// <summary>
    /// The depth of a sector moves the map out of the plane; the generator does not rely on z = 0.
    /// </summary>
    [Fact]
    public void A_sector_with_depth_uses_the_third_axis()
    {
        SectorPlan plan = SectorGenerator.Generate(5, "S1", WorldTestData.Config(new Dictionary<string, string>
        {
            [WorldConfigKeys.SectorDepth] = "500",
        }));

        Assert.Contains(plan.SystemIds, system => system.Position.Z > 0d);
    }

    /// <summary>
    /// An inconsistent configuration is refused before anything is generated.
    /// </summary>
    [Fact]
    public void An_inconsistent_configuration_is_refused()
    {
        WorldConfigurationException exception = Assert.Throws<WorldConfigurationException>(() => SectorGenerator.Generate(
            1,
            "S1",
            WorldTestData.Config(new Dictionary<string, string>
            {
                [WorldConfigKeys.SystemsMin] = "30",
                [WorldConfigKeys.SystemsMax] = "10",
                [WorldConfigKeys.BodiesMax] = "12",
            })));

        Assert.Contains(exception.Errors, error => error.Contains(WorldConfigKeys.SystemsMin, StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains(WorldConfigKeys.BodiesMax, StringComparison.Ordinal));
    }

    /// <summary>
    /// A rule that would put a resource of a forbidden group on a body is refused.
    /// </summary>
    [Fact]
    public void Ether_on_a_body_is_refused_by_the_validation()
    {
        WorldReferenceData seed = WorldTestData.ReferenceFromSeed();
        int forbiddenId = seed.Resources.First(resource => !seed.Groups.Single(group => group.Id == resource.ResourceGroupId).AllowedOnPlanet).Id;
        int terrestrialId = seed.ObjectTypes.Single(type => type.Code == SeedCodes.Terrestrial).Id;

        WorldReferenceData broken = new(
            seed.Groups,
            seed.Resources,
            seed.ObjectTypes,
            seed.SizeClasses,
            [.. seed.RuleGroups, new ReferenceRuleGroup(1000, terrestrialId, "rule_group_1000", 1, 1, 0d, [new ReferenceRule(1000, forbiddenId, 1, 0.1m, 0.2m)])]);

        IReadOnlyList<string> errors = WorldConfigValidator.Validate(new WorldGenerationConfig(WorldTestData.Settings(), broken));

        Assert.Contains(errors, error => error.Contains("forbids", StringComparison.Ordinal));
    }

    /// <summary>
    /// A new resource and a new kind of crystals added as data reach the deposits and the
    /// field compositions without a change of the code (acceptance criterion 11).
    /// </summary>
    [Fact]
    public void A_new_resource_added_as_data_reaches_the_world()
    {
        WorldReferenceData seed = WorldTestData.ReferenceFromSeed();
        int extractable = seed.Groups.Single(group => group.Code == SeedCodes.MetalGroup).Id;
        int crystal = seed.Groups.Single(group => group.Code == SeedCodes.MineralGroup).Id;
        int newMetal = seed.Resources.Max(resource => resource.Id) + 1;
        int newCrystal = newMetal + 1;
        int ruleId = seed.RuleGroups.SelectMany(group => group.RuleIds).Max(rule => rule.Id);

        List<ReferenceRuleGroup> groups =
        [
            .. seed.RuleGroups.Select(group => WorldTestData.GroupOfRules(seed, group) switch
            {
                int groupId when groupId == extractable => group with { RuleIds = [.. group.RuleIds, new ReferenceRule(++ruleId, newMetal, 1_000_000, 0.1m, 0.2m)] },
                int groupId when groupId == crystal => group with { RuleIds = [.. group.RuleIds, new ReferenceRule(++ruleId, newCrystal, 1_000_000, 0.01m, 0.02m)] },
                _ => group,
            }),
        ];

        WorldReferenceData extended = new(
            seed.Groups,
            [.. seed.Resources, new ReferenceResource(newMetal, "test_metal", extractable), new ReferenceResource(newCrystal, "test_crystal", crystal)],
            seed.ObjectTypes,
            seed.SizeClasses,
            groups);

        SectorPlan plan = SectorGenerator.Generate(3, "S1", new WorldGenerationConfig(WorldTestData.Settings(), extended));
        List<ResourceShare> deposits = [.. plan.SystemIds.SelectMany(system => system.BodyIds).SelectMany(body => body.DepositIds)];
        List<ResourceShare> fields = [.. plan.AllFields.SelectMany(field => field.CompositionIds)];

        Assert.Contains(deposits, share => share.ResourceId == newMetal);
        Assert.Contains(deposits, share => share.ResourceId == newCrystal);
        Assert.Contains(fields, share => share.ResourceId == newMetal);
        Assert.Contains(fields, share => share.ResourceId == newCrystal);
    }

    private static void Check(List<string> problems, bool condition, string name)
    {
        if (!condition)
        {
            problems.Add(name);
        }
    }
}

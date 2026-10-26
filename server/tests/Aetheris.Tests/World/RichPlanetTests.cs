using Aetheris.World.Configuration;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the richness multipliers of the deposits of planets (task 00009, decision 12): an
/// ordinary planet keeps every multiplier in the ordinary range of its rule, a rich planet has
/// every multiplier in the rich range, and the probability and the ranges are data.
/// </summary>
public sealed class RichPlanetTests
{
    private const int Seeds = 40;

    /// <summary>
    /// Without rich planets every deposit of a planet lies in 0.8–1.4; with every planet rich,
    /// in 1.4–1.9 (acceptance criterion 9b).
    /// </summary>
    /// <param name="probability">The probability of a rich planet.</param>
    /// <param name="minimum">The smallest expected multiplier.</param>
    /// <param name="maximum">The largest expected multiplier.</param>
    [Theory]
    [InlineData("0", 0.8, 1.4)]
    [InlineData("1", 1.4, 1.9)]
    public void The_multipliers_follow_the_range_of_the_planet(string probability, double minimum, double maximum)
    {
        WorldGenerationConfig config = WorldTestData.Config(Overrides(probability));

        List<decimal> multipliers = [.. PlanetDeposits(config).SelectMany(deposits => deposits)];

        Assert.NotEmpty(multipliers);
        Assert.All(multipliers, multiplier => Assert.InRange(multiplier, (decimal)minimum, (decimal)maximum));
    }

    /// <summary>
    /// A multiplier above the ordinary range appears only on a rich planet, where every deposit
    /// has one; gas giants keep the ranges of their own rules.
    /// </summary>
    [Fact]
    public void A_multiplier_above_the_ordinary_range_belongs_to_a_rich_planet()
    {
        WorldGenerationConfig config = WorldTestData.Config(Overrides("0.5"));
        decimal ordinaryMaximum = OrdinaryMaximum(config.Reference);

        List<IReadOnlyList<decimal>> planets = [.. PlanetDeposits(config).Where(deposits => deposits.Count > 0)];
        List<IReadOnlyList<decimal>> rich = [.. planets.Where(deposits => deposits.Any(multiplier => multiplier > ordinaryMaximum))];

        Assert.NotEmpty(rich);
        Assert.True(rich.Count < planets.Count);
        Assert.All(rich, deposits => Assert.All(deposits, multiplier => Assert.True(multiplier >= ordinaryMaximum)));
    }

    /// <summary>
    /// The ranges are read from the rules: another rich range in the data gives other multipliers.
    /// </summary>
    [Fact]
    public void The_rich_range_comes_from_the_rules()
    {
        WorldGenerationConfig seed = WorldTestData.Config(Overrides("1"));
        WorldReferenceData reference = seed.Reference;
        HashSet<int> planetTypes = [.. reference.TypesOf(WorldObjectClasses.Planet).Select(type => type.Id)];

        WorldReferenceData changed = new(
            reference.Groups,
            reference.Resources,
            reference.ObjectTypes,
            reference.SizeClasses,
            reference.RuleGroups.Select(group => planetTypes.Contains(group.ObjectTypeId)
                ? group with { RuleIds = [.. group.RuleIds.Select(rule => rule with { RichCoefficientMin = 1.5m, RichCoefficientMax = 1.6m })] }
                : group));

        List<decimal> multipliers = [.. PlanetDeposits(seed with { Reference = changed }).SelectMany(deposits => deposits)];

        Assert.NotEmpty(multipliers);
        Assert.All(multipliers, multiplier => Assert.InRange(multiplier, 1.5m, 1.6m));
    }

    private static Dictionary<string, string> Overrides(string probability) =>
        new(WorldTestData.SmallSectors, StringComparer.Ordinal) { [WorldConfigKeys.RichPlanetProbability] = probability };

    private static decimal OrdinaryMaximum(WorldReferenceData reference)
    {
        HashSet<int> planetTypes = [.. reference.TypesOf(WorldObjectClasses.Planet).Select(type => type.Id)];

        return reference.RuleGroups
            .Where(group => planetTypes.Contains(group.ObjectTypeId))
            .SelectMany(group => group.RuleIds)
            .Max(rule => rule.CoefficientMax);
    }

    private static IEnumerable<IReadOnlyList<decimal>> PlanetDeposits(WorldGenerationConfig config)
    {
        HashSet<int> planetTypes = [.. config.Reference.TypesOf(WorldObjectClasses.Planet).Select(type => type.Id)];

        for (int seed = 1; seed <= Seeds; seed++)
        {
            SectorPlan plan = SectorGenerator.Generate(seed, "S" + seed, config);

            foreach (BodyPlan body in plan.SystemIds.SelectMany(system => system.BodyIds).Where(body => planetTypes.Contains(body.ObjectTypeId)))
            {
                yield return [.. body.DepositIds.Select(deposit => deposit.Coefficient)];
            }
        }
    }
}

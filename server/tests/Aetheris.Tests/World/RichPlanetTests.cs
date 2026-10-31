using Aetheris.World.Configuration;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the richness multipliers of the deposits of planets (task 00009, decision 12): an
/// ordinary planet keeps every multiplier in the ordinary range of its own rule, a rich planet
/// uses the rich range of a rule that has one, and the probability and the ranges are data.
/// </summary>
public sealed class RichPlanetTests
{
    private const int Seeds = 40;

    /// <summary>
    /// Without rich planets every deposit lies in the ordinary range of its rule; with every
    /// planet rich, a rule that has a rich range uses it and a rule without one keeps the
    /// ordinary range (acceptance criterion 9b).
    /// </summary>
    /// <param name="probability">The probability of a rich planet.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public void The_multipliers_follow_the_range_of_the_planet(string probability)
    {
        WorldGenerationConfig config = WorldTestData.Config(Overrides(probability));
        List<CheckedDeposit> deposits = [.. PlanetBodies(config).SelectMany(body => body)];

        Assert.NotEmpty(deposits);
        Assert.All(deposits, deposit => AssertInOwnRange(RuleOf(config.Reference, deposit), deposit.Coefficient, probability == "1"));
    }

    /// <summary>
    /// A multiplier above the ordinary range of its rule appears only on a rich planet. On that
    /// planet a rule with a rich range lies in it, and a rule without one stays ordinary. Gas
    /// giants keep the ranges of their own rules because they are not planets.
    /// </summary>
    [Fact]
    public void A_multiplier_above_the_ordinary_range_belongs_to_a_rich_planet()
    {
        WorldGenerationConfig config = WorldTestData.Config(Overrides("0.5"));
        List<IReadOnlyList<CheckedDeposit>> planets = [.. PlanetBodies(config).Where(body => body.Count > 0)];
        List<IReadOnlyList<CheckedDeposit>> rich =
        [
            .. planets.Where(body => body.Any(deposit => deposit.Coefficient > RuleOf(config.Reference, deposit).CoefficientMax)),
        ];

        Assert.NotEmpty(rich);
        Assert.True(rich.Count < planets.Count);
        Assert.All(rich, body => Assert.All(body, deposit => AssertInOwnRange(RuleOf(config.Reference, deposit), deposit.Coefficient, preferRich: true)));
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

        List<decimal> multipliers = [.. PlanetBodies(seed with { Reference = changed }).SelectMany(body => body).Select(deposit => deposit.Coefficient)];

        Assert.NotEmpty(multipliers);
        Assert.All(multipliers, multiplier => Assert.InRange(multiplier, 1.5m, 1.6m));
    }

    private static Dictionary<string, string> Overrides(string probability) =>
        new(WorldTestData.SmallSectors, StringComparer.Ordinal) { [WorldConfigKeys.RichPlanetProbability] = probability };

    private static void AssertInOwnRange(ReferenceRule rule, decimal coefficient, bool preferRich)
    {
        if (preferRich && rule.RichCoefficientMin is decimal richMinimum && rule.RichCoefficientMax is decimal richMaximum)
        {
            Assert.InRange(coefficient, richMinimum, richMaximum);
            return;
        }

        Assert.InRange(coefficient, rule.CoefficientMin, rule.CoefficientMax);
    }

    private static ReferenceRule RuleOf(WorldReferenceData reference, CheckedDeposit deposit) =>
        reference.RuleGroups
            .Where(group => group.ObjectTypeId == deposit.ObjectTypeId)
            .SelectMany(group => group.RuleIds)
            .Single(rule => rule.ResourceId == deposit.ResourceId);

    private static IEnumerable<IReadOnlyList<CheckedDeposit>> PlanetBodies(WorldGenerationConfig config)
    {
        HashSet<int> planetTypes = [.. config.Reference.TypesOf(WorldObjectClasses.Planet).Select(type => type.Id)];

        for (int seed = 1; seed <= Seeds; seed++)
        {
            SectorPlan plan = SectorGenerator.Generate(seed, "S" + seed, config);

            foreach (BodyPlan body in plan.SystemIds.SelectMany(system => system.BodyIds).Where(body => planetTypes.Contains(body.ObjectTypeId)))
            {
                yield return [.. body.DepositIds.Select(deposit => new CheckedDeposit(body.ObjectTypeId, deposit.ResourceId, deposit.Coefficient))];
            }
        }
    }

    private sealed record CheckedDeposit(int ObjectTypeId, int ResourceId, decimal Coefficient);
}

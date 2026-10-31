using Aetheris.Planets.Api;
using Aetheris.Planets.Services;
using Aetheris.Requirements.Services;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the flag that tells the screen a requirement is out of reach of the next step, so a
/// building behind it is shown as closed (task 00020, section 7.10).
/// </summary>
public sealed class RequirementClosedTests
{
    private const int Center = 7;

    private static readonly BuildingCatalog EmptyCatalog = new([], [], [], [], []);

    private static bool IsClosed(string requirementType, int targetLevel, int levelOnBody)
    {
        RequirementDto[] requirements =
        [
            .. PlanetOverviewBuilder.Requirements(
                EmptyCatalog,
                [new UnmetRequirement(1, requirementType, Center, targetLevel, "planets_requirement_unmet")],
                new Dictionary<int, int> { [Center] = levelOnBody }),
        ];

        return Assert.Single(requirements).IsClosed;
    }

    [Fact]
    public void A_building_one_level_short_is_only_to_prepare_for()
    {
        Assert.False(IsClosed(RequirementTypes.BuildingLevel, 3, 2));
    }

    [Fact]
    public void A_building_more_than_one_level_short_is_out_of_reach()
    {
        Assert.True(IsClosed(RequirementTypes.BuildingLevel, 4, 2));
        Assert.True(IsClosed(RequirementTypes.BuildingLevel, 2, 0));
    }

    [Fact]
    public void A_technology_that_is_not_learned_is_out_of_reach()
    {
        Assert.True(IsClosed(RequirementTypes.TechnologyLevel, 1, 0));
    }

    [Fact]
    public void A_deposit_is_never_closed()
    {
        Assert.False(IsClosed(RequirementTypes.Deposit, 1, 0));
    }

    [Fact]
    public void Without_the_levels_of_the_body_no_requirement_is_closed()
    {
        IReadOnlyList<RequirementDto> requirements = PlanetOverviewBuilder.Requirements(
            EmptyCatalog,
            [new UnmetRequirement(1, RequirementTypes.TechnologyLevel, Center, 1, "planets_requirement_unmet")]);

        Assert.False(Assert.Single(requirements).IsClosed);
    }
}

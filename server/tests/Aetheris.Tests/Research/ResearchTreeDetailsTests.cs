using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Requirements;
using Aetheris.Requirements.Services;
using Aetheris.Research;
using Aetheris.Research.Api;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResearchAmount = Aetheris.Research.Api.ResourceAmountDto;

namespace Aetheris.Tests.Research;

/// <summary>
/// Tests of what the tree of the technologies tells the research page (task 00020, section 7.9):
/// what the selected body is short of for the next level, what a technology gives at the level
/// that is learned, and the unknown state of a technology beyond the player's reach.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResearchTreeDetailsTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchTreeDetailsTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ResearchTreeDetailsTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The next level of an open technology names every resource the selected body is short of
    /// and by how much; a body that has enough shows nothing, and no other level shows anything.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_next_level_names_what_the_selected_body_is_short_of()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 10);
        IResearchModule research = services.GetRequiredService<IResearchModule>();

        TechnologyDto technology = (await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token))
            .Technologies.First(entry => entry.Status == ResearchTechnologyStatuses.Available && entry.Levels.First().Costs.Count > 1);
        IReadOnlyList<ResearchAmount> costs = technology.Levels.Single(level => level.Level == 1).Costs;
        ResearchAmount first = costs[0];
        ResearchAmount second = costs[1];
        DateTimeOffset moment = PlanetsTestData.GameNow(services);

        // The first resource is half there, the second is missing, the rest is enough.
        Dictionary<int, long> balance = costs.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount);
        balance[first.ResourceTypeId] = first.Amount / 2;
        balance[second.ResourceTypeId] = 0;
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, balance, moment);

        TechnologyDto shortOf = (await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token)).Technologies.Single(entry => entry.Id == technology.Id);
        IReadOnlyList<ResearchAmount> missing = shortOf.Levels.Single(level => level.Level == 1).MissingResources!;

        Assert.Equal(
            costs.Where(cost => cost.ResourceTypeId == first.ResourceTypeId || cost.ResourceTypeId == second.ResourceTypeId).Select(cost => cost.ResourceTypeId).Order(),
            missing.Select(entry => entry.ResourceTypeId).Order());
        Assert.Equal(first.Amount - (first.Amount / 2), missing.Single(entry => entry.ResourceTypeId == first.ResourceTypeId).Amount);
        Assert.Equal(second.Amount, missing.Single(entry => entry.ResourceTypeId == second.ResourceTypeId).Amount);
        Assert.All(shortOf.Levels.Where(level => level.Level != 1), level => Assert.Empty(level.MissingResources!));

        // A rich body is short of nothing.
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), moment);
        TechnologyDto enough = (await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token)).Technologies.Single(entry => entry.Id == technology.Id);

        Assert.Empty(enough.Levels.Single(level => level.Level == 1).MissingResources!);
    }

    /// <summary>
    /// A body of another player is never read: the tree names no selected body, so it shows no
    /// balance and no shortage, and the answer of the owner is the only one with them.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_balance_of_a_body_of_another_player_is_not_read()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await PlanetsTestData.SetLevelAsync(services, owner.PlanetId, SeedCodes.ScienceCenter, 10);
        IResearchModule research = services.GetRequiredService<IResearchModule>();

        ResearchTreeDto seen = await research.GetTreeAsync(stranger.Account.UserId, owner.PlanetId, Token);

        Assert.NotEqual(owner.PlanetId, seen.SelectedPlanetId);
        Assert.All(seen.Technologies.SelectMany(technology => technology.Levels), level => Assert.Empty(level.MissingResources!));
    }

    /// <summary>
    /// The server counts what a technology gives at the level the player has learned: nothing
    /// before the first level, the effects of the learned level after it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_shows_what_it_gives_at_the_learned_level()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 10);
        IResearchModule research = services.GetRequiredService<IResearchModule>();

        TechnologyDto before = (await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token))
            .Technologies.First(entry => entry.Status == ResearchTechnologyStatuses.Available && entry.MaxLevel > 2 && entry.Levels.First().Effects.Count > 0);

        Assert.Empty(before.CurrentEffects!);

        ResearchDbContext dbContext = services.GetRequiredService<ResearchDbContext>();
        dbContext.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = player.Account.UserId, TechnologyId = before.Id, Level = 2 });
        await dbContext.SaveChangesAsync(Token);

        TechnologyDto after = (await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token)).Technologies.Single(entry => entry.Id == before.Id);
        IReadOnlyList<TechnologyEffectDto> learnedEffects = after.Levels.Single(level => level.Level == 2).Effects;

        Assert.Equal(2, after.LearnedLevel);
        Assert.Equal(learnedEffects, after.CurrentEffects);
        Assert.NotEmpty(after.CurrentEffects!);
    }

    /// <summary>
    /// A technology whose missing technology is open is closed with its reason (the next step);
    /// one that waits for a technology that is itself closed is unknown, with no reason.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_beyond_the_reach_of_the_player_is_unknown()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 10);
        IResearchModule research = services.GetRequiredService<IResearchModule>();

        List<TechnologyDto> open = [.. (await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token))
            .Technologies.Where(technology => technology.Status == ResearchTechnologyStatuses.Available)];
        TechnologyDto root = open[0];
        TechnologyDto next = open[1];
        TechnologyDto beyond = open[2];
        IRequirementsModule requirements = services.GetRequiredService<IRequirementsModule>();

        await requirements.AddAsync(RequirementSubjectTypes.Technology, next.Id, 1, [new RequirementDefinition(RequirementTypes.TechnologyLevel, root.Id, 1)], Token);
        await requirements.AddAsync(RequirementSubjectTypes.Technology, beyond.Id, 1, [new RequirementDefinition(RequirementTypes.TechnologyLevel, next.Id, 1)], Token);

        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        TechnologyDto nextShown = tree.Technologies.Single(technology => technology.Id == next.Id);
        TechnologyDto beyondShown = tree.Technologies.Single(technology => technology.Id == beyond.Id);

        Assert.Equal(ResearchTechnologyStatuses.Unavailable, nextShown.Status);
        Assert.Equal(ResearchErrorCodes.RequirementUnmet, nextShown.ReasonCode);
        Assert.Equal(ResearchTechnologyStatuses.Unknown, beyondShown.Status);
        Assert.Null(beyondShown.ReasonCode);
        Assert.Equal(ResearchTechnologyStatuses.Available, tree.Technologies.Single(technology => technology.Id == root.Id).Status);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, ResearchConfigKeys.CatalogCacheSeconds, "0");
        await scope.ServiceProvider.GetRequiredService<IResearchModule>().EnsureAsync(Token);
    }

    private static async Task<Dictionary<int, long>> RichBalanceAsync(IServiceProvider services) =>
        await services.GetRequiredService<Aetheris.World.Data.WorldDbContext>().ResourceTypes.AsNoTracking()
            .ToDictionaryAsync(resource => resource.Id, _ => 1_000_000L, Token);
}

using Aetheris.Core.Errors;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Services;
using Aetheris.Requirements.Services;
using Aetheris.Research;
using Aetheris.Research.Api;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Research;

/// <summary>
/// Tests of the tree of the technologies, the queue of the researches and the bonuses the
/// technologies give (task 00012).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResearchTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResearchTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ResearchTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The technologies live in the database, so a new technology arrives as a row with its
    /// texts, and the tree of a player shows it with its state.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_technologies_are_data_and_the_tree_shows_their_state()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        ResearchTreeDto tree = await scope.ServiceProvider.GetRequiredService<IResearchModule>()
            .GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotEmpty(tree.Technologies);
        Assert.All(tree.Technologies, technology => Assert.StartsWith("tech_", technology.Code, StringComparison.Ordinal));
        Assert.All(tree.Technologies, technology => Assert.StartsWith("research.technology.", technology.NameKey, StringComparison.Ordinal));
        Assert.Contains(tree.Branches, branch => branch.Branch == TechnologyBranches.Economy);

        // Without a science centre the technologies that need one are not available.
        Assert.All(
            tree.Technologies.Where(technology => technology.ResearchCenterLevel > 0),
            technology => Assert.Equal(ResearchTechnologyStatuses.Unavailable, technology.Status));

        // The tree is the same for a second read, and the texts of the technologies are in the
        // database.
        ResearchTreeDto again = await scope.ServiceProvider.GetRequiredService<IResearchModule>()
            .GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(tree.Technologies.Count, again.Technologies.Count);
    }

    /// <summary>
    /// A technology needs a science centre of the level it names on the body the research
    /// starts from; the reason says why it is not available.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_needs_a_science_centre_of_its_level()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // The science centre is built on the body, so the technology of the first level opens.
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);

        ResearchTreeDto tree = await services.GetRequiredService<IResearchModule>()
            .GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        TechnologyDto first = tree.Technologies.OrderBy(technology => technology.ResearchCenterLevel).First();
        Assert.Equal(1, first.ResearchCenterLevel);
        Assert.Equal(ResearchTechnologyStatuses.Available, first.Status);
        Assert.Null(first.ReasonCode);

        // A technology that needs a higher centre stays unavailable with its reason.
        TechnologyDto higher = tree.Technologies.First(technology => technology.ResearchCenterLevel > 1);
        Assert.Equal(ResearchTechnologyStatuses.Unavailable, higher.Status);
        Assert.Equal(ResearchErrorCodes.ResearchCenterLevel, higher.ReasonCode);
    }

    /// <summary>
    /// A research pays its cost when it enters the queue and returns it in full when it
    /// leaves; the queue of the researches follows one another.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_research_pays_its_cost_and_the_queue_follows_one_another()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 2);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), PlanetsTestData.GameNow(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        TechnologyDto first = tree.Technologies.OrderBy(technology => technology.ResearchCenterLevel).First();
        long cost = first.Levels.First(level => level.Level == 1).Costs.Sum(cost => cost.Amount);

        Dictionary<int, long> before = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);

        ResearchTreeDto queued = await research.EnqueueAsync(
            player.Account.UserId,
            player.PlanetId,
            first.Id,
            1,
            Token);

        Assert.Single(queued.Queue);
        Assert.Equal(ResearchTechnologyStatuses.Researching, queued.Technologies.Single(technology => technology.Id == first.Id).Status);

        Dictionary<int, long> afterQueue = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);
        Assert.Equal(before.Values.Sum() - cost, afterQueue.Values.Sum());

        // The cancellation returns the whole cost.
        ResearchTreeDto cancelled = await research.CancelAsync(player.Account.UserId, queued.Queue[0].Id, Token);

        Assert.Empty(cancelled.Queue);
        Dictionary<int, long> afterCancel = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);
        Assert.Equal(before.Values.Sum(), afterCancel.Values.Sum());
    }

    /// <summary>
    /// The queue has two places, and a research of a player who has no science centre is
    /// refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_queue_has_two_places_and_needs_a_science_centre()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), PlanetsTestData.GameNow(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(2, tree.QueueSlots);

        // Without a science centre the research of a technology is refused.
        TechnologyDto first = tree.Technologies.OrderBy(technology => technology.ResearchCenterLevel).First();
        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            research.EnqueueAsync(player.Account.UserId, player.PlanetId, first.Id, 1, Token));

        Assert.Equal(ResearchErrorCodes.ResearchCenterLevel, refused.Code);
    }

    /// <summary>
    /// The research belongs to the player and not to a body, so the technology learned on one
    /// body gives its bonus to every body of the civilization.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_learned_technology_gives_its_bonus_to_every_body()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), PlanetsTestData.GameNow(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        TechnologyDto first = tree.Technologies.OrderBy(technology => technology.ResearchCenterLevel).First();

        await research.EnqueueAsync(player.Account.UserId, player.PlanetId, first.Id, 1, Token);

        // The research is due, so it completes and the level of the technology is written.
        await services.GetRequiredService<ResearchQueueService>()
            .CompleteDueAsync(player.Account.UserId, DateTimeOffset.MaxValue, Token);

        ITechnologyLevels levels = services.GetRequiredService<ITechnologyLevels>();
        IReadOnlyDictionary<int, int> learned = await levels.GetLevelsAsync(player.Account.UserId, Token);

        Assert.Equal(1, learned[first.Id]);

        PlanetBonuses bonuses = await levels.GetBonusesAsync(player.Account.UserId, Token);
        Assert.True(bonuses.MetalProductionMultiplier > decimal.One);
        Assert.Equal(decimal.One, bonuses.CrystalProductionMultiplier);
    }

    /// <summary>
    /// A building that a technology opens is not available until the technology is learned,
    /// and it is available right after (task 00012, criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_building_opens_only_after_its_technology_is_learned()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 3);
        await PlanetsTestData.SetDepositAsync(services, player.PlanetId, "resource_2", 1m);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), PlanetsTestData.GameNow(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        TechnologyDto opener = tree.Technologies.Single(technology => technology.NameKey == "research.technology.tech_7.name");

        // The mine is closed while the technology is unknown.
        AvailabilityDto before = await services.GetRequiredService<IPlanetsModule>()
            .CheckRequirementsAsync(
                RequirementSubjectTypes.BuildingType,
                await BuildingTypeIdAsync(services, SeedCodes.TitaniumMine),
                1,
                player.PlanetId,
                player.Account.UserId,
                Token);

        Assert.False(before.IsAvailable);
        Assert.Equal(PlanetsErrorCodes.RequirementTechnology, before.UnmetRequirements.Single().ReasonCode);

        // The technology is learned, so the mine opens.
        await research.EnqueueAsync(player.Account.UserId, player.PlanetId, opener.Id, 1, Token);
        await services.GetRequiredService<ResearchQueueService>()
            .CompleteDueAsync(player.Account.UserId, DateTimeOffset.MaxValue, Token);

        AvailabilityDto after = await services.GetRequiredService<IPlanetsModule>()
            .CheckRequirementsAsync(
                RequirementSubjectTypes.BuildingType,
                await BuildingTypeIdAsync(services, SeedCodes.TitaniumMine),
                1,
                player.PlanetId,
                player.Account.UserId,
                Token);

        Assert.True(after.IsAvailable);
    }

    private static Task<int> BuildingTypeIdAsync(IServiceProvider services, string code) =>
        services.GetRequiredService<PlanetsDbContext>().BuildingTypes.AsNoTracking()
            .Where(type => type.Code == code)
            .Select(type => type.Id)
            .SingleAsync(Token);

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

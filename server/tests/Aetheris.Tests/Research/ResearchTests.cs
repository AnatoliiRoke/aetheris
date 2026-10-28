using Aetheris.Core.Errors;
using Aetheris.Planets;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
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
        Assert.Contains(tree.Branches, branch => branch.Branch == ResearchReferenceSeed.Load().Branches[0].Code);

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

    /// <summary>
    /// A science centre of a lower level than the technology needs does not open its research;
    /// any science centre was enough before the review of task 00012.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_is_refused_on_a_science_centre_below_its_level()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), PlanetsTestData.GameNow(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        TechnologyDto higher = tree.Technologies.First(technology => technology.ResearchCenterLevel > 1);

        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            research.EnqueueAsync(player.Account.UserId, player.PlanetId, higher.Id, 1, Token));

        Assert.Equal(ResearchErrorCodes.ResearchCenterLevel, refused.Code);
        Assert.Empty((await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token)).Queue);
    }

    /// <summary>
    /// The levels of a technology follow one another, and the cancellation of a level takes
    /// the later levels of the same technology with it and returns the whole cost of each.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_levels_follow_one_another_and_a_cancel_takes_the_later_levels_with_it()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), FutureMoment(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        TechnologyDto technology = tree.Technologies.First(entry => entry.ResearchCenterLevel == 1 && entry.MaxLevel > 2);
        Dictionary<int, long> before = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);

        // The second level is refused before the first one is learned or queued.
        GameException skipped = await Assert.ThrowsAsync<GameException>(() =>
            research.EnqueueAsync(player.Account.UserId, player.PlanetId, technology.Id, 2, Token));
        Assert.Equal(ResearchErrorCodes.LevelOutOfOrder, skipped.Code);

        ResearchTreeDto first = await research.EnqueueAsync(player.Account.UserId, player.PlanetId, technology.Id, 1, Token);
        ResearchTreeDto second = await research.EnqueueAsync(player.Account.UserId, player.PlanetId, technology.Id, 2, Token);
        Assert.Equal([1, 2], second.Queue.Select(item => item.TargetLevel));

        // The cancellation of the first level takes the second one with it.
        ResearchTreeDto cancelled = await research.CancelAsync(player.Account.UserId, first.Queue[0].Id, Token);

        Assert.Empty(cancelled.Queue);
        Assert.Equal(before, await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId));
    }

    /// <summary>
    /// Twenty parallel commands of one player never spend the same resources twice, never
    /// drive a balance below zero and never take more than the places of the queue (task
    /// 00012, criterion 6). The player has no queue row yet, so the first commands also meet
    /// on its creation.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_researches_never_spend_twice_or_go_below_zero()
    {
        const int requests = 20;
        const int technologies = 3;

        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 3);

        IReadOnlyList<TechnologyEntityCosts> chosen = await CheapestTechnologiesAsync(services, technologies, 3);

        // The balance pays for the first level of each of the three technologies, more than
        // the two places of the queue can take.
        Dictionary<int, long> balance = [];

        foreach ((int resourceTypeId, long amount) in chosen.SelectMany(technology => technology.Costs))
        {
            balance[resourceTypeId] = balance.GetValueOrDefault(resourceTypeId) + amount;
        }

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, balance, FutureMoment(services));

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, requests).Select(index =>
            TestAccounts.SendCommandAsync(
                player.Client,
                EnqueueResearchCommand.RoutePath,
                new Dictionary<string, object?>
                {
                    ["planet_id"] = player.PlanetId,
                    ["technology_id"] = chosen[index % technologies].TechnologyId,
                    ["target_level"] = 1,
                },
                "research-parallel-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        int slots = ResearchSettings.From(services.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>()).QueueBaseSlots;

        Assert.Equal(slots, responses.Count(response => response.StatusCode == System.Net.HttpStatusCode.OK));
        Assert.All(
            responses.Where(response => response.StatusCode != System.Net.HttpStatusCode.OK),
            response => Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode));

        List<int> queued = await services.GetRequiredService<ResearchDbContext>().ResearchQueueItems.AsNoTracking()
            .Where(item => item.PlayerId == player.Account.UserId)
            .Select(item => item.TechnologyId)
            .ToListAsync(Token);

        Assert.Equal(slots, queued.Count);
        Assert.Equal(queued.Count, queued.Distinct().Count());

        // Exactly the costs of the queued researches left the balance.
        Dictionary<int, long> left = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);
        Assert.All(left.Values, amount => Assert.True(amount >= 0));

        foreach (int resourceTypeId in balance.Keys)
        {
            long spent = chosen
                .Where(technology => queued.Contains(technology.TechnologyId))
                .Sum(technology => technology.Costs.GetValueOrDefault(resourceTypeId));

            Assert.Equal(balance[resourceTypeId] - spent, left.GetValueOrDefault(resourceTypeId));
        }

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// A research pays from what the body has produced since its balance was last written,
    /// like a construction; the stored amount alone was checked before the review.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_research_pays_from_what_the_body_has_produced()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        (TechnologyEntityCosts technology, int ironId) = await PrepareProducingBodyAsync(services, player);

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto queued = await research.EnqueueAsync(player.Account.UserId, player.PlanetId, technology.TechnologyId, 1, Token);

        Assert.Single(queued.Queue);
        Dictionary<int, long> left = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);
        Assert.All(left.Values, amount => Assert.True(amount >= 0));
        Assert.True(left.GetValueOrDefault(ironId) >= 0);
    }

    /// <summary>
    /// The bonus of a learned level counts only from the end of the research: the balance of
    /// every body of the player is fixed at that moment before the bonus changes.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_bonus_counts_only_from_the_end_of_the_research()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        (TechnologyEntityCosts technology, _) = await PrepareProducingBodyAsync(services, player);

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto queued = await research.EnqueueAsync(player.Account.UserId, player.PlanetId, technology.TechnologyId, 1, Token);
        DateTimeOffset finishesAt = queued.Queue.Single().FinishesAt!.Value;

        await services.GetRequiredService<ResearchQueueService>()
            .CompleteDueAsync(player.Account.UserId, finishesAt, Token);

        DateTimeOffset fixedAt = await services.GetRequiredService<PlanetsDbContext>().PlanetStates.AsNoTracking()
            .Where(state => state.PlanetId == player.PlanetId)
            .Select(state => state.ResourcesUpdatedAt)
            .SingleAsync(Token);

        Assert.Equal(ConstructionService.Normalize(finishesAt), fixedAt);
    }

    /// <summary>
    /// The tree lists the bodies of the player with their science centres and takes the body
    /// with the highest one when none is named; a body of another player is refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_research_starts_from_a_chosen_body_of_the_player()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 2);
        await PlanetsTestData.SetLevelAsync(services, stranger.PlanetId, SeedCodes.ScienceCenter, 5);
        await PlanetsTestData.SetBalanceAsync(services, stranger.PlanetId, await RichBalanceAsync(services), FutureMoment(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, null, Token);

        ResearchPlanetDto home = Assert.Single(tree.Planets);
        Assert.Equal(player.PlanetId, home.PlanetId);
        Assert.Equal(2, home.ResearchCenterLevel);
        Assert.True(home.IsAvailable);
        Assert.Equal(player.PlanetId, tree.SelectedPlanetId);
        Assert.Equal(2, tree.AvailableCenterLevel);

        // A body of another player is not a body of the player, whatever its science centre.
        ResearchTreeDto foreign = await research.GetTreeAsync(player.Account.UserId, stranger.PlanetId, Token);
        Assert.Null(foreign.SelectedPlanetId);
        Assert.Equal(0, foreign.AvailableCenterLevel);

        TechnologyDto first = tree.Technologies.OrderBy(technology => technology.ResearchCenterLevel).First();
        await Assert.ThrowsAsync<GameException>(() =>
            research.EnqueueAsync(player.Account.UserId, stranger.PlanetId, first.Id, 1, Token));

        Assert.Empty((await research.GetTreeAsync(player.Account.UserId, null, Token)).Queue);
    }

    /// <summary>
    /// The paid places of the queue never exceed the limit of the configuration.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_queue_has_no_more_paid_places_than_the_limit()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ResearchDbContext dbContext = services.GetRequiredService<ResearchDbContext>();
        dbContext.ResearchQueueStates.Add(new Aetheris.Research.Data.Entities.ResearchQueueStateEntity { PlayerId = player.Account.UserId, ExtraQueueSlots = 10 });
        await dbContext.SaveChangesAsync(Token);

        ResearchSettings settings = ResearchSettings.From(services.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>());
        ResearchTreeDto tree = await services.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.Equal(settings.QueueBaseSlots + settings.QueueMaxExtraSlots, tree.QueueSlots);
    }

    /// <summary>
    /// A requirement of a level on another technology keeps the level closed until that
    /// technology is learned; the requirement lives in the common requirements module.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_requirement_on_another_technology_closes_the_level()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 10);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await RichBalanceAsync(services), FutureMoment(services));

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchTreeDto tree = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        List<TechnologyDto> openOnes = [.. tree.Technologies.Where(technology => technology.Status == ResearchTechnologyStatuses.Available)];
        TechnologyDto needed = openOnes[0];
        TechnologyDto closed = openOnes[1];

        await services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>().AddAsync(
            RequirementSubjectTypes.Technology,
            closed.Id,
            1,
            [new RequirementDefinition(RequirementTypes.TechnologyLevel, needed.Id, 1)],
            Token);

        ResearchTreeDto before = await research.GetTreeAsync(player.Account.UserId, player.PlanetId, Token);
        TechnologyDto shown = before.Technologies.Single(technology => technology.Id == closed.Id);
        Assert.Equal(ResearchTechnologyStatuses.Unavailable, shown.Status);
        Assert.Equal(ResearchErrorCodes.RequirementUnmet, shown.ReasonCode);
        Assert.NotEmpty(shown.Levels.Single(level => level.Level == 1).UnmetRequirementIds);

        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            research.EnqueueAsync(player.Account.UserId, player.PlanetId, closed.Id, 1, Token));
        Assert.Equal(ResearchErrorCodes.RequirementUnmet, refused.Code);

        // The needed technology is learned, so the level opens.
        await research.EnqueueAsync(player.Account.UserId, player.PlanetId, needed.Id, 1, Token);
        await services.GetRequiredService<ResearchQueueService>()
            .CompleteDueAsync(player.Account.UserId, DateTimeOffset.MaxValue, Token);

        ResearchTreeDto after = await research.EnqueueAsync(player.Account.UserId, player.PlanetId, closed.Id, 1, Token);
        Assert.Contains(after.Queue, item => item.TechnologyId == closed.Id);
    }

    /// <summary>
    /// The tree names the building a technology opens, lists every branch with the empty ones,
    /// and gives the moment of game time the timers of the client count from.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_tree_names_what_a_technology_opens_and_its_moment()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        ResearchTreeDto tree = await services.GetRequiredService<IResearchModule>().GetTreeAsync(player.Account.UserId, player.PlanetId, Token);

        TechnologyEffectDto opens = tree.Technologies
            .SelectMany(technology => technology.Levels)
            .SelectMany(level => level.Effects)
            .First(effect => effect.UnlockKind == TechnologyUnlockKinds.BuildingType);
        Assert.False(string.IsNullOrEmpty(opens.UnlockNameKey));

        Assert.Equal(ResearchReferenceSeed.Load().Branches.Select(branch => branch.Code), tree.Branches.Select(branch => branch.Branch));
        Assert.True(tree.TimeMultiplier > 0);
        Assert.NotEqual(default, tree.GameNow);
    }

    /// <summary>
    /// The building catalog of the bodies module is read again after the research module
    /// writes the requirements of the buildings its technologies open, even when the catalog
    /// is kept in memory for long.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_building_catalog_sees_the_requirements_of_the_technologies_at_once()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, PlanetsConfigKeys.CatalogCacheSeconds, "3600");

        try
        {
            IPlanetsModule planets = services.GetRequiredService<IPlanetsModule>();
            await planets.EnsureAsync(Token);
            using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

            int mineId = await BuildingTypeIdAsync(services, SeedCodes.TitaniumMine);
            AvailabilityDto before = await planets.CheckRequirementsAsync(RequirementSubjectTypes.BuildingType, mineId, 1, player.PlanetId, player.Account.UserId, Token);
            Assert.DoesNotContain(before.UnmetRequirements, requirement => requirement.ReasonCode == PlanetsErrorCodes.RequirementTechnology);

            await AetherisTestFactory.SetParameterAsync(services, ResearchConfigKeys.CatalogCacheSeconds, "0");
            await services.GetRequiredService<IResearchModule>().EnsureAsync(Token);

            AvailabilityDto after = await planets.CheckRequirementsAsync(RequirementSubjectTypes.BuildingType, mineId, 1, player.PlanetId, player.Account.UserId, Token);
            Assert.Contains(after.UnmetRequirements, requirement => requirement.ReasonCode == PlanetsErrorCodes.RequirementTechnology);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, PlanetsConfigKeys.CatalogCacheSeconds, "0");
        }
    }

    private static DateTimeOffset FutureMoment(IServiceProvider services) =>
        PlanetsTestData.GameNow(services).AddHours(1);

    private static async Task<IReadOnlyList<TechnologyEntityCosts>> CheapestTechnologiesAsync(IServiceProvider services, int count, int centerLevel) =>
        [
            .. (await services.GetRequiredService<ResearchDbContext>().Technologies.AsNoTracking()
                .Include(technology => technology.LevelIds).ThenInclude(level => level.CostIds)
                .Where(technology => technology.ResearchCenterLevel <= centerLevel)
                .OrderBy(technology => technology.Id)
                .ToListAsync(Token))
                .Take(count)
                .Select(technology => new TechnologyEntityCosts(
                    technology.Id,
                    technology.LevelIds.Single(level => level.Level == 1).CostIds.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount))),
        ];

    /// <summary>
    /// Gives the home body an iron mine with a power plant and a balance written long ago:
    /// the iron of the research is produced since then and was not stored.
    /// </summary>
    private static async Task<(TechnologyEntityCosts Technology, int IronId)> PrepareProducingBodyAsync(IServiceProvider services, PlanetsTestPlayer player)
    {
        int ironId = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Iron, 1m);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ScienceCenter, 1);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.IronMine, 5);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.PowerPlant, 10);

        TechnologyEntityCosts technology = (await CheapestTechnologiesAsync(services, int.MaxValue, 1))
            .First(entry => entry.Costs.ContainsKey(ironId));

        Dictionary<int, long> balance = technology.Costs.ToDictionary(cost => cost.Key, cost => cost.Value);
        balance[ironId] = 0;

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, balance, PlanetsTestData.GameNow(services).AddHours(-1000));

        // The body has produced the iron of the research since the balance was written.
        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, PlanetsTestData.GameNow(services));
        Assert.True(overview.Resources.Single(resource => resource.ResourceTypeId == ironId).Amount >= technology.Costs[ironId]);

        return (technology, ironId);
    }

    /// <summary>A technology with the costs of its first level.</summary>
    private sealed record TechnologyEntityCosts(int TechnologyId, IReadOnlyDictionary<int, long> Costs);

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

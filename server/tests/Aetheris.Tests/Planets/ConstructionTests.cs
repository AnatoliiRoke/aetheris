using System.Net;
using Aetheris.Core.Errors;
using Aetheris.Planets.Api;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the construction queue, the energy, the production and the parallel commands
/// (task 00011, criteria 3, 5, 6 and 7).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ConstructionTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConstructionTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public ConstructionTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The queue is sequential with two places: the cost is taken at the queueing, a cancel
    /// returns all of it, and the end of a construction raises the level and starts the next
    /// record at that very moment.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_queue_is_sequential_takes_the_cost_and_returns_it_on_cancel()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ConstructionService construction = services.GetRequiredService<ConstructionService>();

        DateTimeOffset start = PlanetsTestData.GameNow(services);
        Dictionary<int, long> balance = await BuildingsTests.RichBalanceAsync(services);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, balance, start);

        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);
        BuildingTypeEntity housing = await PlanetsTestData.TypeAsync(services, SeedCodes.ResidentialComplex);
        BuildingTypeEntity storage = await PlanetsTestData.TypeAsync(services, SeedCodes.MetalStorage);
        BuildingLevelEntity plantLevel = plant.LevelIds.Single(level => level.Level == 1);
        BuildingLevelEntity housingLevel = housing.LevelIds.Single(level => level.Level == 1);

        PlanetOverviewDto first = await construction.EnqueueAsync(player.PlanetId, plant.Id, start, Token);
        PlanetOverviewDto second = await construction.EnqueueAsync(player.PlanetId, housing.Id, start.AddSeconds(5), Token);

        QueueItemDto underWay = second.Queue.Items[0];
        QueueItemDto waiting = second.Queue.Items[1];

        Assert.Equal(2, second.Queue.SlotCount);
        Assert.Equal(start, underWay.StartedAt);
        Assert.Equal(start.AddSeconds(plantLevel.DurationSeconds), underWay.FinishesAt);
        Assert.Null(waiting.StartedAt);
        HashSet<int> currencyIds = await PlanetsTestData.CurrencyIdsAsync(services);
        Assert.All(
            plantLevel.CostIds.Where(cost => !currencyIds.Contains(cost.ResourceTypeId)),
            cost => Assert.Equal(balance[cost.ResourceTypeId] - cost.Amount, Amount(first, cost.ResourceTypeId)));

        foreach (BuildingLevelCostEntity currencyCost in plantLevel.CostIds.Where(cost => currencyIds.Contains(cost.ResourceTypeId)))
        {
            long housingCost = housingLevel.CostIds
                .Where(cost => cost.ResourceTypeId == currencyCost.ResourceTypeId)
                .Sum(cost => cost.Amount);

            Assert.Equal(
                balance[currencyCost.ResourceTypeId] - currencyCost.Amount - housingCost,
                await EconomyTestData.BalanceAsync(services, player.Account.UserId, currencyCost.ResourceTypeId));
        }

        // A third record finds no place.
        GameException full = await Assert.ThrowsAsync<GameException>(() => construction.EnqueueAsync(player.PlanetId, storage.Id, start.AddSeconds(6), Token));
        Assert.Equal(PlanetsErrorCodes.QueueFull, full.Code);

        // The cancel returns the whole cost of the waiting record.
        PlanetOverviewDto cancelled = await construction.CancelAsync(player.PlanetId, waiting.Id, start.AddSeconds(7), Token);
        Assert.Single(cancelled.Queue.Items);
        Assert.All(housingLevel.CostIds, cost => Assert.Equal(Amount(first, cost.ResourceTypeId), Amount(cancelled, cost.ResourceTypeId)));

        await construction.EnqueueAsync(player.PlanetId, housing.Id, start.AddSeconds(8), Token);

        // Nothing is done a moment before the end.
        DateTimeOffset end = underWay.FinishesAt!.Value;
        PlanetOverviewDto before = await PlanetsTestData.ReadAsync(services, player.PlanetId, end.AddSeconds(-1));
        Assert.Equal(0, PlanetsTestData.Building(before, SeedCodes.PowerPlant).Level);

        // The scheduler completes the plant at its end and starts the housing at that moment.
        int applied = await ShiftedGameClock.RunDueEventsAsync(_factory, end - PlanetsTestData.GameNow(services) + TimeSpan.FromSeconds(1));
        Assert.True(applied >= 1);

        PlanetOverviewDto after = await PlanetsTestData.ReadAsync(services, player.PlanetId, end);
        QueueItemDto next = Assert.Single(after.Queue.Items);

        Assert.Equal(1, PlanetsTestData.Building(after, SeedCodes.PowerPlant).Level);
        Assert.Equal(housing.Id, next.BuildingTypeId);
        Assert.Equal(end, next.StartedAt);
        Assert.Equal(end.AddSeconds(housingLevel.DurationSeconds), next.FinishesAt);

        // A late read completes the housing at its own end as well.
        PlanetOverviewDto done = await PlanetsTestData.ReadAsync(services, player.PlanetId, next.FinishesAt!.Value.AddHours(1));
        Assert.Empty(done.Queue.Items);
        Assert.Equal(1, PlanetsTestData.Building(done, SeedCodes.ResidentialComplex).Level);
    }

    /// <summary>
    /// Cancelling the construction under way returns its cost and starts the next record at once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Cancelling_the_record_under_way_starts_the_next_one()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ConstructionService construction = services.GetRequiredService<ConstructionService>();

        DateTimeOffset start = PlanetsTestData.GameNow(services);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await BuildingsTests.RichBalanceAsync(services), start);

        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);
        BuildingTypeEntity housing = await PlanetsTestData.TypeAsync(services, SeedCodes.ResidentialComplex);

        PlanetOverviewDto initial = await PlanetsTestData.ReadAsync(services, player.PlanetId, start);
        await construction.EnqueueAsync(player.PlanetId, plant.Id, start, Token);
        PlanetOverviewDto queued = await construction.EnqueueAsync(player.PlanetId, housing.Id, start, Token);

        PlanetOverviewDto cancelled = await construction.CancelAsync(player.PlanetId, queued.Queue.Items[0].Id, start.AddSeconds(10), Token);
        QueueItemDto head = Assert.Single(cancelled.Queue.Items);

        Assert.Equal(housing.Id, head.BuildingTypeId);
        Assert.Equal(start.AddSeconds(10), head.StartedAt);

        // The old event of the cancelled plant changes nothing when it fires.
        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromSeconds(plant.LevelIds.Single(level => level.Level == 1).DurationSeconds + 1));
        PlanetOverviewDto after = await PlanetsTestData.ReadAsync(services, player.PlanetId, start.AddSeconds(11));

        Assert.Equal(0, PlanetsTestData.Building(after, SeedCodes.PowerPlant).Level);

        // Every resource is back except the cost of the housing.
        BuildingLevelEntity housingLevel = housing.LevelIds.Single(level => level.Level == 1);
        Assert.All(initial.Resources, resource => Assert.Equal(
            resource.Amount - housingLevel.CostIds.Where(cost => cost.ResourceTypeId == resource.ResourceTypeId).Sum(cost => cost.Amount),
            Amount(after, resource.ResourceTypeId)));
    }

    /// <summary>
    /// A consumer is refused when the balance would go below zero once it and the queued
    /// buildings are done; a producer is never refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_consumer_that_drives_the_energy_below_zero_is_refused_counting_the_queue()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ConstructionService construction = services.GetRequiredService<ConstructionService>();
        DateTimeOffset now = PlanetsTestData.GameNow(services);

        await AetherisTestFactory.SetParameterAsync(services, PlanetsConfigKeys.QueueBaseSlots, "100");
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await BuildingsTests.RichBalanceAsync(services), now);

        // The main building of level 2 lets the next level of the housing through the level cap.
        // The housing is the consumer: the seed spends energy as a resource, so the test adds
        // the consumption effect the rule checks.
        const int mainLevel = 2;
        const decimal housingConsumption = 50m;
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.AdminCenter, mainLevel);

        PlanetsDbContext planets = services.GetRequiredService<PlanetsDbContext>();
        BuildingTypeEntity housingRow = await planets.BuildingTypes
            .Include(type => type.LevelIds)
            .ThenInclude(level => level.EffectIds)
            .SingleAsync(type => type.Code == SeedCodes.ResidentialComplex, Token);

        foreach (BuildingLevelEntity level in housingRow.LevelIds.Where(level => level.Level <= mainLevel))
        {
            level.EffectIds.Add(new BuildingLevelEffectEntity
            {
                EffectType = BuildingEffectTypes.EnergyConsumption,
                Value = housingConsumption,
            });
        }

        await planets.SaveChangesAsync(Token);
        services.GetRequiredService<BuildingCatalogCache>().Invalidate();

        BuildingTypeEntity main = await PlanetsTestData.TypeAsync(services, SeedCodes.AdminCenter);
        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);
        List<BuildingTypeEntity> consumers =
        [
            await PlanetsTestData.TypeAsync(services, SeedCodes.ResidentialComplex),
        ];

        long output = Effect(main, mainLevel, BuildingEffectTypes.EnergyOutput);
        Dictionary<int, int> queuedLevels = [];
        BuildingTypeEntity? refusedType = null;

        // The consumers grow level after level until the next one would not fit.
        for (int level = 1; level <= mainLevel && refusedType is null; level++)
        {
            foreach (BuildingTypeEntity consumer in consumers)
            {
                Dictionary<int, int> next = new(queuedLevels) { [consumer.Id] = level };
                long consumption = consumers.Sum(type => next.TryGetValue(type.Id, out int queued) ? Effect(type, queued, BuildingEffectTypes.EnergyConsumption) : 0);

                if (consumption > output)
                {
                    refusedType = consumer;
                    break;
                }

                PlanetOverviewDto queued = await construction.EnqueueAsync(player.PlanetId, consumer.Id, now, Token);
                queuedLevels = next;

                // The energy built now does not change while the buildings wait in the queue.
                Assert.Equal(output, queued.Energy.Balance);
                Assert.Equal(output - consumption, queued.Energy.QueuedBalance);
            }
        }

        Assert.NotNull(refusedType);

        GameException refused = await Assert.ThrowsAsync<GameException>(() => construction.EnqueueAsync(player.PlanetId, refusedType.Id, now, Token));
        Assert.Equal(PlanetsErrorCodes.EnergyShort, refused.Code);

        // A power plant in the queue makes room for the consumer.
        await construction.EnqueueAsync(player.PlanetId, plant.Id, now, Token);
        PlanetOverviewDto accepted = await construction.EnqueueAsync(player.PlanetId, refusedType.Id, now, Token);

        Assert.True(accepted.Energy.QueuedBalance >= 0);
    }

    /// <summary>
    /// A mine yields the value of its level times the richness of the deposit per hour, lazily
    /// and only up to the capacity of its resource; the alarm "storage full" is written as data
    /// for the resource, and the other resources of the group are not touched by it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_mine_yields_lazily_with_the_deposit_richness_and_stops_at_the_capacity()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        const decimal richness = 1.5m;
        int iron = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Iron, richness);
        DateTimeOffset start = PlanetsTestData.GameNow(services);

        BuildingTypeEntity mine = await PlanetsTestData.TypeAsync(services, SeedCodes.IronMine);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.MetalStorage, 1);
        BuildingTypeEntity warehouse = await PlanetsTestData.TypeAsync(services, SeedCodes.MetalStorage);
        long value = Effect(mine, 1, BuildingEffectTypes.MetalProduction);
        BuildingLevelEffectEntity capacityEffect = warehouse.LevelIds.Single(level => level.Level == 1).EffectIds
            .Single(effect => effect.EffectType == BuildingEffectTypes.StorageCapacity && effect.ResourceGroupId != null
                && services.GetRequiredService<Aetheris.World.Data.WorldDbContext>().ResourceTypes.Any(resource => resource.Id == iron && resource.ResourceGroupId == effect.ResourceGroupId));

        // Copper of the same group pays for a later change; it has the capacity of its own and does
        // not take any of the capacity of the iron.
        const long copperAmount = 10;
        int copper = await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Copper);

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.IronMine, 1);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [iron] = 0, [copper] = copperAmount }, start);

        decimal rate = value * richness;
        PlanetOverviewDto early = await PlanetsTestData.ReadAsync(services, player.PlanetId, start.AddMinutes(20));

        Assert.Equal(rate, early.Resources.Single(resource => resource.ResourceTypeId == iron).RatePerHour);
        Assert.Equal((long)decimal.Floor(rate * 20 / 60), Amount(early, iron));

        // The read writes nothing; the balance is fixed only by a change.
        Assert.Equal(0, (await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId))[iron]);

        // Far later the iron is full and the mine stands still; the copper has not lost any room.
        DateTimeOffset fullAt = start.AddTicks((long)decimal.Ceiling(capacityEffect.Value * (decimal)TimeSpan.TicksPerHour / rate));
        PlanetOverviewDto later = await PlanetsTestData.ReadAsync(services, player.PlanetId, fullAt.AddDays(3));
        PlanetResourceDto ironLater = later.Resources.Single(resource => resource.ResourceTypeId == iron);
        PlanetResourceDto copperLater = later.Resources.Single(resource => resource.ResourceTypeId == copper);

        Assert.Equal(capacityEffect.Value, Amount(later, iron));
        Assert.Equal(capacityEffect.Value, ironLater.Capacity);
        Assert.True(ironLater.IsFull);
        Assert.Equal(fullAt, ironLater.FullAt);
        Assert.Equal(0, ironLater.RatePerHour);
        Assert.Equal(capacityEffect.Value, copperLater.Capacity);
        Assert.False(copperLater.IsFull);

        // A change fixes the balance and writes the alarm as the state of the resource.
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, await BuildingsTests.RichBalanceAsync(services), fullAt.AddDays(3));
        await services.GetRequiredService<ConstructionService>().EnqueueAsync(
            player.PlanetId,
            (await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant)).Id,
            fullAt.AddDays(3),
            Token);

        PlanetResourceEntity stored = await services.GetRequiredService<PlanetsDbContext>().PlanetResources.AsNoTracking()
            .SingleAsync(entry => entry.PlanetId == player.PlanetId && entry.ResourceTypeId == iron, Token);
        Assert.NotNull(stored.FullAt);
    }

    /// <summary>
    /// Twenty parallel commands of one body never spend the same resources twice and never
    /// drive the balance below zero.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_commands_never_spend_twice_or_go_below_zero()
    {
        const int requests = 20;
        const int affordableLevels = 3;

        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await AetherisTestFactory.SetParameterAsync(services, PlanetsConfigKeys.QueueBaseSlots, (requests + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));

        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.AdminCenter, affordableLevels + 1);

        // The balance pays for exactly three levels of the plant.
        Dictionary<int, long> balance = [];

        foreach (BuildingLevelCostEntity cost in plant.LevelIds.Where(level => level.Level <= affordableLevels).SelectMany(level => level.CostIds))
        {
            balance[cost.ResourceTypeId] = balance.GetValueOrDefault(cost.ResourceTypeId) + cost.Amount;
        }

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, balance, PlanetsTestData.GameNow(services));

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, requests).Select(index =>
            TestAccounts.SendCommandAsync(
                player.Client,
                EnqueueConstructionCommand.RoutePath,
                new Dictionary<string, object?> { ["planet_id"] = player.PlanetId, ["building_type_id"] = plant.Id },
                "parallel-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        Assert.Equal(affordableLevels, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode));

        Dictionary<int, long> left = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);
        HashSet<int> spentCurrencyIds = await PlanetsTestData.CurrencyIdsAsync(services);
        Assert.All(left.Values, amount => Assert.True(amount >= 0));
        Assert.All(
            balance.Keys.Where(resourceTypeId => !spentCurrencyIds.Contains(resourceTypeId)),
            resourceTypeId => Assert.Equal(0, left[resourceTypeId]));

        foreach (int resourceTypeId in balance.Keys.Where(spentCurrencyIds.Contains))
        {
            Assert.Equal(0, await EconomyTestData.BalanceAsync(services, player.Account.UserId, resourceTypeId));
        }

        List<int> levels = await services.GetRequiredService<PlanetsDbContext>().QueueItems.AsNoTracking()
            .Where(item => item.PlanetId == player.PlanetId)
            .OrderBy(item => item.Position)
            .Select(item => item.TargetLevel)
            .ToListAsync(Token);
        Assert.Equal([1, 2, 3], levels);

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    private static long Amount(PlanetOverviewDto overview, int resourceTypeId) =>
        overview.Resources.SingleOrDefault(resource => resource.ResourceTypeId == resourceTypeId)?.Amount ?? 0;

    private static long Effect(BuildingTypeEntity type, int level, string effectType) =>
        (long)type.LevelIds.Single(entry => entry.Level == level).EffectIds.Where(effect => effect.EffectType == effectType).Sum(effect => effect.Value);
}

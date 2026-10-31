using System.Text.Json;
using Aetheris.Planets.Api;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.World;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the capacity of the storage of a body per resource (decision of the author,
/// 31.10.2026): every resource of a group has the capacity the storage gives, a full resource
/// stops alone, and the storage of a body works for this body only.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class StorageCapacityTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageCapacityTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public StorageCapacityTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A full resource stops only itself: another resource of the same group goes on, every
    /// resource of the group has the same capacity, the moment the resource is full is counted
    /// for the resource and the answer names the capacity, the alarm and the moment of every
    /// resource.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_full_resource_stops_only_itself_and_the_group_shares_one_capacity()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        const long roomLeft = 10;
        int iron = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Iron, 1m);
        int copper = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Copper, 1m);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.IronMine, 1);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.CopperMine, 1);
        DateTimeOffset start = PlanetsTestData.GameNow(services);

        PlanetOverviewDto first = await PlanetsTestData.ReadAsync(services, player.PlanetId, start);
        long capacity = first.Resources.Single(resource => resource.ResourceTypeId == iron).Capacity;
        decimal ironRate = first.Resources.Single(resource => resource.ResourceTypeId == iron).RatePerHour;

        Assert.True(capacity > roomLeft);
        Assert.True(ironRate > 0);

        // The iron has room for ten units and the copper is empty.
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [iron] = capacity - roomLeft, [copper] = 0 }, start);

        DateTimeOffset later = start.AddHours(1);
        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, later);
        PlanetResourceDto ironRow = overview.Resources.Single(resource => resource.ResourceTypeId == iron);
        PlanetResourceDto copperRow = overview.Resources.Single(resource => resource.ResourceTypeId == copper);

        Assert.Equal(capacity, ironRow.Amount);
        Assert.True(ironRow.IsFull);
        Assert.Equal(0, ironRow.RatePerHour);
        Assert.Equal(start.AddTicks((long)decimal.Ceiling(roomLeft * (decimal)TimeSpan.TicksPerHour / ironRate)), ironRow.FullAt);

        Assert.True(copperRow.Amount > 0);
        Assert.True(copperRow.RatePerHour > 0);
        Assert.False(copperRow.IsFull);
        Assert.NotNull(copperRow.FullAt);
        Assert.True(copperRow.FullAt > later);

        // Every resource of the group has the capacity of the storage, mined or not.
        List<PlanetResourceDto> group = [.. overview.Resources.Where(resource => resource.ResourceGroupId == ironRow.ResourceGroupId)];

        Assert.True(group.Count > 2);
        Assert.All(group, resource => Assert.Equal(capacity, resource.Capacity));
    }

    /// <summary>
    /// The storage of another body does not change the capacity of this body: a storage built
    /// on one body raises the capacity of that body only.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_storage_of_another_body_does_not_change_the_capacity()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer mine = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer other = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        int iron = await PlanetsTestData.ResourceIdAsync(services, SeedCodes.Iron);
        DateTimeOffset now = PlanetsTestData.GameNow(services);

        long before = (await PlanetsTestData.ReadAsync(services, mine.PlanetId, now)).Resources.Single(resource => resource.ResourceTypeId == iron).Capacity;

        await PlanetsTestData.SetLevelAsync(services, other.PlanetId, SeedCodes.MetalStorage, 5);

        long mineAfter = (await PlanetsTestData.ReadAsync(services, mine.PlanetId, now)).Resources.Single(resource => resource.ResourceTypeId == iron).Capacity;
        long otherAfter = (await PlanetsTestData.ReadAsync(services, other.PlanetId, now)).Resources.Single(resource => resource.ResourceTypeId == iron).Capacity;

        Assert.Equal(before, mineAfter);
        Assert.True(otherAfter > before);
    }

    /// <summary>
    /// A refund over the capacity is not cut: the cancel of a record returns the whole cost
    /// even when the resource is already full, and the resource counts as full (task 00011).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_refund_over_the_capacity_is_not_cut()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        ConstructionService construction = services.GetRequiredService<ConstructionService>();

        BuildingTypeEntity plant = await PlanetsTestData.TypeAsync(services, SeedCodes.PowerPlant);
        Dictionary<int, long> costs = plant.LevelIds.Single(level => level.Level == 1).CostIds.ToDictionary(cost => cost.ResourceTypeId, cost => cost.Amount);
        DateTimeOffset start = PlanetsTestData.GameNow(services);

        PlanetOverviewDto overview = await PlanetsTestData.ReadAsync(services, player.PlanetId, start);
        Dictionary<int, long> capacities = costs.Keys.ToDictionary(
            resourceTypeId => resourceTypeId,
            resourceTypeId => overview.Resources.SingleOrDefault(resource => resource.ResourceTypeId == resourceTypeId)?.Capacity ?? 0);

        // The balance covers the cost, the plant is queued and the cost is taken.
        await PlanetsTestData.SetBalanceAsync(
            services,
            player.PlanetId,
            costs.ToDictionary(cost => cost.Key, cost => Math.Max(capacities[cost.Key], cost.Value)),
            start);
        PlanetOverviewDto queued = await construction.EnqueueAsync(player.PlanetId, plant.Id, start, Token);

        // The resources are full again, then the record is cancelled and the cost comes back.
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, capacities.ToDictionary(entry => entry.Key, entry => entry.Value), start);
        PlanetOverviewDto cancelled = await construction.CancelAsync(player.PlanetId, queued.Queue.Items[0].Id, start, Token);

        // A cost in a currency is not a resource of the body, so only the rows of the body are checked.
        List<KeyValuePair<int, long>> bodyCosts = [.. costs.Where(cost => cancelled.Resources.Any(resource => resource.ResourceTypeId == cost.Key))];

        Assert.NotEmpty(bodyCosts);
        Assert.All(bodyCosts, cost =>
        {
            PlanetResourceDto row = cancelled.Resources.Single(resource => resource.ResourceTypeId == cost.Key);

            Assert.Equal(capacities[cost.Key] + cost.Value, row.Amount);
            Assert.True(row.IsFull);
            Assert.Equal(0, row.RatePerHour);
        });
    }

    /// <summary>
    /// Twenty parallel fixes of the balance of one player write the same balance and the same
    /// moments as one fix: no amount is added twice and no resource goes over its capacity.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_fixes_write_one_consistent_balance()
    {
        const int fixes = 20;
        const long roomLeft = 10;

        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        int iron = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Iron, 1m);
        int copper = await PlanetsTestData.SetDepositAsync(services, player.PlanetId, SeedCodes.Copper, 1m);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.IronMine, 1);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.CopperMine, 1);
        DateTimeOffset start = PlanetsTestData.GameNow(services);

        long capacity = (await PlanetsTestData.ReadAsync(services, player.PlanetId, start)).Resources.Single(resource => resource.ResourceTypeId == iron).Capacity;
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, new Dictionary<int, long> { [iron] = capacity - roomLeft, [copper] = 0 }, start);

        DateTimeOffset moment = start.AddHours(1);
        PlanetOverviewDto expected = await PlanetsTestData.ReadAsync(services, player.PlanetId, moment);

        await Task.WhenAll(Enumerable.Range(0, fixes).Select(async _ =>
        {
            using IServiceScope fixScope = _factory.Services.CreateScope();

            await fixScope.ServiceProvider.GetRequiredService<ConstructionService>().FixPlayerAsync(player.Account.UserId, moment, Token);
        }));

        using IServiceScope check = _factory.Services.CreateScope();
        Dictionary<int, long> stored = await PlanetsTestData.StoredBalanceAsync(check.ServiceProvider, player.PlanetId);
        PlanetResourceEntity ironRow = await check.ServiceProvider.GetRequiredService<PlanetsDbContext>().PlanetResources.AsNoTracking()
            .SingleAsync(row => row.PlanetId == player.PlanetId && row.ResourceTypeId == iron, Token);

        Assert.Equal(capacity, stored[iron]);
        Assert.Equal(expected.Resources.Single(resource => resource.ResourceTypeId == copper).Amount, stored[copper]);
        Assert.All(stored, entry => Assert.True(entry.Value <= capacity));

        // The database keeps a moment to the microsecond, so the moment read back may differ by it.
        Assert.NotNull(ironRow.FullAt);
        Assert.Equal(expected.Resources.Single(resource => resource.ResourceTypeId == iron).FullAt!.Value.Ticks, ironRow.FullAt.Value.Ticks, TimeSpan.TicksPerMicrosecond);
    }

    /// <summary>
    /// The answer of the API carries the capacity, the flag and the moment of every resource
    /// with the names of the contract (documentation/tech/08, rule 3), and no total of a group.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_api_names_the_capacity_the_flag_and_the_moment_of_every_resource()
    {
        await PlanetsTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using HttpResponseMessage response = await player.Client.GetAsync($"/api/v1/planets/{player.PlanetId}", Token);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        JsonElement resource = body.RootElement.GetProperty("resources").EnumerateArray().First();

        Assert.True(resource.TryGetProperty("capacity", out JsonElement capacity));
        Assert.True(capacity.GetInt64() > 0);
        Assert.True(resource.TryGetProperty("is_full", out _));
        Assert.True(resource.TryGetProperty("full_at", out _));
        Assert.False(body.RootElement.TryGetProperty("storage_groups", out _));
    }
}

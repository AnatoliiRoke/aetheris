using System.Net;
using System.Text.Json;
using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Data;
using Aetheris.Fleet;
using Aetheris.Fleet.Configuration;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Fleet.Services;
using Aetheris.Production;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.Production;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Fleet;

/// <summary>
/// Tests of the shipyard queue (task 00039): the slots, the exact stacks, the cancellation
/// and the dismantling.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ShipyardQueueTests
{
    private const string Shipyard = "building_51";
    private const string DesignBureau = "building_10";
    private const string OpenHull = "part_h_21";
    private const string ClosedHull = "part_h_1";
    private const string Enqueue = "/api/v1/fleet/shipyard/enqueue";
    private const string Cancel = "/api/v1/fleet/shipyard/cancel";
    private const string Disassemble = "/api/v1/fleet/ship/disassemble";
    private const string Designs = "/api/v1/fleet/designs";
    private const string Catalog = "/api/v1/fleet/ship-catalog";
    private const string Ships = "/api/v1/fleet/ships";
    private const int ParallelCount = 20;
    private const long StockQuantity = 40;
    private const long RichAmount = 100_000_000;

    /// <summary>
    /// The assembly time written on the hull under test. The game seed leaves the characteristic
    /// empty, and a queue record cannot be stored without a positive time.
    /// </summary>
    private const long FixtureAssemblySeconds = 900;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipyardQueueTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public ShipyardQueueTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Twenty commands at once fill the two places of a level-one shipyard and do not spend
    /// twice or drive a balance below zero.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Twenty_parallel_requests_fill_two_slots_and_never_go_negative()
    {
        await ResetAsync();
        await using PreparedYard yard = await PrepareAsync(OpenHull, shipyardLevel: 1);

        Task<HttpResponseMessage>[] requests = [.. Enumerable.Range(0, ParallelCount)
            .Select(_ => SendAsync(yard.Player, Enqueue, new { planet_id = yard.Player.PlanetId, design_id = yard.DesignId }))];
        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        try
        {
            int accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
            HttpResponseMessage[] refused = [.. responses.Where(response => response.StatusCode != HttpStatusCode.OK)];

            Assert.Equal(2, accepted);
            Assert.Equal(ParallelCount - 2, refused.Length);
            Assert.All(refused, response => Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode));

            foreach (HttpResponseMessage response in refused)
            {
                Assert.Equal("fleet_shipyard_queue_full", await TestAccounts.ReadErrorAsync(response));
            }
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        Dictionary<PartKey, long> stacks = await ReadPartsAsync(scope.ServiceProvider, yard.Player.PlanetId);
        Ledger ledger = await ReadLedgerAsync(scope.ServiceProvider, yard.Player);

        Assert.Equal(StockQuantity - 2, stacks[yard.HullKey]);
        Assert.Equal(StockQuantity - 2, stacks[yard.DriveKey]);
        Assert.Equal(StockQuantity - 2, stacks[yard.ReactorKey]);
        Assert.Equal(3, stacks.Count);
        Assert.All(stacks.Values, quantity => Assert.True(quantity >= 0));
        Assert.All(ledger.Planet.Values, amount => Assert.True(amount >= 0));
        Assert.All(ledger.Credits.Values, amount => Assert.True(amount >= 0));
        Assert.True(ledger.Population >= 0);

        int orders = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipyardOrders
            .AsNoTracking()
            .CountAsync(order => order.PlanetId == yard.Player.PlanetId, Token);

        Assert.Equal(2, orders);
    }

    /// <summary>
    /// A hull stays closed until the shipyard reaches the level the ladder names.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_hull_stays_closed_until_the_shipyard_reaches_its_level()
    {
        await ResetAsync();
        await using PreparedYard yard = await PrepareAsync(ClosedHull, shipyardLevel: 1);

        HttpResponseMessage response = await SendAsync(yard.Player, Enqueue, new { planet_id = yard.Player.PlanetId, design_id = yard.DesignId });

        using (response)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

            using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

            Assert.Equal("planets_requirement_building_level", body.RootElement.GetProperty("error").GetString());
            Assert.Equal(2, body.RootElement.GetProperty("details").GetProperty("requirement_level").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("details").GetProperty("requirement_name_key").GetString()));
        }

        using IServiceScope scope = _factory.Services.CreateScope();
        Dictionary<PartKey, long> stacks = await ReadPartsAsync(scope.ServiceProvider, yard.Player.PlanetId);

        Assert.Equal(StockQuantity, stacks[yard.HullKey]);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Ships.CountAsync(Token));
    }

    /// <summary>
    /// An ordinary stack does not pay for a part that carries the bonus of the race.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_race_bonus_stack_is_not_spent_as_an_ordinary_one()
    {
        await ResetAsync();
        await using PreparedYard yard = await PrepareAsync(OpenHull, shipyardLevel: 1, driveHasRaceBonus: true);

        HttpResponseMessage missing = await SendAsync(yard.Player, Enqueue, new { planet_id = yard.Player.PlanetId, design_id = yard.DesignId });

        using (missing)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
            Assert.Equal("fleet_part_missing", await TestAccounts.ReadErrorAsync(missing));
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            Dictionary<PartKey, long> untouched = await ReadPartsAsync(scope.ServiceProvider, yard.Player.PlanetId);

            Assert.Equal(StockQuantity, untouched[yard.DriveKey]);
            Assert.False(untouched.ContainsKey(yard.DriveKey with { HasRaceBonus = true }));

            await scope.ServiceProvider.GetRequiredService<PartStockService>().AddAsync(
                yard.Player.PlanetId,
                [new PartStackAdded(yard.DriveKey.PartTypeId, yard.DriveKey.MarkLevel, true, PartCondition.Full, 1)],
                Token);
        }

        HttpResponseMessage accepted = await SendAsync(yard.Player, Enqueue, new { planet_id = yard.Player.PlanetId, design_id = yard.DesignId });

        using (accepted)
        {
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        await FinishAsync(yard.Player.PlanetId);

        using IServiceScope check = _factory.Services.CreateScope();
        Dictionary<PartKey, long> stacks = await ReadPartsAsync(check.ServiceProvider, yard.Player.PlanetId);
        ShipModuleEntity module = await check.ServiceProvider.GetRequiredService<FleetDbContext>().ShipModules
            .AsNoTracking()
            .SingleAsync(entry => entry.PartTypeId == yard.DriveKey.PartTypeId, Token);

        Assert.Equal(StockQuantity, stacks[yard.DriveKey]);
        Assert.False(stacks.ContainsKey(yard.DriveKey with { HasRaceBonus = true }));
        Assert.True(module.HasRaceBonus);
        Assert.False(module.IsHull);
    }

    /// <summary>
    /// A cancellation returns the recorded cost and the hull. A dismantling reads the project
    /// and returns every component of it, including the hull, and the crew. The resources stay.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Cancelling_returns_the_paid_cost_and_dismantling_returns_the_components_and_the_crew()
    {
        await ResetAsync();
        await using PreparedYard first = await PrepareAsync(OpenHull, shipyardLevel: 1);
        long secondDesignId = await SaveAnotherAsync(first, "Другий");

        await FundAsync(first);
        HttpResponseMessage queuedFirst = await SendAsync(first.Player, Enqueue, new { planet_id = first.Player.PlanetId, design_id = first.DesignId });
        HttpResponseMessage queuedSecond = await SendAsync(first.Player, Enqueue, new { planet_id = first.Player.PlanetId, design_id = secondDesignId });

        using (queuedFirst)
        using (queuedSecond)
        {
            Assert.Equal(HttpStatusCode.OK, queuedFirst.StatusCode);
            Assert.Equal(HttpStatusCode.OK, queuedSecond.StatusCode);
        }

        long waitingOrderId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            List<ShipyardOrderEntity> orders = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipyardOrders
                .AsNoTracking()
                .Where(order => order.PlanetId == first.Player.PlanetId)
                .OrderBy(order => order.Position)
                .ToListAsync(Token);

            Assert.Equal(2, orders.Count);
            Assert.NotNull(orders[0].FinishesAt);
            Assert.Null(orders[1].FinishesAt);
            waitingOrderId = orders[1].Id;
        }

        await FinishHeadAsync(first.Player.PlanetId);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext fleet = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            ShipyardOrderEntity waiting = await fleet.ShipyardOrders.AsNoTracking().SingleAsync(order => order.PlanetId == first.Player.PlanetId, Token);
            ShipEntity finished = await fleet.Ships.AsNoTracking().SingleAsync(ship => ship.PlanetId == first.Player.PlanetId, Token);

            Assert.Equal(waitingOrderId, waiting.Id);
            Assert.NotNull(waiting.FinishesAt);
            Assert.Equal(0m, finished.UpkeepPerHour);
        }

        Ledger beforeCancel;
        List<ShipyardOrderCostEntity> waitingCosts;
        List<ShipyardOrderPartEntity> waitingParts;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            beforeCancel = await ReadLedgerAsync(scope.ServiceProvider, first.Player);
            FleetDbContext fleet = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            waitingCosts = await fleet.ShipyardOrderCosts.AsNoTracking().Where(cost => cost.OrderId == waitingOrderId).ToListAsync(Token);
            waitingParts = await fleet.ShipyardOrderParts.AsNoTracking().Where(part => part.OrderId == waitingOrderId).ToListAsync(Token);
        }

        HttpResponseMessage cancelled = await SendAsync(first.Player, Cancel, new { order_id = waitingOrderId });

        using (cancelled)
        {
            Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            Ledger afterCancel = await ReadLedgerAsync(scope.ServiceProvider, first.Player);
            ResourceKinds kinds = await KindsAsync(scope.ServiceProvider);

            AssertRefund(beforeCancel, afterCancel, waitingCosts, waitingParts, kinds, returnHullPart: true, hullShare: new Dictionary<int, long>());
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipyardOrders.CountAsync(order => order.PlanetId == first.Player.PlanetId, Token));
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Ships.CountAsync(ship => ship.PlanetId == first.Player.PlanetId, Token));
        }

        long shipId;
        List<ShipCostEntity> shipCosts;
        List<ShipModuleEntity> shipModules;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            FleetDbContext fleet = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            ShipEntity ship = await fleet.Ships
                .AsNoTracking()
                .Include(entry => entry.CostIds)
                .Include(entry => entry.ModuleIds)
                .SingleAsync(entry => entry.PlanetId == first.Player.PlanetId, Token);

            shipId = ship.Id;
            shipCosts = ship.CostIds;
            shipModules = ship.ModuleIds;
            Assert.Equal(0m, ship.UpkeepPerHour);
        }

        HttpResponseMessage ships = await first.Player.Client.GetAsync($"{Ships}?planet_id={first.Player.PlanetId}", Token);

        using (ships)
        {
            Assert.Equal(HttpStatusCode.OK, ships.StatusCode);

            using JsonDocument body = JsonDocument.Parse(await ships.Content.ReadAsStringAsync(Token));
            JsonElement shown = body.RootElement.EnumerateArray().Single();

            Assert.Equal(0m, shown.GetProperty("upkeep_per_hour").GetDecimal());
        }

        Ledger beforeDisassembly = await ReadLedgerInScopeAsync(first.Player);
        HttpResponseMessage dismantled = await SendAsync(first.Player, Disassemble, new { ship_id = shipId });

        using (dismantled)
        {
            Assert.Equal(HttpStatusCode.OK, dismantled.StatusCode);
        }

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            Ledger afterDisassembly = await ReadLedgerAsync(scope.ServiceProvider, first.Player);
            ResourceKinds kinds = await KindsAsync(scope.ServiceProvider);

            AssertRefund(
                beforeDisassembly,
                afterDisassembly,
                [.. shipCosts
                    .Where(cost => kinds.PopulationIds.Contains(cost.ResourceTypeId))
                    .Select(cost => new ShipyardOrderCostEntity { ResourceTypeId = cost.ResourceTypeId, Amount = cost.Amount })],
                [.. shipModules.Select(module => new ShipyardOrderPartEntity
                {
                    PartTypeId = module.PartTypeId,
                    MarkLevel = module.MarkLevel,
                    HasRaceBonus = module.HasRaceBonus,
                    ConditionPercent = module.ConditionPercent,
                    Quantity = module.Quantity,
                    IsHull = module.IsHull,
                })],
                kinds,
                returnHullPart: true,
                hullShare: new Dictionary<int, long>());
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().Ships.CountAsync(Token));
            Assert.All(afterDisassembly.Planet.Values, amount => Assert.True(amount >= 0));
            Assert.All(afterDisassembly.Credits.Values, amount => Assert.True(amount >= 0));
            Assert.True(afterDisassembly.Population >= 0);
        }
    }

    /// <summary>
    /// Prepares the world and the hull levels a shipyard opens.
    /// </summary>
    /// <returns>A task that completes when the modules are ready.</returns>
    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await AetherisTestFactory.SetParameterAsync(services, FleetConfigKeys.MinimumAmmunition, "0");
        await AetherisTestFactory.SetParameterAsync(services, EconomyConfigKeys.PopulationGrowthPercent, "0");
        await AetherisTestFactory.SetParameterAsync(services, EconomyConfigKeys.PopulationDeclinePerHour, "0");
        await AetherisTestFactory.SetParameterAsync(services, EconomyConfigKeys.CreditsPerInhabitant, "0");
        await services.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        await services.GetRequiredService<IFleetModule>().EnsureAsync(Token);
    }

    /// <summary>
    /// Founds a colony, raises the bureau and the shipyard, and saves one project with a drive
    /// and a reactor.
    /// </summary>
    /// <param name="hullCode">The hull of the project.</param>
    /// <param name="shipyardLevel">The level of the shipyard.</param>
    /// <param name="driveHasRaceBonus">Whether the drive carries the bonus of the race.</param>
    /// <returns>The colony and the project.</returns>
    private async Task<PreparedYard> PrepareAsync(string hullCode, int shipyardLevel, bool driveHasRaceBonus = false)
    {
        PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, DesignBureau, 5);
            await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, Shipyard, shipyardLevel);
            await ProductionTestData.BuildStorageAsync(player, scope.ServiceProvider);
        }

        int hullTypeId = await HullTypeIdAsync(hullCode);

        using (IServiceScope assemblyScope = _factory.Services.CreateScope())
        {
            await SetAssemblyTimeAsync(assemblyScope.ServiceProvider, hullTypeId);
        }

        ShipCatalog catalog = await ReadCatalogAsync(player);
        ShipHull hull = catalog.Hulls.Single(entry => entry.Id == hullTypeId);
        ShipModule drive = catalog.Modules
            .Where(module => module.Component == "drive" && module.Fits(hull))
            .OrderBy(module => module.EnergyUse)
            .First();
        ShipModule reactor = catalog.Modules
            .Where(module => module.Component == "reactor" && module.Fits(hull) && module.Power >= drive.EnergyUse)
            .OrderByDescending(module => module.Power)
            .First();

        long designId = await CreateDesignAsync(player, hull, drive, reactor, driveHasRaceBonus, "Перший");
        PartKey hullKey;
        PartKey driveKey;
        PartKey reactorKey;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            PartFacts hullFacts = (await scope.ServiceProvider.GetRequiredService<PartStockService>().GetPartFactsAsync(Token))[hull.Id];
            hullKey = new PartKey(hullFacts.PartTypeId, hullFacts.MarkLevel, false, PartCondition.Full);
            driveKey = new PartKey(drive.Id, 1, false, PartCondition.Full);
            reactorKey = new PartKey(reactor.Id, 1, false, PartCondition.Full);

            await scope.ServiceProvider.GetRequiredService<PartStockService>().AddAsync(
                player.PlanetId,
                [
                    new PartStackAdded(hullKey.PartTypeId, hullKey.MarkLevel, false, PartCondition.Full, StockQuantity),
                    new PartStackAdded(driveKey.PartTypeId, driveKey.MarkLevel, false, PartCondition.Full, StockQuantity),
                    new PartStackAdded(reactorKey.PartTypeId, reactorKey.MarkLevel, false, PartCondition.Full, StockQuantity),
                ],
                Token);

            await FundBalancesAsync(scope.ServiceProvider, player);
        }

        return new PreparedYard(player, designId, hull.Id, drive.Id, reactor.Id, hullKey, driveKey, reactorKey);
    }

    /// <summary>
    /// Saves a second project of the same hull, so the queue can hold two records.
    /// </summary>
    /// <param name="yard">The prepared colony.</param>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project.</returns>
    private async Task<long> SaveAnotherAsync(PreparedYard yard, string name)
    {
        ShipCatalog catalog = await ReadCatalogAsync(yard.Player);
        ShipHull hull = catalog.Hulls.Single(entry => entry.Id == yard.HullId);
        ShipModule drive = catalog.Modules.Single(module => module.Id == yard.DriveId);
        ShipModule reactor = catalog.Modules.Single(module => module.Id == yard.ReactorId);

        return await CreateDesignAsync(yard.Player, hull, drive, reactor, false, name);
    }

    /// <summary>
    /// Puts a large balance on every resource of the body and on the credits and the population.
    /// </summary>
    /// <param name="yard">The colony.</param>
    /// <returns>A task that completes when the balances are written.</returns>
    private async Task FundAsync(PreparedYard yard)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        await FundBalancesAsync(scope.ServiceProvider, yard.Player);
    }

    /// <summary>
    /// Puts a large balance on every resource of the body and on the credits and the population.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="player">The player.</param>
    /// <returns>A task that completes when the balances are written.</returns>
    private static async Task FundBalancesAsync(IServiceProvider services, PlanetsTestPlayer player)
    {
        ResourceCatalog catalog = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;
        Dictionary<int, long> amounts = catalog.Resources.ToDictionary(resource => resource.Id, _ => RichAmount);

        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, amounts, now);
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, RichAmount, now);
    }

    /// <summary>
    /// Runs the scheduler until the head of the queue has become a ship and the next record,
    /// when there is one, has started.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <returns>A task that completes when the head is finished.</returns>
    private async Task FinishHeadAsync(long planetId)
    {
        DateTimeOffset finishesAt;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            finishesAt = await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipyardOrders
                .AsNoTracking()
                .Where(order => order.PlanetId == planetId && order.FinishesAt != null)
                .Select(order => order.FinishesAt!.Value)
                .SingleAsync(Token);
        }

        using IServiceScope clockScope = _factory.Services.CreateScope();
        TimeSpan shift = finishesAt - clockScope.ServiceProvider.GetRequiredService<IGameClock>().GameNow + TimeSpan.FromSeconds(2);
        int applied = await ShiftedGameClock.RunDueEventsAsync(_factory, shift);

        Assert.True(applied >= 1);
    }

    /// <summary>
    /// Runs the scheduler far enough that every assembly that was already queued is finished.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <returns>A task that completes when the ships exist.</returns>
    private async Task FinishAsync(long planetId)
    {
        int applied = await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromHours(7));

        Assert.True(applied >= 1);

        using IServiceScope scope = _factory.Services.CreateScope();

        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FleetDbContext>().ShipyardOrders.CountAsync(order => order.PlanetId == planetId, Token));
    }

    /// <summary>
    /// Asserts the change of the ledger is the recorded cost plus the extra share, and that a
    /// hull part comes back only when the caller says so.
    /// </summary>
    /// <param name="before">The ledger before the command.</param>
    /// <param name="after">The ledger after the command.</param>
    /// <param name="costs">The recorded costs.</param>
    /// <param name="parts">The recorded parts. A ship module uses the same fields the assertion reads.</param>
    /// <param name="kinds">Which resources are a population or a currency.</param>
    /// <param name="returnHullPart">Whether the hull itself comes back. A cancellation returns it; a dismantling does not.</param>
    /// <param name="hullShare">The extra resources of the hull recipe, empty for a cancellation.</param>
    private static void AssertRefund(
        Ledger before,
        Ledger after,
        IReadOnlyList<ShipyardOrderCostEntity> costs,
        IReadOnlyList<ShipyardOrderPartEntity> parts,
        ResourceKinds kinds,
        bool returnHullPart,
        IReadOnlyDictionary<int, long> hullShare)
    {
        Dictionary<int, long> expectedPlanet = new(before.Planet);
        Dictionary<int, long> expectedCredits = new(before.Credits);
        long expectedPopulation = before.Population;

        foreach (ShipyardOrderCostEntity cost in costs)
        {
            if (kinds.PopulationIds.Contains(cost.ResourceTypeId))
            {
                expectedPopulation += cost.Amount;
            }
            else if (kinds.CurrencyIds.Contains(cost.ResourceTypeId))
            {
                expectedCredits[cost.ResourceTypeId] = expectedCredits.GetValueOrDefault(cost.ResourceTypeId) + cost.Amount;
            }
            else
            {
                expectedPlanet[cost.ResourceTypeId] = expectedPlanet.GetValueOrDefault(cost.ResourceTypeId) + cost.Amount;
            }
        }

        foreach ((int resourceTypeId, long amount) in hullShare)
        {
            expectedPlanet[resourceTypeId] = expectedPlanet.GetValueOrDefault(resourceTypeId) + amount;
        }

        Dictionary<PartKey, long> expectedParts = new(before.Parts);

        foreach (ShipyardOrderPartEntity part in parts)
        {
            if (part.IsHull && !returnHullPart)
            {
                continue;
            }

            PartKey key = new(part.PartTypeId, part.MarkLevel, part.HasRaceBonus, part.ConditionPercent);
            expectedParts[key] = expectedParts.GetValueOrDefault(key) + part.Quantity;
        }

        Assert.Equal(expectedPopulation, after.Population);
        EqualAmounts(expectedCredits, after.Credits);
        EqualAmounts(expectedPlanet, after.Planet);
        EqualAmounts(expectedParts, after.Parts);
    }

    /// <summary>
    /// Asserts two maps hold the same amounts. A missing key is zero.
    /// </summary>
    /// <typeparam name="TKey">The key.</typeparam>
    /// <param name="expected">The expected amounts.</param>
    /// <param name="actual">The stored amounts.</param>
    private static void EqualAmounts<TKey>(IReadOnlyDictionary<TKey, long> expected, IReadOnlyDictionary<TKey, long> actual)
        where TKey : notnull
    {
        foreach (TKey key in expected.Keys.Union(actual.Keys))
        {
            Assert.Equal(expected.GetValueOrDefault(key), actual.GetValueOrDefault(key));
        }
    }

    /// <summary>
    /// Reads the ledger in its own scope.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The ledger.</returns>
    private async Task<Ledger> ReadLedgerInScopeAsync(PlanetsTestPlayer player)
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        return await ReadLedgerAsync(scope.ServiceProvider, player);
    }

    /// <summary>
    /// Reads the parts, the population, the credits and the resources of a body as they are stored.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="player">The player.</param>
    /// <returns>The ledger.</returns>
    private static async Task<Ledger> ReadLedgerAsync(IServiceProvider services, PlanetsTestPlayer player)
    {
        EconomyDbContext economy = services.GetRequiredService<EconomyDbContext>();
        long population = await economy.PlanetPopulations.AsNoTracking()
            .Where(entry => entry.PlanetId == player.PlanetId)
            .Select(entry => entry.Amount)
            .SingleAsync(Token);
        Dictionary<int, long> credits = await economy.CivilizationBalances.AsNoTracking()
            .Where(entry => entry.PlayerId == player.Account.UserId)
            .ToDictionaryAsync(entry => entry.ResourceTypeId, entry => entry.Amount, Token);
        Dictionary<int, long> planet = await PlanetsTestData.StoredBalanceAsync(services, player.PlanetId);
        Dictionary<PartKey, long> parts = await ReadPartsAsync(services, player.PlanetId);

        return new Ledger(population, credits, planet, parts);
    }

    /// <summary>
    /// Reads the stacks of a body.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <returns>The quantities by stack.</returns>
    private static async Task<Dictionary<PartKey, long>> ReadPartsAsync(IServiceProvider services, long planetId)
    {
        List<PartStockEntity> stacks = await services.GetRequiredService<ProductionDbContext>().PartStocks
            .AsNoTracking()
            .Where(entry => entry.PlanetId == planetId && entry.Quantity > 0)
            .ToListAsync(Token);

        return stacks.ToDictionary(
            stack => new PartKey(stack.PartTypeId, stack.MarkLevel, stack.HasRaceBonus, stack.ConditionPercent),
            stack => stack.Quantity);
    }

    /// <summary>
    /// Tells which resources are paid from the population or from a currency of the civilization.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <returns>The kinds.</returns>
    private static async Task<ResourceKinds> KindsAsync(IServiceProvider services)
    {
        ResourceCatalog catalog = await services.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        return new ResourceKinds(
            [.. catalog.Resources.Where(resource => resource.PaidByPopulation).Select(resource => resource.Id)],
            [.. catalog.Resources.Where(resource => resource.IsCivilizationScope).Select(resource => resource.Id)]);
    }

    /// <summary>
    /// Resolves a code of the starting file to the part the first seed wrote.
    /// </summary>
    /// <param name="fileCode">The code of the file.</param>
    /// <returns>The part.</returns>
    /// <summary>
    /// Writes an assembly time on the hull under test and drops the catalog cache, so the
    /// next read sees it.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="hullTypeId">The hull.</param>
    /// <returns>A task that completes when the characteristic is stored.</returns>
    private static async Task SetAssemblyTimeAsync(IServiceProvider services, int hullTypeId)
    {
        ProductionDbContext production = services.GetRequiredService<ProductionDbContext>();
        int characteristicId = await production.Characteristics.AsNoTracking()
            .Where(characteristic => characteristic.Kind == CharacteristicKinds.AssemblyTime)
            .Select(characteristic => characteristic.Id)
            .SingleAsync(Token);
        PartCharacteristicValueEntity? stored = await production.PartCharacteristicValues
            .SingleOrDefaultAsync(
                characteristicValue => characteristicValue.PartTypeId == hullTypeId && characteristicValue.CharacteristicId == characteristicId,
                Token);

        if (stored is null)
        {
            production.PartCharacteristicValues.Add(new PartCharacteristicValueEntity
            {
                PartTypeId = hullTypeId,
                CharacteristicId = characteristicId,
                NumberValue = FixtureAssemblySeconds,
            });
        }
        else
        {
            stored.NumberValue = FixtureAssemblySeconds;
        }

        await production.SaveChangesAsync(Token);
        services.GetRequiredService<ProductionCatalogCache>().Invalidate();
    }

    private async Task<int> HullTypeIdAsync(string fileCode)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IReadOnlyDictionary<string, int> partIds = await scope.ServiceProvider
            .GetRequiredService<IProductionModule>()
            .FindPartIdsByFileCodeAsync([fileCode], Token);

        return partIds[fileCode];
    }

    /// <summary>
    /// Reads the catalog of the ships.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The catalog.</returns>
    private static async Task<ShipCatalog> ReadCatalogAsync(PlanetsTestPlayer player)
    {
        HttpResponseMessage response = await player.Client.GetAsync($"{Catalog}?planet_id={player.PlanetId}", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));

        return new ShipCatalog(
            [.. body.RootElement.GetProperty("hulls").EnumerateArray().Select(ReadHull)],
            [.. body.RootElement.GetProperty("modules").EnumerateArray().Select(ReadModule)]);
    }

    /// <summary>
    /// Creates a project and saves a drive and a reactor into it.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="hull">The hull.</param>
    /// <param name="drive">The drive.</param>
    /// <param name="reactor">The reactor.</param>
    /// <param name="driveHasRaceBonus">Whether the drive carries the bonus of the race.</param>
    /// <param name="name">The name of the project.</param>
    /// <returns>The project.</returns>
    private async Task<long> CreateDesignAsync(
        PlanetsTestPlayer player,
        ShipHull hull,
        ShipModule drive,
        ShipModule reactor,
        bool driveHasRaceBonus,
        string name)
    {
        HttpResponseMessage created = await SendAsync(player, Designs, new
        {
            planet_id = player.PlanetId,
            name,
            hull_part_type_id = hull.Id,
            has_race_bonus_slot = false,
        });

        long designId;

        using (created)
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);

            using JsonDocument body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Token));
            designId = body.RootElement.GetProperty("id").GetInt64();
        }

        HttpResponseMessage saved = await SendAsync(player, Designs + "/save", new
        {
            design_id = designId,
            hull_part_type_id = hull.Id,
            has_race_bonus_slot = false,
            slots = new object[]
            {
                new { slot_index = 0, part_type_id = drive.Id, mark_level = 1, has_race_bonus = driveHasRaceBonus },
                new { slot_index = 1, part_type_id = reactor.Id, mark_level = 1, has_race_bonus = false },
            },
        });

        using (saved)
        {
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        }

        return designId;
    }

    /// <summary>
    /// Sends a command of the module.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="route">The route of the command.</param>
    /// <param name="body">The body of the command.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> SendAsync(PlanetsTestPlayer player, string route, object body) =>
        TestAccounts.SendCommandAsync(player.Client, route, body, Guid.NewGuid().ToString("N"));

    private static ShipHull ReadHull(JsonElement element) => new(
        element.GetProperty("id").GetInt32(),
        element.GetProperty("code").GetString() ?? string.Empty,
        element.GetProperty("role").GetString() ?? string.Empty,
        element.GetProperty("slots").GetInt32());

    private static ShipModule ReadModule(JsonElement element) => new(
        element.GetProperty("id").GetInt32(),
        element.GetProperty("component").GetString(),
        element.GetProperty("role").GetString() ?? string.Empty,
        element.GetProperty("energy_use").GetDecimal(),
        element.GetProperty("power").GetDecimal(),
        [.. element.GetProperty("compatible_hull_ids").EnumerateArray().Select(id => id.GetInt32())]);

    /// <summary>
    /// A stack of parts named by the type, the mark, the bonus and the condition.
    /// </summary>
    /// <param name="PartTypeId">The kind of the part.</param>
    /// <param name="MarkLevel">The mark.</param>
    /// <param name="HasRaceBonus">Whether the part carries the bonus of the race.</param>
    /// <param name="ConditionPercent">The condition in per cent.</param>
    private sealed record PartKey(int PartTypeId, int MarkLevel, bool HasRaceBonus, int ConditionPercent);

    /// <summary>
    /// The amounts a command may change.
    /// </summary>
    /// <param name="Population">The inhabitants of the body.</param>
    /// <param name="Credits">The currencies of the civilization.</param>
    /// <param name="Planet">The resources of the body.</param>
    /// <param name="Parts">The parts of the body.</param>
    private sealed record Ledger(
        long Population,
        Dictionary<int, long> Credits,
        Dictionary<int, long> Planet,
        Dictionary<PartKey, long> Parts);

    /// <summary>
    /// Which resources are a population or a currency.
    /// </summary>
    /// <param name="PopulationIds">The resources paid from the inhabitants.</param>
    /// <param name="CurrencyIds">The resources of the civilization.</param>
    private sealed record ResourceKinds(HashSet<int> PopulationIds, HashSet<int> CurrencyIds);

    /// <summary>
    /// A colony with one project and the stacks that project spends.
    /// </summary>
    /// <param name="Player">The player.</param>
    /// <param name="DesignId">The project.</param>
    /// <param name="HullId">The hull.</param>
    /// <param name="DriveId">The drive.</param>
    /// <param name="ReactorId">The reactor.</param>
    /// <param name="HullKey">The ordinary stack of the hull.</param>
    /// <param name="DriveKey">The ordinary stack of the drive.</param>
    /// <param name="ReactorKey">The ordinary stack of the reactor.</param>
    private sealed record PreparedYard(
        PlanetsTestPlayer Player,
        long DesignId,
        int HullId,
        int DriveId,
        int ReactorId,
        PartKey HullKey,
        PartKey DriveKey,
        PartKey ReactorKey) : IAsyncDisposable
    {
        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            Player.Dispose();

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// The catalog as the test reads it.
    /// </summary>
    /// <param name="Hulls">The hulls.</param>
    /// <param name="Modules">The modules.</param>
    private sealed record ShipCatalog(IReadOnlyList<ShipHull> Hulls, IReadOnlyList<ShipModule> Modules);

    /// <summary>
    /// A hull as the test reads it.
    /// </summary>
    /// <param name="Id">The hull.</param>
    /// <param name="Code">The neutral code of the hull.</param>
    /// <param name="Role">The role of the hull.</param>
    /// <param name="Slots">How many slots the hull has.</param>
    private sealed record ShipHull(int Id, string Code, string Role, int Slots);

    /// <summary>
    /// A module as the test reads it.
    /// </summary>
    /// <param name="Id">The module.</param>
    /// <param name="Component">The kind of the component.</param>
    /// <param name="Role">The role of the module.</param>
    /// <param name="EnergyUse">How much energy the module takes.</param>
    /// <param name="Power">How much power the module gives.</param>
    /// <param name="CompatibleHullIds">The hulls the module fits.</param>
    private sealed record ShipModule(
        int Id,
        string? Component,
        string Role,
        decimal EnergyUse,
        decimal Power,
        IReadOnlyList<int> CompatibleHullIds)
    {
        /// <summary>
        /// Tells whether the module goes into the hull: the hulls it names, and the role the
        /// constructor accepts.
        /// </summary>
        /// <param name="hull">The hull.</param>
        /// <returns><see langword="true"/> when the module fits the hull.</returns>
        public bool Fits(ShipHull hull) =>
            (CompatibleHullIds.Count == 0 || CompatibleHullIds.Contains(hull.Id))
            && (Role == "universal"
                || Role == hull.Role
                || (Role == "civil" && hull.Role == "transport"));
    }
}

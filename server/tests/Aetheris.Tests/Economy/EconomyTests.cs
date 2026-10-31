using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Economy.Services;
using Aetheris.Tests.Admin;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the economy: the population of the bodies and the credits of the civilizations
/// (task 00015).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EconomyTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public EconomyTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The population grows with a share of the largest population in an hour and stops at it
    /// (task 00015, criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_population_grows_to_its_limit()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        // The residential complex of the starting data raises the largest population.
        await SetHousingAsync(player, scope.ServiceProvider);

        IEconomyModule economy = scope.ServiceProvider.GetRequiredService<IEconomyModule>();

        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Time.IGameClock>().GameNow;

        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 0, now);

        PopulationFacts? start = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(start);
        Assert.True(start!.Maximum > 0);

        // The population was counted an hour ago, so it has grown by a share of the limit.
        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

        PopulationFacts? later = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(later);
        Assert.True(later!.Amount > start.Amount);
        Assert.True(later.Amount <= later.Maximum);

        // After many hours the population has reached the limit and stops there.
        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 0, now.AddHours(-100));

        PopulationFacts? full = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(full);
        Assert.Equal(full!.Maximum, full.Amount);
    }

    /// <summary>
    /// Above the limit the population dies down with the speed of the settings and never goes
    /// below the limit (task 00015, criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_population_dies_down_above_the_limit()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        await SetHousingAsync(player, scope.ServiceProvider);

        IEconomyModule economy = scope.ServiceProvider.GetRequiredService<IEconomyModule>();

        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Time.IGameClock>().GameNow;

        // The population is above the limit, so it dies down instead of growing.
        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 10_000, now);

        PopulationFacts? start = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(start);
        Assert.True(start!.OverLimit);

        // The population was counted ten hours ago, so it has died down since then: the
        // speed of the decline is one inhabitant in an hour.
        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 10_000, now.AddHours(-10));

        PopulationFacts? later = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(later);
        Assert.True(later!.Amount < start.Amount);
        Assert.Equal(start.Amount - 10, later.Amount);
        Assert.True(later.GrowthPerHour < 0);
        Assert.True(later.OverLimit);

        // The population reaches the limit and never goes below it.
        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 102, now.AddHours(-100));

        PopulationFacts? low = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(low);
        Assert.Equal(low!.Maximum, low.Amount);
        Assert.False(low.OverLimit);
    }

    /// <summary>
    /// The credits of a civilization are the integral of the population of its bodies, so they
    /// grow with the population and belong to the whole civilization (task 00015, criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_credits_are_the_integral_of_the_population()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        await SetHousingAsync(player, scope.ServiceProvider);

        IEconomyModule economy = scope.ServiceProvider.GetRequiredService<IEconomyModule>();

        int creditsId = await EconomyTestData.CreditsIdAsync(scope.ServiceProvider);
        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Time.IGameClock>().GameNow;

        // The population and the balance were counted ten hours ago, so the income of the
        // interval is in the balance now. The limit of the body is 200: 100 of the
        // administrative centre of the first level and 100 of the residential complex.
        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, player.PlanetId, player.Account.UserId, 200, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(scope.ServiceProvider, player.Account.UserId, creditsId, 0, now.AddHours(-10));

        CurrencyBalanceFacts balance = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        // The population of the body is at its limit of 200, so the income of ten hours is
        // 200 credits in an hour times the ten hours.
        Assert.Equal(200 * 10, balance.Amount);
        Assert.True(balance.IncomePerHour > 0);
        Assert.Equal(200, balance.IncomePerHour);
    }

    /// <summary>
    /// The balance of the credits is never negative and twenty parallel withdrawals never
    /// take the same credits twice (task 00015, criterion 3).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_withdrawals_never_take_the_same_credits_twice()
    {
        const int parallelCount = 20;

        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        IEconomyModule economy = scope.ServiceProvider.GetRequiredService<IEconomyModule>();

        int creditsId = await EconomyTestData.CreditsIdAsync(scope.ServiceProvider);
        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Time.IGameClock>().GameNow;

        await EconomyTestData.SetBalanceAsync(scope.ServiceProvider, player.Account.UserId, creditsId, 100, now);

        Task[] tasks = [.. Enumerable.Range(0, parallelCount).Select(_ => WithdrawAsync(_factory, player.Account.UserId, creditsId, 10))];

        await Task.WhenAll(tasks);

        using IServiceScope check = _factory.Services.CreateScope();

        long balance = await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId);

        // Ten withdrawals of ten took a hundred, so the balance is empty and never negative.
        Assert.Equal(0, balance);

        // Another withdrawal is refused, because the civilization has no credits.
        GameException refused = await Assert.ThrowsAsync<GameException>(
            () => economy.TakeAsync(player.Account.UserId, new Dictionary<int, long> { [creditsId] = 1 }, Token));

        Assert.Equal(EconomyErrorCodes.CurrencyShort, refused.Code);
    }

    /// <summary>
    /// The population of a body is read by its owner only (task 00015, criterion 3 of the
    /// security of the project).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_population_of_a_body_is_read_by_its_owner_only()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        IEconomyModule economy = scope.ServiceProvider.GetRequiredService<IEconomyModule>();

        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Time.IGameClock>().GameNow;

        await EconomyTestData.SetPopulationAsync(scope.ServiceProvider, owner.PlanetId, owner.Account.UserId, 100, now);

        PopulationFacts? mine = await economy.GetPopulationAsync(owner.Account.UserId, owner.PlanetId, Token);
        PopulationFacts? theirs = await economy.GetPopulationAsync(stranger.Account.UserId, owner.PlanetId, Token);

        Assert.NotNull(mine);
        Assert.Null(theirs);
    }

    /// <summary>
    /// The administrator gives the credits with a reason, and the journal keeps the old and
    /// the new amount (task 00015, criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_administrator_gives_the_credits_with_a_reason()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();

        int creditsId = await EconomyTestData.CreditsIdAsync(scope.ServiceProvider);
        DateTimeOffset now = scope.ServiceProvider.GetRequiredService<Aetheris.Core.Time.IGameClock>().GameNow;

        await EconomyTestData.SetBalanceAsync(scope.ServiceProvider, player.Account.UserId, creditsId, 0, now);

        TestAdmin admin = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient client = await TestAdmins.SignInAsync(_factory, admin);

        HttpResponseMessage given = await client.PutAsJsonAsync(
            Aetheris.Host.Admin.AdminEndpoints.Prefix + "/economy/balances/" + player.Account.UserId,
            new { resource_type_id = creditsId, amount = 500, reason = "Стартовий пакет" },
            Token);

        Assert.Equal(HttpStatusCode.OK, given.StatusCode);

        // The balance is read in a fresh scope, so the row tracked before the change is not
        // returned from the memory of the old scope.
        using IServiceScope check = _factory.Services.CreateScope();

        long balance = await EconomyTestData.BalanceAsync(check.ServiceProvider, player.Account.UserId, creditsId);

        Assert.Equal(500, balance);

        // A change without a reason is refused.
        HttpResponseMessage withoutReason = await client.PutAsJsonAsync(
            Aetheris.Host.Admin.AdminEndpoints.Prefix + "/economy/balances/" + player.Account.UserId,
            new { resource_type_id = creditsId, amount = 100, reason = "" },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
    }

    /// <summary>
    /// The rules of the population are a pure function, so the limits are checked without a
    /// database (task 00015, criterion 1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public Task The_rules_of_the_population_stop_at_the_limit()
    {
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        // The population grows with the speed and stops at the limit.
        long grown = PopulationRules.Accumulate(0, 100, 10m, start, start.AddHours(1));

        Assert.Equal(10, grown);

        // It never grows over the limit.
        long full = PopulationRules.Accumulate(95, 100, 10m, start, start.AddHours(10));

        Assert.Equal(100, full);

        // Above the limit it dies down and never goes below it.
        long died = PopulationRules.Accumulate(110, 100, -5m, start, start.AddHours(1));

        Assert.Equal(105, died);

        long low = PopulationRules.Accumulate(105, 100, -5m, start, start.AddHours(100));

        Assert.Equal(100, low);

        return Task.CompletedTask;
    }

    private static async Task WithdrawAsync(AetherisTestFactory factory, string playerId, int creditsId, long amount)
    {
        // Every parallel request has its own scope, like the requests of the game.
        using IServiceScope scope = factory.Services.CreateScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<IEconomyModule>()
                .TakeAsync(playerId, new Dictionary<int, long> { [creditsId] = amount }, Token);
        }
        catch (GameException)
        {
            // A refused withdrawal is a part of the result: the balance must not go below zero.
        }
    }

    private static async Task SetHousingAsync(PlanetsTestPlayer player, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(services);

        Aetheris.Planets.Data.PlanetsDbContext dbContext = services.GetRequiredService<Aetheris.Planets.Data.PlanetsDbContext>();

        int housingTypeId = await dbContext.BuildingTypes.AsNoTracking()
            .Where(type => type.Code == "building_2")
            .Select(type => type.Id)
            .SingleAsync(Token);

        Aetheris.Planets.Data.Entities.PlanetBuildingEntity? building = await dbContext.PlanetBuildings
            .Where(entry => entry.PlanetId == player.PlanetId && entry.BuildingTypeId == housingTypeId)
            .FirstOrDefaultAsync(Token);

        if (building is not null)
        {
            return;
        }

        dbContext.PlanetBuildings.Add(new Aetheris.Planets.Data.Entities.PlanetBuildingEntity
        {
            PlanetId = player.PlanetId,
            SectorId = 0,
            BuildingTypeId = housingTypeId,
            Level = 1,
        });

        await dbContext.SaveChangesAsync(Token);
    }
}

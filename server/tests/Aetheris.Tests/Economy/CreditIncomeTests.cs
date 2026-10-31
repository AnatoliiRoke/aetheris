using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Economy.Services;
using Aetheris.Planets.Data.Entities;
using Aetheris.Planets.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the income of the credits as the integral of the population: a population that
/// grows, a limit that changes in the middle of an interval and several bodies of one
/// civilization (task 00015, section 3.1, criterion 2). The expected values are counted by hand
/// from the parameters of the starting data: the growth of 5 per cent of the limit in an hour, the
/// decline of one inhabitant in an hour and one credit for an inhabitant in an hour.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class CreditIncomeTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreditIncomeTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public CreditIncomeTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The rules count the credits of a population that grows inside the interval and stops
    /// below the limit: the integral is the area of a trapezoid.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public Task The_rules_count_the_credits_of_a_growing_population()
    {
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        // The population grows from 0 with 5 inhabitants in an hour and is 50 after ten hours:
        // the average is 25, so ten hours bring 250 credits at one credit for an inhabitant.
        long grown = CreditRules.Accumulate(0, 0, 100, 5m, 1m, start, start.AddHours(10));

        Assert.Equal(250, grown);

        // At the limit the population is constant: a hundred inhabitants for ten hours.
        long constant = CreditRules.Accumulate(0, 100, 100, 5m, 1m, start, start.AddHours(10));

        Assert.Equal(1000, constant);

        // The balance the interval starts with is kept, and half a credit for an inhabitant
        // halves the income.
        long half = CreditRules.Accumulate(7, 100, 100, 5m, 0.5m, start, start.AddHours(10));

        Assert.Equal(7 + 500, half);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The rules count the credits of a population that reaches the limit in the middle of the
    /// interval: the growth up to the limit and then the constant population (the integral is
    /// not a trapezoid of the whole interval).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public Task The_rules_count_the_growth_and_then_the_limit()
    {
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        // The population grows from 0 with 5 in an hour and reaches the limit of 100 after twenty
        // hours; the first twenty hours bring 0.5 * 20 * 100 = 1000 credits, and the next ten
        // hours at the limit bring 100 * 10 = 1000 credits.
        long credits = CreditRules.Accumulate(0, 0, 100, 5m, 1m, start, start.AddHours(30));

        Assert.Equal(2000, credits);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The rules count the credits of a population above the limit: it dies down by one
    /// inhabitant in an hour until the limit and then stays there.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public Task The_rules_count_the_credits_of_a_population_that_dies_down()
    {
        DateTimeOffset start = DateTimeOffset.UnixEpoch;

        // Five hours of the decline from 110 to 105: the average is 107.5, so 537.5 credits,
        // and the fraction of a credit is left out.
        long shortInterval = CreditRules.Accumulate(0, 110, 100, -1m, 1m, start, start.AddHours(5));

        Assert.Equal(537, shortInterval);

        // Twenty hours: the population dies down from 110 to 100 in ten hours (the average of
        // 105 brings 1050 credits) and then stays at 100 for ten hours (1000 credits).
        long longInterval = CreditRules.Accumulate(0, 110, 100, -1m, 1m, start, start.AddHours(20));

        Assert.Equal(2050, longInterval);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The credits of a civilization follow a population that grows during the interval: the
    /// balance is the integral of the growing population and not the income of the population of
    /// the end of the interval.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_credits_follow_a_population_that_grows()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // The limit of the body is 200: 100 of the administrative centre of the first level and
        // 100 of the residential complex of the first level.
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        // The population was empty ten hours ago (the start of a civilization has 50, so the empty
        // start is written explicitly) and grows by 5 per cent of the limit of 200, which is 10
        // in an hour: it is 100 now, so the average of the interval is 50 and the balance is 500
        // credits.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, now.AddHours(-10));

        PopulationFacts? population = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);
        CurrencyBalanceFacts balance = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.NotNull(population);
        Assert.Equal(100, population!.Amount);
        Assert.Equal(500, balance.Amount);

        // The income of the moment is the one of the population of the moment.
        Assert.Equal(100, balance.IncomePerHour);
    }

    /// <summary>
    /// The credits of a civilization follow a population that reaches the limit in the middle
    /// of the interval and then stays at it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_credits_follow_the_growth_and_then_the_limit()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        // The population was empty thirty hours ago and reaches the limit of 200 after twenty
        // hours (5 per cent of 200 is 10 in an hour): the first twenty hours bring
        // 0.5 * 20 * 200 = 2000 credits, the last ten hours at the limit bring 200 * 10 = 2000
        // credits.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-30));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, now.AddHours(-30));

        CurrencyBalanceFacts balance = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(4000, balance.Amount);
        Assert.Equal(200, balance.IncomePerHour);
    }

    /// <summary>
    /// The credits of a civilization follow a population above the limit that dies down to it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_credits_follow_a_population_that_dies_down_to_the_limit()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        // The population of 210 (ten above the limit of 200) was counted twenty hours ago; it dies
        // down by one in an hour, reaches the limit of 200 after ten hours (the average of 205
        // brings 2050 credits) and stays there for ten hours (2000 credits).
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 210, now.AddHours(-20));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, now.AddHours(-20));

        CurrencyBalanceFacts balance = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(4050, balance.Amount);
    }

    /// <summary>
    /// The limit of the population changes in the middle of the interval: the balance is fixed
    /// before the change, so the income before it is counted with the old limit and the income
    /// after it grows with the new one (task 00015, criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_change_of_the_limit_in_the_middle_of_an_interval_applies_from_that_moment()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;
        DateTimeOffset start = now.AddHours(-10);
        DateTimeOffset change = start.AddHours(6);

        // The limit is 200 (100 of the administrative centre and 100 of the residential complex of
        // the first level). The population is at it and the balance is empty ten hours ago.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 200, start);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, start);

        // Six hours later the second level of the complex is built: the balance is fixed to that
        // moment first, as a change of the limit asks for.
        await economy.FixPlayerAsync(player.Account.UserId, change, Token);

        long creditsBefore = await EconomyTestData.BalanceAsync(services, player.Account.UserId, creditsId);

        // Six hours of 200 inhabitants bring 1200 credits.
        Assert.Equal(1200, creditsBefore);

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 2);

        // The second level of the complex has 110 in the data, so the new limit is 100 + 110 = 210.
        decimal adminLimit = await EconomyTestData.EffectValueAsync(services, SeedCodes.AdminCenter, 1, EconomyCodes.PopulationMaxEffect);
        decimal housingLimit = await EconomyTestData.EffectValueAsync(services, SeedCodes.ResidentialComplex, 2, EconomyCodes.PopulationMaxEffect);

        Assert.Equal(210m, adminLimit + housingLimit);

        // The growth is 5 per cent of 210, which is 10.5 in an hour, so the population grows from
        // 200 to the limit of 210 in 10 / 10.5 = 20/21 hours. The area until the limit and the rest
        // of the four hours at the limit are 835.238 credits, counted as 835 whole credits.
        CurrencyBalanceFacts balance = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(1200 + 835, balance.Amount);
    }

    /// <summary>
    /// A construction that raises the limit of the population completes in the middle of the
    /// interval: the credits of the time before its end are counted with the old limit, because
    /// the completion fixes the balance of the civilization first (task 00015, section 3.1: the
    /// balance is fixed before every change of the rate).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_completion_of_a_building_fixes_the_credits_before_the_limit_grows()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.AdminCenter, 3);
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        BuildingTypeEntity housing = await PlanetsTestData.TypeAsync(services, SeedCodes.ResidentialComplex);
        long duration = housing.LevelIds.Single(level => level.Level == 2).DurationSeconds;

        DateTimeOffset start = ConstructionService.Normalize(services.GetRequiredService<IGameClock>().GameNow.AddHours(-10));
        DateTimeOffset change = start.AddHours(6);

        // The limit is 221: 121 of the administrative centre of the third level and 100 of the
        // residential complex of the first level. The population is at it and the balance is empty
        // ten hours ago.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 221, start);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, start);
        Dictionary<int, long> richBalance = await BuildingsTests.RichBalanceAsync(services);
        await PlanetsTestData.SetBalanceAsync(services, player.PlanetId, richBalance, start);

        // The second level of the complex is queued so that it ends six hours after the start.
        using (IServiceScope call = _factory.Services.CreateScope())
        {
            await call.ServiceProvider.GetRequiredService<ConstructionService>()
                .EnqueueAsync(player.PlanetId, housing.Id, change.AddSeconds(-duration), Token);
        }

        using (IServiceScope call = _factory.Services.CreateScope())
        {
            int completed = await call.ServiceProvider.GetRequiredService<ConstructionService>()
                .CompleteDueAsync(player.PlanetId, change, Token);

            Assert.Equal(1, completed);
        }

        // The second level of the complex has 110 in the data, so the new limit is 121 + 110 = 231.
        decimal adminLimit = await EconomyTestData.EffectValueAsync(services, SeedCodes.AdminCenter, 3, EconomyCodes.PopulationMaxEffect);
        decimal housingLimit = await EconomyTestData.EffectValueAsync(services, SeedCodes.ResidentialComplex, 2, EconomyCodes.PopulationMaxEffect);

        Assert.Equal(231m, adminLimit + housingLimit);

        // Paying for the level counts the income up to the present first: 221 inhabitants are
        // already at the old limit, so ten hours bring 2210 credits, and the credit cost of the
        // level is taken from that. The completion itself is in the past and adds nothing more.
        long creditCost = housing.LevelIds.Single(level => level.Level == 2).CostIds
            .Where(cost => cost.ResourceTypeId == creditsId)
            .Sum(cost => cost.Amount);

        using IServiceScope check = _factory.Services.CreateScope();
        CurrencyBalanceFacts balance = await check.ServiceProvider.GetRequiredService<IEconomyModule>().GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(richBalance[creditsId] + (221 * 10) - creditCost, balance.Amount);
    }

    /// <summary>
    /// The credits are one balance of the civilization and are the sum of the income of all its
    /// bodies (task 00015, section 3.1).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_credits_are_summed_over_the_bodies_of_a_civilization()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer neighbour = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // The second body becomes a colony of the player.
        await EconomyTestData.GiveBodyAsync(services, neighbour.PlanetId, player.Account.UserId);

        // The home body has the first level of the residential complex, the colony the second.
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);
        await PlanetsTestData.SetLevelAsync(services, neighbour.PlanetId, SeedCodes.ResidentialComplex, 2);

        // Both bodies have the administrative centre of the first level, 100 inhabitants, and the
        // residential complex adds 100 on the first level and 110 on the second: the limits of the
        // bodies are 200 and 210.
        decimal adminLimit = await EconomyTestData.EffectValueAsync(services, SeedCodes.AdminCenter, 1, EconomyCodes.PopulationMaxEffect);
        long homeLimit = (long)(adminLimit + await EconomyTestData.EffectValueAsync(services, SeedCodes.ResidentialComplex, 1, EconomyCodes.PopulationMaxEffect));
        long colonyLimit = (long)(adminLimit + await EconomyTestData.EffectValueAsync(services, SeedCodes.ResidentialComplex, 2, EconomyCodes.PopulationMaxEffect));

        Assert.Equal(200, homeLimit);
        Assert.Equal(210, colonyLimit);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;
        DateTimeOffset start = now.AddHours(-10);

        // Both bodies are full: the home body brings its limit in credits in an hour, the colony
        // its own, so ten hours bring ten times the sum of the limits, 10 * (200 + 210) = 4100.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, homeLimit, start);
        await EconomyTestData.SetPopulationAsync(services, neighbour.PlanetId, player.Account.UserId, colonyLimit, start);
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, start);

        // Founding gave the earlier owner the start package. That grant is not income of the colony.
        await EconomyTestData.SetBalanceAsync(services, neighbour.Account.UserId, creditsId, 0, start);

        CurrencyBalanceFacts balance = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(10 * (homeLimit + colonyLimit), balance.Amount);
        Assert.Equal(homeLimit + colonyLimit, balance.IncomePerHour);

        // The earlier owner of the second body has no part in the income of it.
        CurrencyBalanceFacts former = await economy.GetBalanceAsync(neighbour.Account.UserId, creditsId, Token);

        Assert.Equal(0, former.Amount);
        Assert.Equal(0, former.IncomePerHour);
    }

    /// <summary>
    /// After the population has died down to the limit, the warning "above the limit" is gone
    /// and the population is not declining any more (task 00015, decision 6 of the result).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_warning_above_the_limit_goes_when_the_population_reaches_the_limit()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        // The limit is 200 (100 of the administrative centre and 100 of the residential complex).
        // Counted an hour ago, 203 inhabitants above the limit are 202 now, still above.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 203, now.AddHours(-1));

        PopulationFacts? above = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(above);
        Assert.Equal(202, above!.Amount);
        Assert.True(above.OverLimit);
        Assert.Equal(-1m, above.GrowthPerHour);

        // Counted three hours ago, they are 200 now: at the limit, with no warning and no
        // decline, and the population needs no time to reach the limit.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 203, now.AddHours(-3));

        PopulationFacts? at = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(at);
        Assert.Equal(200, at!.Amount);
        Assert.False(at.OverLimit);
        Assert.True(at.GrowthPerHour >= 0m);
        Assert.Equal(TimeSpan.Zero, at.TimeToMaximum);
    }
}

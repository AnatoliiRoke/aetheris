using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Economy.Services;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the bonuses of the economy: the medical centre and the technologies change the
/// growth of the population and the income of the credits (task 00015, section 3.1). The
/// expected values are counted by hand from the parameters of the starting data: the growth of
/// 5 per cent of the limit in an hour and one credit for an inhabitant in an hour.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class EconomyBonusTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyBonusTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public EconomyBonusTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The medical centre multiplies the growth of the population of its body by the value of its
    /// effect, and a body without it grows with the base speed.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_medical_centre_raises_the_growth_of_the_population()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        // The limit of the body is 100, so the base growth is 5 inhabitants in an hour.
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

        PopulationFacts? plain = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(plain);
        Assert.Equal(5m, plain!.GrowthPerHour);
        Assert.Equal(5, plain.Amount);

        // The medical centre of the first level has the multiplier of its effect in the data; the
        // growth in an hour is the limit times 5 per cent times that multiplier.
        long bonus = await EconomyTestData.EffectValueAsync(
            services,
            EconomyTestData.MedicalCentreCode,
            1,
            EconomyCodes.PopulationGrowthBonusEffect);

        Assert.True(bonus > 1);

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, EconomyTestData.MedicalCentreCode, 1);
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

        PopulationFacts? boosted = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        decimal expectedGrowth = 100m * 0.05m * bonus;

        Assert.NotNull(boosted);
        Assert.Equal(expectedGrowth, boosted!.GrowthPerHour);
        Assert.Equal((long)expectedGrowth, boosted.Amount);
        Assert.True(boosted.Amount > plain.Amount);
    }

    /// <summary>
    /// A technology with the bonus of the growth multiplies the growth of the population of every
    /// body of its player, and a body of another player is not touched.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_raises_the_growth_of_the_population()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer other = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);
        await PlanetsTestData.SetLevelAsync(services, other.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        // The technology multiplies the growth by 1.5: 100 * 0.05 * 1.5 = 7.5 inhabitants in an
        // hour, 15 in two hours.
        await EconomyTestData.GiveTechnologyAsync(services, player.Account.UserId, TechnologyEffectTypes.PopulationGrowthBonus, 1.5m);

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-2));
        await EconomyTestData.SetPopulationAsync(services, other.PlanetId, other.Account.UserId, 0, now.AddHours(-2));

        PopulationFacts? mine = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);
        PopulationFacts? theirs = await economy.GetPopulationAsync(other.Account.UserId, other.PlanetId, Token);

        Assert.NotNull(mine);
        Assert.Equal(7.5m, mine!.GrowthPerHour);
        Assert.Equal(15, mine.Amount);

        // The other player has not learned it: the base growth of 5 in an hour, 10 in two hours.
        Assert.NotNull(theirs);
        Assert.Equal(5m, theirs!.GrowthPerHour);
        Assert.Equal(10, theirs.Amount);
    }

    /// <summary>
    /// A technology with the bonus of the income multiplies the credits of its player: both the
    /// balance and the income in an hour grow by the bonus once.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_technology_raises_the_income_of_the_credits()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        // The body is full at the limit of 100 for the ten hours: a hundred credits in an hour
        // without the bonus.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 100, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, now.AddHours(-10));

        CurrencyBalanceFacts plain = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(1000, plain.Amount);
        Assert.Equal(100m, plain.IncomePerHour);

        // The technology multiplies the income by 1.5: 150 credits in an hour, 1500 in ten hours.
        await EconomyTestData.GiveTechnologyAsync(services, player.Account.UserId, TechnologyEffectTypes.CreditIncomeBonus, 1.5m);

        CurrencyBalanceFacts boosted = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(1500, boosted.Amount);
        Assert.Equal(150m, boosted.IncomePerHour);
    }
}

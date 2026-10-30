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

        // The limit of the body is 200 (100 of the administrative centre and 100 of the residential
        // complex), so the base growth is 5 per cent of it, 10 inhabitants in an hour. The start of a
        // civilization has 50 inhabitants, so the empty start is written explicitly.
        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, SeedCodes.ResidentialComplex, 1);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

        PopulationFacts? plain = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(plain);
        Assert.Equal(10m, plain!.GrowthPerHour);
        Assert.Equal(10, plain.Amount);

        // The medical centre of the first level has the multiplier 1.2 in the data; the growth in an
        // hour is the limit times 5 per cent times that multiplier: 200 * 0.05 * 1.2 = 12 inhabitants.
        decimal bonus = await EconomyTestData.EffectValueAsync(
            services,
            EconomyTestData.MedicalCentreCode,
            1,
            EconomyCodes.PopulationGrowthBonusEffect);

        Assert.Equal(1.2m, bonus);

        await PlanetsTestData.SetLevelAsync(services, player.PlanetId, EconomyTestData.MedicalCentreCode, 1);
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

        PopulationFacts? boosted = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        decimal expectedGrowth = 200m * 0.05m * bonus;

        Assert.NotNull(boosted);
        Assert.Equal(expectedGrowth, boosted!.GrowthPerHour);
        Assert.Equal(12, boosted.Amount);
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

        // The limit of every body is 200 (100 of the administrative centre and 100 of the
        // residential complex). The technology multiplies the growth by 1.5: 200 * 0.05 * 1.5 = 15
        // inhabitants in an hour, 30 in two hours. The start of a civilization has 50 inhabitants,
        // so the empty start is written explicitly.
        await EconomyTestData.GiveTechnologyAsync(services, player.Account.UserId, TechnologyEffectTypes.PopulationGrowthBonus, 1.5m);

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-2));
        await EconomyTestData.SetPopulationAsync(services, other.PlanetId, other.Account.UserId, 0, now.AddHours(-2));

        PopulationFacts? mine = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);
        PopulationFacts? theirs = await economy.GetPopulationAsync(other.Account.UserId, other.PlanetId, Token);

        Assert.NotNull(mine);
        Assert.Equal(15m, mine!.GrowthPerHour);
        Assert.Equal(30, mine.Amount);

        // The other player has not learned it: the base growth of 10 in an hour, 20 in two hours.
        Assert.NotNull(theirs);
        Assert.Equal(10m, theirs!.GrowthPerHour);
        Assert.Equal(20, theirs.Amount);
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

        // The body is full at the limit of 200 (100 of the administrative centre and 100 of the
        // residential complex) for the ten hours: 200 credits in an hour without the bonus.
        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 200, now.AddHours(-10));
        await EconomyTestData.SetBalanceAsync(services, player.Account.UserId, creditsId, 0, now.AddHours(-10));

        CurrencyBalanceFacts plain = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(2000, plain.Amount);
        Assert.Equal(200m, plain.IncomePerHour);

        // The technology multiplies the income by 1.5: 300 credits in an hour, 3000 in ten hours.
        await EconomyTestData.GiveTechnologyAsync(services, player.Account.UserId, TechnologyEffectTypes.CreditIncomeBonus, 1.5m);

        CurrencyBalanceFacts boosted = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.Equal(3000, boosted.Amount);
        Assert.Equal(300m, boosted.IncomePerHour);
    }
}

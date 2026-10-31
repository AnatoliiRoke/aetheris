using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Economy.Configuration;
using Aetheris.Economy.Services;
using Aetheris.Planets.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.Tests.World;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Tests of the start capital of a civilization and of the medical centre with a multiplier for
/// every level (task 00015, the answers of the author of 30.10.2026). The expected values are
/// read from the tables of the starting data and the parameters, not from a formula of the code.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class StartCapitalTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartCapitalTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public StartCapitalTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A new civilization has the population of the start capital, the largest population of its
    /// start building and a credit income from the first moment, and the population does not
    /// exceed the largest population (so it does not die down at once).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_civilization_has_the_population_the_limit_and_the_income_from_the_start()
    {
        await EconomyTestData.ResetAsync(_factory);

        string startPopulation = EconomyConfigKeys.Definitions.Single(definition => definition.Key == EconomyConfigKeys.PopulationInitial).DefaultValue;

        using (IServiceScope configuration = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(configuration.ServiceProvider, EconomyConfigKeys.PopulationInitial, startPopulation);
        }

        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();

        PopulationFacts? population = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(population);
        Assert.Equal(long.Parse(startPopulation), population!.Amount);
        Assert.True(population.Amount > 0);

        // The only building of the start is the administrative centre, and it gives the limit.
        decimal startLimit = await EconomyTestData.EffectValueAsync(services, SeedCodes.AdminCenter, 1, EconomyCodes.PopulationMaxEffect);

        Assert.True(startLimit > 0);
        Assert.Equal(startLimit, population.Maximum);
        Assert.True(population.Amount <= population.Maximum);
        Assert.False(population.OverLimit);

        int creditsId = await EconomyTestData.CreditsIdAsync(services);
        CurrencyBalanceFacts credits = await economy.GetBalanceAsync(player.Account.UserId, creditsId, Token);

        Assert.True(credits.IncomePerHour > 0);
    }

    /// <summary>
    /// The multiplier of the medical centre is a row of data for every level, with its fraction
    /// kept: the levels of the starting data hold the values of the author, and the population
    /// grows with the multiplier of the level the body has.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_medical_centre_holds_a_fractional_multiplier_for_every_level()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        BuildingTypeEntity medicalCentre = await PlanetsTestData.TypeAsync(services, EconomyTestData.MedicalCentreCode);
        decimal[] multipliers = [.. medicalCentre.LevelIds
            .OrderBy(level => level.Level)
            .Select(level => level.EffectIds.Single(effect => effect.EffectType == EconomyCodes.PopulationGrowthBonusEffect).Value)];

        // The values of the author for the first six levels.
        Assert.Equal([100m, 110m, 121m, 133m, 146m, 161m], multipliers[..6]);
        Assert.Equal(multipliers.OrderBy(value => value), multipliers);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        DateTimeOffset now = services.GetRequiredService<IGameClock>().GameNow;

        await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

        PopulationFacts? plain = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(plain);

        // Without the medical centre the multiplier is one: the growth is the share of the limit.
        decimal baseGrowth = plain!.GrowthPerHour;

        Assert.True(baseGrowth > 0m);

        foreach (int level in new[] { 1, 6 })
        {
            await PlanetsTestData.SetLevelAsync(services, player.PlanetId, EconomyTestData.MedicalCentreCode, level);
            await EconomyTestData.SetPopulationAsync(services, player.PlanetId, player.Account.UserId, 0, now.AddHours(-1));

            PopulationFacts? boosted = await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token);

            Assert.NotNull(boosted);
            Assert.Equal(baseGrowth * multipliers[level - 1], boosted!.GrowthPerHour);
        }
    }
}

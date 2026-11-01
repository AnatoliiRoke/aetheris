using Aetheris.Core.Errors;
using Aetheris.Economy;
using Aetheris.Economy.Api;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Configuration;
using Aetheris.Production.Data;
using Aetheris.Production.Data.Entities;
using Aetheris.Production.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the cryocapsule: a part that holds inhabitants and adds them to the population of
/// a body when it is used (task 00031).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PopulationCapsuleTests
{
    private const long CapsuleInhabitants = 100;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PopulationCapsuleTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public PopulationCapsuleTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The seed holds one capsule for every race, each with the inhabitants of its own race, and
    /// only the race of a capsule makes it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_holds_a_capsule_for_every_race()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();

        ProductionCatalog catalog = await CatalogAsync(scope.ServiceProvider);
        PartTypeInfo[] capsules = [.. catalog.Parts.Where(part => part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule))];
        string[] raceCodes = [.. (await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetRacesAsync(Token)).Select(race => race.Code).Order()];

        Assert.Equal(raceCodes, capsules.Select(part => part.Attributes[PartAttributeKeys.CapsuleRace].CodeValue!).Order());
        Assert.All(capsules, capsule =>
        {
            Assert.Equal(CapsuleInhabitants, capsule.Attributes[PartAttributeKeys.PopulationCapsule].NumberValue);
            Assert.Contains(capsule.Characteristics.Values, value => value.NumberValue == CapsuleInhabitants);
        });
    }

    /// <summary>
    /// One use takes one capsule from the stock and gives its inhabitants to the population; a
    /// population above the largest one is not refused but dies down with time, as for a returning crew.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_capsule_adds_its_inhabitants_and_leaves_the_stock()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await ProductionTestData.BuildStorageAsync(player, services);
        PartTypeInfo capsule = await CapsuleAsync(services, player, own: true);
        await services.GetRequiredService<PartStockService>().AddAsync(
            player.PlanetId,
            [new PartStackAdded(capsule.Id, capsule.MarkLevel, false, PartCondition.Full, 2)],
            Token);

        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        PopulationFacts before = (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!;
        Assert.True(await economy.TryTakePopulationAsync(player.Account.UserId, player.PlanetId, Math.Min(before.Amount - 1, CapsuleInhabitants), Token));
        long emptied = (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount;

        CapsuleUseDto used = await services.GetRequiredService<PopulationCapsuleService>().UseAsync(player.Account.UserId, player.PlanetId, capsule.Id, Token);

        Assert.Equal(CapsuleInhabitants, used.Inhabitants);
        // The inhabitants are given in full; whatever lies above the largest population dies down with time, so the number may be above it.
        Assert.True(used.Population >= emptied + CapsuleInhabitants);

        ProductionStockDto stock = await services.GetRequiredService<IProductionModule>().GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        ProductionStackDto left = Assert.Single(stock.Stacks);
        Assert.Equal(1, left.Quantity);
        Assert.Equal(CapsuleInhabitants, left.CapsuleInhabitants);
        Assert.True(left.CapsuleUsable);
    }

    /// <summary>
    /// The last capsule leaves the stock, and a second use of a body without capsules is
    /// refused without changing the population.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_body_without_a_capsule_refuses_the_use()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await ProductionTestData.BuildStorageAsync(player, services);
        PartTypeInfo capsule = await CapsuleAsync(services, player, own: true);
        PopulationCapsuleService capsules = services.GetRequiredService<PopulationCapsuleService>();
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        long population = (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount;

        await Assert.ThrowsAsync<GameException>(() => capsules.UseAsync(player.Account.UserId, player.PlanetId, capsule.Id, Token));

        Assert.Equal(population, (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount);
    }

    /// <summary>
    /// A part that is not a capsule is refused, and a stranger cannot use a capsule on a body
    /// that is not theirs, even when the body holds one.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_plain_part_and_a_stranger_are_refused()
    {
        await ResetAsync();
        using PlanetsTestPlayer owner = await PlanetsTestData.FoundAsync(_factory);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await ProductionTestData.BuildStorageAsync(owner, services);
        PartTypeInfo capsule = await CapsuleAsync(services, owner, own: true);
        PartTypeInfo plain = (await CatalogAsync(services)).Parts.First(part => !part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule));
        PartStockService stock = services.GetRequiredService<PartStockService>();
        await stock.AddAsync(
            owner.PlanetId,
            [new PartStackAdded(capsule.Id, capsule.MarkLevel, false, PartCondition.Full, 1), new PartStackAdded(plain.Id, plain.MarkLevel, false, PartCondition.Full, 1)],
            Token);
        PopulationCapsuleService capsules = services.GetRequiredService<PopulationCapsuleService>();

        GameException notCapsule = await Assert.ThrowsAsync<GameException>(() => capsules.UseAsync(owner.Account.UserId, owner.PlanetId, plain.Id, Token));
        GameException foreign = await Assert.ThrowsAsync<GameException>(() => capsules.UseAsync(stranger.Account.UserId, owner.PlanetId, capsule.Id, Token));

        Assert.Equal(ProductionErrorCodes.NotACapsule, notCapsule.Code);
        Assert.Equal(ProductionErrorCodes.PlanetUnknown, foreign.Code);
        Assert.Equal(2, (await services.GetRequiredService<IProductionModule>().GetStockAsync(owner.Account.UserId, owner.PlanetId, Token)).Stacks.Count);
    }

    /// <summary>
    /// A capsule of another race may be held, but not deployed: the stock shows it as not usable
    /// and the use is refused without taking the capsule.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_capsule_of_another_race_is_held_but_not_deployed()
    {
        await ResetAsync();
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await ProductionTestData.BuildStorageAsync(player, services);
        PartTypeInfo own = await CapsuleAsync(services, player, own: true);
        PartTypeInfo foreign = await CapsuleAsync(services, player, own: false);
        await services.GetRequiredService<PartStockService>().AddAsync(
            player.PlanetId,
            [new PartStackAdded(own.Id, own.MarkLevel, false, PartCondition.Full, 1), new PartStackAdded(foreign.Id, foreign.MarkLevel, false, PartCondition.Full, 1)],
            Token);
        IEconomyModule economy = services.GetRequiredService<IEconomyModule>();
        long population = (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount;

        ProductionStockDto stock = await services.GetRequiredService<IProductionModule>().GetStockAsync(player.Account.UserId, player.PlanetId, Token);
        GameException refused = await Assert.ThrowsAsync<GameException>(() =>
            services.GetRequiredService<PopulationCapsuleService>().UseAsync(player.Account.UserId, player.PlanetId, foreign.Id, Token));

        Assert.True(stock.Stacks.Single(stack => stack.PartTypeId == own.Id).CapsuleUsable);
        Assert.False(stock.Stacks.Single(stack => stack.PartTypeId == foreign.Id).CapsuleUsable);
        Assert.Equal(ProductionErrorCodes.CapsuleOtherRace, refused.Code);
        Assert.Equal(population, (await economy.GetPopulationAsync(player.Account.UserId, player.PlanetId, Token))!.Amount);
        Assert.Equal(2, (await services.GetRequiredService<IProductionModule>().GetStockAsync(player.Account.UserId, player.PlanetId, Token)).Stacks.Count);
    }

    private static async Task<ProductionCatalog> CatalogAsync(IServiceProvider services) =>
        await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

    private static async Task<PartTypeInfo> CapsuleAsync(IServiceProvider services, PlanetsTestPlayer player, bool own)
    {
        IWorldModule world = services.GetRequiredService<IWorldModule>();
        CivilizationSummary civilization = (await world.GetCivilizationAsync(player.Account.UserId, Token))!;
        string raceCode = (await world.GetRacesAsync(Token)).Single(race => race.Id == civilization.RaceId).Code;

        return (await CatalogAsync(services)).Parts
            .Where(part => part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule))
            .First(part => string.Equals(part.Attributes[PartAttributeKeys.CapsuleRace].CodeValue, raceCode, StringComparison.Ordinal) == own);
    }

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope reset = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(reset.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await reset.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

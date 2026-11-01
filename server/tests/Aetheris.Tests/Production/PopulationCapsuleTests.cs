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
    /// The seed holds one capsule whose attribute and characteristic carry the number of the
    /// inhabitants, and the stock tells the client how many inhabitants a part of the stack adds.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_holds_the_capsule_with_its_inhabitants()
    {
        await ResetAsync();
        using IServiceScope scope = _factory.Services.CreateScope();

        PartTypeInfo[] capsules = [.. (await CatalogAsync(scope.ServiceProvider)).Parts.Where(part => part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule))];

        PartTypeInfo capsule = Assert.Single(capsules);
        Assert.Equal(CapsuleInhabitants, capsule.Attributes[PartAttributeKeys.PopulationCapsule].NumberValue);
        Assert.Contains(capsule.Characteristics.Values, value => value.NumberValue == CapsuleInhabitants);
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
        PartTypeInfo capsule = await CapsuleAsync(services);
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
        PartTypeInfo capsule = await CapsuleAsync(services);
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
        PartTypeInfo capsule = await CapsuleAsync(services);
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

    private static async Task<ProductionCatalog> CatalogAsync(IServiceProvider services) =>
        await services.GetRequiredService<ProductionCatalogCache>()
            .GetAsync(
                services.GetRequiredService<ProductionDbContext>(),
                services.GetRequiredService<Aetheris.Requirements.IRequirementsModule>(),
                Token);

    private static async Task<PartTypeInfo> CapsuleAsync(IServiceProvider services) =>
        (await CatalogAsync(services)).Parts.Single(part => part.Attributes.ContainsKey(PartAttributeKeys.PopulationCapsule));

    private async Task ResetAsync()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope reset = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(reset.ServiceProvider, ProductionConfigKeys.CatalogCacheSeconds, "0");
        await reset.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
    }
}

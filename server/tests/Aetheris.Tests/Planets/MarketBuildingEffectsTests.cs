using Aetheris.Planets;
using Aetheris.Planets.Services;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the effects of the buildings of the market (task 00024, section 4.1): the number of
/// the lots of a civilization and the speed of the delivery through the portal come from the
/// level of the building, and the effect reaches the game as data alone.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class MarketBuildingEffectsTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketBuildingEffectsTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public MarketBuildingEffectsTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A body without the trade centre and without the portal has no lot and no speed: the
    /// effects of the market come from the levels of the buildings of the body, and a body
    /// without them has none.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_body_without_the_buildings_of_the_market_has_no_lot_and_no_speed()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IPlanetsModule planets = services.GetRequiredService<IPlanetsModule>();

        Assert.Equal(0, await planets.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.MarketLotLimit, Token));
        Assert.Equal(0, await planets.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.PortalDeliveryFactor, Token));
    }

    /// <summary>
    /// The trade centre gives the number of the lots and the portal gives the speed of the
    /// delivery: both come from the level of the building of the body, and the game reads them
    /// through the contract of the bodies module.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_trade_centre_and_the_portal_give_the_lot_limit_and_the_delivery_speed()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IPlanetsModule planets = services.GetRequiredService<IPlanetsModule>();

        await EconomyTestData.BuildAsync(player, services, BuildingClasses.TradeCenter);
        await EconomyTestData.BuildAsync(player, services, BuildingClasses.Portal);

        // The first level of the trade centre gives two lots, the first level of the portal
        // gives the speed of one.
        Assert.Equal(2, await planets.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.MarketLotLimit, Token));
        Assert.Equal(1, await planets.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.PortalDeliveryFactor, Token));

        // The second level of the trade centre adds one lot and the second level of the portal
        // adds half of the speed.
        await SetLevelAsync(services, player.PlanetId, BuildingClasses.TradeCenter, 2);
        await SetLevelAsync(services, player.PlanetId, BuildingClasses.Portal, 2);

        Assert.Equal(3, await planets.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.MarketLotLimit, Token));
        Assert.Equal(2, await planets.GetBuildingEffectTotalAsync(player.PlanetId, BuildingEffectTypes.PortalDeliveryFactor, Token));
    }

    /// <summary>
    /// The effect of the market reaches the game as data alone: a level of the trade centre
    /// written straight into the tables, without the seed and without the admin panel, gives its
    /// lots to the body (the key test of the thesis of the author).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_effect_added_only_to_the_tables_reaches_the_game()
    {
        await EconomyTestData.ResetAsync(_factory);
        using PlanetsTestPlayer player = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        IPlanetsModule planets = services.GetRequiredService<IPlanetsModule>();

        int? buildingTypeId = await planets.FindBuildingTypeIdByClassAsync(BuildingClasses.TradeCenter, Token);

        Assert.NotNull(buildingTypeId);

        await SetLevelAsync(services, player.PlanetId, BuildingClasses.TradeCenter, 1);

        // The effect of the level of the trade centre is in the answer of the body.
        global::Aetheris.Planets.Api.PlanetOverviewDto? overview = await planets.GetPlanetAsync(player.Account.UserId, player.PlanetId, Token);

        Assert.NotNull(overview);
        Assert.Contains(
            overview.Buildings,
            building => building.BuildingTypeId == buildingTypeId && building.Effects.Any(effect => effect.EffectType == BuildingEffectTypes.MarketLotLimit));
    }

    /// <summary>
    /// Puts a building of a class on a body at the given level.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="buildingClass">The class of the building.</param>
    /// <param name="level">The level of the building.</param>
    /// <returns>A task that completes when the building is written.</returns>
    private static async Task SetLevelAsync(IServiceProvider services, long planetId, string buildingClass, int level)
    {
        IPlanetsModule planets = services.GetRequiredService<IPlanetsModule>();
        int? typeId = await planets.FindBuildingTypeIdByClassAsync(buildingClass, Token);

        Assert.NotNull(typeId);

        global::Aetheris.Planets.Data.PlanetsDbContext dbContext = services.GetRequiredService<global::Aetheris.Planets.Data.PlanetsDbContext>();

        global::Aetheris.Planets.Data.Entities.PlanetBuildingEntity? building = await dbContext.PlanetBuildings
            .Where(entry => entry.PlanetId == planetId && entry.BuildingTypeId == typeId)
            .FirstOrDefaultAsync(Token)
            .ConfigureAwait(false);

        if (building is null)
        {
            building = new global::Aetheris.Planets.Data.Entities.PlanetBuildingEntity
            {
                PlanetId = planetId,
                SectorId = 0,
                BuildingTypeId = typeId.Value,
                Level = level,
            };

            dbContext.PlanetBuildings.Add(building);
        }
        else
        {
            building.Level = level;
        }

        await dbContext.SaveChangesAsync(Token).ConfigureAwait(false);

        // The catalog of the buildings keeps the levels in memory, so it must read them again.
        planets.InvalidateCatalog();
    }
}

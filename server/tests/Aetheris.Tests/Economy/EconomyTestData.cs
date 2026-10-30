using Aetheris.Core.Time;
using Aetheris.Economy.Data;
using Aetheris.Economy.Data.Entities;
using Aetheris.Economy.Services;
using Aetheris.Planets.Services;
using Aetheris.Research;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Economy;

/// <summary>
/// Helpers of the tests of the economy: the population of a body, the balances of a
/// civilization and the passage of the game time (task 00015).
/// </summary>
internal static class EconomyTestData
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The code of the currency of the civilization in the starting data.
    /// </summary>
    internal const string CreditsCode = "resource_18";

    /// <summary>
    /// The code of the medical centre in the starting data.
    /// </summary>
    internal const string MedicalCentreCode = "building_25";

    /// <summary>
    /// Clears the database, creates the world and prepares the modules, as a fresh server does
    /// at start.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    internal static async Task ResetAsync(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await PlanetsTestData.ResetAsync(factory);

        using IServiceScope scope = factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, Aetheris.Economy.Configuration.EconomyConfigKeys.PopulationGrowthPercent, "5");
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, Aetheris.Economy.Configuration.EconomyConfigKeys.PopulationDeclinePerHour, "1");
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, Aetheris.Economy.Configuration.EconomyConfigKeys.CreditsPerInhabitant, "1");
    }

    /// <summary>
    /// Gets the identifier of the currency of the civilization.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <returns>The identifier.</returns>
    internal static async Task<int> CreditsIdAsync(IServiceProvider services)
    {
        ResourceCatalog catalog = await services.GetRequiredService<Aetheris.World.IWorldModule>()
            .GetResourceCatalogAsync(Token)
            .ConfigureAwait(false);

        return catalog.Resources
            .Single(resource => string.Equals(resource.Code, CreditsCode, StringComparison.Ordinal))
            .Id;
    }

    /// <summary>
    /// Puts the population of a body and the moment it was counted at.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="ownerPlayerId">The owner of the body.</param>
    /// <param name="amount">The number of the inhabitants.</param>
    /// <param name="at">The moment of the count.</param>
    /// <returns>A task that completes when the population is written.</returns>
    internal static async Task SetPopulationAsync(
        IServiceProvider services,
        long planetId,
        string ownerPlayerId,
        long amount,
        DateTimeOffset at)
    {
        EconomyDbContext dbContext = services.GetRequiredService<EconomyDbContext>();

        PlanetPopulationEntity? row = await dbContext.PlanetPopulations
            .Where(entry => entry.PlanetId == planetId)
            .FirstOrDefaultAsync(Token)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new PlanetPopulationEntity { PlanetId = planetId };
            dbContext.PlanetPopulations.Add(row);
        }

        row.OwnerPlayerId = ownerPlayerId;
        row.SectorId = 0;
        row.Amount = amount;
        row.UpdatedAt = at;

        await dbContext.SaveChangesAsync(Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Puts the balance of a currency of a civilization.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="resourceTypeId">The currency.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="at">The moment of the count.</param>
    /// <returns>A task that completes when the balance is written.</returns>
    internal static async Task SetBalanceAsync(
        IServiceProvider services,
        string playerId,
        int resourceTypeId,
        long amount,
        DateTimeOffset at)
    {
        EconomyDbContext dbContext = services.GetRequiredService<EconomyDbContext>();

        CivilizationBalanceEntity? row = await dbContext.CivilizationBalances
            .Where(entry => entry.PlayerId == playerId && entry.ResourceTypeId == resourceTypeId)
            .FirstOrDefaultAsync(Token)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new CivilizationBalanceEntity { PlayerId = playerId, ResourceTypeId = resourceTypeId };
            dbContext.CivilizationBalances.Add(row);
        }

        row.Amount = amount;
        row.UpdatedAt = at;

        await dbContext.SaveChangesAsync(Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the balance of a currency of a civilization from the tables.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="resourceTypeId">The currency.</param>
    /// <returns>The amount of the balance.</returns>
    internal static async Task<long> BalanceAsync(IServiceProvider services, string playerId, int resourceTypeId)
    {
        EconomyDbContext dbContext = services.GetRequiredService<EconomyDbContext>();

        CivilizationBalanceEntity? row = await dbContext.CivilizationBalances.AsNoTracking()
            .Where(entry => entry.PlayerId == playerId && entry.ResourceTypeId == resourceTypeId)
            .FirstOrDefaultAsync(Token)
            .ConfigureAwait(false);

        return row?.Amount ?? 0;
    }

    /// <summary>
    /// Puts a building of a class on a body at the first level, so the body has the maximum
    /// population and the speed of the growth.
    /// </summary>
    /// <param name="player">The player and the body.</param>
    /// <param name="services">The scoped services.</param>
    /// <param name="buildingClass">The class of the building.</param>
    /// <returns>A task that completes when the building stands on the body.</returns>
    internal static async Task BuildAsync(PlanetsTestPlayer player, IServiceProvider services, string buildingClass)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(services);

        int? typeId = await services.GetRequiredService<Aetheris.Planets.IPlanetsModule>()
            .FindBuildingTypeIdByClassAsync(buildingClass, Token)
            .ConfigureAwait(false);

        if (typeId is not { } buildingTypeId)
        {
            return;
        }

        Aetheris.Planets.Data.PlanetsDbContext dbContext = services.GetRequiredService<Aetheris.Planets.Data.PlanetsDbContext>();

        Aetheris.Planets.Data.Entities.PlanetBuildingEntity? building = await dbContext.PlanetBuildings
            .Where(entry => entry.PlanetId == player.PlanetId && entry.BuildingTypeId == buildingTypeId)
            .FirstOrDefaultAsync(Token)
            .ConfigureAwait(false);

        if (building is not null)
        {
            return;
        }

        dbContext.PlanetBuildings.Add(new Aetheris.Planets.Data.Entities.PlanetBuildingEntity
        {
            PlanetId = player.PlanetId,
            SectorId = 0,
            BuildingTypeId = buildingTypeId,
            Level = 1,
        });

        await dbContext.SaveChangesAsync(Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Gives a body to a player, as a colonization does, so the player has more than one body.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="planetId">The body.</param>
    /// <param name="playerId">The new owner.</param>
    /// <returns>A task that completes when the body belongs to the player.</returns>
    internal static async Task GiveBodyAsync(IServiceProvider services, long planetId, string playerId)
    {
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        Aetheris.World.Data.Entities.PlanetEntity planet = await world.Planets.SingleAsync(entry => entry.Id == planetId, Token).ConfigureAwait(false);

        planet.OwnerPlayerId = playerId;
        await world.SaveChangesAsync(Token).ConfigureAwait(false);

        Aetheris.Planets.Data.PlanetsDbContext planets = services.GetRequiredService<Aetheris.Planets.Data.PlanetsDbContext>();
        Aetheris.Planets.Data.Entities.PlanetStateEntity state = await planets.PlanetStates.SingleAsync(entry => entry.PlanetId == planetId, Token).ConfigureAwait(false);

        state.OwnerPlayerId = playerId;
        await planets.SaveChangesAsync(Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes a player learn a technology of one level that has one effect, written straight into
    /// the tables.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="playerId">The player.</param>
    /// <param name="effectType">The kind of the effect.</param>
    /// <param name="value">The value of the effect.</param>
    /// <returns>A task that completes when the player has learned the technology.</returns>
    internal static async Task GiveTechnologyAsync(IServiceProvider services, string playerId, string effectType, decimal value)
    {
        await AetherisTestFactory.SetParameterAsync(services, ResearchConfigKeys.CatalogCacheSeconds, "0").ConfigureAwait(false);
        await services.GetRequiredService<IResearchModule>().EnsureAsync(Token).ConfigureAwait(false);

        ResearchDbContext research = services.GetRequiredService<ResearchDbContext>();
        int branchId = await research.TechnologyBranches.AsNoTracking()
            .OrderBy(branch => branch.Id)
            .Select(branch => branch.Id)
            .FirstAsync(Token)
            .ConfigureAwait(false);
        int code = 900 + await research.Technologies.CountAsync(Token).ConfigureAwait(false);

        TechnologyEntity technology = new()
        {
            Code = "tech_" + code,
            NameKey = "research.technology.tech_" + code + ".name",
            DescriptionKey = "research.technology.tech_" + code + ".description",
            BranchId = branchId,
            SortOrder = code,
            MaxLevel = 1,
            ResearchCenterLevel = 1,
            LevelIds =
            [
                new TechnologyLevelEntity
                {
                    Level = 1,
                    DurationSeconds = 60,
                    EffectIds = [new TechnologyLevelEffectEntity { EffectType = effectType, Value = value }],
                },
            ],
        };

        research.Technologies.Add(technology);
        await research.SaveChangesAsync(Token).ConfigureAwait(false);

        research.PlayerTechnologies.Add(new PlayerTechnologyEntity { PlayerId = playerId, TechnologyId = technology.Id, Level = 1 });
        await research.SaveChangesAsync(Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the value of the first effect of a kind at a level of a building type of the
    /// starting data, so a test counts from the data and not from the formula of the code.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="buildingCode">The code of the building type.</param>
    /// <param name="level">The level.</param>
    /// <param name="effectType">The kind of the effect.</param>
    /// <returns>The value of the effect, with its fraction.</returns>
    internal static async Task<decimal> EffectValueAsync(IServiceProvider services, string buildingCode, int level, string effectType)
    {
        Aetheris.Planets.Data.Entities.BuildingTypeEntity type = await PlanetsTestData.TypeAsync(services, buildingCode).ConfigureAwait(false);

        return type.LevelIds.Single(entry => entry.Level == level)
            .EffectIds.Single(effect => string.Equals(effect.EffectType, effectType, StringComparison.Ordinal))
            .Value;
    }

    /// <summary>
    /// Moves the game time of the server forward, so the population and the credits are
    /// counted over the time that passed.
    /// </summary>
    /// <param name="services">The scoped services.</param>
    /// <param name="delay">How much the game time moves forward.</param>
    /// <returns>The new moment of game time.</returns>
    internal static DateTimeOffset MoveTime(IServiceProvider services, TimeSpan delay)
    {
        GameClock clock = services.GetRequiredService<GameClock>();

        // The multiplier runs the game time faster, so the test does not wait for it.
        clock.SetMultiplier(delay.TotalSeconds * 10);

        return clock.GameNow;
    }
}

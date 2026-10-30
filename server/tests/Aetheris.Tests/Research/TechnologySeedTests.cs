using Aetheris.Research;
using Aetheris.Research.Configuration;
using Aetheris.Research.Data;
using Aetheris.Research.Data.Entities;
using Aetheris.Research.Services;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Research;

/// <summary>
/// Tests of the seed of the technologies in the mode "add what is missing by the code of the
/// starting data" (task 00015, the answers of the author of 30.10.2026).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class TechnologySeedTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TechnologySeedTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public TechnologySeedTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A database that already has technologies still gets a technology of the starting data that
    /// it lacks, with its levels, costs and effects, and the seed leaves what an administrator
    /// changed in the other technologies as it is.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_seed_adds_a_missing_technology_to_an_existing_database_and_keeps_the_changes()
    {
        await PlanetsTestData.ResetAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        await AetherisTestFactory.SetParameterAsync(services, ResearchConfigKeys.CatalogCacheSeconds, "0");

        IResearchModule research = services.GetRequiredService<IResearchModule>();
        ResearchDbContext dbContext = services.GetRequiredService<ResearchDbContext>();

        await research.EnsureAsync(Token);

        // The technology of the credits is in the starting data; it is taken out of the database.
        SeedTechnology missing = ResearchReferenceSeed.Load().Technologies
            .First(technology => technology.Effects.Any(effect => effect.Type == TechnologyEffectTypes.CreditIncomeBonus));
        SeedTechnology changed = ResearchReferenceSeed.Load().Technologies
            .First(technology => technology.Code != missing.Code);

        await dbContext.Technologies.Where(technology => technology.Code == missing.Code).ExecuteDeleteAsync(Token);

        // The administrator changed another technology; the seed must not overwrite it.
        const int changedOrder = 777;

        await dbContext.Technologies.Where(technology => technology.Code == changed.Code)
            .ExecuteUpdateAsync(setters => setters.SetProperty(technology => technology.SortOrder, changedOrder), Token);

        await research.EnsureAsync(Token);

        dbContext.ChangeTracker.Clear();

        TechnologyEntity added = await dbContext.Technologies.AsNoTracking()
            .Include(technology => technology.LevelIds).ThenInclude(level => level.CostIds)
            .Include(technology => technology.LevelIds).ThenInclude(level => level.EffectIds)
            .AsSplitQuery()
            .SingleAsync(technology => technology.Code == missing.Code, Token);

        Assert.Equal(missing.MaxLevel, added.LevelIds.Count);
        Assert.All(added.LevelIds, level => Assert.NotEmpty(level.CostIds));
        Assert.Contains(added.LevelIds.SelectMany(level => level.EffectIds), effect => effect.EffectType == TechnologyEffectTypes.CreditIncomeBonus);

        Assert.Equal(changedOrder, await dbContext.Technologies.AsNoTracking()
            .Where(technology => technology.Code == changed.Code)
            .Select(technology => technology.SortOrder)
            .SingleAsync(Token));

        // A repeated start adds nothing more: the codes stay one each.
        await research.EnsureAsync(Token);

        Assert.Equal(1, await dbContext.Technologies.AsNoTracking().CountAsync(technology => technology.Code == missing.Code, Token));
    }
}

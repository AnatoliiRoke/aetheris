using Aetheris.Tests.Infrastructure;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the nebula that has no mass (task 00053): it has only an area, it is an endless source of
/// ether and of dark matter, and nothing is ever taken away from it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class NebulaTests
{
    private const long ProcessedMass = 1000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="NebulaTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public NebulaTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// The nebula has no mass anywhere: not in the entity, not in the table, not in the configuration.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_nebula_has_no_mass_in_the_model_the_table_or_the_configuration()
    {
        await WorldTestData.ResetWorldAsync(_factory, WorldTestData.SmallSectors);

        Assert.Null(typeof(NebulaEntity).GetProperty("Mass"));
        Assert.Null(typeof(NebulaEntity).GetProperty("InitialMass"));
        Assert.DoesNotContain(WorldConfigKeys.Definitions, definition => definition.Key.StartsWith("world.nebula.mass", StringComparison.Ordinal));

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext database = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        int massColumns = await database.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = 'nebula' AND column_name IN ('mass', 'initial_mass')")
            .SingleAsync(Token);

        Assert.Equal(0, massColumns);
    }

    /// <summary>
    /// The extraction from a nebula is the yield of the cycle times the share of the resource, and it
    /// never changes: a nebula that was taken from many times gives the same again, and its
    /// composition is what it was.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_nebula_is_never_emptied()
    {
        await WorldTestData.ResetWorldAsync(_factory, WorldTestData.SmallSectors);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext database = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();

        NebulaEntity nebula = await database.Nebulae.AsNoTracking().OrderBy(entry => entry.Id).FirstAsync(Token);
        List<NebulaCompositionEntity> shares = await database.NebulaCompositions.AsNoTracking()
            .Where(share => share.NebulaId == nebula.Id)
            .OrderBy(share => share.Id)
            .ToListAsync(Token);

        Assert.NotEmpty(shares);

        ExtractionResultView first = await ExtractAsync(world, nebula.Id, ProcessedMass, long.MaxValue);

        // The yield is the processed mass times the share of every resource, rounded down.
        foreach (NebulaCompositionEntity share in shares)
        {
            long expected = (long)decimal.Floor(ProcessedMass * share.Coefficient);

            Assert.Equal(expected, first.Yield.GetValueOrDefault(share.ResourceId));
        }

        for (int cycle = 0; cycle < 25; cycle++)
        {
            ExtractionResultView again = await ExtractAsync(world, nebula.Id, ProcessedMass, long.MaxValue);

            Assert.Equal(first.Yield, again.Yield);
            Assert.Equal(0L, again.RemainingMass);
            Assert.False(again.Depleted);
        }

        // The composition and the row of the nebula are what they were.
        List<NebulaCompositionEntity> after = await database.NebulaCompositions.AsNoTracking()
            .Where(share => share.NebulaId == nebula.Id)
            .OrderBy(share => share.Id)
            .ToListAsync(Token);

        Assert.Equal(shares.Select(share => (share.ResourceId, share.Coefficient)), after.Select(share => (share.ResourceId, share.Coefficient)));
        Assert.Equal(1, await database.Nebulae.CountAsync(entry => entry.Id == nebula.Id, Token));
    }

    /// <summary>
    /// A hold that is smaller than the yield takes only what fits, and the nebula still keeps nothing
    /// back: the place of the map has no mass.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_small_hold_limits_the_yield_and_the_place_has_no_mass()
    {
        await WorldTestData.ResetWorldAsync(_factory, WorldTestData.SmallSectors);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext database = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();

        NebulaEntity nebula = await database.Nebulae.AsNoTracking().OrderBy(entry => entry.Id).FirstAsync(Token);

        ExtractionResultView limited = await ExtractAsync(world, nebula.Id, ProcessedMass, 100);

        Assert.True(limited.Yield.Values.Sum() <= 100);
        Assert.True(limited.Yield.Values.Sum() > 0);

        PlaceContentDto? place = await world.GetPlaceContentAsync(nebula.CelestialObjectId, Token);

        Assert.NotNull(place);
        Assert.Equal(0L, place.Mass);
        Assert.Equal(nebula.Id, place.Object.NebulaId);
        Assert.NotEmpty(place.Compositions);
    }

    /// <summary>
    /// Every generated nebula has three resources and only three: the deuterium of 0.05 to 0.1, the dark
    /// matter of 0.30 to 0.40 and the ether of 0.35 to 0.45 (task 00055).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_generated_nebula_has_the_deuterium_the_dark_matter_and_the_ether_in_their_ranges()
    {
        Dictionary<string, string> overrides = new(WorldTestData.SmallSectors, StringComparer.Ordinal)
        {
            [WorldConfigKeys.NebulaCount] = "12",
        };

        await WorldTestData.ResetWorldAsync(_factory, overrides);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext database = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        ResourceCatalog catalog = await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetResourceCatalogAsync(Token);

        Dictionary<int, string> codes = catalog.Resources.ToDictionary(resource => resource.Id, resource => resource.Code);
        List<NebulaCompositionEntity> shares = await database.NebulaCompositions.AsNoTracking().ToListAsync(Token);
        List<IGrouping<long, NebulaCompositionEntity>> byNebula = [.. shares.GroupBy(share => share.NebulaId)];

        Assert.True(byNebula.Count >= 12);
        Assert.Equal(byNebula.Count, await database.Nebulae.CountAsync(Token));

        foreach (IGrouping<long, NebulaCompositionEntity> nebula in byNebula)
        {
            Dictionary<string, decimal> coefficients = nebula.ToDictionary(share => codes[share.ResourceId], share => share.Coefficient, StringComparer.Ordinal);

            Assert.Equal(3, coefficients.Count);
            Assert.All([SeedCodes.Deuterium, SeedCodes.DarkMatter, SeedCodes.Ether], code => Assert.True(coefficients.ContainsKey(code)));
            Assert.InRange(coefficients[SeedCodes.Deuterium], 0.05m, 0.1m);
            Assert.InRange(coefficients[SeedCodes.DarkMatter], 0.30m, 0.40m);
            Assert.InRange(coefficients[SeedCodes.Ether], 0.35m, 0.45m);
        }
    }

    private static async Task<ExtractionResultView> ExtractAsync(IWorldModule world, long nebulaId, long mass, long freeHold)
    {
        ExtractionResult result = await world.ExtractFromNebulaAsync(nebulaId, mass, freeHold, Token);

        return new ExtractionResultView(
            result.YieldIds.ToDictionary(amount => amount.ResourceId, amount => amount.Amount),
            result.RemainingMass,
            result.Depleted);
    }

    private sealed record ExtractionResultView(Dictionary<int, long> Yield, long RemainingMass, bool Depleted);
}

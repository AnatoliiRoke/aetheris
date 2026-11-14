using Aetheris.Core.Admin;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.Production.Data;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Production;

/// <summary>
/// Tests of the accuracy rows of the weapons (task 00062).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PartAccuracyTests
{
    private const int ExpectedRows = 90;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartAccuracyTests"/> class.
    /// </summary>
    /// <param name="factory">The factory of the tests.</param>
    public PartAccuracyTests(AetherisTestFactory factory)
    {
        _factory = factory;
    }

    /// <summary>A row is refused when the target is not a hull group or the number is outside 0..1.</summary>
    [Fact]
    public void A_row_is_refused_when_the_target_is_not_a_hull_group()
    {
        Assert.Equal(PartAccuracyRules.NotHullGroup, PartAccuracyRules.Problem(0.5, false));
        Assert.Null(PartAccuracyRules.Problem(0.5, true));
        Assert.Equal(AdminReferenceErrors.OutOfBounds, PartAccuracyRules.Problem(1.5, true));
    }

    /// <summary>The starting data names ninety rows and no row twice.</summary>
    [Fact]
    public void The_starting_data_names_the_accuracy_rows()
    {
        ProductionSeedData seed = ProductionReferenceSeed.Load();
        IReadOnlyList<SeedPartAccuracy> rows = seed.PartAccuracies ?? [];

        Assert.Equal(ExpectedRows, rows.Count);
        Assert.Equal(ExpectedRows, rows.Select(row => (row.Part, row.TargetGroup)).Distinct().Count());
    }

    /// <summary>
    /// Filling the tables twice does not add a second copy of a row.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Filling_the_tables_twice_keeps_one_row_per_pair()
    {
        await PlanetsTestData.ResetAsync(_factory);

        await using AsyncServiceScope first = _factory.Services.CreateAsyncScope();
        await first.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        int afterFirst = await CountAsync();

        await using AsyncServiceScope second = _factory.Services.CreateAsyncScope();
        await second.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync(Token);
        int afterSecond = await CountAsync();

        Assert.Equal(ExpectedRows, afterFirst);
        Assert.Equal(afterFirst, afterSecond);
    }

    private async Task<int> CountAsync()
    {
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<ProductionDbContext>().PartAccuracies
            .CountAsync(Token);
    }
}

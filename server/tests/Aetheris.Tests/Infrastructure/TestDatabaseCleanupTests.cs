using Aetheris.Host.Data;
using Aetheris.Host.Data.Entities;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// Tests that the test database is cleaned before a run.
/// </summary>
/// <remarks>
/// The task requires the test database to be cleared between runs, so that a test never
/// sees rows left by an earlier run.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class TestDatabaseCleanupTests
{
    private const string LeftoverKey = "test.leftover_marker";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestDatabaseCleanupTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory.</param>
    public TestDatabaseCleanupTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// Rows written by a previous run are removed by the cleanup.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_cleanup_removes_rows_left_by_an_earlier_run()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        // Simulate a row left over from an earlier run.
        dbContext.ConfigParameters.Add(new ConfigParameterEntity
        {
            Key = LeftoverKey,
            Value = "leftover",
            Version = 1,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = "leftover",
        });

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.True(
            await dbContext.ConfigParameters.AnyAsync(
                parameter => parameter.Key == LeftoverKey,
                TestContext.Current.CancellationToken),
            "The leftover row was not written.");

        await AetherisTestFactory.ClearDatabaseAsync(dbContext);

        Assert.False(
            await dbContext.ConfigParameters.AnyAsync(
                parameter => parameter.Key == LeftoverKey,
                TestContext.Current.CancellationToken),
            "The cleanup did not remove the leftover row.");
    }

    /// <summary>
    /// The cleanup leaves the schema in place: the tables still exist and are queryable.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_cleanup_keeps_the_schema()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

        await AetherisTestFactory.ClearDatabaseAsync(dbContext);

        int tables = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                SELECT count(*)::int AS "Value"
                FROM pg_tables
                WHERE schemaname = 'public'
                """)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.True(tables > 0, "The schema was dropped by the cleanup.");
    }
}

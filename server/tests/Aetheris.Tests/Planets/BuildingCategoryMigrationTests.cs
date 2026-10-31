using Aetheris.Host.Data;
using Aetheris.Planets.Data;
using Aetheris.Requirements.Data;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Aetheris.Tests.Planets;

/// <summary>
/// Tests of the migration that gives the building types a mandatory category (task 00020,
/// section 3.1; documentation/tech/09, section 10.4).
/// </summary>
/// <remarks>
/// The test builds the schema before the migration in its own schema of the test database, fills it
/// with buildings of the starting data, a building of a class the table of the codes does not know
/// and a building added by hand, runs the new migration and checks the categories, the links, the
/// texts and the counter of the codes. An empty table gets no category: the seed writes them.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class BuildingCategoryMigrationTests
{
    private const string Schema = "building_category_migration";

    private const string LastPlanetsMigration = "20261030233124_StorageCapacityPerResource";

    /// <summary>
    /// A building of the starting data with an identifier unlike the starting data, a mine, a
    /// building the table does not know with a class, and one it knows neither by the code nor by
    /// the class.
    /// </summary>
    private const string OldRows =
        """
        INSERT INTO building_type (id, code, name_key, description_key, sort_order, is_start_building, building_class, is_archived, version) VALUES
            (31, 'building_2', 'planets.building.building_2.name', 'planets.building.building_2.description', 2, false, 'research_center', false, 1),
            (32, 'building_5', 'planets.building.building_5.name', 'planets.building.building_5.description', 5, false, '', false, 1),
            (33, 'building_40', 'planets.building.building_40.name', 'planets.building.building_40.description', 40, false, 'sensor', false, 1),
            (34, 'building_41', 'planets.building.building_41.name', 'planets.building.building_41.description', 41, false, '', false, 1);
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// An old database keeps its buildings and gets the nine categories with their texts, every
    /// building in the category that fits it, and the counter of the codes above the last number.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_old_database_gives_its_buildings_a_category()
    {
        string connectionString = new NpgsqlConnectionStringBuilder(AetherisTestFactory.ConnectionString)
        {
            SearchPath = Schema,
            Pooling = false,
        }.ConnectionString;

        await ResetSchemaAsync();

        try
        {
            await MigrateBaseAsync(connectionString);
            await MigrateAsync(Options<PlanetsDbContext>(connectionString), LastPlanetsMigration);
            await MigrateAsync(Options<RequirementsDbContext>(connectionString), null);

            await using (AetherisDbContext old = new(HostOptions(connectionString)))
            {
                await old.Database.ExecuteSqlRawAsync(OldRows, Token);
            }

            await MigrateAsync(Options<PlanetsDbContext>(connectionString), null);

            Assert.Equal(9, await ScalarAsync<int>(connectionString, $"""SELECT count(*)::int AS "Value" FROM building_category"""));
            Assert.Equal("group_8", await CategoryOfAsync(connectionString, 31));
            Assert.Equal("group_1", await CategoryOfAsync(connectionString, 32));

            // A building the table does not know gets the category of its class, and a building
            // that fits neither gets the first category.
            Assert.Equal("group_7", await CategoryOfAsync(connectionString, 33));
            Assert.Equal("group_1", await CategoryOfAsync(connectionString, 34));

            Assert.Equal("Дослідження", await TextAsync(connectionString, "planets.building_category.group_8.name", "uk"));
            Assert.Equal("Trade", await TextAsync(connectionString, "planets.building_category.group_9.name", "en"));
            Assert.NotNull(await TextAsync(connectionString, "planets.building_category.group_1.description", "uk"));
            Assert.Equal(9, await ScalarAsync<int>(connectionString, $"""SELECT last_number AS "Value" FROM code_sequence WHERE code_group = 'group'"""));
            Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""SELECT version AS "Value" FROM building_category WHERE code = 'group_1'"""));
            Assert.False(await ScalarAsync<bool>(connectionString, $"""SELECT is_archived AS "Value" FROM building_category WHERE code = 'group_1'"""));
        }
        finally
        {
            await DropSchemaAsync();
        }
    }

    /// <summary>
    /// A database without buildings gets the table and no category: the seed of the module writes
    /// the categories together with the buildings.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_empty_database_gets_no_category_from_the_migration()
    {
        string connectionString = new NpgsqlConnectionStringBuilder(AetherisTestFactory.ConnectionString)
        {
            SearchPath = Schema,
            Pooling = false,
        }.ConnectionString;

        await ResetSchemaAsync();

        try
        {
            await MigrateBaseAsync(connectionString);
            await MigrateAsync(Options<PlanetsDbContext>(connectionString), null);

            Assert.Equal(0, await ScalarAsync<int>(connectionString, $"""SELECT count(*)::int AS "Value" FROM building_category"""));
        }
        finally
        {
            await DropSchemaAsync();
        }
    }

    private static async Task ResetSchemaAsync()
    {
        await using AetherisDbContext main = new(HostOptions(AetherisTestFactory.ConnectionString));
        await main.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {Schema} CASCADE; CREATE SCHEMA {Schema};", Token);
    }

    private static async Task DropSchemaAsync()
    {
        await using AetherisDbContext main = new(HostOptions(AetherisTestFactory.ConnectionString));
        await main.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {Schema} CASCADE;", Token);
    }

    private static async Task MigrateBaseAsync(string connectionString)
    {
        await MigrateAsync(HostOptions(connectionString), null);
        await MigrateAsync(Options<WorldDbContext>(connectionString), null);
    }

    private static DbContextOptions<AetherisDbContext> HostOptions(string connectionString)
    {
        DbContextOptionsBuilder<AetherisDbContext> options = new();
        AetherisDbContextSetup.Configure(options, connectionString);

        return options.Options;
    }

    private static DbContextOptions<TContext> Options<TContext>(string connectionString)
        where TContext : DbContext =>
        new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(TContext).Assembly.FullName))
            .Options;

    private static async Task MigrateAsync<TContext>(DbContextOptions<TContext> options, string? target)
        where TContext : DbContext
    {
        await using TContext context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await context.GetService<IMigrator>().MigrateAsync(target, Token);
    }

    private static async Task<string> CategoryOfAsync(string connectionString, int buildingTypeId) =>
        await ScalarAsync<string>(
            connectionString,
            $"""SELECT c.code AS "Value" FROM building_type t JOIN building_category c ON c.id = t.category_id WHERE t.id = {buildingTypeId}""");

    private static async Task<T> ScalarAsync<T>(string connectionString, FormattableString query) =>
        (await ListAsync<T>(connectionString, query)).Single();

    private static async Task<string?> TextAsync(string connectionString, string key, string language) =>
        (await ListAsync<string>(
            connectionString,
            $"""SELECT text AS "Value" FROM localization_text WHERE key = {key} AND language = {language}""")).SingleOrDefault();

    private static async Task<List<T>> ListAsync<T>(string connectionString, FormattableString query)
    {
        await using AetherisDbContext context = new(HostOptions(connectionString));

        // The statements are read as they are, never composed into a subquery.
        return await context.Database.SqlQuery<T>(query).ToListAsync(Token);
    }
}

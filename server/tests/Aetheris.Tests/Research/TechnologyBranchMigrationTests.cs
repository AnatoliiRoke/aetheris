using Aetheris.Host.Data;
using Aetheris.Planets.Data;
using Aetheris.Requirements.Data;
using Aetheris.Research.Data;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Aetheris.Tests.Research;

/// <summary>
/// Tests of the migration of the branches of the technologies into their own table (task 00023,
/// decision 8; documentation/tech/09, section 10.4).
/// </summary>
/// <remarks>
/// The test builds the schema of task 00012 in its own schema of the test database, fills it with
/// technologies whose branch is a text, the key technologies of task 00012 and a branch written by
/// hand, runs the new migration and checks the identifiers, the links and the texts.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class TechnologyBranchMigrationTests
{
    private const string Schema = "technology_branch_migration";

    private const string LastResearchMigration = "20261028193353_InitialResearch";

    /// <summary>
    /// The technologies of task 00012 with identifiers unlike the starting data, a name of a branch
    /// changed in the admin panel and a branch the old list did not know.
    /// </summary>
    private const string OldRows =
        """
        INSERT INTO technology (id, code, name_key, description_key, branch, sort_order, max_level, research_center_level) VALUES
            (41, 'tech_1', 'research.technology.tech_1.name', 'research.technology.tech_1.description', 'economy', 1, 10, 1),
            (47, 'tech_7', 'research.technology.tech_7.name', 'research.technology.tech_7.description', 'titanium_mining', 7, 1, 3),
            (55, 'tech_15', 'research.technology.tech_15.name', 'research.technology.tech_15.description', 'trade_center_building', 15, 1, 4),
            (60, 'tech_20', 'research.technology.tech_20.name', 'research.technology.tech_20.description', 'hand_made', 1, 1, 1);
        INSERT INTO localization_text (key, language, text, version, updated_at, updated_by) VALUES
            ('research.branch.economy.name', 'uk', 'Господарство', 2, now(), 'admin-1'),
            ('research.branch.economy.name', 'en', 'Economy', 1, now(), 'seed'),
            ('research.branch.hand_made.name', 'uk', 'Саморобна', 1, now(), 'admin-1');
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// An old database keeps its technologies and gets the six branches in their old order, the
    /// key technologies in their branches, a branch written by hand with the next number, the names
    /// at the new keys and the counter above every taken number.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_old_database_moves_the_branches_into_their_table()
    {
        string connectionString = new NpgsqlConnectionStringBuilder(AetherisTestFactory.ConnectionString)
        {
            SearchPath = Schema,
            Pooling = false,
        }.ConnectionString;

        await using (AetherisDbContext main = new(HostOptions(AetherisTestFactory.ConnectionString)))
        {
            await main.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {Schema} CASCADE; CREATE SCHEMA {Schema};", Token);
        }

        try
        {
            await MigrateAsync(HostOptions(connectionString), null);
            await MigrateAsync(Options<WorldDbContext>(connectionString), null);
            await MigrateAsync(Options<PlanetsDbContext>(connectionString), null);
            await MigrateAsync(Options<RequirementsDbContext>(connectionString), null);
            await MigrateAsync(Options<ResearchDbContext>(connectionString), LastResearchMigration);

            await using (AetherisDbContext old = new(HostOptions(connectionString)))
            {
                await old.Database.ExecuteSqlRawAsync(OldRows, Token);
            }

            await MigrateAsync(Options<ResearchDbContext>(connectionString), null);

            Assert.Equal(
                ["branch_1", "branch_2", "branch_3", "branch_4", "branch_5", "branch_6", "branch_7"],
                await ListAsync(connectionString, $"""SELECT code AS "Value" FROM technology_branch ORDER BY sort_order"""));

            Assert.Equal("branch_1", await BranchOfAsync(connectionString, 41));
            Assert.Equal("branch_1", await BranchOfAsync(connectionString, 47));
            Assert.Equal("branch_6", await BranchOfAsync(connectionString, 55));
            Assert.Equal("branch_7", await BranchOfAsync(connectionString, 60));

            // A name changed in the admin panel moves as it is; the others come from the migration.
            Assert.Equal("Господарство", await TextAsync(connectionString, "research.technology_branch.branch_1.name", "uk"));
            Assert.Equal("Флот", await TextAsync(connectionString, "research.technology_branch.branch_2.name", "uk"));
            Assert.Equal("Trade", await TextAsync(connectionString, "research.technology_branch.branch_6.name", "en"));
            Assert.NotNull(await TextAsync(connectionString, "research.technology_branch.branch_1.description", "uk"));
            Assert.Equal("Саморобна", await TextAsync(connectionString, "research.technology_branch.branch_7.name", "uk"));

            Assert.Equal(7, await ScalarAsync<int>(connectionString, $"""SELECT last_number AS "Value" FROM code_sequence WHERE code_group = 'branch'"""));
            Assert.Equal(1, await ScalarAsync<int>(connectionString, $"""SELECT version AS "Value" FROM technology WHERE id = 41"""));
            Assert.False(await ScalarAsync<bool>(connectionString, $"""SELECT is_archived AS "Value" FROM technology WHERE id = 41"""));
        }
        finally
        {
            await using AetherisDbContext main = new(HostOptions(AetherisTestFactory.ConnectionString));
            await main.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {Schema} CASCADE;", Token);
        }
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

    private static async Task<string> BranchOfAsync(string connectionString, int technologyId) =>
        await ScalarAsync<string>(
            connectionString,
            $"""SELECT b.code AS "Value" FROM technology t JOIN technology_branch b ON b.id = t.branch_id WHERE t.id = {technologyId}""");

    private static async Task<T> ScalarAsync<T>(string connectionString, FormattableString query) =>
        (await ListAsync<T>(connectionString, query)).Single();

    private static async Task<string?> TextAsync(string connectionString, string key, string language) =>
        (await ListAsync<string>(
            connectionString,
            $"""SELECT text AS "Value" FROM localization_text WHERE key = {key} AND language = {language}""")).SingleOrDefault();

    private static Task<List<string>> ListAsync(string connectionString, FormattableString query) =>
        ListAsync<string>(connectionString, query);

    private static async Task<List<T>> ListAsync<T>(string connectionString, FormattableString query)
    {
        await using AetherisDbContext context = new(HostOptions(connectionString));

        // The statements are read as they are, never composed into a subquery.
        return await context.Database.SqlQuery<T>(query).ToListAsync(Token);
    }
}

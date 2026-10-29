using Aetheris.Host.Data;
using Aetheris.Planets.Data;
using Aetheris.Requirements.Data;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the migration of an old database to the neutral codes (task 00022, acceptance
/// criterion 4).
/// </summary>
/// <remarks>
/// The test builds the schema of the last release in its own schema of the test database,
/// fills it with rows that have the old codes and identifiers unlike the starting data, runs the
/// new migrations and checks that the identifiers, the links and the texts survived.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class NeutralCodeMigrationTests
{
    private const string Schema = "neutral_code_migration";

    private const string LastWorldMigration = "20261026143405_AdminObjectEditing";

    private const string LastPlanetsMigration = "20261026153740_BuildingsAndQueues";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// An old database keeps its identifiers and links, gets the neutral codes and keys, the
    /// texts of its rows, the new group «Кристали», the start flags instead of the codes of the
    /// configuration and counters above every taken number.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_old_database_moves_to_neutral_codes_and_keeps_its_links()
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
            await MigrateAsync(WorldOptions(connectionString), LastWorldMigration);
            await MigrateAsync(PlanetsOptions(connectionString), LastPlanetsMigration);

            // The common requirements module takes the table of the requirements of the bodies
            // module, so it runs after the bodies module reached the table (task 00012).
            await MigrateAsync(RequirementsOptions(connectionString), null);

            await using (AetherisDbContext old = new(HostOptions(connectionString)))
            {
                await old.Database.ExecuteSqlRawAsync(OldRows, Token);
            }

            await MigrateAsync(WorldOptions(connectionString), null);
            await MigrateAsync(PlanetsOptions(connectionString), null);

            Assert.Equal("resource_group_2", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM resource_group WHERE id = 12"""));
            Assert.Equal("world.resource_group.resource_group_2.name", await ScalarAsync<string>(connectionString, $"""SELECT name_key AS "Value" FROM resource_group WHERE id = 12"""));
            Assert.Equal("Мінерали", await TextAsync(connectionString, "world.resource_group.resource_group_2.name", "uk"));
            Assert.Equal("Minerals", await TextAsync(connectionString, "world.resource_group.resource_group_2.name", "en"));

            Assert.Equal("resource_1", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM resource_type WHERE id = 101"""));
            Assert.Equal("resource_5", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM resource_type WHERE id = 205"""));
            Assert.Equal(12, await ScalarAsync<int>(connectionString, $"""SELECT resource_group_id AS "Value" FROM resource_type WHERE id = 205"""));
            Assert.Equal("Залізо", await TextAsync(connectionString, "world.resource.resource_1.name", "uk"));
            Assert.Equal("Quartzite", await TextAsync(connectionString, "world.resource.resource_5.name", "en"));

            // A row the table of codes does not know gets the next number of its group.
            Assert.Equal("resource_12", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM resource_type WHERE id = 300"""));

            // The new group «Кристали» and its resource come after every existing number.
            Assert.Equal("Кристали", await TextAsync(connectionString, "world.resource_group.resource_group_5.name", "uk"));
            int crystalId = await ScalarAsync<int>(connectionString, $"""SELECT id AS "Value" FROM resource_type WHERE code = 'resource_13'""");
            Assert.Equal("Кристал", await TextAsync(connectionString, "world.resource.resource_13.name", "uk"));
            Assert.Equal(crystalId, await ScalarAsync<int>(connectionString, $"""SELECT hidden_resource_type_id AS "Value" FROM celestial_object_type WHERE id = 40"""));
            Assert.Equal(0.04m, await ScalarAsync<decimal>(connectionString, $"""SELECT hidden_share_min AS "Value" FROM celestial_object_type WHERE id = 40"""));

            Assert.Equal("object_type_5", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM celestial_object_type WHERE id = 31"""));
            Assert.True(await ScalarAsync<bool>(connectionString, $"""SELECT is_start_default AS "Value" FROM celestial_object_type WHERE id = 31"""));
            Assert.Equal("art-12a", await ScalarAsync<string>(connectionString, $"""SELECT image_key AS "Value" FROM celestial_object_type WHERE id = 31"""));
            Assert.Equal(0, await ScalarAsync<long>(connectionString, $"""SELECT count(*) AS "Value" FROM config_parameter WHERE key LIKE '%.planet_type_code' OR key LIKE '%.building_code'"""));

            Assert.Equal(
                ["rule_group_1", "rule_group_2"],
                await ListAsync(connectionString, $"""SELECT code AS "Value" FROM generation_rule_group ORDER BY id"""));

            Assert.Equal("race_2", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM race WHERE id = 9"""));
            Assert.Equal(31, await ScalarAsync<int>(connectionString, $"""SELECT preferred_object_type_id AS "Value" FROM race WHERE id = 9"""));
            Assert.Equal(4, await ScalarAsync<int>(connectionString, $"""SELECT start_building_type_id AS "Value" FROM race WHERE id = 9"""));
            Assert.Equal(101, await ScalarAsync<int>(connectionString, $"""SELECT resource_type_id AS "Value" FROM race_start_resource WHERE race_id = 9"""));
            Assert.Equal("art-08", await ScalarAsync<string>(connectionString, $"""SELECT image_key AS "Value" FROM race WHERE id = 9"""));
            Assert.Equal("Кварри", await TextAsync(connectionString, "world.race.race_2.name", "uk"));
            Assert.Equal("Броня", await TextAsync(connectionString, "world.race.race_2.trait_1", "uk"));

            Assert.Equal("building_1", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM building_type WHERE id = 4"""));
            Assert.True(await ScalarAsync<bool>(connectionString, $"""SELECT is_start_building AS "Value" FROM building_type WHERE id = 4"""));
            Assert.Equal("building_8", await ScalarAsync<string>(connectionString, $"""SELECT code AS "Value" FROM building_type WHERE id = 6"""));
            Assert.Equal("Склад мінералів", await TextAsync(connectionString, "planets.building.building_8.name", "uk"));
            Assert.Equal(6, await ScalarAsync<int>(connectionString, $"""SELECT building_type_id AS "Value" FROM building_level WHERE id = 60"""));

            // The counters are above every number that is taken: the produced goods of the
            // production module added their group and their five resources (task 00013).
            Assert.Equal(17, await ScalarAsync<int>(connectionString, $"""SELECT last_number AS "Value" FROM code_sequence WHERE code_group = 'resource'"""));
            Assert.Equal(6, await ScalarAsync<int>(connectionString, $"""SELECT last_number AS "Value" FROM code_sequence WHERE code_group = 'resource_group'"""));
            Assert.Equal(8, await ScalarAsync<int>(connectionString, $"""SELECT last_number AS "Value" FROM code_sequence WHERE code_group = 'building'"""));
        }
        finally
        {
            await using AetherisDbContext main = new(HostOptions(AetherisTestFactory.ConnectionString));
            await main.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {Schema} CASCADE;", Token);
        }
    }

    /// <summary>
    /// The rows of the last release with the old codes, the identifiers unlike the starting data
    /// and a row that the table of codes does not know.
    /// </summary>
    private const string OldRows =
        """
        INSERT INTO resource_group (id, code, name_key, allowed_on_planet) VALUES
            (11, 'extractable', 'world.resource_group.extractable', true),
            (12, 'crystal', 'world.resource_group.crystal', true),
            (13, 'fuel', 'world.resource_group.fuel', true),
            (14, 'ether', 'world.resource_group.ether', false);
        INSERT INTO resource_type (id, code, resource_group_id, name_key) VALUES
            (101, 'iron', 11, 'world.resource.iron'),
            (205, 'quartzite', 12, 'world.resource.quartzite'),
            (211, 'ether', 14, 'world.resource.ether'),
            (300, 'test_custom', 11, 'world.resource.test_custom');
        INSERT INTO celestial_object_type (id, code, object_class, name_key, is_colonizable, generation_weight, mass_min, mass_max, version) VALUES
            (31, 'terrestrial', 'planet', 'world.object_type.terrestrial', true, 30, NULL, NULL, 1),
            (40, 'field_rich', 'asteroid_field', 'world.object_type.field_rich', false, 20, 300000, 1000000, 1);
        INSERT INTO generation_rule_group (id, object_type_id, code, min_picks, max_picks, extra_pick_probability, version) VALUES
            (71, 31, 'metals', 2, 4, 0.5, 1),
            (72, 31, 'crystals', 1, 2, 0.05, 1);
        INSERT INTO race (id, code, name_key, sort_order, preferred_object_type_id, start_building_type_id, start_building_level) VALUES
            (9, 'kvarr', 'races.kvarr.name', 2, 31, 4, 1);
        INSERT INTO race_start_resource (race_id, resource_type_id, amount) VALUES (9, 101, 1000);
        INSERT INTO building_type (id, code, name_key, description_key, sort_order) VALUES
            (4, 'admin_center', 'planets.building.admin_center.name', 'planets.building.admin_center.description', 1),
            (6, 'crystal_storage', 'planets.building.crystal_storage.name', 'planets.building.crystal_storage.description', 8);
        INSERT INTO building_level (id, building_type_id, level, duration_seconds) VALUES (60, 6, 1, 60);
        INSERT INTO config_parameter (key, value, version, updated_at, updated_by) VALUES
            ('world.start.planet_type_code', 'terrestrial', 1, now(), 'seed'),
            ('planets.start.building_code', 'admin_center', 1, now(), 'seed');
        """;

    private static DbContextOptions<AetherisDbContext> HostOptions(string connectionString)
    {
        DbContextOptionsBuilder<AetherisDbContext> options = new();
        AetherisDbContextSetup.Configure(options, connectionString);

        return options.Options;
    }

    private static DbContextOptions<WorldDbContext> WorldOptions(string connectionString) =>
        new DbContextOptionsBuilder<WorldDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(WorldDbContext).Assembly.FullName))
            .Options;

    private static DbContextOptions<PlanetsDbContext> PlanetsOptions(string connectionString) =>
        new DbContextOptionsBuilder<PlanetsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(PlanetsDbContext).Assembly.FullName))
            .Options;

    private static DbContextOptions<RequirementsDbContext> RequirementsOptions(string connectionString) =>
        new DbContextOptionsBuilder<RequirementsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(RequirementsDbContext).Assembly.FullName))
            .Options;

    private static async Task MigrateAsync<TContext>(DbContextOptions<TContext> options, string? target)
        where TContext : DbContext
    {
        await using TContext context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await context.GetService<IMigrator>().MigrateAsync(target, Token);
    }

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

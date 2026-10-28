using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Aetheris.Core.Time;
using Aetheris.Host.Data;
using Aetheris.Host.Scheduling;
using Aetheris.Tests.Infrastructure;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Extraction;
using Aetheris.World.Services;
using Aetheris.World.Visibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the world on the database (task 00004, acceptance criteria 4–9, 11–13).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class WorldDatabaseTests
{
    /// <summary>
    /// The number of parallel newcomers of the concurrency test (acceptance criterion 8).
    /// </summary>
    private const int ParallelPlayers = 100;

    /// <summary>
    /// The number of calls that run at the same moment. The local PostgreSQL accepts about a
    /// hundred clients in total, so the hundred calls share a smaller set of connections.
    /// </summary>
    private const int ParallelConnections = 32;

    /// <summary>
    /// The largest time of the generation of one sector of 150 systems (acceptance criterion 9).
    /// </summary>
    private static readonly TimeSpan SectorGenerationBudget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The largest time of the read of the objects of one system (acceptance criterion 9).
    /// </summary>
    private static readonly TimeSpan SystemReadBudget = TimeSpan.FromMilliseconds(20);

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldDatabaseTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public WorldDatabaseTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The first start creates the galaxy and the initial sectors with the reference data;
    /// a repeated start changes nothing (acceptance criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_first_start_creates_the_world_and_a_repeated_start_changes_nothing()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        WorldSettings settings = WorldSettings.From(scope.ServiceProvider.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>());

        List<SectorEntity> sectors = await db.Sectors.OrderBy(sector => sector.LayoutIndex).ToListAsync(Token);
        Assert.Single(await db.Galaxies.ToListAsync(Token));
        Assert.Equal(settings.InitialSectorCount, sectors.Count);
        Assert.Equal(settings.NewbieSectorCount, sectors.Count(sector => sector.IsNewbie));
        Assert.True(sectors[0].IsNewbie);

        int systems = await db.Systems.CountAsync(Token);
        int objects = await db.Objects.CountAsync(Token);
        int deposits = await db.Deposits.CountAsync(Token);
        int logs = await db.GenerationLog.CountAsync(Token);
        Assert.Equal(settings.InitialSectorCount, await db.GenerationLog.CountAsync(entry => entry.Action == WorldGenerationActions.SectorGenerated, Token));

        WorldBootstrapResult again = await scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync(Token);

        Assert.False(again.ReferenceSeeded);
        Assert.False(again.GalaxyCreated);
        Assert.Empty(again.CreatedSectorIds);
        Assert.Equal(systems, await db.Systems.CountAsync(Token));
        Assert.Equal(objects, await db.Objects.CountAsync(Token));
        Assert.Equal(deposits, await db.Deposits.CountAsync(Token));
        Assert.Equal(logs, await db.GenerationLog.CountAsync(Token));
    }

    /// <summary>
    /// The stored world keeps the invariants: every system has a star, gas giants have no
    /// owner and are not colonizable, bodies carry no ether.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_stored_world_keeps_the_invariants()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        int systemsWithoutStar = await db.Systems.CountAsync(
            system => !db.Objects.Any(entry => entry.SystemId == system.Id
                && db.ObjectTypes.Any(type => type.Id == entry.ObjectTypeId && type.ObjectClass == WorldObjectClasses.Star)),
            Token);

        var gasGiants = await db.Planets
            .Join(db.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet, entry })
            .Join(db.ObjectTypes, pair => pair.entry.ObjectTypeId, type => type.Id, (pair, type) => new { pair.planet, type })
            .Where(row => row.type.ObjectClass == WorldObjectClasses.GasGiant)
            .Select(row => new { row.planet.OwnerPlayerId, row.type.IsColonizable })
            .ToListAsync(Token);

        int etherDeposits = await db.Deposits
            .Join(db.ResourceTypes, deposit => deposit.ResourceId, resource => resource.Id, (deposit, resource) => resource)
            .Join(db.ResourceGroups, resource => resource.ResourceGroupId, group => group.Id, (resource, group) => group)
            .CountAsync(group => !group.AllowedOnPlanet, Token);

        Assert.Equal(0, systemsWithoutStar);
        Assert.NotEmpty(gasGiants);
        Assert.All(gasGiants, giant => Assert.Null(giant.OwnerPlayerId));
        Assert.All(gasGiants, giant => Assert.False(giant.IsColonizable));
        Assert.Equal(0, etherDeposits);
    }

    /// <summary>
    /// A parameter changed in the database changes the next generation without a rebuild
    /// (acceptance criterion 4).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_parameter_changed_in_the_database_changes_the_generation()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.SystemsMin, "9", reload: false);
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.SystemsMax, "9");

        SectorSummary sector = await scope.ServiceProvider.GetRequiredService<IWorldModule>().AddSectorAsync(null, false, "test", Token);

        Assert.Equal(9, sector.SystemCount);
        Assert.Equal(9, await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Systems.CountAsync(system => system.SectorId == sector.Id, Token));
    }

    /// <summary>
    /// A field processed to zero disappears, and a new one appears in the same sector through
    /// a scheduled event on an accelerated clock (acceptance criterion 6).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_depleted_field_disappears_and_a_new_one_appears_by_a_scheduled_event()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        GameClock clock = _factory.Services.GetRequiredService<GameClock>();
        double multiplier = clock.Multiplier;

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        WorldDbContext db = services.GetRequiredService<WorldDbContext>();
        IWorldModule world = services.GetRequiredService<IWorldModule>();

        AsteroidFieldEntity field = await db.Fields.AsNoTracking().OrderBy(entry => entry.Id).FirstAsync(Token);
        int activeBefore = await db.Fields.CountAsync(entry => entry.SectorId == field.SectorId && entry.DepletedAt == null, Token);

        // Two players on one field: the first takes most of it, the second gets the rest.
        ExtractionResult first = await world.ExtractFromFieldAsync(field.Id, field.Mass - 1, long.MaxValue, Token);
        ExtractionResult second = await world.ExtractFromFieldAsync(field.Id, field.Mass, long.MaxValue, Token);

        Assert.False(first.Depleted);
        Assert.Equal(1, second.ProcessedMass);
        Assert.True(second.Depleted);

        AsteroidFieldEntity depleted = await db.Fields.AsNoTracking().SingleAsync(entry => entry.Id == field.Id, Token);
        Assert.NotNull(depleted.DepletedAt);
        Assert.Equal(0, depleted.Mass);

        if (depleted.SystemId is long systemId)
        {
            SystemContentDto content = (await world.GetSystemContentAsync(systemId, Token))!;
            Assert.DoesNotContain(content.Objects, entry => entry.AsteroidFieldId == field.Id);
        }

        AetherisDbContext main = services.GetRequiredService<AetherisDbContext>();
        Assert.Single(await main.ScheduledEvents.AsNoTracking()
            .Where(entry => entry.Type == FieldService.RespawnEventType && entry.Status == ScheduledEventStatus.Pending)
            .ToListAsync(Token));

        ScheduledEventRunner runner = _factory.Services.GetRequiredService<ScheduledEventRunner>();

        // Before its time the event waits.
        Assert.Equal(0, await runner.RunDueEventsAsync(Token));

        try
        {
            TimeSpan delay = WorldSettings.From(services.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>()).FieldRespawnDelay;
            DateTimeOffset due = clock.GameNow + delay;

            // One real second becomes far more than the respawn delay of game time.
            clock.SetMultiplier(delay.TotalSeconds * 10);

            while (clock.GameNow < due)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), Token);
            }

            Assert.Equal(1, await runner.RunDueEventsAsync(Token));
        }
        finally
        {
            clock.SetMultiplier(multiplier);
        }

        int activeAfter = await db.Fields.CountAsync(entry => entry.SectorId == field.SectorId && entry.DepletedAt == null, Token);
        Assert.True(activeAfter >= activeBefore, $"The sector had {activeBefore} fields and has {activeAfter} after the event.");
        Assert.True(await db.Fields.AnyAsync(entry => entry.SectorId == field.SectorId && entry.Id > field.Id && entry.DepletedAt == null && entry.Mass > 0, Token));
        Assert.True(await db.GenerationLog.AnyAsync(entry => entry.Action == WorldGenerationActions.FieldsSpawned && entry.SectorId == field.SectorId, Token));
        Assert.Equal(0, await runner.RunDueEventsAsync(Token));
    }

    /// <summary>
    /// The start body is a free body of the start type in a sector for newcomers; a repeated
    /// call returns the same body (acceptance criterion 8).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_start_body_is_a_free_body_of_the_start_type()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();
        WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        StartPlanetResult first = await world.AllocateStartPlanetAsync("player-one", Token);
        StartPlanetResult again = await world.AllocateStartPlanetAsync("player-one", Token);

        Assert.False(first.AlreadyAllocated);
        Assert.True(again.AlreadyAllocated);
        Assert.Equal(first.PlanetId, again.PlanetId);

        PlanetEntity planet = await db.Planets.AsNoTracking().Include(entry => entry.CelestialObject).SingleAsync(entry => entry.Id == first.PlanetId, Token);
        CelestialObjectTypeEntity type = await db.ObjectTypes.AsNoTracking().SingleAsync(entry => entry.Id == planet.CelestialObject!.ObjectTypeId, Token);
        SectorEntity sector = await db.Sectors.AsNoTracking().SingleAsync(entry => entry.Id == planet.SectorId, Token);

        Assert.Equal("player-one", planet.OwnerPlayerId);
        Assert.True(type.IsStartDefault);
        Assert.True(type.IsColonizable);
        Assert.True(sector.IsNewbie);
    }

    /// <summary>
    /// A hundred parallel newcomers get a hundred different bodies; no system holds more
    /// newcomers than the limit (acceptance criterion 8).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Parallel_newcomers_never_share_a_body()
    {
        // The sectors are large enough for every newcomer of the test.
        await WorldTestData.ResetWorldAsync(_factory, new Dictionary<string, string>(WorldTestData.SmallSectors)
        {
            [WorldConfigKeys.SystemsMin] = "50",
            [WorldConfigKeys.SystemsMax] = "60",
        });

        ConcurrentBag<StartPlanetResult> results = [];

        await Parallel.ForEachAsync(
            Enumerable.Range(0, ParallelPlayers),
            new ParallelOptions { MaxDegreeOfParallelism = ParallelConnections, CancellationToken = Token },
            async (index, cancellationToken) =>
            {
                await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
                IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();

                results.Add(await world.AllocateStartPlanetAsync($"parallel-{index}", cancellationToken));
            });

        using IServiceScope check = _factory.Services.CreateScope();
        WorldDbContext db = check.ServiceProvider.GetRequiredService<WorldDbContext>();
        int limit = WorldSettings.From(check.ServiceProvider.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>()).MaxNewbiesPerSystem;

        Assert.Equal(ParallelPlayers, results.Count);
        Assert.Equal(ParallelPlayers, results.Select(result => result.PlanetId).Distinct().Count());
        Assert.Equal(ParallelPlayers, await db.Planets.CountAsync(planet => planet.OwnerPlayerId != null && planet.OwnerPlayerId.StartsWith("parallel-"), Token));
        Assert.All(results.GroupBy(result => result.SystemId), group => Assert.True(group.Count() <= limit));
        Assert.All(await db.Systems.ToListAsync(Token), system => Assert.True(system.NewbieCount <= limit));
    }

    /// <summary>
    /// One sector of 150 systems is generated and stored within two seconds, and the objects
    /// of a system are read within 20 ms (acceptance criterion 9).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_full_sector_is_generated_fast_and_a_system_is_read_fast()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.SystemsMin, "150", reload: false);
        await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.SystemsMax, "150");

        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();

        // The first call warms the code paths up; the measured one is a fresh sector.
        await world.AddSectorAsync(null, false, "test", Token);

        Stopwatch generation = Stopwatch.StartNew();
        SectorSummary sector = await world.AddSectorAsync(null, false, "test", Token);
        generation.Stop();

        Assert.Equal(150, sector.SystemCount);
        Assert.True(generation.Elapsed <= SectorGenerationBudget, $"The sector took {generation.ElapsedMilliseconds} ms.");

        long systemId = await scope.ServiceProvider.GetRequiredService<WorldDbContext>().Systems
            .Where(system => system.SectorId == sector.Id)
            .OrderByDescending(system => system.Id)
            .Select(system => system.Id)
            .FirstAsync(Token);

        await using AsyncServiceScope warm = _factory.Services.CreateAsyncScope();
        await warm.ServiceProvider.GetRequiredService<IWorldModule>().GetSystemContentAsync(systemId, Token);

        const int reads = 10;
        Stopwatch reading = Stopwatch.StartNew();

        for (int index = 0; index < reads; index++)
        {
            await using AsyncServiceScope read = _factory.Services.CreateAsyncScope();
            Assert.NotNull(await read.ServiceProvider.GetRequiredService<IWorldModule>().GetSystemContentAsync(systemId, Token));
        }

        reading.Stop();

        TimeSpan average = reading.Elapsed / reads;
        Assert.True(average <= SystemReadBudget, $"A read of a system took {average.TotalMilliseconds:F1} ms.");
    }

    /// <summary>
    /// A new resource and a new kind of crystals added to the reference tables, with their
    /// rules, reach the stored deposits and field compositions without a change of the code
    /// (acceptance criterion 11).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_new_resource_added_to_the_tables_reaches_the_stored_world()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        int extractable = await db.ResourceGroups.Where(group => group.Code == SeedCodes.MetalGroup).Select(group => group.Id).SingleAsync(Token);
        int crystal = await db.ResourceGroups.Where(group => group.Code == SeedCodes.MineralGroup).Select(group => group.Id).SingleAsync(Token);

        ResourceTypeEntity metal = new() { Code = "test_alloy", NameKey = "world.resource.test_alloy", ResourceGroupId = extractable };
        ResourceTypeEntity gem = new() { Code = "test_gem", NameKey = "world.resource.test_gem", ResourceGroupId = crystal };
        db.ResourceTypes.AddRange(metal, gem);
        await db.SaveChangesAsync(Token);

        // A rule group gives the resources of one resource group; the groups of the metals and of
        // the minerals get the new resources.
        var ruleGroups = await db.RuleGroups
            .Select(group => new
            {
                group.Id,
                ResourceGroupId = db.Rules.Where(rule => rule.RuleGroupId == group.Id)
                    .Join(db.ResourceTypes, rule => rule.ResourceId, resource => resource.Id, (rule, resource) => resource.ResourceGroupId)
                    .First(),
            })
            .ToListAsync(Token);

        foreach (var group in ruleGroups.Where(entry => entry.ResourceGroupId == extractable || entry.ResourceGroupId == crystal))
        {
            db.Rules.Add(new GenerationRuleEntity
            {
                RuleGroupId = group.Id,
                ResourceId = group.ResourceGroupId == extractable ? metal.Id : gem.Id,
                Weight = 1_000_000,
                CoefficientMin = 0.01m,
                CoefficientMax = 0.02m,
            });
        }

        await db.SaveChangesAsync(Token);

        SectorSummary sector = await scope.ServiceProvider.GetRequiredService<IWorldModule>().AddSectorAsync(null, false, "test", Token);

        Assert.True(await db.Deposits.AnyAsync(deposit => deposit.SectorId == sector.Id && deposit.ResourceId == metal.Id, Token));
        Assert.True(await db.Deposits.AnyAsync(deposit => deposit.SectorId == sector.Id && deposit.ResourceId == gem.Id, Token));
        Assert.True(await db.FieldCompositions.AnyAsync(share => share.SectorId == sector.Id && share.ResourceId == metal.Id, Token));
        Assert.True(await db.FieldCompositions.AnyAsync(share => share.SectorId == sector.Id && share.ResourceId == gem.Id, Token));
    }

    /// <summary>
    /// Coordinates have three axes and the distance between systems is three-dimensional:
    /// a system moved along z is further away (acceptance criterion 12).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_distance_between_systems_uses_the_third_axis()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        IWorldModule world = scope.ServiceProvider.GetRequiredService<IWorldModule>();

        List<StarSystemEntity> pair = await db.Systems.Where(system => system.SectorId == 1).OrderBy(system => system.Id).Take(2).ToListAsync(Token);
        Assert.All(pair, system => Assert.Equal(0d, system.Z));

        double flat = (await world.GetSystemDistanceAsync(pair[0].Id, pair[1].Id, Token))!.Value;
        double expectedFlat = Math.Sqrt(Math.Pow(pair[0].X - pair[1].X, 2) + Math.Pow(pair[0].Y - pair[1].Y, 2));

        pair[1].Z = 120;
        await db.SaveChangesAsync(Token);

        double deep = (await world.GetSystemDistanceAsync(pair[0].Id, pair[1].Id, Token))!.Value;

        Assert.Equal(expectedFlat, flat, 6);
        Assert.Equal(Math.Sqrt((flat * flat) + (120d * 120d)), deep, 6);

        StarSystemEntity other = await db.Systems.Where(system => system.SectorId == 2).OrderBy(system => system.Id).FirstAsync(Token);
        SectorEntity first = await db.Sectors.SingleAsync(sector => sector.Id == 1, Token);
        SectorEntity second = await db.Sectors.SingleAsync(sector => sector.Id == 2, Token);
        double across = (await world.GetSystemDistanceAsync(pair[0].Id, other.Id, Token))!.Value;
        double expectedAcross = Math.Sqrt(
            Math.Pow(first.X + pair[0].X - second.X - other.X, 2)
            + Math.Pow(first.Y + pair[0].Y - second.Y - other.Y, 2)
            + Math.Pow(first.Z + pair[0].Z - second.Z - other.Z, 2));

        Assert.Equal(expectedAcross, across, 6);
    }

    /// <summary>
    /// The names of the world schema follow documentation/tech/08, rule 3: a reference to one
    /// row ends with <c>_id</c>, a list of references with <c>_ids</c>, and no column is named
    /// after a resource (acceptance criterion 13).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_world_schema_follows_the_naming_rule()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        WorldDbContext db = scope.ServiceProvider.GetRequiredService<WorldDbContext>();
        List<string> resourceCodes = await db.ResourceTypes.Select(resource => resource.Code).ToListAsync(Token);
        List<string> problems = [];

        foreach (IEntityType entity in db.Model.GetEntityTypes())
        {
            StoreObjectIdentifier table = StoreObjectIdentifier.Table(entity.GetTableName()!);

            foreach (IForeignKey foreignKey in entity.GetForeignKeys())
            {
                foreach (IProperty property in foreignKey.Properties)
                {
                    string column = property.GetColumnName(table)!;

                    if (!column.EndsWith("_id", StringComparison.Ordinal))
                    {
                        problems.Add($"{entity.GetTableName()}.{column} references a row but does not end with _id.");
                    }
                }
            }

            foreach (IProperty property in entity.GetProperties())
            {
                string column = property.GetColumnName(table)!;

                if (column != "id" && column.EndsWith("id", StringComparison.Ordinal) && !column.EndsWith("_id", StringComparison.Ordinal))
                {
                    problems.Add($"{entity.GetTableName()}.{column} uses another suffix than _id.");
                }

                if (resourceCodes.Any(code => column.Contains(code, StringComparison.Ordinal)))
                {
                    problems.Add($"{entity.GetTableName()}.{column} is named after a resource.");
                }
            }

            foreach (INavigation navigation in entity.GetNavigations().Where(navigation => navigation.IsCollection))
            {
                if (!navigation.Name.EndsWith("Ids", StringComparison.Ordinal))
                {
                    problems.Add($"{entity.ClrType.Name}.{navigation.Name} is a list of rows but does not end with Ids.");
                }
            }
        }

        foreach (Type dto in typeof(ObjectDto).Assembly.GetExportedTypes().Where(type => type.Namespace == typeof(ObjectDto).Namespace))
        {
            foreach (System.Reflection.PropertyInfo property in dto.GetProperties())
            {
                bool isIdList = property.PropertyType == typeof(IReadOnlyList<long>) || property.PropertyType == typeof(IReadOnlyList<int>);

                if (isIdList && !property.Name.EndsWith("Ids", StringComparison.Ordinal))
                {
                    problems.Add($"{dto.Name}.{property.Name} is a list of references but does not end with Ids.");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        long systemId = await db.Systems.Where(system => db.Planets.Any(planet => planet.SystemId == system.Id)).Select(system => system.Id).FirstAsync(Token);
        SystemContentDto content = (await scope.ServiceProvider.GetRequiredService<IWorldModule>().GetSystemContentAsync(systemId, Token))!;
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(content, WorldJson.Options));
        JsonElement body = json.RootElement.GetProperty("objects").EnumerateArray().First(entry => entry.GetProperty("planet_id").ValueKind == JsonValueKind.Number);

        Assert.Equal(JsonValueKind.Array, body.GetProperty("deposit_ids").ValueKind);
        Assert.Equal(JsonValueKind.Number, body.GetProperty("object_type_id").ValueKind);
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("deposits")[0].GetProperty("resource_id").ValueKind);
    }

    /// <summary>
    /// Before a radar a newcomer sees only their own body; the development switch opens the map
    /// (task 00004, decisions 9 and 13).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_newcomer_sees_only_the_own_body_unless_the_map_is_open()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        StartPlanetResult start = await services.GetRequiredService<IWorldModule>().AllocateStartPlanetAsync("watcher", Token);
        WorldQueryService queries = services.GetRequiredService<WorldQueryService>();

        IReadOnlyList<SystemDto> systems = await queries.GetSystemsAsync("watcher", start.SectorId, Token);
        SystemContentDto content = (await queries.GetSystemContentAsync("watcher", start.SystemId, Token))!;
        IReadOnlyList<FreePlanetDto> free = await queries.GetFreePlanetsAsync("watcher", start.SectorId, null, Token);
        VisibilityScope stranger = await services.GetRequiredService<IWorldVisibility>().GetScopeAsync("stranger", Token);

        Assert.Equal(start.SystemId, Assert.Single(systems).Id);
        Assert.Equal(start.CelestialObjectId, Assert.Single(content.Objects).Id);
        Assert.Empty(free);
        Assert.Empty(stranger.VisibleObjectIds);
        Assert.Null(await queries.GetSystemContentAsync("stranger", start.SystemId, Token));

        try
        {
            await AetherisTestFactory.SetParameterAsync(services, WorldConfigKeys.RevealAll, "true");

            Assert.True((await queries.GetSystemsAsync("watcher", start.SectorId, Token)).Count > 1);
            Assert.True((await queries.GetSystemContentAsync("watcher", start.SystemId, Token))!.Objects.Count > 1);
            Assert.NotEmpty(await queries.GetFreePlanetsAsync("watcher", start.SectorId, null, Token));
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(services, WorldConfigKeys.RevealAll, "false");
        }
    }
}

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Core.Security;
using Aetheris.Host.Localization;
using Aetheris.Host.Observability;
using Aetheris.Market;
using Aetheris.Planets;
using Aetheris.Planets.Configuration;
using Aetheris.Planets.Services;
using Aetheris.Processes.Configuration;
using Aetheris.Production.Configuration;
using Aetheris.Research.Configuration;
using Aetheris.Tests.Economy;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Observability;
using Aetheris.Tests.Processes;
using Aetheris.Tests.Production;
using Aetheris.Tests.World;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace Aetheris.Tests.Planets;

/// <summary>
/// The meter and the budgets of the reads of a player (task 00036, section 6.3): every read is
/// counted in SQL commands and milliseconds from its own <c>Server-Timing</c>, so a read that
/// grows an N+1 or reads a catalog again turns the budget red.
/// </summary>
/// <remarks>
/// The meter runs with the catalogs kept in memory as a running server keeps them
/// (<c>*.catalog.cache_seconds</c> at the default), because a budget counts the steady state.
/// When the variable <c>AETHERIS_COST_REPORT</c> holds a file path, the table of the first and the
/// warm read of every endpoint is written there; <c>AETHERIS_COST_SQL</c> with a path also writes
/// the text of every SQL command.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed partial class RequestCostTests
{
    private const string ReportVariable = "AETHERIS_COST_REPORT";
    private const string SqlVariable = "AETHERIS_COST_SQL";
    private const string CatalogCacheDefaultSeconds = "30";
    private const string TextsPath = "/api/v1/texts/uk";
    private const string TextsName = "GET " + TextsPath;
    private const string TextsConditionalName = TextsName + " (If-None-Match)";
    private const int ExtraBodyCount = 3;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestCostTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public RequestCostTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The number of the SQL commands that a read may take once the catalogs are in memory, with the
    /// three commands of the check of the session that every signed-in request pays. The numbers
    /// are what the optimization of task 00036 reached; a read that needs more has grown an N+1 or
    /// reads a catalog again.
    /// </summary>
    private static IReadOnlyDictionary<string, int> Budgets { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["GET /api/v1/auth/me"] = 5,
        ["GET /api/v1/world/reference"] = 7,
        ["GET /api/v1/world/races"] = 4,
        ["GET /api/v1/world/civilization"] = 4,
        ["GET /api/v1/world/planets"] = 4,
        ["GET /api/v1/economy/"] = 8,
        ["GET /api/v1/economy/population"] = 6,
        ["GET /api/v1/planets/{planet}"] = 13,
        ["GET /api/v1/production/orders"] = 7,
        ["GET /api/v1/production/recipes"] = 11,
        ["GET /api/v1/production/stock"] = 7,
        ["GET /api/v1/production/factories"] = 4,
        ["GET /api/v1/processes/orders"] = 7,
        ["GET /api/v1/processes/buildings"] = 5,
        ["GET /api/v1/research/"] = 22,
        [TextsName] = 3,
        [TextsConditionalName] = 3,
        ["GET /api/v1/market/overview"] = 10,
        ["GET /api/v1/market/lots"] = 7,
        ["GET /api/v1/market/my-lots"] = 4,
        ["GET /api/v1/market/limits"] = 6,
    };

    /// <summary>
    /// Reads every main endpoint of a player twice, writes the table when asked, and checks
    /// that each read answers and stays inside its budget. The pack of the texts that has not
    /// changed answers 304 to the conditional read.
    /// </summary>
    /// <returns>A task that completes when the reads have been measured.</returns>
    [Fact]
    public async Task The_main_reads_of_a_player_stay_inside_their_budgets()
    {
        using Scenario scenario = await PrepareAsync();
        List<Measurement> rows = await MeasureAsync(scenario, MainPaths(scenario), writeReport: true);

        foreach (Measurement row in rows)
        {
            HttpStatusCode expected = row.Name == TextsConditionalName ? HttpStatusCode.NotModified : HttpStatusCode.OK;

            Assert.True(row.Status == expected, $"{row.Name} answered {(int)row.Status}, expected {(int)expected}.");
            Assert.True(Budgets.ContainsKey(row.Name), $"{row.Name} has no budget.");
            Assert.True(row.WarmCommands <= Budgets[row.Name], $"{row.Name} took {row.WarmCommands} SQL commands, the budget is {Budgets[row.Name]}.");
        }

        Assert.Equal(Budgets.Count, rows.Count);
    }

    /// <summary>
    /// A player with more bodies costs the reads of the civilization as many commands as a player
    /// with one: the reads count the bodies in batches, never one body at a time.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_cost_of_a_read_does_not_grow_with_the_number_of_bodies()
    {
        using Scenario scenario = await PrepareAsync();
        string[] paths =
        [
            "/api/v1/world/planets",
            "/api/v1/economy/",
            "/api/v1/research/",
            "/api/v1/market/overview",
            "/api/v1/market/limits",
            "/api/v1/planets/" + scenario.PlanetId.ToString(CultureInfo.InvariantCulture),
        ];

        List<Measurement> before = await MeasureAsync(scenario, paths, writeReport: false);

        for (int i = 0; i < ExtraBodyCount; i++)
        {
            using PlanetsTestPlayer neighbour = await PlanetsTestData.FoundWithTheWholeStartAsync(_factory);
            await EconomyTestData.GiveBodyAsync(scenario.Services, neighbour.PlanetId, scenario.Player.Account.UserId);
        }

        scenario.Services.GetRequiredService<BuildingCatalogCache>().Invalidate();
        List<Measurement> after = await MeasureAsync(scenario, paths, writeReport: false);

        IReadOnlyList<OwnedPlanetDto> bodies = await scenario.Services.GetRequiredService<WorldQueryService>()
            .GetOwnedPlanetsDetailedAsync(scenario.Player.Account.UserId, Token);
        Assert.Equal(ExtraBodyCount + 1, bodies.Count);

        for (int i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].Name, after[i].Name);
            Assert.True(
                after[i].WarmCommands <= before[i].WarmCommands,
                $"{before[i].Name} took {before[i].WarmCommands} SQL commands with one body and {after[i].WarmCommands} with {ExtraBodyCount + 1}.");
        }
    }

    /// <summary>
    /// The facts of a body that one query reads equal the rows of the world: the names, the type,
    /// the orbit, the deposits and whether the body may be settled.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_facts_of_a_body_equal_the_rows_of_the_world()
    {
        using Scenario scenario = await PrepareAsync();
        WorldDbContext world = scenario.Services.GetRequiredService<WorldDbContext>();
        IWorldModule module = scenario.Services.GetRequiredService<IWorldModule>();

        PlanetFacts facts = (await module.GetPlanetFactsAsync(scenario.PlanetId, Token))!;
        PlanetEntity planet = await world.Planets.AsNoTracking().Include(entry => entry.DepositIds).SingleAsync(entry => entry.Id == scenario.PlanetId, Token);
        CelestialObjectEntity celestial = await world.Objects.AsNoTracking().SingleAsync(entry => entry.Id == planet.CelestialObjectId, Token);

        Assert.Equal(planet.Id, facts.PlanetId);
        Assert.Equal(planet.SectorId, facts.SectorId);
        Assert.Equal(planet.OwnerPlayerId, facts.OwnerPlayerId);
        Assert.Equal(planet.Name, facts.Name);
        Assert.Equal(planet.IsTerraformed, facts.IsTerraformed);
        Assert.Equal(celestial.ObjectTypeId, facts.ObjectTypeId);
        Assert.Equal(planet.SystemId, facts.SystemId);
        Assert.Equal(celestial.Orbit, facts.Orbit);
        Assert.Equal(await world.ObjectTypes.Where(type => type.Id == celestial.ObjectTypeId).Select(type => type.IsColonizable).SingleAsync(Token), facts.IsColonizable);
        Assert.Equal(await world.Systems.Where(system => system.Id == planet.SystemId).Select(system => system.Name).SingleAsync(Token), facts.SystemName);
        Assert.Equal(await world.Sectors.Where(sector => sector.Id == planet.SectorId).Select(sector => sector.Name).SingleAsync(Token), facts.SectorName);
        Assert.Equal(
            planet.DepositIds.OrderBy(deposit => deposit.ResourceId).Select(deposit => (deposit.ResourceId, deposit.Coefficient)),
            facts.Deposits.OrderBy(deposit => deposit.ResourceTypeId).Select(deposit => (deposit.ResourceTypeId, deposit.Coefficient)));

        // A body that does not exist has no facts.
        Assert.Null(await module.GetPlanetFactsAsync(long.MaxValue, Token));
    }

    /// <summary>
    /// The bodies of a player that one query reads equal the rows of the world, with the deposits
    /// in the order of their resources.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_bodies_of_a_player_equal_the_rows_of_the_world()
    {
        using Scenario scenario = await PrepareAsync();
        WorldDbContext world = scenario.Services.GetRequiredService<WorldDbContext>();

        using PlanetsTestPlayer neighbour = await PlanetsTestData.FoundWithTheWholeStartAsync(_factory);
        await EconomyTestData.GiveBodyAsync(scenario.Services, neighbour.PlanetId, scenario.Player.Account.UserId);

        IReadOnlyList<OwnedPlanetDto> bodies = await scenario.Services.GetRequiredService<WorldQueryService>()
            .GetOwnedPlanetsDetailedAsync(scenario.Player.Account.UserId, Token);
        List<PlanetEntity> rows = await world.Planets.AsNoTracking()
            .Include(entry => entry.DepositIds)
            .Where(entry => entry.OwnerPlayerId == scenario.Player.Account.UserId)
            .OrderBy(entry => entry.Id)
            .ToListAsync(Token);

        Assert.Equal(2, bodies.Count);
        Assert.Equal(rows.Select(row => row.Id), bodies.Select(body => body.PlanetId));

        foreach ((PlanetEntity row, OwnedPlanetDto body) in rows.Zip(bodies))
        {
            CelestialObjectEntity celestial = await world.Objects.AsNoTracking().SingleAsync(entry => entry.Id == row.CelestialObjectId, Token);

            Assert.Equal(row.Name, body.Name);
            Assert.Equal(celestial.ObjectTypeId, body.ObjectTypeId);
            Assert.Equal(row.SectorId, body.SectorId);
            Assert.Equal(row.SystemId, body.SystemId);
            Assert.Equal(celestial.Orbit, body.Orbit);
            Assert.Equal(row.SizeClassId, body.SizeClassId);
            Assert.Equal(row.Size, body.Size);
            Assert.Equal(row.IsTerraformed, body.IsTerraformed);
            Assert.Equal(await world.Systems.Where(system => system.Id == row.SystemId).Select(system => system.Name).SingleAsync(Token), body.SystemName);
            Assert.Equal(await world.Sectors.Where(sector => sector.Id == row.SectorId).Select(sector => sector.Name).SingleAsync(Token), body.SectorName);
            Assert.Equal(
                row.DepositIds.OrderBy(deposit => deposit.ResourceId).Select(deposit => (deposit.ResourceId, deposit.Coefficient)),
                body.Deposits.Select(deposit => (deposit.ResourceTypeId, deposit.Coefficient)));
        }
    }

    /// <summary>
    /// The resource catalog stays in memory until a change of the resources in the admin panel
    /// drops it: a new resource and an archived one show at once, a change made behind the back of
    /// the server shows after the time of the cache.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_resource_catalog_is_kept_until_the_admin_panel_changes_it()
    {
        using Scenario scenario = await PrepareAsync();
        IWorldModule world = scenario.Services.GetRequiredService<IWorldModule>();
        WorldDbContext dbContext = scenario.Services.GetRequiredService<WorldDbContext>();

        ResourceCatalog first = await world.GetResourceCatalogAsync(Token);
        Assert.Same(first, await world.GetResourceCatalogAsync(Token));

        // A row written straight into the table is not seen while the catalog is in memory.
        ResourceTypeEntity changed = await dbContext.ResourceTypes.OrderBy(resource => resource.Id).FirstAsync(Token);
        string originalKey = changed.NameKey;
        changed.NameKey = "changed.behind.the.back";
        await dbContext.SaveChangesAsync(Token);

        try
        {
            Assert.Equal(originalKey, (await world.GetResourceCatalogAsync(Token)).Resources.Single(resource => resource.Id == changed.Id).NameKey);
        }
        finally
        {
            changed.NameKey = originalKey;
            await dbContext.SaveChangesAsync(Token);
        }

        // A resource made in the admin panel is in the catalog at once, and so is its archiving.
        TestAdmin owner = await TestAdmins.CreateAsync(_factory, AdminRoles.Owner);
        using HttpClient admin = await TestAdmins.SignInAsync(_factory, owner);
        int groupId = await dbContext.ResourceGroups.AsNoTracking().OrderBy(group => group.Id).Select(group => group.Id).FirstAsync(Token);

        (int id, int version) = await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.CreateAsync(admin, "/world/resources", new
        {
            image_key = (string?)null,
            resource_group_id = groupId,
            scope = ResourceScopes.Planet,
            name = ReferenceAdmin.Texts("Кеш", "Cache"),
            description = ReferenceAdmin.Texts("Кеш опис", "Cache description"),
        }));

        Assert.Contains((await world.GetResourceCatalogAsync(Token)).Resources, resource => resource.Id == id && !resource.IsArchived);

        await ReferenceAdmin.ChangedAsync(await ReferenceAdmin.ArchiveAsync(admin, "/world/resources", id, version));

        Assert.Contains((await world.GetResourceCatalogAsync(Token)).Resources, resource => resource.Id == id && resource.IsArchived);
    }

    private static string[] MainPaths(Scenario scenario)
    {
        string planet = scenario.PlanetId.ToString(CultureInfo.InvariantCulture);

        return
        [
            "/api/v1/auth/me",
            "/api/v1/world/reference",
            "/api/v1/world/races",
            "/api/v1/world/civilization",
            "/api/v1/world/planets",
            "/api/v1/economy/",
            "/api/v1/economy/population?planet_id=" + planet,
            "/api/v1/planets/" + planet,
            "/api/v1/production/orders?planet_id=" + planet,
            "/api/v1/production/recipes?planet_id=" + planet,
            "/api/v1/production/stock?planet_id=" + planet,
            "/api/v1/production/factories?planet_id=" + planet,
            "/api/v1/processes/orders?planet_id=" + planet,
            "/api/v1/processes/buildings?planet_id=" + planet,
            "/api/v1/research/?planetId=" + planet,
            TextsPath,
            "/api/v1/market/overview",
            "/api/v1/market/lots",
            "/api/v1/market/my-lots",
            "/api/v1/market/limits",
        ];
    }

    private async Task<Scenario> PrepareAsync()
    {
        await ProcessesTestData.ResetAsync(_factory);

        IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        await services.GetRequiredService<IMarketModule>().EnsureAsync(Token);

        // The tests keep the catalogs out of memory; a running server keeps them for the default time.
        foreach (string key in new[]
        {
            PlanetsConfigKeys.CatalogCacheSeconds,
            ResearchConfigKeys.CatalogCacheSeconds,
            ProductionConfigKeys.CatalogCacheSeconds,
            ProcessesConfigKeys.CatalogCacheSeconds,
            WorldConfigKeys.CatalogCacheSeconds,
            TextKeys.PackCacheSeconds,
        })
        {
            await AetherisTestFactory.SetParameterAsync(services, key, CatalogCacheDefaultSeconds);
        }

        PlanetsTestPlayer player = await PlanetsTestData.FoundWithTheWholeStartAsync(_factory);
        await ProductionTestData.BuildFactoryAsync(player, services);
        await ProcessesTestData.BuildAsync(player, services, BuildingClasses.OrbitalWorkshop);
        await ProductionTestData.GiveEveryResourceAsync(player, services);
        services.GetRequiredService<BuildingCatalogCache>().Invalidate();
        services.GetRequiredService<ResourceCatalogCache>().Invalidate();

        return new Scenario(scope, player);
    }

    private async Task<List<Measurement>> MeasureAsync(Scenario scenario, IReadOnlyList<string> paths, bool writeReport)
    {
        string? sqlReport = writeReport ? Environment.GetEnvironmentVariable(SqlVariable) : null;
        List<Measurement> rows = [];
        StringBuilder sqlText = new();

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            new Dictionary<string, string> { [ObservabilityKeys.SqlVerbose] = string.IsNullOrEmpty(sqlReport) ? "false" : "true" },
            async () =>
            {
                foreach (string path in paths)
                {
                    Measurement first = await ReadAsync(scenario.Player.Client, path, sqlText);
                    Measurement warm = await ReadAsync(scenario.Player.Client, path, sqlText);

                    rows.Add(first with { WarmCommands = warm.Commands, WarmMilliseconds = warm.Milliseconds, WarmDbMilliseconds = warm.DbMilliseconds });
                }

                if (paths.Contains(TextsPath))
                {
                    // A pack that has not changed answers 304 without being built again.
                    using HttpResponseMessage full = await scenario.Player.Client.GetAsync(TextsPath, Token);
                    HttpRequestMessage conditional = new(HttpMethod.Get, TextsPath);
                    conditional.Headers.TryAddWithoutValidation("If-None-Match", full.Headers.ETag!.ToString());
                    Measurement notModified = await ReadAsync(scenario.Player.Client, conditional, TextsConditionalName, sqlText);

                    rows.Add(notModified with { WarmCommands = notModified.Commands, WarmMilliseconds = notModified.Milliseconds, WarmDbMilliseconds = notModified.DbMilliseconds });
                }
            });

        if (writeReport && Environment.GetEnvironmentVariable(ReportVariable) is { Length: > 0 } reportPath)
        {
            await File.WriteAllTextAsync(reportPath, Table(rows), Token);
        }

        if (!string.IsNullOrEmpty(sqlReport))
        {
            await File.WriteAllTextAsync(sqlReport, sqlText.ToString(), Token);
        }

        return rows;
    }

    private Task<Measurement> ReadAsync(HttpClient client, string path, StringBuilder sqlText)
    {
        HttpRequestMessage request = new(HttpMethod.Get, path);

        return ReadAsync(client, request, "GET " + PlanetPathPattern().Replace(path.Split('?')[0], "/api/v1/planets/{planet}"), sqlText);
    }

    private async Task<Measurement> ReadAsync(HttpClient client, HttpRequestMessage request, string name, StringBuilder sqlText)
    {
        using (request)
        {
            int mark = _factory.Logs.Events.Count;
            using HttpResponseMessage response = await client.SendAsync(request, Token);
            byte[] body = await response.Content.ReadAsByteArrayAsync(Token);
            string timing = response.Headers.GetValues(RequestTraceMiddleware.ServerTimingHeader).Single();
            string requestId = response.Headers.GetValues(RequestTraceMiddleware.RequestIdHeader).Single();
            Match totals = TimingPattern().Match(timing);

            sqlText.AppendLine(CultureInfo.InvariantCulture, $"## {name} [{(int)response.StatusCode}] {timing}");

            foreach (LogEvent logEvent in ObservabilityTestSupport.Since(_factory, mark))
            {
                if (ObservabilityTestSupport.Text(logEvent, "SourceContext") == "Aetheris.Sql"
                    && ObservabilityTestSupport.Text(logEvent, "RequestId") == requestId
                    && ObservabilityTestSupport.Text(logEvent, "SqlText") is { } text)
                {
                    sqlText.AppendLine(CultureInfo.InvariantCulture, $"  {text.ReplaceLineEndings(" ")}");
                }
            }

            return new Measurement(
                name,
                response.StatusCode,
                body.Length,
                int.Parse(totals.Groups["count"].Value, CultureInfo.InvariantCulture),
                double.Parse(totals.Groups["total"].Value, CultureInfo.InvariantCulture),
                double.Parse(totals.Groups["db"].Value, CultureInfo.InvariantCulture));
        }
    }

    private static string Table(IEnumerable<Measurement> rows)
    {
        StringBuilder table = new();

        table.AppendLine("| Endpoint | Status | Bytes | SQL first | ms first | SQL warm | ms warm | db ms warm |");
        table.AppendLine("|---|---|---|---|---|---|---|---|");

        foreach (Measurement row in rows)
        {
            table.AppendLine(CultureInfo.InvariantCulture, $"| {row.Name} | {(int)row.Status} | {row.Bytes} | {row.Commands} | {row.Milliseconds:0} | {row.WarmCommands} | {row.WarmMilliseconds:0} | {row.WarmDbMilliseconds:0} |");
        }

        return table.ToString();
    }

    [GeneratedRegex(@"^/api/v1/planets/\d+$")]
    private static partial Regex PlanetPathPattern();

    [GeneratedRegex(@"total;dur=(?<total>[\d.]+), db;dur=(?<db>[\d.]+);desc=""(?<count>\d+) quer")]
    private static partial Regex TimingPattern();

    private sealed record Measurement(
        string Name,
        HttpStatusCode Status,
        int Bytes,
        int Commands,
        double Milliseconds,
        double DbMilliseconds,
        int WarmCommands = 0,
        double WarmMilliseconds = 0,
        double WarmDbMilliseconds = 0);

    private sealed class Scenario(IServiceScope scope, PlanetsTestPlayer player) : IDisposable
    {
        public PlanetsTestPlayer Player { get; } = player;

        public long PlanetId => Player.PlanetId;

        public IServiceProvider Services => scope.ServiceProvider;

        public void Dispose()
        {
            Player.Dispose();
            scope.Dispose();
        }
    }
}

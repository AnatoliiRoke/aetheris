using Aetheris.Admin;
using Aetheris.Chat;
using Aetheris.Combat;
using Aetheris.Combat.Data;
using Aetheris.Core.Admin;
using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Text;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Economy.Data;
using Aetheris.Fleet;
using Aetheris.Fleet.Data;
using Aetheris.Host.Admin;
using Aetheris.Host.Auth;
using Aetheris.Host.Commands;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Health;
using Aetheris.Host.Localization;
using Aetheris.Host.Observability;
using Aetheris.Host.Realtime;
using Aetheris.Host.Scheduling;
using Aetheris.Host.Security;
using Aetheris.Host.Time;
using Aetheris.Mail;
using Aetheris.Mail.Data;
using Aetheris.Mail.Events;
using Aetheris.Mail.Services;
using Aetheris.Market;
using Aetheris.Market.Data;
using Aetheris.Planets;
using Aetheris.Processes;
using Aetheris.Processes.Data;
using Aetheris.Production;
using Aetheris.Production.Data;
using Aetheris.Rating;
using Aetheris.Requirements;
using Aetheris.Requirements.Data;
using Aetheris.Research;
using Aetheris.Research.Data;
using Aetheris.World;
using Aetheris.World.Api;
using Aetheris.World.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sentry;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// The values below are the technical limits of the server. They are named so that they
// can be reviewed in one place and so that the magic-number rule AETHERIS001 stays
// silent. The balance values live in the database, these are not balance values.

// The share of the traces that Sentry receives. The development environment sends
// everything, the production environment sends a fraction to keep the quota.
const double SentryFullTraceSampleRate = 1.0;
const double SentryReducedTraceSampleRate = 0.2;

// The local configuration file holds the database connection string and any local keys.
// It is git-ignored and never committed (task 00001, section 2.3).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// The masking enricher is the last one, so it also sees the properties added by the
// enrichers before it (task 00002, section 2.7).
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.With<SensitiveDataMaskingEnricher>()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console());

// ---------------------------------------------------------------------------------------------
// Observability: tracing, metrics and error reporting
//
// Measurements are collected from the first day (documentation/tech/01, section 3.4):
// request handling time and event queue length.
// ---------------------------------------------------------------------------------------------
builder.Services.AddOpenTelemetry()
    .ConfigureResource(serviceInfo => serviceInfo.AddService(
        serviceName: "aetheris",
        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporterIfConfigured(builder.Configuration))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddSentry();
builder.Services.Configure<SentryOptions>(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
    options.Release = typeof(Program).Assembly.GetName().Version?.ToString();
    options.TracesSampleRate = builder.Environment.IsDevelopment()
        ? SentryFullTraceSampleRate
        : SentryReducedTraceSampleRate;
    options.SendDefaultPii = false;
    options.AutoSessionTracking = false;
});

// ---------------------------------------------------------------------------------------------
// Configuration
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<GameConfig>();
builder.Services.AddSingleton<IGameConfig>(sp => sp.GetRequiredService<GameConfig>());
builder.Services.AddScoped<IConfigParameterStore, EfConfigParameterStore>();
builder.Services.AddScoped<ConfigSeedService>();
builder.Services.AddSingleton<ConfigReloader>();
builder.Services.AddHostedService<ConfigReloadService>();

// ---------------------------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------------------------
string connectionString = DatabaseOptions.GetConnectionString(builder.Configuration);

builder.Services.AddDbContext<AetherisDbContext>(options =>
    AetherisDbContextSetup.Configure(options, connectionString));

// The contexts of the modules work on the connection of the main context and join its
// transaction, so a request commits as one.
builder.Services.AddScoped<IDatabaseSession, EfDatabaseSession>();

// The neutral codes of the reference rows and the texts of the game data (task 00022).
builder.Services.AddScoped<ICodeSequence, EfCodeSequence>();
builder.Services.AddScoped<SeedCodeReconciliation>();
builder.Services.AddSingleton<LocalizationTextCache>();
builder.Services.AddSingleton<TextSettings>();
builder.Services.AddScoped<ILocalizationTexts, LocalizationTextStore>();
builder.Services.AddScoped<AdminTextService>();

// The pictures of the reference rows of the admin panel (task 00023).
builder.Services.AddSingleton<ReferenceImageStore>();
builder.Services.AddSingleton<IAdminReferenceImages>(sp => sp.GetRequiredService<ReferenceImageStore>());

// ---------------------------------------------------------------------------------------------
// Security: identity, policies, rate limits, command pipeline, admin panel (task 00002)
// ---------------------------------------------------------------------------------------------
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.AddAetherisSecurity(builder.Configuration, builder.Environment);

// ---------------------------------------------------------------------------------------------
// Game clock
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton(_ =>
{
    DateTimeOffset now = DateTimeOffset.UtcNow;

    return new GameClock(now, now);
});
builder.Services.AddSingleton<IGameClock>(sp => sp.GetRequiredService<GameClock>());

// ---------------------------------------------------------------------------------------------
// Event scheduler (documentation/tech/07)
// ---------------------------------------------------------------------------------------------
builder.Services.AddScoped<IEventScheduler, EfEventScheduler>();
builder.Services.AddSingleton<ScheduledEventRunner>();
builder.Services.AddHostedService<ScheduledEventService>();

// ---------------------------------------------------------------------------------------------
// Realtime
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<GameEventPublisher>();
builder.Services.AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment())
    .AddMessagePackProtocol();

// ---------------------------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: false);

// ---------------------------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// ---------------------------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<IRatingModule, RatingModule>();
builder.Services.AddSingleton<IChatModule, ChatModule>();
builder.Services.AddSingleton<IAdminModule, AdminModule>();
builder.Services.AddWorldModule();

// The common requirements module keeps the requirements of every closed subject, so it comes
// before the modules that add their requirements through its contract.
builder.Services.AddRequirementsModule();

// The Planets module plugs into the extension points of the world, so it comes after it.
PlanetsModule.Register(builder.Services);

// The Research module reads the bodies module, so it comes after it.
builder.Services.AddResearchModule();

// The Production module reads the bodies and the research modules, so it comes after them.
builder.Services.AddProductionModule();
builder.Services.AddProcessesModule();

// The Economy module reads the world, and the bodies and the research modules give it their
// effects through the contracts it declares (task 00015).
builder.Services.AddEconomyModule();

// The Market module reads the world, the production and the economy, so it comes after them.
builder.Services.AddScoped<Aetheris.Core.Players.IPlayerDirectory, Aetheris.Host.Data.IdentityPlayerDirectory>();
builder.Services.AddMarketModule();

// The Fleet module reads the world, the bodies and the production, so it comes after them.
builder.Services.AddFleetModule();

// The Combat module keeps its own reference rows and reads only the core (task 00062).
builder.Services.AddCombatModule();

// The Mail module reads the world for the races of the players and writes the letters of the
// game, so it comes after the world (task 00060).
builder.Services.AddMailModule();

// The tracing of the requests and the measuring of the SQL commands of every database context
// (task 00036). It comes after the modules, because it finds their contexts. The settings are
// read from the configuration, so they change without a restart.
builder.Services.AddAetherisObservability();

WebApplication app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Database migrations are applied on startup so that one command starts a working server.
// ---------------------------------------------------------------------------------------------
await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    ILogger startupLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    // The migrations may run under a separate database account with the right to change
    // the schema, while the server itself runs with a minimal account (task 00002,
    // section 2.4). Without a separate connection string the main one is used.
    string? migrationConnectionString = DatabaseOptions.GetMigrationConnectionString(app.Configuration);

    startupLogger.LogInformation("Applying database migrations...");

    AetherisDbContext mainContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

    await StartupTimer.RunAsync(startupLogger, "migrations:main", async () =>
    {
        if (migrationConnectionString is null)
        {
            await mainContext.Database.MigrateAsync().ConfigureAwait(false);
        }
        else
        {
            DbContextOptionsBuilder<AetherisDbContext> migrationOptions = new();
            AetherisDbContextSetup.Configure(migrationOptions, migrationConnectionString);

            await using AetherisDbContext migrationContext = new(migrationOptions.Options);
            await migrationContext.Database.MigrateAsync().ConfigureAwait(false);
        }
    }).ConfigureAwait(false);

    // Every module with its own tables applies its migrations the same way.
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(WorldDatabase), () => WorldDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(PlanetsModule), () => PlanetsModule.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(RequirementsDatabase), () => RequirementsDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(ResearchDatabase), () => ResearchDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(ProductionDatabase), () => ProductionDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(ProcessesDatabase), () => ProcessesDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(EconomyDatabase), () => EconomyDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(MarketDatabase), () => MarketDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(FleetDatabase), () => FleetDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(CombatDatabase), () => CombatDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);
    await StartupTimer.RunAsync(startupLogger, "migrations:" + nameof(MailDatabase), () => MailDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!)).ConfigureAwait(false);

    startupLogger.LogInformation("Database migrations applied.");

    // The configuration parameters are seeded after the migrations. The seed never overwrites an
    // existing value, so a database that was filled by hand keeps its settings.
    ConfigSeedService seedService = scope.ServiceProvider.GetRequiredService<ConfigSeedService>();
    await StartupTimer.RunAsync(startupLogger, "seed:config", () => seedService.SeedAsync()).ConfigureAwait(false);

    // The game time goes on from the stored anchor, so a restart never moves it back (code audit
    // of 04.11.2026); the scheduler and the requests start after it.
    await StartupTimer.RunAsync(startupLogger, "clock:restore", () => GameClockStore.RestoreAsync(mainContext, scope.ServiceProvider.GetRequiredService<GameClock>())).ConfigureAwait(false);

    // The protection limits are read from the configuration, so it is loaded before the
    // first request instead of waiting for the background reload.
    await StartupTimer.RunAsync(startupLogger, "config:reload", () => scope.ServiceProvider.GetRequiredService<ConfigReloader>().ReloadAsync()).ConfigureAwait(false);

    // The command that brings the codes of a database to the codes of the starting data runs on
    // the migrated schema and before the seeders, because a seeder stops on a table whose codes
    // are not the codes of the file (task 00052). It does not start the web server.
    if (SeedCodeCommandLine.IsCommand(args))
    {
        return await SeedCodeCommandLine.RunAsync(app.Services, args, Console.Out).ConfigureAwait(false);
    }

    // A generation run of the admin panel that was running when the server stopped is marked
    // failed, so the next run is not blocked (task 00007, decision 3).
    await StartupTimer.RunAsync(startupLogger, "fail_interrupted_runs:" + nameof(IWorldModule), () => scope.ServiceProvider.GetRequiredService<IWorldModule>().FailInterruptedGenerationRunsAsync()).ConfigureAwait(false);

    // The first start creates the galaxy and its initial sectors; a later start finds them
    // and changes nothing (task 00004, section 2.6).
    if (app.Configuration.GetValue(WorldStartup.GenerateOnStartupKey, true))
    {
        WorldBootstrapResult world = await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IWorldModule), () => scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync()).ConfigureAwait(false);

        startupLogger.LogInformation(
            "World ready: galaxy created {GalaxyCreated}, new sectors {Sectors}.",
            world.GalaxyCreated,
            world.CreatedSectorIds.Count);

        // The Planets module fills its tables once the world has its reference data, and adopts
        // the civilizations created before it (task 00011).
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IPlanetsModule), () => scope.ServiceProvider.GetRequiredService<IPlanetsModule>().EnsureAsync()).ConfigureAwait(false);

        // The Research module fills its tables once the bodies module has its own reference
        // data, because a research starts on a body (task 00012).
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IResearchModule), () => scope.ServiceProvider.GetRequiredService<IResearchModule>().EnsureAsync()).ConfigureAwait(false);

        // The Production module fills its tables once the bodies and the research modules have
        // their own reference data, because a factory stands on a body (task 00013).
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IProductionModule), () => scope.ServiceProvider.GetRequiredService<IProductionModule>().EnsureAsync()).ConfigureAwait(false);
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IFleetModule), () => scope.ServiceProvider.GetRequiredService<IFleetModule>().EnsureAsync()).ConfigureAwait(false);
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(ICombatModule), () => scope.ServiceProvider.GetRequiredService<ICombatModule>().EnsureAsync()).ConfigureAwait(false);

        // The Processes module fills its tables once the bodies, the research and the production
        // modules have their own reference data, because a process runs on a body and works with
        // the parts (task 00014).
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IProcessesModule), () => scope.ServiceProvider.GetRequiredService<IProcessesModule>().EnsureAsync()).ConfigureAwait(false);

        // The Economy module fills its tables once the bodies, the research and the world modules
        // have their own reference data, because the numbers of the game live in them (task 00015).
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IEconomyModule), () => scope.ServiceProvider.GetRequiredService<IEconomyModule>().EnsureAsync()).ConfigureAwait(false);

        // The Market module fills its levels of the market once the bodies, the production and the
        // economy modules have their own reference data, because a lot reads them (task 00024).
        await StartupTimer.RunAsync(startupLogger, "ensure:" + nameof(IMarketModule), () => scope.ServiceProvider.GetRequiredService<IMarketModule>().EnsureAsync()).ConfigureAwait(false);

        // The Mail module writes the categories of the letters and their texts, and puts the first
        // cleanup of the bin in place (task 00060).
        await StartupTimer.RunAsync(startupLogger, "ensure:mail", () => scope.ServiceProvider.GetRequiredService<MailBootstrapService>().EnsureAsync()).ConfigureAwait(false);
        await StartupTimer.RunAsync(startupLogger, "schedule:mail_trash", () => scope.ServiceProvider.GetRequiredService<MailTrashCleanupHandler>().ScheduleNextAsync()).ConfigureAwait(false);
    }
}

if (AdminCommandLine.IsCommand(args))
{
    return await AdminCommandLine.RunAsync(app.Services, args).ConfigureAwait(false);
}

// The tracing stands before the protection pipeline, so the rejected requests are traced too
// (task 00036).
app.UseAetherisRequestTrace();
app.UseAetherisSecurity();

// Prometheus scraping endpoint. The metrics are collected by OpenTelemetry, this only
// exposes them for a scraper.
app.MapPrometheusScrapingEndpoint("/metrics");

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthResponse,
});

app.MapHub<GameHub>(GameHub.Route);

app.MapGet("/api/v1/ping", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapTextEndpoints();
app.MapReferenceImageEndpoints();
app.MapWorldEndpoints();
PlanetsModule.MapEndpoints(app);
ResearchServiceCollectionExtensions.MapEndpoints(app);
ProductionServiceCollectionExtensions.MapEndpoints(app);
ProcessesServiceCollectionExtensions.MapEndpoints(app);
EconomyServiceCollectionExtensions.MapEndpoints(app);
MarketServiceCollectionExtensions.MapEndpoints(app);
FleetServiceCollectionExtensions.MapEndpoints(app);
MailServiceCollectionExtensions.MapEndpoints(app);
app.MapCommands();

await app.RunAsync().ConfigureAwait(false);

return 0;

/// <summary>
/// Writes the health report as JSON, including the state of every check.
/// </summary>
/// <param name="context">The health check context.</param>
/// <param name="report">The health report.</param>
/// <returns>A task that completes when the response has been written.</returns>
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds,
            data = entry.Value.Data,
        }),
    };

    return context.Response.WriteAsJsonAsync(payload);
}

/// <summary>
/// Entry point marker, used by the integration test host.
/// </summary>
public partial class Program
{
}

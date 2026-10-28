using Aetheris.Admin;
using Aetheris.Chat;
using Aetheris.Core.Configuration;
using Aetheris.Core.Data;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Text;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Fleet;
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
using Aetheris.Market;
using Aetheris.Planets;
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
builder.Services.AddSingleton<LocalizationTextCache>();
builder.Services.AddSingleton<TextSettings>();
builder.Services.AddScoped<ILocalizationTexts, LocalizationTextStore>();
builder.Services.AddScoped<AdminTextService>();

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
builder.Services.AddSingleton<IEconomyModule, EconomyModule>();
builder.Services.AddSingleton<IFleetModule, FleetModule>();
builder.Services.AddSingleton<IMarketModule, MarketModule>();
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

    // Every module with its own tables applies its migrations the same way.
    await WorldDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!).ConfigureAwait(false);
    await RequirementsDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!).ConfigureAwait(false);
    await PlanetsModule.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!).ConfigureAwait(false);
    await ResearchDatabase.MigrateAsync(migrationConnectionString ?? mainContext.Database.GetConnectionString()!).ConfigureAwait(false);

    startupLogger.LogInformation("Database migrations applied.");

    // The configuration parameters are seeded after the migrations. The seed never overwrites an
    // existing value, so a database that was filled by hand keeps its settings.
    ConfigSeedService seedService = scope.ServiceProvider.GetRequiredService<ConfigSeedService>();
    await seedService.SeedAsync().ConfigureAwait(false);

    // The protection limits are read from the configuration, so it is loaded before the
    // first request instead of waiting for the background reload.
    await scope.ServiceProvider.GetRequiredService<ConfigReloader>().ReloadAsync().ConfigureAwait(false);

    // A generation run of the admin panel that was running when the server stopped is marked
    // failed, so the next run is not blocked (task 00007, decision 3).
    await scope.ServiceProvider.GetRequiredService<IWorldModule>().FailInterruptedGenerationRunsAsync().ConfigureAwait(false);

    // The first start creates the galaxy and its initial sectors; a later start finds them
    // and changes nothing (task 00004, section 2.6).
    if (app.Configuration.GetValue(WorldStartup.GenerateOnStartupKey, true))
    {
        WorldBootstrapResult world = await scope.ServiceProvider.GetRequiredService<IWorldModule>().EnsureWorldAsync().ConfigureAwait(false);

        startupLogger.LogInformation(
            "World ready: galaxy created {GalaxyCreated}, new sectors {Sectors}.",
            world.GalaxyCreated,
            world.CreatedSectorIds.Count);
    }

    // The Planets module fills its tables once the world has its reference data, and adopts
    // the civilizations created before it (task 00011).
    await scope.ServiceProvider.GetRequiredService<IPlanetsModule>().EnsureAsync().ConfigureAwait(false);

    // The Research module fills its tables once the bodies module has its own reference
    // data, because a research starts on a body (task 00012).
    await scope.ServiceProvider.GetRequiredService<IResearchModule>().EnsureAsync().ConfigureAwait(false);
}

if (AdminCommandLine.IsCommand(args))
{
    return await AdminCommandLine.RunAsync(app.Services, args).ConfigureAwait(false);
}

app.UseSerilogRequestLogging();
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
app.MapWorldEndpoints();
PlanetsModule.MapEndpoints(app);
ResearchServiceCollectionExtensions.MapEndpoints(app);
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

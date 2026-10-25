using Aetheris.Admin;
using Aetheris.Chat;
using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Fleet;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Health;
using Aetheris.Host.Observability;
using Aetheris.Host.Realtime;
using Aetheris.Market;
using Aetheris.Planets;
using Aetheris.Rating;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
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

// The minimal length of a password of an administrator account.
const int MinimumPasswordLength = 12;

// The maximal size of a SignalR message, in bytes.
const int MaximumSignalRMessageSize = 64 * 1024;

// The local configuration file holds the database connection string and any local keys.
// It is git-ignored and never committed (task 00001, section 2.3).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
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
    options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AetherisDbContext).Assembly.FullName)));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = MinimumPasswordLength;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<AetherisDbContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------------------------
// Game clock
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<IGameClock>(sp =>
{
    DateTimeOffset now = DateTimeOffset.UtcNow;
    GameClock clock = new(now, now);

    return clock;
});

// ---------------------------------------------------------------------------------------------
// Realtime
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<GameEventPublisher>();
builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaximumReceiveMessageSize = MaximumSignalRMessageSize;
    })
    .AddMessagePackProtocol();

// ---------------------------------------------------------------------------------------------
// Validation
// ---------------------------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ---------------------------------------------------------------------------------------------
// Health checks
// ---------------------------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

// ---------------------------------------------------------------------------------------------
// Modules (stubs, no game logic yet)
// ---------------------------------------------------------------------------------------------
builder.Services.AddSingleton<IEconomyModule, EconomyModule>();
builder.Services.AddSingleton<IPlanetsModule, PlanetsModule>();
builder.Services.AddSingleton<IFleetModule, FleetModule>();
builder.Services.AddSingleton<IMarketModule, MarketModule>();
builder.Services.AddSingleton<IRatingModule, RatingModule>();
builder.Services.AddSingleton<IChatModule, ChatModule>();
builder.Services.AddSingleton<IAdminModule, AdminModule>();

WebApplication app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Database migrations are applied on startup so that one command starts a working server.
// ---------------------------------------------------------------------------------------------
await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
    ILogger startupLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    startupLogger.LogInformation("Applying database migrations...");
    await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    startupLogger.LogInformation("Database migrations applied.");

    // The configuration parameters are seeded after the migrations. The seed never overwrites an
    // existing value, so a database that was filled by hand keeps its settings.
    ConfigSeedService seedService = scope.ServiceProvider.GetRequiredService<ConfigSeedService>();
    await seedService.SeedAsync().ConfigureAwait(false);
}

app.UseSerilogRequestLogging();
app.UseRouting();

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

app.Run();

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

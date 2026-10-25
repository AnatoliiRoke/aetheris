using Aetheris.Core.Configuration;
using Aetheris.Core.Time;
using Aetheris.Economy;
using Aetheris.Planets;
using Aetheris.Fleet;
using Aetheris.Market;
using Aetheris.Rating;
using Aetheris.Chat;
using Aetheris.Admin;
using Aetheris.Host.Configuration;
using Aetheris.Host.Data;
using Aetheris.Host.Health;
using Aetheris.Host.Realtime;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using FluentValidation;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
// Configuration
// ---------------------------------------------------------------------------------------------
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.AddSingleton<GameConfig>();
builder.Services.AddSingleton<IGameConfig>(sp => sp.GetRequiredService<GameConfig>());
builder.Services.AddScoped<IConfigParameterStore, EfConfigParameterStore>();
builder.Services.AddSingleton<ConfigReloader>();
builder.Services.AddHostedService<ConfigReloadService>();

// ---------------------------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------------------------
DatabaseOptions databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

string connectionString = databaseOptions.GetConnectionString(builder.Configuration);

builder.Services.AddDbContext<AetherisDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AetherisDbContext).Assembly.FullName)));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
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
        options.MaximumReceiveMessageSize = 64 * 1024;
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
}

app.UseSerilogRequestLogging();
app.UseRouting();

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

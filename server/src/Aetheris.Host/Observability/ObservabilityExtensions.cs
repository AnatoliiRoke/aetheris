using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

namespace Aetheris.Host.Observability;

/// <summary>
/// Registers the tracing of the requests and of the SQL commands (task 00036).
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// The template of the line that tells about a request. The summary is text made by
    /// <see cref="Summarize"/>, empty when the tracing is off.
    /// </summary>
    private const string RequestMessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms{TraceSummary:l}";

    /// <summary>
    /// Adds the settings and the interceptors that measure the SQL commands, the transactions and
    /// the saves of every database context of the server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    /// <remarks>
    /// Call it after the modules are registered: it adds the interceptors to every context that is
    /// registered at that moment, found by its <see cref="DbContextOptions{TContext}"/>. The
    /// contexts of all the modules are covered by this one call, and no module registers anything.
    /// </remarks>
    public static IServiceCollection AddAetherisObservability(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ObservabilitySettings>();
        services.AddSingleton<SqlTimingInterceptor>();
        services.AddSingleton<TransactionTimingInterceptor>();
        services.AddSingleton<SaveChangesTimingInterceptor>();

        MethodInfo configure = typeof(ObservabilityExtensions).GetMethod(nameof(ConfigureContext), BindingFlags.NonPublic | BindingFlags.Static)!;

        List<Type> contextTypes =
        [
            .. services
                .Select(descriptor => descriptor.ServiceType)
                .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
                .Select(type => type.GetGenericArguments()[0])
                .Distinct(),
        ];

        foreach (Type contextType in contextTypes)
        {
            configure.MakeGenericMethod(contextType).Invoke(null, [services]);
        }

        return services;
    }

    private static void ConfigureContext<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        services.ConfigureDbContext<TContext>((serviceProvider, options) => options.AddInterceptors(
            serviceProvider.GetRequiredService<SqlTimingInterceptor>(),
            serviceProvider.GetRequiredService<TransactionTimingInterceptor>(),
            serviceProvider.GetRequiredService<SaveChangesTimingInterceptor>()));
    }

    /// <summary>
    /// Adds the middleware of the tracing and the log line of every request.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application.</returns>
    /// <remarks>
    /// The middleware stands before the log line, so the identifier of the request is a property of
    /// the line, and before the protection pipeline, so the rejected requests are traced too.
    /// </remarks>
    public static WebApplication UseAetherisRequestTrace(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ObservabilitySettings settings = app.Services.GetRequiredService<ObservabilitySettings>();

        app.UseMiddleware<RequestTraceMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = RequestMessageTemplate;
            options.GetLevel = (httpContext, elapsedMilliseconds, exception) =>
                GetLevel(httpContext, elapsedMilliseconds, exception, settings.Current);
            options.EnrichDiagnosticContext = EnrichRequestLine;
        });

        return app;
    }

    private static LogEventLevel GetLevel(HttpContext httpContext, double elapsedMilliseconds, Exception? exception, ObservabilitySnapshot settings)
    {
        if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        return settings.TraceEnabled && elapsedMilliseconds >= settings.SlowRequestMilliseconds
            ? LogEventLevel.Warning
            : LogEventLevel.Information;
    }

    private static void EnrichRequestLine(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        if (!httpContext.Items.TryGetValue(RequestTraceMiddleware.TraceItemKey, out object? item) || item is not RequestTrace trace)
        {
            diagnosticContext.Set("TraceSummary", string.Empty);

            return;
        }

        long? responseBytes = RequestTraceMiddleware.ResponseBytes(httpContext);

        diagnosticContext.Set("DbMs", trace.DatabaseMilliseconds);
        diagnosticContext.Set("DbCommands", trace.Commands);
        diagnosticContext.Set("ResponseBytes", responseBytes);
        diagnosticContext.Set("BodiesCaptured", httpContext.Items.TryGetValue(RequestTraceMiddleware.BodiesCapturedItemKey, out object? captured) && captured is true);
        diagnosticContext.Set("TraceSummary", Summarize(trace, responseBytes));
    }

    private static string Summarize(RequestTrace trace, long? responseBytes) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $" | request {trace.RequestId}, {responseBytes?.ToString(CultureInfo.InvariantCulture) ?? "-"} B, db {trace.DatabaseMilliseconds:0.0} ms in {trace.Commands} commands");
}

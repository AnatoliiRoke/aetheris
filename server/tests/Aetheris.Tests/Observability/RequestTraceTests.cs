using System.Net;
using System.Text.RegularExpressions;
using Aetheris.Core.Configuration;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Observability;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;

namespace Aetheris.Tests.Observability;

/// <summary>
/// Tests of the identifier, the timing headers and the log line of a request (task 00036,
/// acceptance criteria 1, 3 and 5).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class RequestTraceTests
{
    private const int MinimumContextCount = 9;
    private const string PingPath = "/api/v1/ping";
    private const string MePath = AuthEndpoints.Prefix + "/me";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestTraceTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public RequestTraceTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// A response carries the request identifier, the trace identifier and the timing with the
    /// share of the database.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_response_carries_the_identifiers_and_the_server_timing()
    {
        using HttpClient client = await SignedInClientAsync();

        HttpResponseMessage response = await client.GetAsync(MePath, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues(RequestTraceMiddleware.RequestIdHeader).Single());
        Assert.Matches("^[0-9a-f]{32}$", response.Headers.GetValues(RequestTraceMiddleware.TraceIdHeader).Single());

        string timing = response.Headers.GetValues(RequestTraceMiddleware.ServerTimingHeader).Single();

        Assert.Matches(ServerTimingPattern(), timing);
        Assert.True(QueryCount(timing) >= 1, "The request reads the account, so the database share has commands: " + timing);
    }

    /// <summary>
    /// The identifier that a client sends is kept: it is in the response and in the log line of
    /// the request.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_request_identifier_of_the_client_reaches_the_response_and_the_log()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        int mark = _factory.Logs.Events.Count;

        using HttpRequestMessage request = new(HttpMethod.Get, PingPath);
        request.Headers.Add(RequestTraceMiddleware.RequestIdHeader, "client-id-123.abc:4");

        HttpResponseMessage response = await client.SendAsync(request, Token);

        Assert.Equal("client-id-123.abc:4", response.Headers.GetValues(RequestTraceMiddleware.RequestIdHeader).Single());

        LogEvent? line = ObservabilityTestSupport.RequestLine(ObservabilityTestSupport.Since(_factory, mark), PingPath);

        Assert.NotNull(line);
        Assert.Equal("client-id-123.abc:4", ObservabilityTestSupport.Text(line, "RequestId"));
        Assert.Contains("client-id-123.abc:4", line.RenderMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// An identifier that is too long or holds other characters than letters, digits and
    /// <c>-_.:</c> is replaced by a generated one.
    /// </summary>
    /// <param name="sent">The identifier of the client.</param>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Theory]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnop")]
    [InlineData("has space inside")]
    [InlineData("bad<script>alert(1)")]
    public async Task A_long_or_malformed_request_identifier_is_replaced(string sent)
    {
        using HttpClient client = _factory.CreateBrowserClient();
        int mark = _factory.Logs.Events.Count;

        using HttpRequestMessage request = new(HttpMethod.Get, PingPath);
        request.Headers.TryAddWithoutValidation(RequestTraceMiddleware.RequestIdHeader, sent);

        HttpResponseMessage response = await client.SendAsync(request, Token);
        string requestId = response.Headers.GetValues(RequestTraceMiddleware.RequestIdHeader).Single();

        Assert.NotEqual(sent, requestId);
        Assert.Matches("^[0-9a-f]{32}$", requestId);
        Assert.DoesNotContain(
            ObservabilityTestSupport.Since(_factory, mark),
            logEvent => ObservabilityTestSupport.Text(logEvent, "RequestId") == sent);
    }

    /// <summary>
    /// The log line of a request has the identifier, the time of the database and the number of
    /// the commands, and the same numbers are in the message that the console shows.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_request_line_has_the_identifier_and_the_database_figures()
    {
        using HttpClient client = await SignedInClientAsync();
        int mark = _factory.Logs.Events.Count;

        HttpResponseMessage response = await client.GetAsync(MePath, Token);
        string requestId = response.Headers.GetValues(RequestTraceMiddleware.RequestIdHeader).Single();

        LogEvent? line = ObservabilityTestSupport.RequestLine(ObservabilityTestSupport.Since(_factory, mark), MePath);

        Assert.NotNull(line);
        Assert.Equal(requestId, ObservabilityTestSupport.Text(line, "RequestId"));
        Assert.Equal("200", ObservabilityTestSupport.Text(line, "StatusCode"));
        Assert.True(int.Parse(ObservabilityTestSupport.Text(line, "DbCommands")!, System.Globalization.CultureInfo.InvariantCulture) >= 1);
        Assert.NotNull(ObservabilityTestSupport.Text(line, "DbMs"));
        Assert.Contains("db ", line.RenderMessage(), StringComparison.Ordinal);
        Assert.Contains(requestId, line.RenderMessage(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The number of the commands in <c>Server-Timing</c> is the number of the SQL commands that
    /// the known request ran, and it stays small: the reading of the account is not a loop.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Server_timing_counts_the_commands_of_a_known_request()
    {
        using HttpClient client = await SignedInClientAsync();

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            new Dictionary<string, string> { [ObservabilityKeys.SqlVerbose] = "true" },
            async () =>
            {
                int mark = _factory.Logs.Events.Count;

                HttpResponseMessage response = await client.GetAsync(MePath, Token);
                string requestId = response.Headers.GetValues(RequestTraceMiddleware.RequestIdHeader).Single();
                int reported = QueryCount(response.Headers.GetValues(RequestTraceMiddleware.ServerTimingHeader).Single());

                int logged = ObservabilityTestSupport.Since(_factory, mark).Count(logEvent =>
                    ObservabilityTestSupport.Text(logEvent, "SourceContext") == "Aetheris.Sql"
                    && ObservabilityTestSupport.Text(logEvent, "SqlText") is not null
                    && ObservabilityTestSupport.Text(logEvent, "RequestId") == requestId);

                Assert.Equal(logged, reported);
                Assert.InRange(reported, 1, 6);
            });
    }

    /// <summary>
    /// The switch of the tracing applies without a restart: off, the response has no
    /// identifiers; on again, it has them.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_tracing_switch_applies_without_a_restart()
    {
        using HttpClient client = _factory.CreateBrowserClient();

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            new Dictionary<string, string> { [ObservabilityKeys.HttpTraceEnabled] = "false" },
            async () =>
            {
                int mark = _factory.Logs.Events.Count;
                HttpResponseMessage off = await client.GetAsync(PingPath, Token);

                Assert.False(off.Headers.Contains(RequestTraceMiddleware.RequestIdHeader));
                Assert.False(off.Headers.Contains(RequestTraceMiddleware.ServerTimingHeader));

                LogEvent? line = ObservabilityTestSupport.RequestLine(ObservabilityTestSupport.Since(_factory, mark), PingPath);

                Assert.NotNull(line);
                Assert.Null(ObservabilityTestSupport.Text(line, "DbCommands"));
                Assert.DoesNotContain("request ", line.RenderMessage(), StringComparison.Ordinal);

                await Task.CompletedTask;
            });

        HttpResponseMessage on = await client.GetAsync(PingPath, Token);

        Assert.True(on.Headers.Contains(RequestTraceMiddleware.RequestIdHeader));
    }

    /// <summary>
    /// A request slower than the threshold is a warning, and the threshold changes without a
    /// restart.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_request_over_the_slow_threshold_is_a_warning()
    {
        using HttpClient client = _factory.CreateBrowserClient();

        int mark = _factory.Logs.Events.Count;
        await client.GetAsync(PingPath, Token);
        Assert.Equal(LogEventLevel.Information, ObservabilityTestSupport.RequestLine(ObservabilityTestSupport.Since(_factory, mark), PingPath)!.Level);

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            new Dictionary<string, string> { [ObservabilityKeys.SlowRequestMilliseconds] = "0" },
            async () =>
            {
                int slowMark = _factory.Logs.Events.Count;
                await client.GetAsync(PingPath, Token);

                Assert.Equal(LogEventLevel.Warning, ObservabilityTestSupport.RequestLine(ObservabilityTestSupport.Since(_factory, slowMark), PingPath)!.Level);
            });
    }

    /// <summary>
    /// A request that the protection rejects is traced too, because the middleware stands before it.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_rejected_request_is_traced()
    {
        using HttpClient client = _factory.CreateBrowserClient();

        HttpResponseMessage response = await client.GetAsync(MePath, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains(RequestTraceMiddleware.RequestIdHeader));
        Assert.True(response.Headers.Contains(RequestTraceMiddleware.ServerTimingHeader));
    }

    /// <summary>
    /// The parameters exist with the starting values: the tracing is on, the bodies are off, and
    /// each parameter is in the seed and in the database.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_parameters_are_seeded_with_the_production_defaults()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        List<string> stored = await dbContext.ConfigParameters.AsNoTracking().Select(parameter => parameter.Key).ToListAsync(Token);

        foreach (ConfigParameterDefinition definition in ObservabilityKeys.Definitions)
        {
            Assert.NotNull(ConfigSeed.Find(definition.Key));
            Assert.Contains(definition.Key, stored);
            Assert.StartsWith("observability.", definition.Key, StringComparison.Ordinal);
        }

        Assert.Equal("true", ConfigSeed.Find(ObservabilityKeys.HttpTraceEnabled)!.DefaultValue);
        Assert.Equal("false", ConfigSeed.Find(ObservabilityKeys.HttpBodiesEnabled)!.DefaultValue);
        Assert.Equal("false", ConfigSeed.Find(ObservabilityKeys.SqlVerbose)!.DefaultValue);
    }

    /// <summary>
    /// The start of the server wrote the duration of its steps.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_startup_steps_are_logged_with_their_duration()
    {
        _ = _factory.Services;
        await Task.CompletedTask;

        List<string?> steps = [.. _factory.Logs.Events.Select(logEvent => ObservabilityTestSupport.Text(logEvent, "StepName"))];

        Assert.Contains("migrations:main", steps);
        Assert.True(steps.Count(step => step is not null && step.StartsWith("migrations:", StringComparison.Ordinal)) > 1, "Every module reports its migrations.");
        Assert.Contains("seed:config", steps);
        Assert.Contains("config:reload", steps);
    }

    /// <summary>
    /// A scheduled event writes how long it took and how much of it was the database, and the
    /// pass writes its totals.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_scheduled_event_logs_its_duration_and_database_share()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, account);

        HttpResponseMessage requested = await TestAccounts.SendCommandAsync(
            client,
            AccountActions.Prefix + "/delete",
            new { password = account.Password },
            "trace-delete-1");

        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);

        int mark = _factory.Logs.Events.Count;

        await ShiftedGameClock.RunDueEventsAsync(_factory, TimeSpan.FromDays(8));

        List<LogEvent> events = ObservabilityTestSupport.Since(_factory, mark);
        LogEvent? handled = events.FirstOrDefault(logEvent => ObservabilityTestSupport.Text(logEvent, "EventType") is not null
            && logEvent.MessageTemplate.Text.StartsWith("Scheduled event", StringComparison.Ordinal));

        Assert.NotNull(handled);
        Assert.StartsWith("event-", ObservabilityTestSupport.Text(handled, "RequestId"), StringComparison.Ordinal);
        Assert.NotNull(ObservabilityTestSupport.Text(handled, "DbCommands"));
        Assert.Contains(events, logEvent => logEvent.MessageTemplate.Text.StartsWith("Scheduler pass", StringComparison.Ordinal));
    }

    /// <summary>
    /// The commands of the context of every module are measured, and no context of these
    /// assemblies is left out of the list: the interceptors are added in one place for all.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Every_database_context_is_measured_by_the_one_registration()
    {
        // The contexts are found in the assemblies that the host references, so a module that
        // adds a context later is covered by this test without a change here.
        List<Type> contexts =
        [
            .. typeof(Program).Assembly.GetReferencedAssemblies()
                .Where(name => name.Name is not null && name.Name.StartsWith("Aetheris.", StringComparison.Ordinal))
                .Select(System.Reflection.Assembly.Load)
                .Append(typeof(Program).Assembly)
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type is { IsAbstract: false } && type.IsSubclassOf(typeof(DbContext)) && !type.Name.EndsWith("Factory", StringComparison.Ordinal)),
        ];

        Assert.True(contexts.Count >= MinimumContextCount, "The contexts of the modules were not found: " + contexts.Count);

        foreach (Type contextType in contexts)
        {
            using IServiceScope scope = _factory.Services.CreateScope();
            DbContext context = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
            RequestTrace trace = new("probe-" + contextType.Name);

            using (trace.Begin())
            {
                await context.Database.SqlQuery<int>($"SELECT 1 AS \"Value\"").ToListAsync(Token);
            }

            Assert.True(trace.Commands >= 1, contextType.Name + " is not measured.");
        }
    }

    private static int QueryCount(string serverTiming)
    {
        Match match = QueriesPattern().Match(serverTiming);

        return int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);
        HttpClient client = _factory.CreateBrowserClient();

        await TestAccounts.SignInAsync(client, account);

        return client;
    }

    [GeneratedRegex(@"^total;dur=\d+(\.\d)?, db;dur=\d+(\.\d)?;desc=""\d+ quer(y|ies)""$")]
    private static partial Regex ServerTimingPattern();

    [GeneratedRegex(@"desc=""(?<count>\d+) quer")]
    private static partial Regex QueriesPattern();
}

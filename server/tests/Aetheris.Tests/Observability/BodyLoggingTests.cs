using System.Net;
using System.Net.Http.Json;
using System.Text;
using Aetheris.Core.Configuration;
using Aetheris.Host.Auth;
using Aetheris.Host.Observability;
using Aetheris.Tests.Infrastructure;
using Serilog.Events;

namespace Aetheris.Tests.Observability;

/// <summary>
/// Tests of the logging of the JSON bodies and of the SQL commands (task 00036, acceptance
/// criteria 2, 3 and 4).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BodyLoggingTests
{
    private const string LoginPath = AuthEndpoints.Prefix + "/login";
    private const string PaddingValue = "padding-padding-padding-padding-padding-padding-padding-padding";
    private const int PaddingRepeat = 12;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="BodyLoggingTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public BodyLoggingTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> BodiesOn(int maxBytes) => new()
    {
        [ObservabilityKeys.HttpBodiesEnabled] = "true",
        [ObservabilityKeys.HttpBodiesMaxBytes] = maxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// A JSON body is written with its secret fields masked, and nothing of the secrets is in the log.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_json_body_is_logged_with_the_secret_fields_masked()
    {
        string email = TestAccounts.NewEmail();
        const string password = "Never-in-the-log-9317";

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            BodiesOn(maxBytes: 4096),
            async () =>
            {
                using HttpClient client = _factory.CreateBrowserClient();
                int mark = _factory.Logs.Events.Count;

                await client.PostAsJsonAsync(LoginPath, TestAccounts.LoginBody(email, password), Token);

                List<LogEvent> events = ObservabilityTestSupport.Since(_factory, mark);
                LogEvent bodies = Assert.Single(events, logEvent => logEvent.MessageTemplate.Text.StartsWith("HTTP bodies", StringComparison.Ordinal));
                string requestBody = ObservabilityTestSupport.Text(bodies, "RequestBody")!;

                Assert.Contains("\"password\":\"***\"", requestBody, StringComparison.Ordinal);
                Assert.Contains("\"login\":\"***\"", requestBody, StringComparison.Ordinal);
                Assert.Contains("\"remember_me\":false", requestBody, StringComparison.Ordinal);
                Assert.DoesNotContain(events.Select(logEvent => logEvent.RenderMessage()), line => line.Contains(password, StringComparison.Ordinal));
                Assert.DoesNotContain(_factory.Logs.RenderAll().Skip(mark), line => line.Contains(password, StringComparison.Ordinal) || line.Contains(email, StringComparison.Ordinal));
            });
    }

    /// <summary>
    /// A body over the limit is cut, marked as cut, and a secret inside the kept part is masked.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_body_over_the_limit_is_cut_and_still_masked()
    {
        const string password = "Cut-body-secret-5521";
        const int maxBytes = 256;

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            BodiesOn(maxBytes),
            async () =>
            {
                using HttpClient client = _factory.CreateBrowserClient();
                int mark = _factory.Logs.Events.Count;
                string padding = string.Concat(Enumerable.Repeat(PaddingValue, PaddingRepeat));
                string body = "{\"login\":\"someone\",\"password\":\"" + password + "\",\"padding\":\"" + padding + "\"}";

                using StringContent content = new(body, Encoding.UTF8, "application/json");
                await client.PostAsync(LoginPath, content, Token);

                LogEvent bodies = Assert.Single(
                    ObservabilityTestSupport.Since(_factory, mark),
                    logEvent => logEvent.MessageTemplate.Text.StartsWith("HTTP bodies", StringComparison.Ordinal));
                string requestBody = ObservabilityTestSupport.Text(bodies, "RequestBody")!;

                Assert.Contains("...[cut, ", requestBody, StringComparison.Ordinal);
                Assert.Contains("\"password\":\"***\"", requestBody, StringComparison.Ordinal);
                Assert.DoesNotContain(password, requestBody, StringComparison.Ordinal);
                Assert.True(requestBody.Length < body.Length, "The logged body is shorter than the sent one.");
            });
    }

    /// <summary>
    /// The response body is logged for a JSON response, without the e-mail address of the account.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_json_response_is_logged_without_the_email()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            BodiesOn(maxBytes: 4096),
            async () =>
            {
                using HttpClient client = _factory.CreateBrowserClient();
                await TestAccounts.SignInAsync(client, account);
                int mark = _factory.Logs.Events.Count;

                HttpResponseMessage response = await client.GetAsync(AuthEndpoints.Prefix + "/me", Token);
                string sent = await response.Content.ReadAsStringAsync(Token);

                Assert.Contains(account.Email, sent, StringComparison.Ordinal);

                LogEvent bodies = Assert.Single(
                    ObservabilityTestSupport.Since(_factory, mark),
                    logEvent => logEvent.MessageTemplate.Text.StartsWith("HTTP bodies", StringComparison.Ordinal));

                Assert.DoesNotContain(account.Email, bodies.RenderMessage(), StringComparison.Ordinal);
                Assert.DoesNotContain(_factory.Logs.RenderAll().Skip(mark), line => line.Contains(account.Email, StringComparison.Ordinal));
                Assert.NotNull(ObservabilityTestSupport.Text(bodies, "RequestBody"));
            });
    }

    /// <summary>
    /// With the bodies off nothing is captured: there is no body line and the stream of the
    /// request is not wrapped.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task With_the_bodies_off_nothing_is_captured()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        int mark = _factory.Logs.Events.Count;

        await client.PostAsJsonAsync(LoginPath, TestAccounts.LoginBody(TestAccounts.NewEmail(), "Some-password-1234"), Token);

        List<LogEvent> events = ObservabilityTestSupport.Since(_factory, mark);

        Assert.DoesNotContain(events, logEvent => logEvent.MessageTemplate.Text.StartsWith("HTTP bodies", StringComparison.Ordinal));
        Assert.Equal("False", ObservabilityTestSupport.Text(ObservabilityTestSupport.RequestLine(events, LoginPath)!, "BodiesCaptured"));
    }

    /// <summary>
    /// A path on the list of the excluded ones gets no body capture even when the bodies are on.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task An_excluded_path_is_not_captured()
    {
        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            BodiesOn(maxBytes: 4096),
            async () =>
            {
                using HttpClient client = _factory.CreateBrowserClient();
                int mark = _factory.Logs.Events.Count;

                HttpResponseMessage response = await client.GetAsync("/health", Token);

                Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);

                List<LogEvent> events = ObservabilityTestSupport.Since(_factory, mark);

                Assert.DoesNotContain(events, logEvent => logEvent.MessageTemplate.Text.StartsWith("HTTP bodies", StringComparison.Ordinal));
                Assert.Equal("False", ObservabilityTestSupport.Text(ObservabilityTestSupport.RequestLine(events, "/health")!, "BodiesCaptured"));

                await Task.CompletedTask;
            });
    }

    /// <summary>
    /// A command slower than the threshold is logged with its text and the names of its
    /// parameters, never with their values.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_slow_sql_command_is_logged_without_the_parameter_values()
    {
        string email = TestAccounts.NewEmail();
        string emailName = email.Split('@')[0];
        const string password = "Parameter-value-leak-7788";

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            new Dictionary<string, string> { [ObservabilityKeys.SqlSlowMilliseconds] = "0" },
            async () =>
            {
                using HttpClient client = _factory.CreateBrowserClient();
                int mark = _factory.Logs.Events.Count;

                await client.PostAsJsonAsync(LoginPath, TestAccounts.LoginBody(email, password), Token);

                List<LogEvent> commands = [.. ObservabilityTestSupport.Since(_factory, mark).Where(logEvent =>
                    ObservabilityTestSupport.Text(logEvent, "SourceContext") == "Aetheris.Sql"
                    && ObservabilityTestSupport.Text(logEvent, "SqlText") is not null)];

                Assert.NotEmpty(commands);
                Assert.All(commands, logEvent => Assert.Equal(LogEventLevel.Warning, logEvent.Level));
                Assert.Contains(commands, logEvent => ObservabilityTestSupport.Text(logEvent, "SqlText")!.Contains('@', StringComparison.Ordinal));

                foreach (string line in _factory.Logs.RenderAll().Skip(mark))
                {
                    Assert.DoesNotContain(password, line, StringComparison.Ordinal);
                    Assert.DoesNotContain(emailName, line, StringComparison.Ordinal);
                }

                await Task.CompletedTask;
            });
    }

    /// <summary>
    /// Without the switch of the verbose mode and below the threshold, a command is not written.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_fast_command_is_not_logged_by_default()
    {
        using HttpClient client = _factory.CreateBrowserClient();
        int mark = _factory.Logs.Events.Count;

        await client.PostAsJsonAsync(LoginPath, TestAccounts.LoginBody(TestAccounts.NewEmail(), "Another-password-4321"), Token);

        Assert.DoesNotContain(
            ObservabilityTestSupport.Since(_factory, mark),
            logEvent => ObservabilityTestSupport.Text(logEvent, "SourceContext") == "Aetheris.Sql"
                && ObservabilityTestSupport.Text(logEvent, "SqlText") is not null
                && logEvent.Level == LogEventLevel.Information);
    }

    /// <summary>
    /// The masking goes into nested objects and arrays and keeps the other fields.
    /// </summary>
    [Fact]
    public void The_masker_hides_nested_secret_fields()
    {
        ObservabilitySnapshot settings = Snapshot("password,token,email");
        byte[] body = Encoding.UTF8.GetBytes("{\"name\":\"Rock\",\"owner\":{\"Email\":\"a@b.test\",\"list\":[{\"refresh_token\":\"x\",\"n\":1}]},\"newPassword\":\"p\"}");

        string masked = BodyMasker.Mask(body, body.Length, settings);

        Assert.Equal("{\"name\":\"Rock\",\"owner\":{\"Email\":\"***\",\"list\":[{\"refresh_token\":\"***\",\"n\":1}]},\"newPassword\":\"***\"}", masked);
    }

    /// <summary>
    /// A body that is cut in the middle of a secret value is still masked, and a secret object is
    /// masked up to the end of the kept text.
    /// </summary>
    [Fact]
    public void The_masker_hides_a_secret_in_a_cut_body()
    {
        ObservabilitySnapshot settings = Snapshot("password,token");
        byte[] cutInString = Encoding.UTF8.GetBytes("{\"a\":1,\"password\":\"abcdef");
        byte[] cutInObject = Encoding.UTF8.GetBytes("{\"a\":1,\"token\":{\"inner\":\"zzz\",\"more\":");

        string first = BodyMasker.Mask(cutInString, cutInString.Length + 50, settings);
        string second = BodyMasker.Mask(cutInObject, cutInObject.Length + 50, settings);

        Assert.DoesNotContain("abcdef", first, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"***\"", first, StringComparison.Ordinal);
        Assert.DoesNotContain("zzz", second, StringComparison.Ordinal);
        Assert.Contains("...[cut, ", second, StringComparison.Ordinal);
    }

    /// <summary>
    /// The request stream passes every byte on unchanged and keeps only the limit.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_capture_of_a_request_forwards_everything_and_keeps_the_limit()
    {
        byte[] source = Encoding.UTF8.GetBytes(new string('x', 1000));
        const int limit = 100;
        BodyCapture capture = new(limit);

        await using CaptureReadStream stream = new(new MemoryStream(source), capture);
        using MemoryStream received = new();
        await stream.CopyToAsync(received, Token);

        Assert.Equal(source, received.ToArray());
        Assert.Equal(source.Length, capture.TotalBytes);
        Assert.Equal(limit, capture.Captured.Length);
    }

    /// <summary>
    /// A response that is not logged is only counted, and one that is logged is kept up to the limit.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_capture_of_a_response_counts_a_binary_body_and_keeps_a_json_one()
    {
        byte[] payload = Encoding.UTF8.GetBytes(new string('y', 500));
        const int limit = 64;

        BodyCapture binaryCapture = new(limit);
        using MemoryStream binarySink = new();
        await using (CaptureWriteStream binary = new(binarySink, binaryCapture, () => false))
        {
            await binary.WriteAsync(payload, Token);
            Assert.False(binary.Kept);
        }

        BodyCapture jsonCapture = new(limit);
        using MemoryStream jsonSink = new();
        await using (CaptureWriteStream json = new(jsonSink, jsonCapture, () => true))
        {
            await json.WriteAsync(payload, Token);
            Assert.True(json.Kept);
        }

        Assert.Equal(payload, binarySink.ToArray());
        Assert.Equal(payload.Length, binaryCapture.TotalBytes);
        Assert.Equal(0, binaryCapture.Captured.Length);
        Assert.Equal(payload, jsonSink.ToArray());
        Assert.Equal(payload.Length, jsonCapture.TotalBytes);
        Assert.Equal(limit, jsonCapture.Captured.Length);
    }

    private static ObservabilitySnapshot Snapshot(string maskedFields)
    {
        GameConfig config = new();
        config.Replace([new KeyValuePair<string, ConfigParameter>(
            ObservabilityKeys.HttpBodiesMaskedFields,
            new ConfigParameter(ObservabilityKeys.HttpBodiesMaskedFields, maskedFields, 1))]);

        return new ObservabilitySettings(config).Current;
    }
}

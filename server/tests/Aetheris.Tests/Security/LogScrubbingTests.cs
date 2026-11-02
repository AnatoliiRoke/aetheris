using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Host.Auth;
using Aetheris.Host.Observability;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Observability;
using Microsoft.AspNetCore.Mvc.Testing;
using Serilog.Events;
using Serilog.Parsing;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests that the logs hold no password, token or e-mail address.
/// </summary>
/// <remarks>
/// Acceptance criterion 9 of task 00002. The test runs the account flows, then checks
/// every log event the server wrote, and also checks the masking itself on a sample event.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class LogScrubbingTests
{
    /// <summary>
    /// Cookie values and tokens shorter than this are not searched for in the logs: a short value
    /// such as a flag would match unrelated text.
    /// </summary>
    private const int MinimumSecretLength = 16;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogScrubbingTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public LogScrubbingTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// After registration, confirmation, a failed and a successful sign-in, and a password
    /// reset, no log event contains the password, the tokens or the e-mail address.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_account_flows_leave_no_secrets_in_the_logs()
    {
        TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();

        await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/login",
            TestAccounts.LoginBody(account.Email, "a-wrong-password-for-logs"),
            TestContext.Current.CancellationToken);
        await TestAccounts.SignInAsync(client, account);
        await client.PostAsJsonAsync(
            AuthEndpoints.Prefix + "/forgot-password",
            new { email = account.Email },
            TestContext.Current.CancellationToken);

        List<string> secrets =
        [
            account.Password,
            "a-wrong-password-for-logs",
            account.Email,
            account.Email.Split('@')[0],
            .. _factory.Emails.Sent.Where(email => email.Address == account.Email).Select(email => email.Token),
        ];

        List<string> logs = _factory.Logs.RenderAll();

        Assert.NotEmpty(logs);

        foreach (string secret in secrets)
        {
            string? leak = logs.FirstOrDefault(line => line.Contains(secret, StringComparison.Ordinal));
            Assert.True(leak is null, $"A log event contains a secret: {leak}");
        }
    }

    /// <summary>
    /// With the tracing, the bodies and the verbose SQL on, the registration, the sign-in, the
    /// password reset, a request with a cookie and an authorization header leave no password,
    /// token, cookie, authorization value, anti-forgery token or e-mail address in any log event
    /// (task 00036, acceptance criterion 2).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_account_flows_leave_no_secrets_with_the_bodies_and_the_sql_logging_on()
    {
        const string bearer = "Bearer-value-that-must-not-be-logged-8842";
        const string resetPassword = "Reset-harbor-lantern-77";
        List<string> secrets = [];
        int mark = _factory.Logs.Events.Count;

        await ObservabilityTestSupport.WithParametersAsync(
            _factory,
            new Dictionary<string, string>
            {
                [ObservabilityKeys.HttpBodiesEnabled] = "true",
                [ObservabilityKeys.HttpBodiesMaxBytes] = "4096",
                [ObservabilityKeys.SqlVerbose] = "true",
                [ObservabilityKeys.SqlSlowMilliseconds] = "0",
            },
            async () =>
            {
                TestAccount account = await TestAccounts.RegisterConfirmedAsync(_factory);

                using HttpClient client = _factory.CreateBrowserClient();

                await client.PostAsJsonAsync(
                    AuthEndpoints.Prefix + "/login",
                    TestAccounts.LoginBody(account.Email, "a-wrong-password-for-logs"),
                    TestContext.Current.CancellationToken);
                await TestAccounts.SignInAsync(client, account);
                await client.PostAsJsonAsync(
                    AuthEndpoints.Prefix + "/forgot-password",
                    new { email = account.Email },
                    TestContext.Current.CancellationToken);

                AccountEmail resetEmail = _factory.Emails.Last(account.Email, AccountEmailKinds.PasswordReset);

                using HttpClient anonymous = _factory.CreateBrowserClient();
                HttpResponseMessage reset = await anonymous.PostAsJsonAsync(
                    AuthEndpoints.Prefix + "/reset-password",
                    new { userId = resetEmail.UserId, token = resetEmail.Token, newPassword = resetPassword },
                    TestContext.Current.CancellationToken);

                Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

                // A client without a cookie jar, to read the cookies the server sets and to send them back.
                using HttpClient raw = _factory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    BaseAddress = new Uri("https://localhost"),
                    AllowAutoRedirect = false,
                    HandleCookies = false,
                });

                HttpResponseMessage login = await raw.PostAsJsonAsync(
                    AuthEndpoints.Prefix + "/login",
                    TestAccounts.LoginBody(account.Email, resetPassword),
                    TestContext.Current.CancellationToken);

                Assert.Equal(HttpStatusCode.OK, login.StatusCode);

                List<string> cookies = [.. login.Headers.GetValues("Set-Cookie").Select(header => header.Split(';')[0])];

                secrets.AddRange(cookies.Select(cookie => cookie[(cookie.IndexOf('=', StringComparison.Ordinal) + 1)..]).Where(value => value.Length >= MinimumSecretLength));

                using HttpRequestMessage me = new(HttpMethod.Get, AuthEndpoints.Prefix + "/csrf");
                me.Headers.Add("Cookie", string.Join("; ", cookies));
                me.Headers.Add("Authorization", bearer);

                HttpResponseMessage csrf = await raw.SendAsync(me, TestContext.Current.CancellationToken);
                using JsonDocument csrfBody = JsonDocument.Parse(await csrf.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

                secrets.Add(csrfBody.RootElement.GetProperty("token").GetString()!);
                secrets.AddRange(
                [
                    account.Password,
                    "a-wrong-password-for-logs",
                    resetPassword,
                    bearer,
                    account.Email,
                    account.Email.Split('@')[0],
                    .. _factory.Emails.Sent.Where(email => email.Address == account.Email).Select(email => email.Token),
                ]);
            });

        List<string> logs = [.. _factory.Logs.RenderAll().Skip(mark)];

        Assert.Contains(logs, line => line.Contains("HTTP bodies", StringComparison.Ordinal));
        Assert.Contains(logs, line => line.Contains("SQL command", StringComparison.Ordinal));

        foreach (string secret in secrets)
        {
            string? leak = logs.FirstOrDefault(line => line.Contains(secret, StringComparison.Ordinal));
            Assert.True(leak is null, $"A log event contains a secret ({secret.Length} characters): {leak}");
        }
    }

    /// <summary>
    /// The masking replaces a property named like a secret, an e-mail address inside any
    /// text, and a long token.
    /// </summary>
    [Fact]
    public void The_masking_hides_secrets_in_a_sample_event()
    {
        LogEvent sample = new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("Sample {Password} {Note} {Path}"),
            [
                new LogEventProperty("Password", new ScalarValue("hunter2-hunter2")),
                new LogEventProperty("Note", new ScalarValue("written by someone@example.test")),
                new LogEventProperty("Path", new ScalarValue("token " + new string('A', 64))),
            ]);

        new SensitiveDataMaskingEnricher().Enrich(sample, null!);

        string rendered = sample.RenderMessage();

        Assert.DoesNotContain("hunter2", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("someone@example.test", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('A', 64), rendered, StringComparison.Ordinal);
    }
}

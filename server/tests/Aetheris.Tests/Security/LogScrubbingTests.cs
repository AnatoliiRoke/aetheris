using System.Net.Http.Json;
using Aetheris.Host.Auth;
using Aetheris.Host.Observability;
using Aetheris.Tests.Infrastructure;
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
            new { email = account.Email, password = "a-wrong-password-for-logs" },
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

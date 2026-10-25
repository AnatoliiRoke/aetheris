using System.Net;
using System.Net.Http.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Security;
using Aetheris.Host.Auth;
using Aetheris.Host.Data;
using Aetheris.Host.Security;
using Aetheris.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Security;

/// <summary>
/// Tests of the rate limits.
/// </summary>
/// <remarks>
/// Acceptance criterion 4 of task 00002: going over the limit answers <c>429</c> and is
/// written to the security journal. The limit comes from the database, so the test lowers
/// it for itself and puts it back afterwards.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public sealed class RateLimitTests
{
    private const int TestPermitLimit = 2;

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public RateLimitTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// The sign-in requests over the limit get <c>429</c>, and the rejection is written to
    /// the security journal.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Going_over_the_limit_returns_429_and_writes_a_security_event()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        string key = SecurityKeys.RateLimitPermits(RateLimitGroups.Auth);
        string? previous = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, TestPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));

        try
        {
            using HttpClient client = _factory.CreateBrowserClient();
            List<HttpStatusCode> statuses = [];

            for (int attempt = 0; attempt <= TestPermitLimit; attempt++)
            {
                HttpResponseMessage response = await client.PostAsJsonAsync(
                    AuthEndpoints.Prefix + "/login",
                    new { email = TestAccounts.NewEmail(), password = TestAccounts.StrongPassword },
                    TestContext.Current.CancellationToken);

                statuses.Add(response.StatusCode);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Assert.Equal(GameErrorCodes.RateLimited, await TestAccounts.ReadErrorAsync(response));
                }
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
            Assert.All(statuses.Take(TestPermitLimit), status => Assert.Equal(HttpStatusCode.Unauthorized, status));

            AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();

            Assert.True(await dbContext.SecurityEvents.AnyAsync(
                entry => entry.Type == SecurityEventTypes.RateLimitExceeded && entry.Detail == RateLimitGroups.Auth,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, previous!);
        }
    }

    /// <summary>
    /// A changed limit applies without a restart: after raising it, the requests pass again.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_changed_limit_applies_without_a_restart()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        string key = SecurityKeys.RateLimitPermits(RateLimitGroups.Auth);
        string? previous = await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, "1");

        try
        {
            using HttpClient client = _factory.CreateBrowserClient();

            await client.GetAsync(AuthEndpoints.Prefix + "/csrf", TestContext.Current.CancellationToken);
            HttpResponseMessage limited = await client.GetAsync(AuthEndpoints.Prefix + "/csrf", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, previous!);

            HttpResponseMessage passed = await client.GetAsync(AuthEndpoints.Prefix + "/csrf", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, passed.StatusCode);
        }
        finally
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, key, previous!);
        }
    }
}

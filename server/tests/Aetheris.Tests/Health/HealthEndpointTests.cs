using System.Net;
using Aetheris.Tests.Infrastructure;

namespace Aetheris.Tests.Health;

/// <summary>
/// Tests the health endpoint: it must report OK and confirm the database connection.
/// </summary>
/// <remarks>
/// This is acceptance criterion 2 of task 00001.
/// </remarks>
public sealed class HealthEndpointTests : IClassFixture<AetherisTestFactory>
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthEndpointTests"/> class.
    /// </summary>
    /// <param name="factory">The test host factory.</param>
    public HealthEndpointTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    /// <summary>
    /// The health endpoint returns a successful status.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Health_endpoint_reports_ok()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health").ConfigureAwait(false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The health endpoint confirms that the database is reachable.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Health_endpoint_confirms_the_database_connection()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health").ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.Contains("database", body, StringComparison.OrdinalIgnoreCase);
    }
}

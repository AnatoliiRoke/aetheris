using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Api;

namespace Aetheris.Tests.World;

/// <summary>
/// Drives the civilization step the way the client does (task 00006).
/// </summary>
public static class CivilizationTestData
{
    /// <summary>
    /// Reads the races and returns the first one of the list.
    /// </summary>
    /// <param name="client">A signed-in client.</param>
    /// <returns>The race.</returns>
    public static async Task<int> FirstRaceIdAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        using JsonDocument races = JsonDocument.Parse(
            await client.GetStringAsync(WorldEndpoints.Prefix + "/races", TestContext.Current.CancellationToken));

        return races.RootElement[0].GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Sends the civilization step.
    /// </summary>
    /// <param name="client">A signed-in client.</param>
    /// <param name="raceId">The race.</param>
    /// <param name="planetName">The name of the home body.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> FoundAsync(HttpClient client, int raceId, string planetName, string requestId) =>
        TestAccounts.SendCommandAsync(
            client,
            FoundCivilizationCommand.RoutePath,
            new Dictionary<string, object?> { ["race_id"] = raceId, ["planet_name"] = planetName },
            requestId);
}

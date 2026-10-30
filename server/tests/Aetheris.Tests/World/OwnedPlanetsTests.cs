using System.Net;
using System.Text.Json;
using Aetheris.Tests.Infrastructure;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the read of the bodies of a player, the list the screen of the civilization shows
/// (task 00010, section 7.4). The endpoint returns the bodies of the caller and nothing of
/// anybody else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class OwnedPlanetsTests
{
    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OwnedPlanetsTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public OwnedPlanetsTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// The endpoint is closed for an outsider and gives the caller its own body with the type,
    /// the place in the galaxy and the deposits (task 00010, acceptance criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_endpoint_gives_the_caller_its_own_bodies_only()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient anonymous = _factory.CreateBrowserClient();
        HttpResponseMessage closed = await anonymous.GetAsync(WorldEndpoints.Prefix + "/planets", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, closed.StatusCode);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        int raceId = await CivilizationTestData.FirstRaceIdAsync(client);
        HttpResponseMessage start = await CivilizationTestData.FoundAsync(client, raceId, "Nova Prima", "owned-1");
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using JsonDocument created = JsonDocument.Parse(await start.Content.ReadAsStringAsync(Token));
        long planetId = created.RootElement.GetProperty("planet_id").GetInt64();
        long systemId = created.RootElement.GetProperty("system_id").GetInt64();
        int sectorId = created.RootElement.GetProperty("sector_id").GetInt32();

        using JsonDocument bodies = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + "/planets", Token));

        JsonElement own = Assert.Single(bodies.RootElement.EnumerateArray());
        Assert.Equal(planetId, own.GetProperty("planet_id").GetInt64());
        Assert.Equal("Nova Prima", own.GetProperty("name").GetString());
        Assert.Equal(systemId, own.GetProperty("system_id").GetInt64());
        Assert.Equal(sectorId, own.GetProperty("sector_id").GetInt32());
        Assert.NotEqual(0, own.GetProperty("object_type_id").GetInt32());
        Assert.NotEqual(0, own.GetProperty("deposits").GetArrayLength());

        // The deposits name the resources and the richness of every one of them.
        JsonElement deposit = own.GetProperty("deposits")[0];
        Assert.NotEqual(0, deposit.GetProperty("resource_type_id").GetInt32());
        Assert.True(deposit.GetProperty("coefficient").GetDecimal() > 0);

        // The second player sees its own body and never the body of the first one.
        using HttpClient other = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(other, await TestAccounts.RegisterConfirmedAsync(_factory));

        int otherRaceId = await CivilizationTestData.FirstRaceIdAsync(other);
        HttpResponseMessage otherStart = await CivilizationTestData.FoundAsync(other, otherRaceId, "Nova Secunda", "owned-2");
        Assert.Equal(HttpStatusCode.OK, otherStart.StatusCode);

        using JsonDocument otherBodies = JsonDocument.Parse(await other.GetStringAsync(WorldEndpoints.Prefix + "/planets", Token));

        JsonElement otherOwn = Assert.Single(otherBodies.RootElement.EnumerateArray());
        Assert.Equal("Nova Secunda", otherOwn.GetProperty("name").GetString());
        Assert.NotEqual(planetId, otherOwn.GetProperty("planet_id").GetInt64());
    }

    /// <summary>
    /// A player without a civilization has no bodies, and the endpoint says so with an empty
    /// list instead of the bodies of somebody else (task 00010, acceptance criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_player_without_a_civilization_has_no_bodies()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        using JsonDocument bodies = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + "/planets", Token));

        Assert.Equal(0, bodies.RootElement.GetArrayLength());
    }

    /// <summary>
    /// The body of another player never comes into the list, even when the bodies of the two
    /// players stand in one sector (task 00010, acceptance criterion 5).
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_bodies_of_another_player_stay_out_of_the_list()
    {
        await WorldTestData.ResetWorldAsync(_factory);

        using HttpClient first = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(first, await TestAccounts.RegisterConfirmedAsync(_factory));
        int raceId = await CivilizationTestData.FirstRaceIdAsync(first);
        HttpResponseMessage start = await CivilizationTestData.FoundAsync(first, raceId, "Nova Prima", "owned-3");
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using JsonDocument created = JsonDocument.Parse(await start.Content.ReadAsStringAsync(Token));
        long planetId = created.RootElement.GetProperty("planet_id").GetInt64();

        // A body of another player, taken over in the world tables.
        long otherPlanetId = await TakeForeignBodyAsync(_factory);

        using HttpClient client = _factory.CreateBrowserClient();
        await TestAccounts.SignInAsync(client, await TestAccounts.RegisterConfirmedAsync(_factory));

        using JsonDocument bodies = JsonDocument.Parse(await client.GetStringAsync(WorldEndpoints.Prefix + "/planets", Token));

        List<long> ids = [.. bodies.RootElement.EnumerateArray().Select(body => body.GetProperty("planet_id").GetInt64())];

        Assert.DoesNotContain(planetId, ids);
        Assert.DoesNotContain(otherPlanetId, ids);
    }

    /// <summary>
    /// Gives a free body to a foreign player, so the test has a body of somebody else.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <returns>The identifier of the body.</returns>
    private static async Task<long> TakeForeignBodyAsync(AetherisTestFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        WorldDbContext world = scope.ServiceProvider.GetRequiredService<WorldDbContext>();

        PlanetEntity? body = await world.Planets
            .Where(planet => planet.OwnerPlayerId == null)
            .OrderBy(planet => planet.Id)
            .FirstOrDefaultAsync(Token);

        Assert.NotNull(body);

        body!.OwnerPlayerId = "foreign-player";
        await world.SaveChangesAsync(Token);

        return body.Id;
    }
}

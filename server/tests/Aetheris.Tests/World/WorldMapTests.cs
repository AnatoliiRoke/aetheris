using System.Net;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Tests.Infrastructure;
using Aetheris.Tests.Planets;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.World;

/// <summary>
/// Tests of the reads of the map screen: what a player sees follows the level of the radar of
/// their bodies (the test stub or the real building), the places the player only knows of carry
/// no name, distances and times come from the settings, and a point of space snaps to what is
/// near it (task 00034).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class WorldMapTests
{
    private const string RadarCode = "building_52";

    private readonly AetherisTestFactory _factory;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldMapTests"/> class.
    /// </summary>
    /// <param name="factory">The test host.</param>
    public WorldMapTests(AetherisTestFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// With the test stub of the radar on, every body has a radar of the stub level: the range is
    /// the level times the range of a level, the systems inside it are open with their names, and
    /// the galaxy counts the systems and the bodies of the player.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_stub_radar_opens_the_map_around_the_body()
    {
        using PlanetsTestPlayer player = await StartAsync(stub: true);
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldMapService map = scope.ServiceProvider.GetRequiredService<WorldMapService>();
        WorldSettings settings = WorldSettings.From(scope.ServiceProvider.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>());
        int sectorId = await SectorOfAsync(scope.ServiceProvider, player.PlanetId);

        MapGalaxyDto galaxy = await map.GetGalaxyAsync(player.Account.UserId, Token);
        MapSectorViewDto sector = await map.GetSectorAsync(player.Account.UserId, sectorId, player.PlanetId, Token);

        Assert.False(galaxy.SeesOnlyOwn);
        Assert.Equal(1, galaxy.Sectors.Single(entry => entry.Id == sectorId).OwnPlanetCount);
        Assert.True(galaxy.Sectors.Single(entry => entry.Id == sectorId).SystemCount > 1);
        MapRadarDto radar = Assert.Single(sector.Radars);
        Assert.Equal(settings.RadarStubLevel * settings.RadarRangePerLevel, radar.Radius, 6);
        Assert.Equal(player.PlanetId, radar.PlanetId);
        Assert.Contains(sector.Systems, system => system.State == MapStates.Open && system.Name is not null);
        Assert.All(sector.Systems, system => Assert.NotNull(system.Distance));
        Assert.All(sector.Systems.Where(system => system.State == MapStates.Known), system => Assert.Null(system.Name));
    }

    /// <summary>
    /// With the stub off and no radar built, the player sees only their own system: the other
    /// stars are known places without a name, the map says so, and a system that is not open is
    /// refused.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task Without_a_radar_the_player_sees_only_their_own_system()
    {
        using PlanetsTestPlayer player = await StartAsync(stub: false);
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldMapService map = scope.ServiceProvider.GetRequiredService<WorldMapService>();
        int sectorId = await SectorOfAsync(scope.ServiceProvider, player.PlanetId);

        MapSectorViewDto sector = await map.GetSectorAsync(player.Account.UserId, sectorId, null, Token);

        Assert.True(sector.SeesOnlyOwn);
        Assert.Empty(sector.Radars);
        MapSystemDto own = Assert.Single(sector.Systems, system => system.State == MapStates.Open);
        Assert.NotNull(own.Name);
        Assert.Equal(1, own.OwnPlanetCount);
        Assert.True(sector.Systems.Count > 1);
        Assert.All(sector.Systems.Where(system => system.State == MapStates.Known), system => Assert.Null(system.Name));

        MapSystemViewDto view = await map.GetSystemAsync(player.Account.UserId, own.Id, player.PlanetId, Token);

        Assert.Contains(view.Objects, entry => entry.IsOwn && entry.State == MapStates.Open);
        // What the player does not see on the orbits is a place without a class and a name.
        Assert.All(view.Objects.Where(entry => entry.State == MapStates.Known), entry => Assert.Equal("unknown", entry.ObjectClass));
        Assert.All(view.Objects.Where(entry => entry.State == MapStates.Known), entry => Assert.Null(entry.Name));

        long closed = sector.Systems.First(system => system.State == MapStates.Known).Id;
        GameException refused = await Assert.ThrowsAsync<GameException>(() => map.GetSystemAsync(player.Account.UserId, closed, null, Token));
        Assert.Equal(GameErrorCodes.NotFound, refused.Code);
    }

    /// <summary>
    /// With the stub off the level of the real radar decides: a radar of a higher level opens a
    /// wider range, and the range follows the level.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_real_radar_level_decides_the_range_when_the_stub_is_off()
    {
        using PlanetsTestPlayer player = await StartAsync(stub: false);
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldMapService map = scope.ServiceProvider.GetRequiredService<WorldMapService>();
        WorldSettings settings = WorldSettings.From(scope.ServiceProvider.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>());
        int sectorId = await SectorOfAsync(scope.ServiceProvider, player.PlanetId);

        int opened(MapSectorViewDto view) => view.Systems.Count(system => system.State == MapStates.Open);
        int none = opened(await map.GetSectorAsync(player.Account.UserId, sectorId, null, Token));

        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, RadarCode, 1);
        MapSectorViewDto first = await map.GetSectorAsync(player.Account.UserId, sectorId, null, Token);
        await PlanetsTestData.SetLevelAsync(scope.ServiceProvider, player.PlanetId, RadarCode, 10);
        MapSectorViewDto tenth = await map.GetSectorAsync(player.Account.UserId, sectorId, null, Token);

        Assert.Equal(settings.RadarRangePerLevel, Assert.Single(first.Radars).Radius, 6);
        Assert.Equal(10 * settings.RadarRangePerLevel, Assert.Single(tenth.Radars).Radius, 6);
        Assert.True(opened(first) >= none);
        Assert.True(opened(tenth) >= opened(first));
        Assert.True(opened(tenth) > none);
        Assert.False(tenth.SeesOnlyOwn);
    }

    /// <summary>
    /// A point of the map snaps to a star system near it and is a free point otherwise; a point
    /// outside the sector is refused; the flight inside the system of the body takes the fixed
    /// time and the other flights take the time of the settings.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task A_target_snaps_to_a_system_or_is_a_free_point_with_the_time_of_the_settings()
    {
        using PlanetsTestPlayer player = await StartAsync(stub: true);
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;
        WorldMapService map = services.GetRequiredService<WorldMapService>();
        WorldSettings settings = WorldSettings.From(services.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>());
        WorldDbContext world = services.GetRequiredService<WorldDbContext>();
        int sectorId = await SectorOfAsync(services, player.PlanetId);
        long homeSystemId = await world.Planets.Where(planet => planet.Id == player.PlanetId).Select(planet => planet.SystemId).SingleAsync(Token);
        List<StarSystemEntity> systems = await world.Systems.AsNoTracking().Where(system => system.SectorId == sectorId).ToListAsync(Token);
        StarSystemEntity home = systems.Single(system => system.Id == homeSystemId);
        StarSystemEntity other = systems.First(system => system.Id != homeSystemId);

        MapTargetDto homeTarget = await map.GetTargetAsync(player.Account.UserId, sectorId, home.X + 1, home.Y, home.Z, player.PlanetId, Token);
        MapTargetDto otherTarget = await map.GetTargetAsync(player.Account.UserId, sectorId, other.X, other.Y, other.Z, player.PlanetId, Token);

        Assert.Equal(MapTargetKinds.System, homeTarget.Kind);
        Assert.Equal(homeSystemId, homeTarget.TargetId);
        Assert.Equal((long)Math.Ceiling(settings.InSystemTravelTime.TotalSeconds), homeTarget.TravelSeconds);
        Assert.Equal(MapTargetKinds.System, otherTarget.Kind);
        Assert.Equal(other.Id, otherTarget.TargetId);
        Assert.True(otherTarget.Distance > 0);
        Assert.True(otherTarget.TravelSeconds >= (long)Math.Ceiling(otherTarget.Distance!.Value * settings.TravelTimePerDistance.TotalSeconds));

        // A point far from every system is a free point of space with the nearest system named.
        double far = settings.SectorSize;
        MapTargetDto free = await map.GetTargetAsync(player.Account.UserId, sectorId, far, far, 0, player.PlanetId, Token);

        if (free.Kind == MapTargetKinds.Point)
        {
            Assert.NotNull(free.NearSystemId);
            Assert.Empty(free.Errors);
            Assert.True(free.Distance > 0);
        }

        MapTargetDto outside = await map.GetTargetAsync(player.Account.UserId, sectorId, settings.SectorSize + 500, 0, 0, player.PlanetId, Token);
        Assert.Contains(MapTargetErrors.OutOfSector, outside.Errors);
        Assert.Equal(MapTargetKinds.Point, outside.Kind);
    }

    /// <summary>
    /// The body the distances are counted from must be the player's own; the search finds the open
    /// systems and bodies by a part of the name, ignores a too short text and keeps to the limit.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_origin_must_be_own_and_the_search_finds_open_places_within_the_limit()
    {
        using PlanetsTestPlayer player = await StartAsync(stub: true);
        using PlanetsTestPlayer stranger = await PlanetsTestData.FoundAsync(_factory);
        using IServiceScope scope = _factory.Services.CreateScope();
        WorldMapService map = scope.ServiceProvider.GetRequiredService<WorldMapService>();
        WorldSettings settings = WorldSettings.From(scope.ServiceProvider.GetRequiredService<Aetheris.Core.Configuration.IGameConfig>());
        int sectorId = await SectorOfAsync(scope.ServiceProvider, player.PlanetId);

        GameException refused = await Assert.ThrowsAsync<GameException>(() => map.GetSectorAsync(player.Account.UserId, sectorId, stranger.PlanetId, Token));
        Assert.Equal(GameErrorCodes.NotFound, refused.Code);

        MapSectorViewDto sector = await map.GetSectorAsync(player.Account.UserId, sectorId, player.PlanetId, Token);
        string name = sector.Systems.First(system => system.Name is { Length: > 3 }).Name![..3];

        Assert.Empty(await map.SearchAsync(player.Account.UserId, "a", Token));
        Assert.Empty(await map.SearchAsync(player.Account.UserId, null, Token));
        IReadOnlyList<MapSearchResultDto> found = await map.SearchAsync(player.Account.UserId, name, Token);

        Assert.NotEmpty(found);
        Assert.True(found.Count <= settings.MapSearchLimit);
        Assert.Contains(found, entry => entry.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The reads of the map are an API of the signed-in player: the route answers with the same
    /// numbers, and an unknown body is not found.
    /// </summary>
    /// <returns>A task that completes when the assertion has been evaluated.</returns>
    [Fact]
    public async Task The_routes_of_the_map_answer_a_signed_in_player()
    {
        using PlanetsTestPlayer player = await StartAsync(stub: true);
        int sectorId;

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            sectorId = await SectorOfAsync(scope.ServiceProvider, player.PlanetId);
        }

        using JsonDocument galaxy = JsonDocument.Parse(await player.Client.GetStringAsync(WorldEndpoints.Prefix + "/map/galaxy", Token));
        using JsonDocument sector = JsonDocument.Parse(await player.Client.GetStringAsync($"{WorldEndpoints.Prefix}/map/sectors/{sectorId}?from_planet_id={player.PlanetId}", Token));
        using JsonDocument target = JsonDocument.Parse(await player.Client.GetStringAsync($"{WorldEndpoints.Prefix}/map/target?sector_id={sectorId}&x=1&y=1&z=0&from_planet_id={player.PlanetId}", Token));
        HttpResponseMessage missing = await player.Client.GetAsync($"{WorldEndpoints.Prefix}/map/sectors/{sectorId}?from_planet_id=999999999", Token);

        Assert.True(galaxy.RootElement.GetProperty("sectors").GetArrayLength() > 0);
        Assert.True(sector.RootElement.GetProperty("systems").GetArrayLength() > 0);
        Assert.Equal(sectorId, target.RootElement.GetProperty("sector_id").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<int> SectorOfAsync(IServiceProvider services, long planetId) =>
        await services.GetRequiredService<WorldDbContext>().Planets.AsNoTracking().Where(planet => planet.Id == planetId).Select(planet => planet.SectorId).SingleAsync(Token);

    private async Task<PlanetsTestPlayer> StartAsync(bool stub)
    {
        await PlanetsTestData.ResetAsync(_factory);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            await AetherisTestFactory.SetParameterAsync(scope.ServiceProvider, WorldConfigKeys.RadarStubEnabled, stub ? "true" : "false");
        }

        return await PlanetsTestData.FoundAsync(_factory);
    }
}

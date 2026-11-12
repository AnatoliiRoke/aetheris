using System.Globalization;
using Aetheris.Core.Errors;
using Aetheris.Core.Players;
using Aetheris.Core.Time;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Geometry;
using Aetheris.World.Visibility;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The reads of the map screen (task 00034): the galaxy, a sector, a star system, the search and
/// the point a player points at as the target of a flight. Every read passes
/// <see cref="IWorldVisibility"/>: the client draws only what is in the answer, and a place the
/// player knows of without details carries no name and no type.
/// </summary>
/// <remarks>
/// Distances and the time of a flight are counted here from the settings, never by the client.
/// Inside a star system the distance is the number of orbits between the places times the step
/// of an orbit and the time is fixed; between systems and in open space the distance is
/// three-dimensional between the absolute positions and the time is proportional to it.
/// </remarks>
public sealed class WorldMapService
{
    private const double SamePlaceDistance = 0.001;
    private const int MinQueryLength = 2;

    private readonly WorldDbContext _dbContext;
    private readonly IWorldVisibility _visibility;
    private readonly WorldReferenceRepository _reference;
    private readonly IPlayerDirectory _players;
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldMapService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="visibility">The visibility of the map.</param>
    /// <param name="reference">The parameters.</param>
    /// <param name="players">The nicknames of the players.</param>
    public WorldMapService(WorldDbContext dbContext, IWorldVisibility visibility, WorldReferenceRepository reference, IPlayerDirectory players, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _visibility = visibility;
        _reference = reference;
        _players = players;
        _clock = clock;
    }

    /// <summary>
    /// Reads the map of the galaxy: the sectors with the number of their systems and of the bodies of the player.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sectors.</returns>
    public async Task<MapGalaxyDto> GetGalaxyAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);
        List<SectorEntity> sectors = await _dbContext.Sectors.AsNoTracking().OrderBy(sector => sector.LayoutIndex).ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, int> systemCounts = await _dbContext.Systems.AsNoTracking()
            .GroupBy(system => system.SectorId)
            .Select(group => new { SectorId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.SectorId, row => row.Count, cancellationToken).ConfigureAwait(false);
        List<(int SectorId, long ObjectId)> own = await OwnPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false);

        // What the player knows of every sector (task 00050): a sector the radar reaches is open, a
        // sector with a body of the player in it is open too, a sector the player has a known place in
        // is known, and a sector nothing of is unknown.
        Dictionary<int, string> sectorStates = await SectorStatesAsync(scope, own, cancellationToken).ConfigureAwait(false);

        return new MapGalaxyDto(
            [.. sectors.Select(sector => new MapSectorDto(
                sector.Id,
                sector.Name,
                sector.X,
                sector.Y,
                sector.Z,
                sector.IsNewbie,
                systemCounts.GetValueOrDefault(sector.Id),
                own.Count(entry => entry.SectorId == sector.Id),
                sectorStates.GetValueOrDefault(sector.Id, MapStates.Unknown)))],
            _reference.Settings.SectorSize,
            SeesOnlyOwn(scope, own));
    }

    /// <summary>
    /// Tells what the player knows of every sector (task 00050). A sector is open when a radar of the
    /// player reaches it or a body of the player stands in it, known when the player has a place in it
    /// that is only known, and unknown when the player knows nothing of it.
    /// </summary>
    /// <param name="scope">What the player sees.</param>
    /// <param name="own">The bodies of the player with their sectors.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The state of every sector the player knows of.</returns>
    private async Task<Dictionary<int, string>> SectorStatesAsync(
        VisibilityScope scope,
        List<(int SectorId, long ObjectId)> own,
        CancellationToken cancellationToken)
    {
        Dictionary<int, string> states = [];

        // The sectors the radars of the player reach are open.
        foreach (RadarCircle circle in scope.RadarCircles ?? [])
        {
            states[circle.SectorId] = MapStates.Open;
        }

        foreach (ScanSnapshotEntity scan in scope.ScanSnapshots ?? [])
        {
            states[scan.SectorId] = MapStates.Open;
            foreach (ScanSnapshotSystemEntity system in scan.SystemIds) states[system.SectorId] = MapStates.Open;
            foreach (ScanSnapshotObjectEntity obj in scan.ObjectIds) states[obj.SectorId] = MapStates.Open;
        }

        // The sectors with a body of the player in them are open: the player stands there.
        foreach ((int sectorId, _) in own)
        {
            states[sectorId] = MapStates.Open;
        }

        if (scope.SeesEverything)
        {
            return states;
        }

        // The sectors with a known place of the player in them are known.
        long[] visible = [.. scope.VisibleObjectIds];
        long[] visibleSystems = [.. scope.VisibleSystemIds];

        List<int> knownSectors = await (
                from entry in _dbContext.Objects.AsNoTracking()
                where visible.Contains(entry.Id) || (entry.SystemId != null && visibleSystems.Contains(entry.SystemId.Value))
                select entry.SectorId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (int sectorId in knownSectors)
        {
            states.TryAdd(sectorId, MapStates.Known);
        }

        return states;
    }

    /// <summary>
    /// Reads the map of a sector: every star system (open ones with a name, the others as known
    /// places), the nebulas, the free fields the player sees and the ranges of their radars.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="fromPlanetId">The body of the player the distances are counted from, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The map of the sector.</returns>
    /// <exception cref="GameException">The sector or the body is unknown.</exception>
    public async Task<MapSectorViewDto> GetSectorAsync(string playerId, int sectorId, long? fromPlanetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        WorldSettings settings = _reference.Settings;
        SectorEntity sector = await _dbContext.Sectors.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == sectorId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(GameErrorCodes.NotFound, "The sector is unknown.");
        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);
        Origin? origin = await OriginAsync(playerId, fromPlanetId, cancellationToken).ConfigureAwait(false);
        List<(int SectorId, long ObjectId)> own = await OwnPlanetsAsync(playerId, cancellationToken).ConfigureAwait(false);

        ScanSnapshotEntity[] scans = [.. (scope.ScanSnapshots ?? []).OrderByDescending(scan => scan.ScannedAt)];
        int[] circleSectorIds = [.. scans.Select(scan => scan.SectorId).Concat((scope.RadarCircles ?? []).Select(circle => circle.SectorId)).Distinct()];
        Dictionary<int, SectorEntity> circleSectors = await _dbContext.Sectors.AsNoTracking().Where(s => circleSectorIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken).ConfigureAwait(false);
        bool Covered(double x, double y, double z, int circleSectorId, double circleX, double circleY, double circleZ, double radius)
        {
            SectorEntity source = circleSectors[circleSectorId];
            double dx = sector.X + x - source.X - circleX, dy = sector.Y + y - source.Y - circleY, dz = sector.Z + z - source.Z - circleZ;
            return (dx * dx) + (dy * dy) + (dz * dz) <= radius * radius;
        }
        bool LiveAt(double x, double y, double z) => scope.SeesEverything || (scope.RadarCircles ?? []).Any(circle => Covered(x, y, z, circle.SectorId, circle.X, circle.Y, circle.Z, circle.Radius));
        bool ScannedAt(double x, double y, double z) => scans.Any(scan => Covered(x, y, z, scan.SectorId, scan.X, scan.Y, scan.Z, scan.Radius));
        Dictionary<long, (ScanSnapshotSystemEntity System, ScanSnapshotEntity Scan)> recordedSystems = scans
            .SelectMany(scan => scan.SystemIds.Where(system => system.SectorId == sectorId).Select(system => (System: system, Scan: scan)))
            .GroupBy(row => row.System.SystemId).ToDictionary(group => group.Key, group => group.First());
        Dictionary<(long ScanId, long SystemId), int> recordedCounts = scans
            .SelectMany(scan => scan.ObjectIds.Where(obj => obj.SystemId != null).Select(obj => (scan.Id, SystemId: obj.SystemId!.Value)))
            .GroupBy(row => (row.Id, row.SystemId)).ToDictionary(group => group.Key, group => group.Count());

        List<StarSystemEntity> systems = await _dbContext.Systems.AsNoTracking()
            .Where(system => system.SectorId == sectorId)
            .OrderBy(system => system.Number)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        long[] openIds = [.. systems.Where(system => scope.SeesSystem(system.Id)).Select(system => system.Id)];

        // The objects the player sees, counted by system, in one read.
        Dictionary<long, int> seenCounts = (await _dbContext.Objects.AsNoTracking()
                .Where(entry => entry.SystemId != null && openIds.Contains(entry.SystemId.Value))
                .Select(entry => new { entry.Id, SystemId = entry.SystemId!.Value })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Where(entry => scope.SeesObject(entry.Id))
            .GroupBy(entry => entry.SystemId)
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<long, int> ownCounts = (await _dbContext.Planets.AsNoTracking()
                .Where(planet => planet.SectorId == sectorId && planet.OwnerPlayerId == playerId)
                .Select(planet => planet.SystemId)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());

        List<MapSystemDto> systemDtos =
        [
            .. systems.Select(system =>
            {
                // A system is open when a radar of the player reaches it or a body of the player
                // stands in it. A system the player knows nothing of is unknown (task 00050): the
                // player may not name it as the target of a flight.
                bool own = ownCounts.GetValueOrDefault(system.Id) > 0;
                bool open = scope.SeesEverything || openIds.Contains(system.Id) || own;
                (double? distance, long? seconds) = Travel(settings, origin, sector, new Point3(system.X, system.Y, system.Z), system.Id, null);

                return new MapSystemDto(
                    system.Id,
                    system.Number,
                    open ? system.Name : null,
                    system.X,
                    system.Y,
                    system.Z,
                    system.InNebula,
                    open ? MapStates.Open : MapStates.Unknown,
                    seenCounts.GetValueOrDefault(system.Id),
                    ownCounts.GetValueOrDefault(system.Id),
                    distance,
                    seconds);
            }),
        ];

        for (int i = 0; i < systemDtos.Count; i++)
        {
            MapSystemDto current = systemDtos[i];
            if (!scope.SeesSystem(current.Id) && !LiveAt(current.X, current.Y, current.Z) && recordedSystems.TryGetValue(current.Id, out var row))
            {
                ScanSnapshotSystemEntity saved = row.System;
                int count = recordedCounts.GetValueOrDefault((row.Scan.Id, saved.SystemId));
                systemDtos[i] = current with { Name = saved.Name, Number = saved.Number, X = saved.X, Y = saved.Y, Z = saved.Z, InNebula = saved.InNebula, State = MapStates.Open, ObjectCount = count, FromScan = true, ScannedAt = row.Scan.ScannedAt, ScanExpiresAtReal = _clock.ToRealTime(row.Scan.ExpiresAt) };
            }
        }
        systemDtos.RemoveAll(system => !scope.SeesSystem(system.Id) && !system.FromScan && ScannedAt(system.X, system.Y, system.Z) && !LiveAt(system.X, system.Y, system.Z));
        HashSet<long> listedSystemIds = [.. systemDtos.Select(system => system.Id)];
        foreach (var row in recordedSystems.Values.Where(row => !listedSystemIds.Contains(row.System.SystemId) && !LiveAt(row.System.X, row.System.Y, row.System.Z)))
        {
            ScanSnapshotSystemEntity saved = row.System;
            (double? distance, long? seconds) = Travel(settings, origin, sector, new Point3(saved.X, saved.Y, saved.Z), saved.SystemId, null);
            systemDtos.Add(new MapSystemDto(saved.SystemId, saved.Number, saved.Name, saved.X, saved.Y, saved.Z, saved.InNebula, MapStates.Open, recordedCounts.GetValueOrDefault((row.Scan.Id, saved.SystemId)), 0, distance, seconds, true, row.Scan.ScannedAt, _clock.ToRealTime(row.Scan.ExpiresAt)));
        }

        // A nebula is drawn for everyone, but only a nebula a radar reaches is open: the player
        // may name only that one as the target of a flight.
        List<MapNebulaDto> nebulas = (await (
                    from nebula in _dbContext.Nebulae.AsNoTracking()
                    join entry in _dbContext.Objects on nebula.CelestialObjectId equals entry.Id
                    where nebula.SectorId == sectorId
                    orderby nebula.Id
                    select new { nebula.Id, ObjectId = entry.Id, entry.Name, entry.X, entry.Y, entry.Z, nebula.Radius, nebula.BlocksJumpDrive })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(row => new MapNebulaDto(
                row.Id,
                row.ObjectId,
                row.Name,
                row.X,
                row.Y,
                row.Z,
                row.Radius,
                row.BlocksJumpDrive,
                scope.SeesObject(row.ObjectId) ? MapStates.Open : MapStates.Unknown))
            .ToList();

        List<MapFieldDto> fields = (await (
                    from field in _dbContext.Fields.AsNoTracking()
                    join entry in _dbContext.Objects on field.CelestialObjectId equals entry.Id
                    where field.SectorId == sectorId && field.SystemId == null && field.DepletedAt == null
                    orderby field.Id
                    select new { entry.Id, entry.ObjectTypeId, entry.Name, entry.X, entry.Y, entry.Z, field.Mass })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Where(row => scope.SeesObject(row.Id))
            .Select(row =>
            {
                (double? distance, long? seconds) = Travel(settings, origin, sector, new Point3(row.X, row.Y, row.Z), null, null);

                return new MapFieldDto(row.Id, row.ObjectTypeId, row.Name, row.X, row.Y, row.Z, row.Mass, distance, seconds);
            })
            .ToList();

        nebulas.RemoveAll(nebula => !LiveAt(nebula.X, nebula.Y, nebula.Z) && ScannedAt(nebula.X, nebula.Y, nebula.Z));
        var savedNebulae = scans.SelectMany(scan => scan.ObjectIds)
            .Where(obj => obj.SectorId == sectorId && obj.NebulaId != null)
            .GroupBy(obj => obj.CelestialObjectId).Select(group => group.First());
        foreach (ScanSnapshotObjectEntity saved in savedNebulae.Where(obj => !LiveAt(obj.X, obj.Y, obj.Z)))
        {
            nebulas.Add(new MapNebulaDto(saved.NebulaId!.Value, saved.CelestialObjectId, saved.Name, saved.X, saved.Y, saved.Z, saved.NebulaRadius ?? 0, saved.NebulaBlocksJumpDrive, MapStates.Open));
        }

        List<MapRadarDto> radars =
        [
            .. (scope.RadarCircles ?? []).Where(circle => circle.SectorId == sectorId)
                .Select(circle => new MapRadarDto(circle.PlanetId, circle.X, circle.Y, circle.Z, circle.Radius)),
        ];

        var recordedFields = scans.SelectMany(scan => scan.ObjectIds.Select(obj => (Object: obj, Scan: scan)))
            .Where(row => row.Object.SectorId == sectorId && row.Object.SystemId == null && row.Object.AsteroidFieldId != null)
            .GroupBy(row => row.Object.CelestialObjectId).Select(group => group.First());
        foreach (var row in recordedFields.Where(row => !scope.SeesObject(row.Object.CelestialObjectId) && !LiveAt(row.Object.X, row.Object.Y, row.Object.Z)))
        {
            ScanSnapshotObjectEntity saved = row.Object;
            (double? distance, long? seconds) = Travel(settings, origin, sector, new Point3(saved.X, saved.Y, saved.Z), null, null);
            fields.Add(new MapFieldDto(saved.CelestialObjectId, saved.ObjectTypeId, saved.Name, saved.X, saved.Y, saved.Z, saved.FieldMass ?? 0, distance, seconds,
                [.. saved.DepositIds.Select(deposit => new MapResourceShareDto(deposit.ResourceId, deposit.Coefficient))], row.Scan.ScannedAt));
        }
        List<MapScanDto> scanDtos = [.. scans.Select(scan =>
        {
            SectorEntity source = circleSectors[scan.SectorId];
            return new MapScanDto(source.X + scan.X - sector.X, source.Y + scan.Y - sector.Y, source.Z + scan.Z - sector.Z, scan.Radius, scan.ScannedAt, scan.ExpiresAt, _clock.ToRealTime(scan.ExpiresAt));
        })];

        return new MapSectorViewDto(sector.Id, sector.Name, settings.SectorSize, systemDtos, nebulas, fields, radars, SeesOnlyOwn(scope, own) && scans.Length == 0, scanDtos);
    }

    /// <summary>
    /// Reads the map of a star system: the objects the player sees with their details and the
    /// others as places on their orbits.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="systemId">The system.</param>
    /// <param name="fromPlanetId">The body of the player the distances are counted from, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The map of the system.</returns>
    /// <exception cref="GameException">The system or the body is unknown, or the system is not open to the player.</exception>
    public async Task<MapSystemViewDto> GetSystemAsync(string playerId, long systemId, long? fromPlanetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        WorldSettings settings = _reference.Settings;
        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (!scope.SeesSystem(systemId))
        {
            ScanSnapshotEntity? scan = (scope.ScanSnapshots ?? []).OrderByDescending(entry => entry.ScannedAt)
                .FirstOrDefault(entry => entry.SystemIds.Any(system => system.SystemId == systemId));
            if (scan is null) throw new GameException(GameErrorCodes.NotFound, "The system is unknown.");
            ScanSnapshotSystemEntity saved = scan.SystemIds.Single(system => system.SystemId == systemId);
            int[] sectorIds = [.. (scope.RadarCircles ?? []).Select(circle => circle.SectorId).Append(saved.SectorId).Distinct()];
            Dictionary<int, SectorEntity> sectors = await _dbContext.Sectors.AsNoTracking().Where(sector => sectorIds.Contains(sector.Id)).ToDictionaryAsync(sector => sector.Id, cancellationToken).ConfigureAwait(false);
            SectorEntity savedSector = sectors[saved.SectorId];
            bool live = (scope.RadarCircles ?? []).Any(circle =>
            {
                SectorEntity radarSector = sectors[circle.SectorId];
                double dx = savedSector.X + saved.X - radarSector.X - circle.X;
                double dy = savedSector.Y + saved.Y - radarSector.Y - circle.Y;
                double dz = savedSector.Z + saved.Z - radarSector.Z - circle.Z;
                return (dx * dx) + (dy * dy) + (dz * dz) <= circle.Radius * circle.Radius;
            });
            if (live) throw new GameException(GameErrorCodes.NotFound, "The system is unknown.");
            return await GetScannedSystemAsync(playerId, scan, systemId, fromPlanetId, cancellationToken).ConfigureAwait(false);
        }

        StarSystemEntity system = await _dbContext.Systems.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == systemId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(GameErrorCodes.NotFound, "The system is unknown.");
        SectorEntity sector = await _dbContext.Sectors.AsNoTracking().SingleAsync(entry => entry.Id == system.SectorId, cancellationToken).ConfigureAwait(false);
        Origin? origin = await OriginAsync(playerId, fromPlanetId, cancellationToken).ConfigureAwait(false);

        List<CelestialObjectEntity> objects = await _dbContext.Objects.AsNoTracking()
            .Where(entry => entry.SystemId == systemId)
            .OrderBy(entry => entry.Orbit)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, CelestialObjectTypeEntity> types = await _dbContext.ObjectTypes.AsNoTracking().ToDictionaryAsync(type => type.Id, cancellationToken).ConfigureAwait(false);
        long[] objectIds = [.. objects.Select(entry => entry.Id)];
        Dictionary<long, PlanetEntity> planets = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.SystemId == systemId)
            .Include(planet => planet.DepositIds)
            .ToDictionaryAsync(planet => planet.CelestialObjectId, cancellationToken).ConfigureAwait(false);
        Dictionary<long, AsteroidFieldEntity> fields = await _dbContext.Fields.AsNoTracking()
            .Where(field => field.SystemId == systemId && field.DepletedAt == null && objectIds.Contains(field.CelestialObjectId))
            .Include(field => field.CompositionIds)
            .ToDictionaryAsync(field => field.CelestialObjectId, cancellationToken).ConfigureAwait(false);
        Dictionary<string, MapOwnerDto> owners = await OwnersAsync(
            [.. planets.Values.Where(planet => planet.OwnerPlayerId is not null && scope.SeesObject(planet.CelestialObjectId)).Select(planet => planet.OwnerPlayerId!).Distinct()],
            cancellationToken).ConfigureAwait(false);

        List<MapObjectDto> dtos = [];

        foreach (CelestialObjectEntity entry in objects)
        {
            CelestialObjectTypeEntity type = types[entry.ObjectTypeId];

            // A depleted field has disappeared from the map; a nebula is not an object of a system.
            if ((type.ObjectClass == WorldObjectClasses.AsteroidField && !fields.ContainsKey(entry.Id)) || type.ObjectClass == WorldObjectClasses.Nebula)
            {
                continue;
            }

            (double? distance, long? seconds) = Travel(settings, origin, sector, new Point3(system.X, system.Y, system.Z), system.Id, entry.Orbit);
            bool open = type.ObjectClass == WorldObjectClasses.Star || scope.SeesObject(entry.Id);

            if (!open)
            {
                // The player only knows that something lies on the orbit: no class, no name.
                dtos.Add(new MapObjectDto(entry.Id, MapStates.Known, entry.Orbit, "unknown", null, null, null, null, null, false, false, false, null, null, null, [], distance, seconds));

                continue;
            }

            planets.TryGetValue(entry.Id, out PlanetEntity? planet);
            fields.TryGetValue(entry.Id, out AsteroidFieldEntity? field);
            MapOwnerDto? owner = planet?.OwnerPlayerId is { } ownerId && owners.TryGetValue(ownerId, out MapOwnerDto? found) ? found : null;

            dtos.Add(new MapObjectDto(
                entry.Id,
                MapStates.Open,
                entry.Orbit,
                type.ObjectClass,
                entry.ObjectTypeId,
                planet?.Name ?? entry.Name,
                planet?.Id,
                planet?.SizeClassId,
                planet?.Size,
                planet is not null && planet.OwnerPlayerId == playerId,
                planet is not null && planet.OwnerPlayerId is null && type.IsColonizable,
                planet?.IsTerraformed ?? false,
                owner,
                type.ObjectClass == WorldObjectClasses.GasGiant ? settings.GasGiantStationLimit : null,
                field?.Mass,
                planet is not null
                    ? [.. planet.DepositIds.OrderBy(deposit => deposit.Id).Select(deposit => new MapResourceShareDto(deposit.ResourceId, deposit.Coefficient))]
                    : field is not null ? [.. field.CompositionIds.OrderBy(share => share.Id).Select(share => new MapResourceShareDto(share.ResourceId, share.Coefficient))] : [],
                distance,
                seconds));
        }

        int ownCount = planets.Values.Count(planet => planet.OwnerPlayerId == playerId);
        (double? systemDistance, long? systemSeconds) = Travel(settings, origin, sector, new Point3(system.X, system.Y, system.Z), system.Id, null);
        MapSystemDto systemDto = new(
            system.Id,
            system.Number,
            system.Name,
            system.X,
            system.Y,
            system.Z,
            system.InNebula,
            MapStates.Open,
            dtos.Count(entry => entry.State == MapStates.Open),
            ownCount,
            systemDistance,
            systemSeconds);

        return new MapSystemViewDto(systemDto, sector.Name, _reference.Settings.OrbitCount, dtos);
    }

    private async Task<MapSystemViewDto> GetScannedSystemAsync(string playerId, ScanSnapshotEntity scan, long systemId, long? fromPlanetId, CancellationToken cancellationToken)
    {
        ScanSnapshotSystemEntity system = scan.SystemIds.Single(entry => entry.SystemId == systemId);
        SectorEntity sector = await _dbContext.Sectors.AsNoTracking().SingleAsync(entry => entry.Id == system.SectorId, cancellationToken).ConfigureAwait(false);
        Origin? origin = await OriginAsync(playerId, fromPlanetId, cancellationToken).ConfigureAwait(false);
        ScanSnapshotObjectEntity[] objects = [.. scan.ObjectIds.Where(obj => obj.SystemId == systemId).OrderBy(obj => obj.Orbit).ThenBy(obj => obj.CelestialObjectId)];
        int[] typeIds = [.. objects.Select(obj => obj.ObjectTypeId).Distinct()];
        Dictionary<int, CelestialObjectTypeEntity> types = await _dbContext.ObjectTypes.AsNoTracking().Where(type => typeIds.Contains(type.Id)).ToDictionaryAsync(type => type.Id, cancellationToken).ConfigureAwait(false);
        Dictionary<string, MapOwnerDto> owners = await OwnersAsync([.. objects.Where(obj => obj.OwnerPlayerId is not null).Select(obj => obj.OwnerPlayerId!).Distinct()], cancellationToken).ConfigureAwait(false);
        List<MapObjectDto> dtos = [];
        foreach (ScanSnapshotObjectEntity obj in objects)
        {
            CelestialObjectTypeEntity type = types[obj.ObjectTypeId];
            if (type.ObjectClass == WorldObjectClasses.Nebula) continue;
            (double? distance, long? seconds) = Travel(_reference.Settings, origin, sector, new Point3(system.X, system.Y, system.Z), systemId, obj.Orbit);
            MapOwnerDto? owner = obj.OwnerPlayerId is { } ownerId && owners.TryGetValue(ownerId, out MapOwnerDto? known) ? known : null;
            dtos.Add(new MapObjectDto(obj.CelestialObjectId, MapStates.Open, obj.Orbit, type.ObjectClass, obj.ObjectTypeId, obj.Name,
                obj.PlanetId, obj.SizeClassId, obj.Size, obj.OwnerPlayerId == playerId, obj.PlanetId is not null && obj.OwnerPlayerId is null && type.IsColonizable,
                obj.IsTerraformed, owner, type.ObjectClass == WorldObjectClasses.GasGiant ? _reference.Settings.GasGiantStationLimit : null,
                obj.FieldMass, [.. obj.DepositIds.OrderBy(deposit => deposit.ResourceId).Select(deposit => new MapResourceShareDto(deposit.ResourceId, deposit.Coefficient))], distance, seconds));
        }

        (double? systemDistance, long? systemSeconds) = Travel(_reference.Settings, origin, sector, new Point3(system.X, system.Y, system.Z), systemId, null);
        MapSystemDto dto = new(systemId, system.Number, system.Name, system.X, system.Y, system.Z, system.InNebula, MapStates.Open,
            dtos.Count, objects.Count(obj => obj.OwnerPlayerId == playerId), systemDistance, systemSeconds, true, scan.ScannedAt, _clock.ToRealTime(scan.ExpiresAt));
        return new MapSystemViewDto(dto, sector.Name, _reference.Settings.OrbitCount, dtos);
    }

    /// <summary>
    /// Searches the map: the systems, the bodies and fields, and the players the caller sees,
    /// by a part of the name.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="query">What to look for.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The results, at most the limit of the settings.</returns>
    public async Task<IReadOnlyList<MapSearchResultDto>> SearchAsync(string playerId, string? query, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        string needle = (query ?? string.Empty).Trim();
        int limit = _reference.Settings.MapSearchLimit;

        if (needle.Length < MinQueryLength)
        {
            return [];
        }

        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);
        string pattern = "%" + needle.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
        long[] visibleSystems = [.. scope.VisibleSystemIds];
        long[] visibleObjects = [.. scope.VisibleObjectIds];

        List<MapSearchResultDto> results = [];

        results.AddRange((await _dbContext.Systems.AsNoTracking()
                .Where(system => (scope.SeesEverything || visibleSystems.Contains(system.Id)) && EF.Functions.ILike(system.Name, pattern, "\\"))
                .OrderBy(system => system.Name)
                .Take(limit)
                .Select(system => new { system.Id, system.SectorId, system.Name })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(system => new MapSearchResultDto(MapTargetKinds.System, system.SectorId, system.Id, null, null, system.Name, system.Name, null)));

        var bodies = await (
                from entry in _dbContext.Objects.AsNoTracking()
                join type in _dbContext.ObjectTypes on entry.ObjectTypeId equals type.Id
                join planet in _dbContext.Planets on entry.Id equals planet.CelestialObjectId into planetRows
                from planet in planetRows.DefaultIfEmpty()
                join system in _dbContext.Systems on entry.SystemId equals system.Id into systemRows
                from system in systemRows.DefaultIfEmpty()
                where (scope.SeesEverything || visibleObjects.Contains(entry.Id))
                    && type.ObjectClass != WorldObjectClasses.Star && type.ObjectClass != WorldObjectClasses.Nebula
                    && (EF.Functions.ILike(entry.Name, pattern, "\\") || (planet != null && planet.Name != null && EF.Functions.ILike(planet.Name, pattern, "\\")))
                orderby entry.Name
                select new { entry.Id, entry.SectorId, entry.SystemId, entry.ObjectTypeId, Name = planet != null && planet.Name != null ? planet.Name : entry.Name, entry.Orbit, SystemName = system != null ? system.Name : null })
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        results.AddRange(bodies.Select(row => new MapSearchResultDto(MapTargetKinds.Object, row.SectorId, row.SystemId, row.Id, row.ObjectTypeId, row.Name, row.SystemName, row.Orbit)));

        // The players whose bodies the caller sees, by nickname.
        var owned = await (
                from planet in _dbContext.Planets.AsNoTracking()
                join entry in _dbContext.Objects on planet.CelestialObjectId equals entry.Id
                join system in _dbContext.Systems on planet.SystemId equals system.Id
                where planet.OwnerPlayerId != null && planet.OwnerPlayerId != playerId && (scope.SeesEverything || visibleObjects.Contains(entry.Id))
                orderby planet.Id
                select new { Owner = planet.OwnerPlayerId!, planet.SectorId, planet.SystemId, ObjectId = entry.Id, entry.ObjectTypeId, entry.Orbit, SystemName = system.Name })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (owned.Count > 0)
        {
            IReadOnlyDictionary<string, string> nicknames = await _players.GetNicknamesAsync([.. owned.Select(row => row.Owner).Distinct()], cancellationToken).ConfigureAwait(false);

            results.AddRange(owned
                .Where(row => nicknames.TryGetValue(row.Owner, out string? nickname) && nickname.Contains(needle, StringComparison.OrdinalIgnoreCase))
                .GroupBy(row => row.Owner)
                .Select(group => group.First())
                .Take(limit)
                .Select(row => new MapSearchResultDto("player", row.SectorId, row.SystemId, row.ObjectId, row.ObjectTypeId, nicknames[row.Owner], row.SystemName, row.Orbit)));
        }

        return results.Take(limit).ToList();
    }

    /// <summary>
    /// Takes the point a player pointed at on the map of a sector as the target of a flight
    /// (task 00034): the point snaps to a star system or a free field near it, otherwise it is a
    /// free point of space. Nothing is remembered.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="sectorId">The sector the player pointed in.</param>
    /// <param name="x">The x coordinate within the sector.</param>
    /// <param name="y">The y coordinate within the sector.</param>
    /// <param name="z">The z coordinate within the sector.</param>
    /// <param name="fromPlanetId">The body the flight starts from, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The target with its distance, its time and what forbids it.</returns>
    /// <exception cref="GameException">The sector or the body is unknown.</exception>
    public async Task<MapTargetDto> GetTargetAsync(string playerId, int sectorId, double x, double y, double z, long? fromPlanetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        WorldSettings settings = _reference.Settings;
        SectorEntity sector = await _dbContext.Sectors.AsNoTracking().SingleOrDefaultAsync(entry => entry.Id == sectorId, cancellationToken).ConfigureAwait(false)
            ?? throw new GameException(GameErrorCodes.NotFound, "The sector is unknown.");
        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);
        Origin? origin = await OriginAsync(playerId, fromPlanetId, cancellationToken).ConfigureAwait(false);
        Point3 point = new(x, y, z);
        List<string> errors = [];

        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)
            || x < 0 || x > settings.SectorSize || y < 0 || y > settings.SectorSize || z < 0 || z > settings.SectorDepth)
        {
            errors.Add(MapTargetErrors.OutOfSector);
        }

        if (errors.Count == 0 && !scope.SeesEverything && scope.ScanSnapshots is { Count: > 0 })
        {
            int[] circleIds = [.. scope.ScanSnapshots.Select(scan => scan.SectorId).Concat((scope.RadarCircles ?? []).Select(circle => circle.SectorId)).Distinct()];
            Dictionary<int, SectorEntity> origins = await _dbContext.Sectors.AsNoTracking().Where(entry => circleIds.Contains(entry.Id)).ToDictionaryAsync(entry => entry.Id, cancellationToken).ConfigureAwait(false);
            bool Covers(int originId, double ox, double oy, double oz, double radius)
            {
                SectorEntity source = origins[originId];
                double dx = sector.X + x - source.X - ox, dy = sector.Y + y - source.Y - oy, dz = sector.Z + z - source.Z - oz;
                return (dx * dx) + (dy * dy) + (dz * dz) <= radius * radius;
            }
            bool radar = (scope.RadarCircles ?? []).Any(circle => Covers(circle.SectorId, circle.X, circle.Y, circle.Z, circle.Radius));
            ScanSnapshotEntity? scan = scope.ScanSnapshots.OrderByDescending(entry => entry.ScannedAt)
                .FirstOrDefault(entry => Covers(entry.SectorId, entry.X, entry.Y, entry.Z, entry.Radius));
            if (!radar && scan is not null)
            {
                ScanSnapshotSystemEntity? savedSystem = scan.SystemIds.Where(entry => entry.SectorId == sectorId)
                    .OrderBy(entry => WorldGeometry.Distance(point, new Point3(entry.X, entry.Y, entry.Z))).FirstOrDefault();
                ScanSnapshotObjectEntity? savedField = scan.ObjectIds.Where(entry => entry.SectorId == sectorId && entry.SystemId == null && entry.AsteroidFieldId != null)
                    .OrderBy(entry => WorldGeometry.Distance(point, new Point3(entry.X, entry.Y, entry.Z))).FirstOrDefault();
                double systemDistance = savedSystem is null ? double.MaxValue : WorldGeometry.Distance(point, new Point3(savedSystem.X, savedSystem.Y, savedSystem.Z));
                double fieldDistance = savedField is null ? double.MaxValue : WorldGeometry.Distance(point, new Point3(savedField.X, savedField.Y, savedField.Z));
                string savedKind = MapTargetKinds.Point;
                long? savedId = null;
                string? savedName = null;
                Point3 savedPlace = point;
                if (savedSystem is not null && systemDistance <= settings.MapSnapRadius && systemDistance <= fieldDistance)
                {
                    savedKind = MapTargetKinds.System;
                    savedId = savedSystem.SystemId;
                    savedName = savedSystem.Name;
                    savedPlace = new Point3(savedSystem.X, savedSystem.Y, savedSystem.Z);
                }
                else if (savedField is not null && fieldDistance <= settings.MapSnapRadius)
                {
                    savedKind = MapTargetKinds.Object;
                    savedId = savedField.CelestialObjectId;
                    savedName = savedField.Name;
                    savedPlace = new Point3(savedField.X, savedField.Y, savedField.Z);
                }
                var insideNebulae = scan.ObjectIds.Where(entry => entry.SectorId == sectorId && entry.NebulaId is not null && entry.NebulaRadius is not null)
                    .Where(entry => WorldGeometry.Distance(savedPlace, new Point3(entry.X, entry.Y, entry.Z)) <= entry.NebulaRadius).ToArray();
                (double? savedDistance, long? savedSeconds) = Travel(settings, origin, sector, savedPlace, savedKind == MapTargetKinds.System ? savedId : null, null);
                if (savedKind == MapTargetKinds.Point && savedDistance is < SamePlaceDistance) errors.Add(MapTargetErrors.SamePlace);
                return new MapTargetDto(savedKind, savedId, sectorId, savedPlace.X, savedPlace.Y, savedPlace.Z, savedName,
                    savedKind == MapTargetKinds.Point ? savedSystem?.SystemId : null, savedKind == MapTargetKinds.Point ? savedSystem?.Name : null,
                    insideNebulae.Length > 0 || (savedKind == MapTargetKinds.System && savedSystem?.InNebula == true), insideNebulae.Any(entry => entry.NebulaBlocksJumpDrive),
                    MapStates.Open, savedDistance, savedSeconds, errors);
            }
        }

        List<StarSystemEntity> systems = await _dbContext.Systems.AsNoTracking().Where(system => system.SectorId == sectorId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var fields = (await (
                    from field in _dbContext.Fields.AsNoTracking()
                    join entry in _dbContext.Objects on field.CelestialObjectId equals entry.Id
                    where field.SectorId == sectorId && field.SystemId == null && field.DepletedAt == null
                    select new { entry.Id, entry.Name, entry.X, entry.Y, entry.Z })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Where(row => scope.SeesObject(row.Id))
            .ToList();
        List<NebulaEntity> nebulas = await _dbContext.Nebulae.AsNoTracking().Where(nebula => nebula.SectorId == sectorId).Include(nebula => nebula.CelestialObject).ToListAsync(cancellationToken).ConfigureAwait(false);

        StarSystemEntity? nearestSystem = systems.OrderBy(system => WorldGeometry.Distance(point, new Point3(system.X, system.Y, system.Z))).FirstOrDefault();
        double systemGap = nearestSystem is null ? double.MaxValue : WorldGeometry.Distance(point, new Point3(nearestSystem.X, nearestSystem.Y, nearestSystem.Z));
        var nearestField = fields.OrderBy(field => WorldGeometry.Distance(point, new Point3(field.X, field.Y, field.Z))).FirstOrDefault();
        double fieldGap = nearestField is null ? double.MaxValue : WorldGeometry.Distance(point, new Point3(nearestField.X, nearestField.Y, nearestField.Z));

        string kind;
        long? targetId = null;
        string? name = null;
        Point3 place = point;
        bool inNebula;
        string state;

        if (errors.Count == 0 && nearestSystem is not null && systemGap <= settings.MapSnapRadius && systemGap <= fieldGap)
        {
            bool open = scope.SeesSystem(nearestSystem.Id);

            kind = MapTargetKinds.System;
            targetId = nearestSystem.Id;
            name = open ? nearestSystem.Name : null;
            place = new Point3(nearestSystem.X, nearestSystem.Y, nearestSystem.Z);
            inNebula = nearestSystem.InNebula;
            // A system the radar does not reach is unknown (task 00050).
            state = open ? MapStates.Open : MapStates.Unknown;
        }
        else if (errors.Count == 0 && nearestField is not null && fieldGap <= settings.MapSnapRadius)
        {
            kind = MapTargetKinds.Object;
            targetId = nearestField.Id;
            name = nearestField.Name;
            place = new Point3(nearestField.X, nearestField.Y, nearestField.Z);
            inNebula = nebulas.Any(nebula => nebula.CelestialObject is { } center && WorldGeometry.Distance(place, new Point3(center.X, center.Y, center.Z)) <= nebula.Radius);
            state = MapStates.Open;
        }
        else
        {
            kind = MapTargetKinds.Point;
            inNebula = nebulas.Any(nebula => nebula.CelestialObject is { } center && WorldGeometry.Distance(point, new Point3(center.X, center.Y, center.Z)) <= nebula.Radius);
            state = scope.SeesEverything || (scope.RadarCircles ?? []).Any(circle => circle.SectorId == sectorId && WorldGeometry.Distance(point, new Point3(circle.X, circle.Y, circle.Z)) <= circle.Radius)
                ? MapStates.Open
                : MapStates.Unknown;
        }

        bool jumpForbidden = inNebula && nebulas.Any(nebula => nebula.BlocksJumpDrive && nebula.CelestialObject is { } center && WorldGeometry.Distance(place, new Point3(center.X, center.Y, center.Z)) <= nebula.Radius);
        (double? distance, long? seconds) = Travel(settings, origin, sector, place, kind == MapTargetKinds.System ? targetId : null, null);

        if (kind == MapTargetKinds.Point && distance is < SamePlaceDistance)
        {
            errors.Add(MapTargetErrors.SamePlace);
        }

        long? nearId = kind == MapTargetKinds.Point ? nearestSystem?.Id : null;
        string? nearName = nearId is { } near && scope.SeesSystem(near) ? nearestSystem!.Name : null;

        return new MapTargetDto(kind, targetId, sectorId, place.X, place.Y, place.Z, name, nearId, nearName, inNebula, jumpForbidden, state, distance, seconds, errors);
    }

    private static bool SeesOnlyOwn(VisibilityScope scope, List<(int SectorId, long ObjectId)> own) =>
        !scope.SeesEverything && scope.VisibleObjectIds.All(id => own.Any(entry => entry.ObjectId == id));

    /// <summary>
    /// The distance and the time from the origin to a place. Inside a system the distance is the
    /// orbits between the places times the step and the time is fixed; elsewhere the distance is
    /// the straight one between the absolute positions and the time grows with it.
    /// </summary>
    private static (double? Distance, long? Seconds) Travel(WorldSettings settings, Origin? origin, SectorEntity sector, Point3 local, long? systemId, int? orbit)
    {
        if (origin is null)
        {
            return (null, null);
        }

        long fixedSeconds = (long)Math.Ceiling(settings.InSystemTravelTime.TotalSeconds);

        if (systemId is { } id && id == origin.SystemId)
        {
            double orbits = Math.Abs((orbit ?? 0) - (origin.Orbit ?? 0));

            return (orbits * settings.MapOrbitDistanceStep, fixedSeconds);
        }

        Point3 absolute = new(sector.X + local.X, sector.Y + local.Y, sector.Z + local.Z);
        double distance = WorldGeometry.Distance(origin.Position, absolute);
        long seconds = Math.Max(fixedSeconds, (long)Math.Ceiling(distance * settings.TravelTimePerDistance.TotalSeconds));

        return (distance, seconds);
    }

    private async Task<Origin?> OriginAsync(string playerId, long? planetId, CancellationToken cancellationToken)
    {
        if (planetId is null)
        {
            return null;
        }

        var row = await (
                from planet in _dbContext.Planets.AsNoTracking()
                join entry in _dbContext.Objects on planet.CelestialObjectId equals entry.Id
                join system in _dbContext.Systems on planet.SystemId equals system.Id
                join sector in _dbContext.Sectors on system.SectorId equals sector.Id
                where planet.Id == planetId && planet.OwnerPlayerId == playerId
                select new { planet.SystemId, entry.Orbit, X = sector.X + system.X, Y = sector.Y + system.Y, Z = sector.Z + system.Z })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return row is null
            ? throw new GameException(GameErrorCodes.NotFound, "The body is not a colony of the player.")
            : new Origin(new Point3(row.X, row.Y, row.Z), row.SystemId, row.Orbit);
    }

    private async Task<List<(int SectorId, long ObjectId)>> OwnPlanetsAsync(string playerId, CancellationToken cancellationToken) =>
        [.. (await _dbContext.Planets.AsNoTracking()
                .Where(planet => planet.OwnerPlayerId == playerId)
                .Select(planet => new { planet.SectorId, planet.CelestialObjectId })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(row => (row.SectorId, row.CelestialObjectId))];

    private async Task<Dictionary<string, MapOwnerDto>> OwnersAsync(string[] ownerIds, CancellationToken cancellationToken)
    {
        if (ownerIds.Length == 0)
        {
            return [];
        }

        IReadOnlyDictionary<string, string> nicknames = await _players.GetNicknamesAsync(ownerIds, cancellationToken).ConfigureAwait(false);
        var races = await (
                from civilization in _dbContext.Civilizations.AsNoTracking()
                join race in _dbContext.Races on civilization.RaceId equals race.Id
                where ownerIds.Contains(civilization.PlayerId)
                select new { civilization.PlayerId, race.Code, race.NameKey, race.ColorKey })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return races
            .Where(row => nicknames.ContainsKey(row.PlayerId))
            .ToDictionary(row => row.PlayerId, row => new MapOwnerDto(nicknames[row.PlayerId], row.Code, row.NameKey, row.ColorKey), StringComparer.Ordinal);
    }

    private sealed record Origin(Point3 Position, long SystemId, int? Orbit);
}

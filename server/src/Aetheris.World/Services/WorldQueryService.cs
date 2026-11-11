using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Geometry;
using Aetheris.World.Visibility;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The read side of the world for the client (task 00004, section 2.7).
/// </summary>
/// <remarks>
/// Every read of a player passes <see cref="IWorldVisibility"/>: the player sees only the
/// open part of the map. The list of sectors is the frame of the galaxy and is always open.
/// </remarks>
public sealed class WorldQueryService
{
    private readonly WorldDbContext _dbContext;
    private readonly IWorldVisibility _visibility;
    private readonly WorldReferenceRepository _reference;
    private readonly ResourceCatalogCache _resourceCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="visibility">The visibility of the map.</param>
    /// <param name="reference">The parameters.</param>
    /// <param name="resourceCatalog">The resource catalog kept in memory.</param>
    public WorldQueryService(
        WorldDbContext dbContext,
        IWorldVisibility visibility,
        WorldReferenceRepository reference,
        ResourceCatalogCache resourceCatalog)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(resourceCatalog);

        _dbContext = dbContext;
        _visibility = visibility;
        _reference = reference;
        _resourceCatalog = resourceCatalog;
    }

    /// <summary>
    /// Gets the sectors of the galaxy.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sectors.</returns>
    public async Task<IReadOnlyList<SectorDto>> GetSectorsAsync(CancellationToken cancellationToken) =>
        await _dbContext.Sectors.AsNoTracking()
            .OrderBy(sector => sector.LayoutIndex)
            .Select(sector => new SectorDto(sector.Id, sector.Name, sector.X, sector.Y, sector.Z, sector.IsNewbie))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gets the open star systems of a sector.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The systems.</returns>
    public async Task<IReadOnlyList<SystemDto>> GetSystemsAsync(string playerId, int sectorId, CancellationToken cancellationToken)
    {
        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);
        long[] visible = [.. scope.VisibleSystemIds];

        return await _dbContext.Systems.AsNoTracking()
            .Where(system => system.SectorId == sectorId)
            .Where(system => scope.SeesEverything || visible.Contains(system.Id))
            .OrderBy(system => system.Number)
            .Select(system => new SystemDto(
                system.Id,
                system.SectorId,
                system.Number,
                system.Name,
                system.X,
                system.Y,
                system.Z,
                system.InNebula))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the open content of a star system.
    /// </summary>
    /// <param name="playerId">The player, or <see langword="null"/> to read without the visibility filter.</param>
    /// <param name="systemId">The system.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The content, or <see langword="null"/> when the system is unknown or closed.</returns>
    public async Task<SystemContentDto?> GetSystemContentAsync(string? playerId, long systemId, CancellationToken cancellationToken)
    {
        VisibilityScope scope = playerId is null
            ? new VisibilityScope(true, new HashSet<long>(), new HashSet<long>())
            : await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);

        if (!scope.SeesSystem(systemId))
        {
            return null;
        }

        StarSystemEntity? system = await _dbContext.Systems.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == systemId, cancellationToken).ConfigureAwait(false);

        if (system is null)
        {
            return null;
        }

        List<CelestialObjectEntity> objects = await _dbContext.Objects.AsNoTracking()
            .Where(entry => entry.SystemId == systemId)
            .OrderBy(entry => entry.Orbit)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        objects = [.. objects.Where(entry => scope.SeesObject(entry.Id))];
        long[] objectIds = [.. objects.Select(entry => entry.Id)];

        Dictionary<int, CelestialObjectTypeEntity> types = await _dbContext.ObjectTypes.AsNoTracking()
            .ToDictionaryAsync(type => type.Id, cancellationToken).ConfigureAwait(false);

        Dictionary<long, PlanetEntity> planets = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.SystemId == systemId && objectIds.Contains(planet.CelestialObjectId))
            .Include(planet => planet.DepositIds)
            .ToDictionaryAsync(planet => planet.CelestialObjectId, cancellationToken).ConfigureAwait(false);

        Dictionary<long, AsteroidFieldEntity> fields = await _dbContext.Fields.AsNoTracking()
            .Where(field => field.SystemId == systemId && field.DepletedAt == null && objectIds.Contains(field.CelestialObjectId))
            .Include(field => field.CompositionIds)
            .ToDictionaryAsync(field => field.CelestialObjectId, cancellationToken).ConfigureAwait(false);

        int stationLimit = _reference.Settings.GasGiantStationLimit;
        List<ObjectDto> objectDtos = [];

        foreach (CelestialObjectEntity entry in objects)
        {
            CelestialObjectTypeEntity type = types[entry.ObjectTypeId];

            // A depleted field has disappeared from the map.
            if (type.ObjectClass == WorldObjectClasses.AsteroidField && !fields.ContainsKey(entry.Id))
            {
                continue;
            }

            planets.TryGetValue(entry.Id, out PlanetEntity? planet);
            fields.TryGetValue(entry.Id, out AsteroidFieldEntity? field);

            objectDtos.Add(new ObjectDto(
                entry.Id,
                entry.SectorId,
                entry.SystemId,
                entry.ObjectTypeId,
                type.ObjectClass,
                entry.Orbit,
                entry.Name,
                entry.X,
                entry.Y,
                entry.Z,
                planet?.Id,
                planet?.SizeClassId,
                planet?.Size,
                planet?.OwnerPlayerId,
                type.ObjectClass == WorldObjectClasses.GasGiant ? stationLimit : null,
                planet is null ? [] : [.. planet.DepositIds.OrderBy(deposit => deposit.Id).Select(deposit => deposit.Id)],
                field?.Id,
                field?.Mass,
                null,
                null,
                field is null ? [] : [.. field.CompositionIds.OrderBy(share => share.Id).Select(share => share.Id)]));
        }

        List<DepositDto> deposits =
        [
            .. planets.Values
                .SelectMany(planet => planet.DepositIds)
                .OrderBy(deposit => deposit.Id)
                .Select(deposit => new DepositDto(deposit.Id, deposit.PlanetId, deposit.ResourceId, deposit.Coefficient)),
        ];

        List<CompositionDto> compositions =
        [
            .. fields.Values
                .SelectMany(field => field.CompositionIds)
                .OrderBy(share => share.Id)
                .Select(share => new CompositionDto(share.Id, share.AsteroidFieldId, null, share.ResourceId, share.Coefficient)),
        ];

        return new SystemContentDto(
            new SystemDto(system.Id, system.SectorId, system.Number, system.Name, system.X, system.Y, system.Z, system.InNebula),
            objectDtos,
            deposits,
            compositions);
    }

    /// <summary>
    /// Gets the place of the map by its identifier, without the visibility filter, for the server
    /// itself (task 00050): a field of a sector, a field of a system, a gas giant or a nebula.
    /// </summary>
    /// <param name="objectId">The object of the map.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when the object is unknown.</returns>
    public async Task<PlaceContentDto?> GetPlaceContentAsync(long objectId, CancellationToken cancellationToken)
    {
        CelestialObjectEntity? entry = await _dbContext.Objects.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == objectId, cancellationToken).ConfigureAwait(false);

        if (entry is null)
        {
            return null;
        }

        CelestialObjectTypeEntity? type = await _dbContext.ObjectTypes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == entry.ObjectTypeId, cancellationToken).ConfigureAwait(false);

        if (type is null)
        {
            return null;
        }

        int stationLimit = _reference.Settings.GasGiantStationLimit;

        PlanetEntity? planet = await _dbContext.Planets.AsNoTracking()
            .Include(item => item.DepositIds)
            .SingleOrDefaultAsync(item => item.CelestialObjectId == objectId, cancellationToken).ConfigureAwait(false);

        AsteroidFieldEntity? field = await _dbContext.Fields.AsNoTracking()
            .Include(item => item.CompositionIds)
            .SingleOrDefaultAsync(item => item.CelestialObjectId == objectId, cancellationToken).ConfigureAwait(false);

        NebulaEntity? nebula = await _dbContext.Nebulae.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CelestialObjectId == objectId, cancellationToken).ConfigureAwait(false);

        // A depleted field has disappeared from the map.
        if (type.ObjectClass == WorldObjectClasses.AsteroidField && (field is null || field.DepletedAt is not null))
        {
            return null;
        }

        IReadOnlyList<long> compositionIds = field is null
            ? []
            : [.. field.CompositionIds.OrderBy(share => share.Id).Select(share => share.Id)];

        ObjectDto dto = new(
            entry.Id,
            entry.SectorId,
            entry.SystemId,
            entry.ObjectTypeId,
            type.ObjectClass,
            entry.Orbit,
            entry.Name,
            entry.X,
            entry.Y,
            entry.Z,
            planet?.Id,
            planet?.SizeClassId,
            planet?.Size,
            planet?.OwnerPlayerId,
            type.ObjectClass == WorldObjectClasses.GasGiant ? stationLimit : null,
            planet is null ? [] : [.. planet.DepositIds.OrderBy(deposit => deposit.Id).Select(deposit => deposit.Id)],
            field?.Id,
            field?.Mass,
            nebula?.Id,
            nebula?.Radius,
            compositionIds);

        List<CompositionDto> compositions =
        [
            .. field?.CompositionIds
                .OrderBy(share => share.Id)
                .Select(share => new CompositionDto(share.Id, share.AsteroidFieldId, null, share.ResourceId, share.Coefficient))
                ?? [],
        ];

        // The shares of a nebula are the shares of the place, just as the shares of a field are.
        if (nebula is not null)
        {
            List<NebulaCompositionEntity> shares = await _dbContext.NebulaCompositions.AsNoTracking()
                .Where(share => share.NebulaId == nebula.Id)
                .OrderBy(share => share.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            compositions =
            [
                .. compositions,
                .. shares.Select(share => new CompositionDto(share.Id, null, share.NebulaId, share.ResourceId, share.Coefficient)),
            ];
        }

        // The mass of the place: the mass of a field. A gas giant and a nebula have no mass at all
        // (task 00053): they are endless sources and are never emptied.
        long mass = field?.Mass ?? 0;

        return new PlaceContentDto(dto, compositions, mass);
    }

    /// <summary>
    /// Gets the bodies among the given ones that are free and may be colonized.
    /// </summary>
    /// <param name="planetIds">The bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The free bodies that may be colonized, in the order of their identifiers.</returns>
    public async Task<IReadOnlyList<FreePlanetDto>> GetFreePlanetsByIdsAsync(
        IReadOnlyCollection<long> planetIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planetIds);

        long[] ids = [.. planetIds.Distinct()];

        return await _dbContext.Planets.AsNoTracking()
            .Where(planet => ids.Contains(planet.Id) && planet.OwnerPlayerId == null)
            .Join(_dbContext.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet, entry })
            .Join(_dbContext.ObjectTypes, pair => pair.entry.ObjectTypeId, type => type.Id, (pair, type) => new { pair.planet, pair.entry, type })
            .Where(row => row.type.IsColonizable)
            .OrderBy(row => row.planet.Id)
            .Select(row => new FreePlanetDto(
                row.planet.Id,
                row.entry.Id,
                row.planet.SystemId,
                row.planet.SectorId,
                row.entry.ObjectTypeId,
                row.entry.Orbit,
                row.entry.Name,
                row.planet.SizeClassId,
                row.planet.Size,
                row.planet.IsTerraformed))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the open free bodies of a sector that a player may take.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="sectorId">The sector.</param>
    /// <param name="objectTypeId">The body type, or <see langword="null"/> for every colonizable type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The free bodies, at most one page.</returns>
    public async Task<IReadOnlyList<FreePlanetDto>> GetFreePlanetsAsync(
        string playerId,
        int sectorId,
        int? objectTypeId,
        CancellationToken cancellationToken)
    {
        VisibilityScope scope = await _visibility.GetScopeAsync(playerId, cancellationToken).ConfigureAwait(false);
        long[] visible = [.. scope.VisibleObjectIds];
        int pageSize = _reference.Settings.FreeBodiesPageSize;

        return await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.SectorId == sectorId && planet.OwnerPlayerId == null)
            .Join(_dbContext.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet, entry })
            .Join(_dbContext.ObjectTypes, pair => pair.entry.ObjectTypeId, type => type.Id, (pair, type) => new { pair.planet, pair.entry, type })
            .Where(row => row.type.IsColonizable)
            .Where(row => objectTypeId == null || row.entry.ObjectTypeId == objectTypeId)
            .Where(row => scope.SeesEverything || visible.Contains(row.entry.Id))
            .OrderBy(row => row.planet.Id)
            .Take(pageSize)
            .Select(row => new FreePlanetDto(
                row.planet.Id,
                row.entry.Id,
                row.planet.SystemId,
                row.planet.SectorId,
                row.entry.ObjectTypeId,
                row.entry.Orbit,
                row.entry.Name,
                row.planet.SizeClassId,
                row.planet.Size,
                row.planet.IsTerraformed))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the neutral code of a type of an object, for the modules that name a body by its
    /// type (task 00020).
    /// </summary>
    /// <param name="objectTypeId">The type.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The code of the type, or <see langword="null"/> when the type is unknown.</returns>
    public async Task<string?> GetObjectTypeCodeAsync(int objectTypeId, CancellationToken cancellationToken)
    {
        return await _dbContext.ObjectTypes.AsNoTracking()
            .Where(type => type.Id == objectTypeId)
            .Select(type => type.Code)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the reference tables, so the client can name the identifiers.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The reference tables.</returns>
    public async Task<WorldReferenceDto> GetReferenceAsync(CancellationToken cancellationToken)
    {
        List<ResourceGroupEntity> groups = await _dbContext.ResourceGroups.AsNoTracking()
            .OrderBy(group => group.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<ResourceTypeEntity> resources = await _dbContext.ResourceTypes.AsNoTracking()
            .OrderBy(resource => resource.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<CelestialObjectTypeEntity> types = await _dbContext.ObjectTypes.AsNoTracking()
            .OrderBy(type => type.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        List<PlanetSizeClassEntity> sizes = await _dbContext.SizeClasses.AsNoTracking()
            .OrderBy(size => size.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new WorldReferenceDto(
            [.. groups.Select(group => new ReferenceDto(group.Id, group.Code, group.NameKey, group.DescriptionKey, null, null, null, group.IconShape, group.IconTone, group.IsBalance))],
            [.. resources.Select(resource => new ReferenceDto(
                resource.Id,
                resource.Code,
                resource.NameKey,
                resource.DescriptionKey,
                null,
                resource.ResourceGroupId,
                resource.ImageKey,
                EarnsPopulationIncome: resource.EarnsPopulationIncome))],
            [.. types.Select(type => new ReferenceDto(type.Id, type.Code, type.NameKey, type.DescriptionKey, type.ObjectClass, null, type.ImageKey))],
            [.. sizes.Select(size => new ReferenceDto(size.Id, size.Code, size.NameKey, size.DescriptionKey, null, null, null))]);
    }

    /// <summary>
    /// Gets the distance between two star systems in the coordinates of the galaxy.
    /// </summary>
    /// <param name="fromSystemId">The first system.</param>
    /// <param name="toSystemId">The second system.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The three-dimensional distance, or <see langword="null"/> when a system is unknown.</returns>
    public async Task<double?> GetSystemDistanceAsync(long fromSystemId, long toSystemId, CancellationToken cancellationToken)
    {
        Point3? from = await GetGalaxyPositionAsync(fromSystemId, cancellationToken).ConfigureAwait(false);
        Point3? to = await GetGalaxyPositionAsync(toSystemId, cancellationToken).ConfigureAwait(false);

        return from is null || to is null ? null : WorldGeometry.Distance(from.Value, to.Value);
    }

    /// <summary>
    /// Gets the distances from one body to other bodies in one read: the three-dimensional distance
    /// between their star systems, zero inside one system (task 00024: the delivery of the market).
    /// </summary>
    /// <param name="fromPlanetId">The body the distances are counted from.</param>
    /// <param name="planetIds">The other bodies.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The distance by body; a body that is unknown is not in the answer.</returns>
    public async Task<IReadOnlyDictionary<long, double>> GetPlanetDistancesAsync(long fromPlanetId, IReadOnlyCollection<long> planetIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(planetIds);

        List<long> ids = [.. planetIds.Append(fromPlanetId).Distinct()];
        var rows = await (
                from planet in _dbContext.Planets.AsNoTracking()
                join system in _dbContext.Systems on planet.SystemId equals system.Id
                join sector in _dbContext.Sectors on system.SectorId equals sector.Id
                where ids.Contains(planet.Id)
                select new
                {
                    planet.Id,
                    planet.SystemId,
                    Local = new { system.X, system.Y, system.Z },
                    Origin = new { sector.X, sector.Y, sector.Z },
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var source = rows.FirstOrDefault(row => row.Id == fromPlanetId);

        if (source is null)
        {
            return new Dictionary<long, double>();
        }

        Point3 sourcePoint = new Point3(source.Origin.X, source.Origin.Y, source.Origin.Z).Plus(new Point3(source.Local.X, source.Local.Y, source.Local.Z));

        return rows
            .Where(row => row.Id != fromPlanetId)
            .ToDictionary(
                row => row.Id,
                row => row.SystemId == source.SystemId
                    ? 0d
                    : WorldGeometry.Distance(sourcePoint, new Point3(row.Origin.X, row.Origin.Y, row.Origin.Z).Plus(new Point3(row.Local.X, row.Local.Y, row.Local.Z))));
    }

    /// <summary>
    /// Gets the position of a star system in the coordinates of the galaxy.
    /// </summary>
    /// <param name="systemId">The system.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The position, or <see langword="null"/> when the system is unknown.</returns>
    public async Task<Point3?> GetGalaxyPositionAsync(long systemId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.Systems.AsNoTracking()
            .Where(system => system.Id == systemId)
            .Join(_dbContext.Sectors, system => system.SectorId, sector => sector.Id, (system, sector) => new
            {
                Local = new { system.X, system.Y, system.Z },
                Origin = new { sector.X, sector.Y, sector.Z },
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return row is null
            ? null
            : new Point3(row.Origin.X, row.Origin.Y, row.Origin.Z).Plus(new Point3(row.Local.X, row.Local.Y, row.Local.Z));
    }

    /// <summary>
    /// Finds the owner of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The owner, or <see langword="null"/>.</returns>
    public Task<string?> GetPlanetOwnerAsync(long planetId, CancellationToken cancellationToken) =>
        _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.Id == planetId)
            .Select(planet => planet.OwnerPlayerId)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Gets the facts of a body that another module needs: the sector, the owner and the deposits.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The facts, or <see langword="null"/> when the body does not exist.</returns>
    public async Task<PlanetFacts?> GetPlanetFactsAsync(long planetId, CancellationToken cancellationToken)
    {
        // The type of the body and its place in the galaxy are facts of the object it is, so
        // they come from the objects of the world and never from the code of the type
        // (task 00020). Whether the body may be settled is a column of the table of the types,
        // so a gas giant is told by its data and never by its code. The screen writes the names
        // of the sector and the system; each name is a column of that row. One query reads the
        // body, its object, its type, its names and its deposits (task 00036, section 6.3).
        var row = await _dbContext.Planets.AsNoTracking()
            .Where(entry => entry.Id == planetId)
            .Select(entry => new
            {
                entry.Id,
                entry.SectorId,
                entry.OwnerPlayerId,
                entry.Name,
                entry.IsTerraformed,
                entry.SystemId,
                ObjectTypeId = entry.CelestialObject == null ? (int?)null : entry.CelestialObject.ObjectTypeId,
                Orbit = entry.CelestialObject == null ? null : entry.CelestialObject.Orbit,
                IsColonizable = _dbContext.ObjectTypes
                    .Where(type => entry.CelestialObject != null && type.Id == entry.CelestialObject.ObjectTypeId)
                    .Select(type => type.IsColonizable)
                    .FirstOrDefault(),
                SystemName = _dbContext.Systems
                    .Where(system => system.Id == entry.SystemId)
                    .Select(system => system.Name)
                    .FirstOrDefault(),
                SectorName = _dbContext.Sectors
                    .Where(sector => sector.Id == entry.SectorId)
                    .Select(sector => sector.Name)
                    .FirstOrDefault(),
                Deposits = entry.DepositIds
                    .Select(deposit => new PlanetDepositFact(deposit.ResourceId, deposit.Coefficient))
                    .ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return new PlanetFacts(
            row.Id,
            row.SectorId,
            row.OwnerPlayerId,
            row.Name,
            row.Deposits,
            row.IsTerraformed,
            row.IsColonizable,
            row.ObjectTypeId ?? 0,
            row.SystemId,
            row.Orbit,
            string.IsNullOrWhiteSpace(row.SystemName) ? null : row.SystemName,
            string.IsNullOrWhiteSpace(row.SectorName) ? null : row.SectorName);
    }

    /// <summary>
    /// Gets the bodies a player owns.
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bodies in the order of their identifiers.</returns>
    public async Task<IReadOnlyList<OwnedPlanet>> GetOwnedPlanetsAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        return await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.OwnerPlayerId == playerId)
            .OrderBy(planet => planet.Id)
            .Select(planet => new OwnedPlanet(planet.Id, planet.SectorId, planet.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the bodies of several players in one read.
    /// </summary>
    /// <param name="playerIds">The players.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of bodies by player. A player with none is absent.</returns>
    public async Task<IReadOnlyDictionary<string, int>> CountOwnedPlanetsAsync(
        IReadOnlyCollection<string> playerIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        if (playerIds.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        List<string> ids = [.. playerIds];

        var counts = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.OwnerPlayerId != null && ids.Contains(planet.OwnerPlayerId))
            .GroupBy(planet => planet.OwnerPlayerId!)
            .Select(group => new { PlayerId = group.Key, PlanetCount = group.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return counts.ToDictionary(entry => entry.PlayerId, entry => entry.PlanetCount);
    }

    /// <summary>
    /// Gets the bodies of a player with everything the screen of the civilization shows about
    /// them: the name, the type, the place in the galaxy and the deposits (task 00010,
    /// section 7.4).
    /// </summary>
    /// <param name="playerId">The player.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The bodies of the player, in the order of their identifiers.</returns>
    public async Task<IReadOnlyList<OwnedPlanetDto>> GetOwnedPlanetsDetailedAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        // The type of a body is a column of the object it is, so the types come from the
        // objects of the bodies and never from the code of the type. One query reads the
        // bodies with their objects, their names and their deposits (task 00036, section 6.3).
        var rows = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.OwnerPlayerId == playerId)
            .OrderBy(planet => planet.Id)
            .Select(planet => new
            {
                planet.Id,
                planet.Name,
                planet.CelestialObject!.ObjectTypeId,
                planet.SectorId,
                planet.SystemId,
                planet.CelestialObject.Orbit,
                planet.SizeClassId,
                planet.Size,
                planet.IsTerraformed,
                SystemName = _dbContext.Systems
                    .Where(system => system.Id == planet.SystemId)
                    .Select(system => system.Name)
                    .FirstOrDefault(),
                SectorName = _dbContext.Sectors
                    .Where(sector => sector.Id == planet.SectorId)
                    .Select(sector => sector.Name)
                    .FirstOrDefault(),
                Deposits = planet.DepositIds
                    .OrderBy(deposit => deposit.ResourceId)
                    .Select(deposit => new PlanetDepositFact(deposit.ResourceId, deposit.Coefficient))
                    .ToList(),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new OwnedPlanetDto(
                row.Id,
                row.Name,
                row.ObjectTypeId,
                row.SectorId,
                row.SystemId,
                row.Orbit,
                row.SizeClassId,
                row.Size,
                row.IsTerraformed,
                row.Deposits,
                string.IsNullOrWhiteSpace(row.SystemName) ? null : row.SystemName,
                string.IsNullOrWhiteSpace(row.SectorName) ? null : row.SectorName))
        ];
    }

    /// <summary>
    /// Gets the resource types and their groups.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The catalog.</returns>
    public Task<ResourceCatalog> GetResourceCatalogAsync(CancellationToken cancellationToken) =>
        _resourceCatalog.GetAsync(_dbContext, cancellationToken);
}

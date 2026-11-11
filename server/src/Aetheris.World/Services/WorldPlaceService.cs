using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Geometry;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The places of the map in the coordinates of the galaxy, for the flights of the fleets (task 00053).
/// </summary>
/// <remarks>
/// <para>
/// A fleet stands at a place and flies to another place, so the module of the fleets needs the
/// absolute position of an object, of a body or of a point of open space, and the distance between
/// two places. The reads pass no visibility filter: they are for the server itself, and the player
/// who sends a fleet there is checked by the command.
/// </para>
/// <para>
/// The distance follows the rule of the map: inside one star system it is the number of orbits
/// between the places times the step of an orbit, between systems and in open space it is the
/// straight three-dimensional distance between the absolute positions.
/// </para>
/// </remarks>
public sealed class WorldPlaceService
{
    private readonly WorldDbContext _dbContext;
    private readonly WorldReferenceRepository _reference;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldPlaceService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="reference">The parameters.</param>
    public WorldPlaceService(WorldDbContext dbContext, WorldReferenceRepository reference)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(reference);

        _dbContext = dbContext;
        _reference = reference;
    }

    /// <summary>
    /// Gets the place of an object of the map.
    /// </summary>
    /// <param name="objectId">The object of the map.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when the object is unknown or a field that is gone.</returns>
    public async Task<WorldPlaceDto?> GetObjectPlaceAsync(long objectId, CancellationToken cancellationToken)
    {
        // One query reads the object, its type, its sector, its system and its body.
        var row = await (
                from entry in _dbContext.Objects.AsNoTracking()
                where entry.Id == objectId
                join type in _dbContext.ObjectTypes on entry.ObjectTypeId equals type.Id
                join sector in _dbContext.Sectors on entry.SectorId equals sector.Id
                join system in _dbContext.Systems on entry.SystemId equals system.Id into systemRows
                from system in systemRows.DefaultIfEmpty()
                select new
                {
                    entry.SectorId,
                    entry.SystemId,
                    entry.Orbit,
                    type.ObjectClass,
                    SectorX = sector.X,
                    SectorY = sector.Y,
                    SectorZ = sector.Z,
                    LocalX = system == null ? entry.X : system.X,
                    LocalY = system == null ? entry.Y : system.Y,
                    LocalZ = system == null ? entry.Z : system.Z,
                    PlanetId = _dbContext.Planets
                        .Where(planet => planet.CelestialObjectId == entry.Id)
                        .Select(planet => (long?)planet.Id)
                        .FirstOrDefault(),
                    OwnerPlayerId = _dbContext.Planets
                        .Where(planet => planet.CelestialObjectId == entry.Id)
                        .Select(planet => planet.OwnerPlayerId)
                        .FirstOrDefault(),
                    FieldGone = _dbContext.Fields
                        .Where(field => field.CelestialObjectId == entry.Id)
                        .Select(field => field.DepletedAt != null)
                        .FirstOrDefault(),
                    HasField = _dbContext.Fields.Any(field => field.CelestialObjectId == entry.Id),
                })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        // A depleted field has disappeared from the map: a fleet cannot fly to it.
        if (row.ObjectClass == WorldObjectClasses.AsteroidField && (!row.HasField || row.FieldGone))
        {
            return null;
        }

        return new WorldPlaceDto(
            row.SectorId,
            row.SystemId,
            objectId,
            row.PlanetId,
            row.Orbit,
            row.ObjectClass,
            row.OwnerPlayerId,
            row.SectorX + row.LocalX,
            row.SectorY + row.LocalY,
            row.SectorZ + row.LocalZ);
    }

    /// <summary>
    /// Gets the place of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when the body is unknown.</returns>
    public async Task<WorldPlaceDto?> GetPlanetPlaceAsync(long planetId, CancellationToken cancellationToken)
    {
        long? objectId = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.Id == planetId)
            .Select(planet => (long?)planet.CelestialObjectId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return objectId is null ? null : await GetObjectPlaceAsync(objectId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a point of open space inside a sector as a place.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="x">The first axis inside the sector.</param>
    /// <param name="y">The second axis inside the sector.</param>
    /// <param name="z">The third axis inside the sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The place, or <see langword="null"/> when the sector is unknown or the point lies outside it.</returns>
    public async Task<WorldPlaceDto?> GetPointPlaceAsync(int sectorId, double x, double y, double z, CancellationToken cancellationToken)
    {
        WorldSettings settings = _reference.Settings;

        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)
            || x < 0 || x > settings.SectorSize
            || y < 0 || y > settings.SectorSize
            || z < 0 || z > settings.SectorDepth)
        {
            return null;
        }

        SectorEntity? sector = await _dbContext.Sectors.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == sectorId, cancellationToken).ConfigureAwait(false);

        return sector is null
            ? null
            : new WorldPlaceDto(sectorId, null, null, null, null, string.Empty, null, sector.X + x, sector.Y + y, sector.Z + z);
    }

    /// <summary>
    /// Finds the place a fleet standing at a point of the galaxy takes from: a free asteroid field whose
    /// point is within the tolerance of the point, or a nebula whose sphere holds the point (task 00054).
    /// </summary>
    /// <remarks>
    /// The search is bounded: the sectors whose box (widened by the largest radius of a nebula and the
    /// tolerance) holds the point are read first, and only the free fields and the nebulae of those
    /// sectors are read, each with a bounding box on the coordinates, so the whole map is never walked.
    /// A field of a star system is not looked for by the point, because a fleet stands at it by the
    /// reference to its object.
    /// </remarks>
    /// <param name="x">The first axis in the galaxy.</param>
    /// <param name="y">The second axis in the galaxy.</param>
    /// <param name="z">The third axis in the galaxy.</param>
    /// <param name="tolerance">How far from the point of a free field the point may lie, in the units of the map.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The nearest place, or <see langword="null"/> when the point holds none.</returns>
    public async Task<WorldPlaceDto?> FindExtractionPlaceAsync(double x, double y, double z, double tolerance, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(tolerance))
        {
            return null;
        }

        WorldSettings settings = _reference.Settings;
        double reach = Math.Max(0d, tolerance) + settings.NebulaRadiusMax;
        double size = settings.SectorSize;
        double depth = settings.SectorDepth;

        int[] sectorIds = [.. await _dbContext.Sectors.AsNoTracking()
            .Where(sector => sector.X - reach <= x && x <= sector.X + size + reach
                && sector.Y - reach <= y && y <= sector.Y + size + reach
                && sector.Z - reach <= z && z <= sector.Z + depth + reach)
            .Select(sector => sector.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (sectorIds.Length == 0)
        {
            return null;
        }

        double near = Math.Max(0d, tolerance);

        // The free fields that are not gone, with the box of the tolerance around the point.
        var fields = await (
                from field in _dbContext.Fields.AsNoTracking()
                where field.SystemId == null && field.DepletedAt == null && sectorIds.Contains(field.SectorId)
                join entry in _dbContext.Objects on field.CelestialObjectId equals entry.Id
                join sector in _dbContext.Sectors on entry.SectorId equals sector.Id
                where sector.X + entry.X >= x - near && sector.X + entry.X <= x + near
                    && sector.Y + entry.Y >= y - near && sector.Y + entry.Y <= y + near
                    && sector.Z + entry.Z >= z - near && sector.Z + entry.Z <= z + near
                select new { entry.Id, entry.SectorId, X = sector.X + entry.X, Y = sector.Y + entry.Y, Z = sector.Z + entry.Z })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // The nebulae whose bounding box of the radius holds the point.
        var nebulae = await (
                from nebula in _dbContext.Nebulae.AsNoTracking()
                where sectorIds.Contains(nebula.SectorId)
                join entry in _dbContext.Objects on nebula.CelestialObjectId equals entry.Id
                join sector in _dbContext.Sectors on entry.SectorId equals sector.Id
                where sector.X + entry.X - nebula.Radius <= x && x <= sector.X + entry.X + nebula.Radius
                    && sector.Y + entry.Y - nebula.Radius <= y && y <= sector.Y + entry.Y + nebula.Radius
                    && sector.Z + entry.Z - nebula.Radius <= z && z <= sector.Z + entry.Z + nebula.Radius
                select new { entry.Id, entry.SectorId, nebula.Radius, X = sector.X + entry.X, Y = sector.Y + entry.Y, Z = sector.Z + entry.Z })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Point3 point = new(x, y, z);
        WorldPlaceDto? best = null;
        double bestDistance = double.MaxValue;

        foreach (var field in fields)
        {
            double distance = WorldGeometry.Distance(point, new Point3(field.X, field.Y, field.Z));

            if (distance <= near && IsCloser(distance, field.Id, bestDistance, best))
            {
                best = new WorldPlaceDto(field.SectorId, null, field.Id, null, null, WorldObjectClasses.AsteroidField, null, field.X, field.Y, field.Z);
                bestDistance = distance;
            }
        }

        foreach (var nebula in nebulae)
        {
            double distance = WorldGeometry.Distance(point, new Point3(nebula.X, nebula.Y, nebula.Z));

            if (distance <= nebula.Radius && IsCloser(distance, nebula.Id, bestDistance, best))
            {
                best = new WorldPlaceDto(nebula.SectorId, null, nebula.Id, null, null, WorldObjectClasses.Nebula, null, nebula.X, nebula.Y, nebula.Z);
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// Gets the distance between two places.
    /// </summary>
    /// <param name="from">The place the flight starts at.</param>
    /// <param name="to">The place the flight ends at.</param>
    /// <returns>The distance, in the units of the map.</returns>
    public double GetTravelDistance(WorldPlaceDto from, WorldPlaceDto to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        // Inside one star system the bodies are told apart by their orbits, not by coordinates.
        if (from.SystemId is { } systemId && systemId == to.SystemId)
        {
            double orbits = Math.Abs((to.Orbit ?? 0) - (from.Orbit ?? 0));

            return orbits * _reference.Settings.MapOrbitDistanceStep;
        }

        return WorldGeometry.Distance(new Point3(from.X, from.Y, from.Z), new Point3(to.X, to.Y, to.Z));
    }

    // The nearer place wins; places at the same distance are told apart by the identifier of the object.
    private static bool IsCloser(double distance, long objectId, double bestDistance, WorldPlaceDto? best) =>
        best is null
        || distance < bestDistance
        || (distance == bestDistance && objectId < best.ObjectId);
}

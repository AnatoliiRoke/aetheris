using Aetheris.Core.Configuration;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Geometry;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Visibility;

/// <summary>
/// What a player sees of the map (task 00034; decisions 9, 13, 16 of task 00004): their own
/// bodies and everything inside the range of the radars of their bodies.
/// </summary>
/// <remarks>
/// <para>
/// The range of a radar is its level times <see cref="WorldConfigKeys.RadarRangePerLevel"/>, and
/// a nebula weakens a radar that stands inside it by <see cref="WorldConfigKeys.NebulaRadarMultiplier"/>.
/// What lies inside the range is open: the star systems, their objects and the free objects of
/// the space. The distances are three-dimensional and measured between the absolute positions
/// (the origin of the sector plus the place inside it).
/// </para>
/// <para>
/// The level of a radar comes from <see cref="IRadarLevels"/>, filled by the module of the
/// buildings. While the test stub (<see cref="WorldConfigKeys.RadarStubEnabled"/>) is on, every
/// body counts as having a radar of the stub level; turning the stub off hands the decision to
/// the level of the real building with nothing else changed.
/// </para>
/// </remarks>
public sealed class RadarVisibility : IWorldVisibility
{
    private readonly WorldDbContext _dbContext;
    private readonly IGameConfig _config;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadarVisibility"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="config">The game configuration.</param>
    /// <param name="services">The services; the levels of the radars are asked from them only when the stub is off, because the module that gives them itself depends on the world.</param>
    public RadarVisibility(WorldDbContext dbContext, IGameConfig config, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(services);

        _dbContext = dbContext;
        _config = config;
        _services = services;
    }

    /// <inheritdoc />
    public async Task<VisibilityScope> GetScopeAsync(string playerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        WorldSettings settings = WorldSettings.From(_config);

        if (settings.RevealAll)
        {
            return new VisibilityScope(true, new HashSet<long>(), new HashSet<long>());
        }

        var observers = await (
                from planet in _dbContext.Planets.AsNoTracking()
                join system in _dbContext.Systems on planet.SystemId equals system.Id
                join sector in _dbContext.Sectors on system.SectorId equals sector.Id
                where planet.OwnerPlayerId == playerId
                select new
                {
                    planet.Id,
                    planet.CelestialObjectId,
                    planet.SystemId,
                    planet.SectorId,
                    system.InNebula,
                    SystemX = system.X,
                    SystemY = system.Y,
                    SystemZ = system.Z,
                    SectorX = sector.X,
                    SectorY = sector.Y,
                    SectorZ = sector.Z,
                })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        HashSet<long> objects = [.. observers.Select(observer => observer.CelestialObjectId)];
        HashSet<long> systems = [.. observers.Select(observer => observer.SystemId)];
        List<RadarCircle> circles = [];

        if (observers.Count == 0)
        {
            return new VisibilityScope(false, objects, systems, circles);
        }

        IReadOnlyDictionary<long, int> levels = await LevelsAsync(playerId, [.. observers.Select(observer => observer.Id)], settings, cancellationToken).ConfigureAwait(false);

        foreach (var observer in observers)
        {
            double radius = levels.GetValueOrDefault(observer.Id) * settings.RadarRangePerLevel * (observer.InNebula ? settings.NebulaRadarMultiplier : 1);

            if (radius <= 0)
            {
                continue;
            }

            circles.Add(new RadarCircle(observer.SectorId, observer.Id, observer.SystemX, observer.SystemY, observer.SystemZ, radius));

            Point3 center = new(observer.SectorX + observer.SystemX, observer.SectorY + observer.SystemY, observer.SectorZ + observer.SystemZ);
            double minX = center.X - radius;
            double maxX = center.X + radius;
            double minY = center.Y - radius;
            double maxY = center.Y + radius;
            double minZ = center.Z - radius;
            double maxZ = center.Z + radius;

            // A box in the database first, then the exact sphere in memory.
            var near = await (
                    from system in _dbContext.Systems.AsNoTracking()
                    join sector in _dbContext.Sectors on system.SectorId equals sector.Id
                    where sector.X + system.X >= minX && sector.X + system.X <= maxX
                        && sector.Y + system.Y >= minY && sector.Y + system.Y <= maxY
                        && sector.Z + system.Z >= minZ && sector.Z + system.Z <= maxZ
                    select new { system.Id, X = sector.X + system.X, Y = sector.Y + system.Y, Z = sector.Z + system.Z })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            long[] inRange = [.. near.Where(entry => WorldGeometry.Distance(center, new Point3(entry.X, entry.Y, entry.Z)) <= radius).Select(entry => entry.Id)];

            systems.UnionWith(inRange);
            objects.UnionWith(await _dbContext.Objects.AsNoTracking()
                .Where(entry => entry.SystemId != null && inRange.Contains(entry.SystemId.Value))
                .Select(entry => entry.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false));

            var free = await (
                    from entry in _dbContext.Objects.AsNoTracking()
                    join sector in _dbContext.Sectors on entry.SectorId equals sector.Id
                    where entry.SystemId == null
                        && sector.X + entry.X >= minX && sector.X + entry.X <= maxX
                        && sector.Y + entry.Y >= minY && sector.Y + entry.Y <= maxY
                        && sector.Z + entry.Z >= minZ && sector.Z + entry.Z <= maxZ
                    select new { entry.Id, X = sector.X + entry.X, Y = sector.Y + entry.Y, Z = sector.Z + entry.Z })
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            objects.UnionWith(free.Where(entry => WorldGeometry.Distance(center, new Point3(entry.X, entry.Y, entry.Z)) <= radius).Select(entry => entry.Id));
        }

        return new VisibilityScope(false, objects, systems, circles);
    }

    private async Task<IReadOnlyDictionary<long, int>> LevelsAsync(string playerId, long[] planetIds, WorldSettings settings, CancellationToken cancellationToken)
    {
        if (settings.RadarStubEnabled)
        {
            return planetIds.ToDictionary(id => id, _ => settings.RadarStubLevel);
        }

        IRadarLevels? levels = _services.GetService(typeof(IRadarLevels)) as IRadarLevels;

        return levels is null
            ? new Dictionary<long, int>()
            : await levels.GetLevelsAsync(playerId, planetIds, cancellationToken).ConfigureAwait(false);
    }
}

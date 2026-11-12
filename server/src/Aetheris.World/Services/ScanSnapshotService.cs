using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Scheduling;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>Records and expires a player's scans of the world.</summary>
public sealed class ScanSnapshotService
{
    /// <summary>The scheduled event that removes an expired snapshot.</summary>
    public const string ExpireEventType = "world.scan_snapshot_expire";
    private readonly WorldDbContext _db;
    private readonly WorldUnitOfWork _unit;
    private readonly IEventScheduler _scheduler;

    /// <summary>Initializes a new instance of the <see cref="ScanSnapshotService"/> class.</summary>
    /// <param name="db">The world context.</param>
    /// <param name="unit">The world transaction.</param>
    /// <param name="scheduler">The event scheduler.</param>
    public ScanSnapshotService(WorldDbContext db, WorldUnitOfWork unit, IEventScheduler scheduler)
    {
        _db = db;
        _unit = unit;
        _scheduler = scheduler;
    }

    /// <summary>Captures the systems and free fields within a sphere.</summary>
    /// <param name="playerId">The player.</param>
    /// <param name="sectorId">The sector containing the centre.</param>
    /// <param name="x">The absolute x coordinate.</param>
    /// <param name="y">The absolute y coordinate.</param>
    /// <param name="z">The absolute z coordinate.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="now">The scan time.</param>
    /// <param name="expiresAt">The expiration time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot and the contents of its report.</returns>
    public Task<ScanSnapshotResult> TakeAsync(string playerId, int sectorId, double x, double y, double z, double radius, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        _unit.RunAsync(async () =>
        {
            ArgumentException.ThrowIfNullOrEmpty(playerId);
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(radius) || radius <= 0 || expiresAt <= now)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            await _db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({playerId}, 0))", cancellationToken).ConfigureAwait(false);
            SectorEntity centre = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == sectorId, cancellationToken).ConfigureAwait(false);
            double cx = x, cy = y, cz = z, r2 = radius * radius;
            var systems = await (from system in _db.Systems.AsNoTracking()
                                 join sector in _db.Sectors.AsNoTracking() on system.SectorId equals sector.Id
                                 where (sector.X + system.X - cx) * (sector.X + system.X - cx)
                                     + (sector.Y + system.Y - cy) * (sector.Y + system.Y - cy)
                                     + (sector.Z + system.Z - cz) * (sector.Z + system.Z - cz) <= r2
                                 select system).ToListAsync(cancellationToken).ConfigureAwait(false);
            long[] systemIds = [.. systems.Select(s => s.Id)];
            var freeFields = await (from field in _db.Fields.AsNoTracking()
                                    join obj in _db.Objects.AsNoTracking() on field.CelestialObjectId equals obj.Id
                                    join sector in _db.Sectors.AsNoTracking() on obj.SectorId equals sector.Id
                                    where field.SystemId == null && field.DepletedAt == null
                                        && (sector.X + obj.X - cx) * (sector.X + obj.X - cx)
                                        + (sector.Y + obj.Y - cy) * (sector.Y + obj.Y - cy)
                                        + (sector.Z + obj.Z - cz) * (sector.Z + obj.Z - cz) <= r2
                                    select obj.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var freeNebulae = await (from nebula in _db.Nebulae.AsNoTracking()
                                     join obj in _db.Objects.AsNoTracking() on nebula.CelestialObjectId equals obj.Id
                                     join sector in _db.Sectors.AsNoTracking() on obj.SectorId equals sector.Id
                                     where obj.SystemId == null
                                         && (sector.X + obj.X - cx) * (sector.X + obj.X - cx)
                                         + (sector.Y + obj.Y - cy) * (sector.Y + obj.Y - cy)
                                         + (sector.Z + obj.Z - cz) * (sector.Z + obj.Z - cz) <= r2
                                     select obj.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var objects = await _db.Objects.AsNoTracking().Where(o => (o.SystemId != null && systemIds.Contains(o.SystemId.Value)) || freeFields.Contains(o.Id) || freeNebulae.Contains(o.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
            long[] objectIds = [.. objects.Select(o => o.Id)];
            var planets = await _db.Planets.AsNoTracking().Where(p => objectIds.Contains(p.CelestialObjectId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var fields = await _db.Fields.AsNoTracking().Where(f => objectIds.Contains(f.CelestialObjectId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var nebulae = await _db.Nebulae.AsNoTracking().Where(n => objectIds.Contains(n.CelestialObjectId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            long[] planetIds = [.. planets.Select(p => p.Id)];
            long[] fieldIds = [.. fields.Select(f => f.Id)];
            var deposits = await _db.Deposits.AsNoTracking().Where(d => planetIds.Contains(d.PlanetId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var compositions = await _db.FieldCompositions.AsNoTracking().Where(d => fieldIds.Contains(d.AsteroidFieldId)).ToListAsync(cancellationToken).ConfigureAwait(false);
            var planetByObject = planets.ToDictionary(p => p.CelestialObjectId);
            var fieldByObject = fields.ToDictionary(f => f.CelestialObjectId);
            var nebulaByObject = nebulae.ToDictionary(n => n.CelestialObjectId);
            var depositsByPlanet = deposits.ToLookup(d => d.PlanetId);
            var compositionByField = compositions.ToLookup(d => d.AsteroidFieldId);

            var old = await _db.ScanSnapshots.Where(s => s.PlayerId == playerId).Include(s => s.SystemIds).Include(s => s.ObjectIds).AsSplitQuery().ToListAsync(cancellationToken).ConfigureAwait(false);
            int[] oldSectorIds = [.. old.SelectMany(s => s.SystemIds.Select(system => system.SectorId).Concat(s.ObjectIds.Select(obj => obj.SectorId))).Distinct()];
            var sectors = await _db.Sectors.AsNoTracking().Where(s => oldSectorIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken).ConfigureAwait(false);
            foreach (ScanSnapshotEntity previous in old)
            {
                var replaced = previous.SystemIds.Where(s =>
                {
                    SectorEntity sector = sectors[s.SectorId];
                    double dx = sector.X + s.X - cx, dy = sector.Y + s.Y - cy, dz = sector.Z + s.Z - cz;
                    return (dx * dx) + (dy * dy) + (dz * dz) <= r2;
                }).ToArray();
                HashSet<long> replacedIds = [.. replaced.Select(s => s.SystemId)];
                var removedObjects = previous.ObjectIds.Where(o =>
                {
                    if (o.SystemId is long id) return replacedIds.Contains(id);
                    SectorEntity sector = sectors[o.SectorId];
                    double dx = sector.X + o.X - cx, dy = sector.Y + o.Y - cy, dz = sector.Z + o.Z - cz;
                    return (dx * dx) + (dy * dy) + (dz * dz) <= r2;
                }).ToArray();
                _db.ScanSnapshotSystems.RemoveRange(replaced);
                _db.ScanSnapshotObjects.RemoveRange(removedObjects);
                if (previous.SystemIds.Count == replaced.Length && previous.ObjectIds.Count == removedObjects.Length)
                {
                    _db.ScanSnapshots.Remove(previous);
                }
            }

            ScanSnapshotEntity snapshot = new() { PlayerId = playerId, SectorId = sectorId, X = x - centre.X, Y = y - centre.Y, Z = z - centre.Z, Radius = radius, ScannedAt = now, ExpiresAt = expiresAt };
            snapshot.SystemIds = [.. systems.Select(s => new ScanSnapshotSystemEntity { SystemId = s.Id, SectorId = s.SectorId, Name = s.Name, Number = s.Number, InNebula = s.InNebula, X = s.X, Y = s.Y, Z = s.Z })];
            foreach (CelestialObjectEntity obj in objects)
            {
                planetByObject.TryGetValue(obj.Id, out PlanetEntity? planet);
                fieldByObject.TryGetValue(obj.Id, out AsteroidFieldEntity? field);
                nebulaByObject.TryGetValue(obj.Id, out NebulaEntity? nebula);
                if (field?.DepletedAt is not null) continue;
                if (obj.SystemId == null && field is null && nebula is null) continue;
                ScanSnapshotObjectEntity saved = new() { CelestialObjectId = obj.Id, SystemId = obj.SystemId, SectorId = obj.SectorId, ObjectTypeId = obj.ObjectTypeId, Orbit = obj.Orbit, Name = planet?.Name ?? obj.Name, X = obj.X, Y = obj.Y, Z = obj.Z, OwnerPlayerId = planet?.OwnerPlayerId, PlanetId = planet?.Id, SizeClassId = planet?.SizeClassId, Size = planet?.Size, IsTerraformed = planet?.IsTerraformed ?? false, AsteroidFieldId = field?.Id, FieldMass = field?.Mass, NebulaId = nebula?.Id, NebulaRadius = nebula?.Radius, NebulaBlocksJumpDrive = nebula?.BlocksJumpDrive ?? false };
                if (planet is not null) saved.DepositIds.AddRange(depositsByPlanet[planet.Id].Select(d => new ScanSnapshotDepositEntity { ResourceId = d.ResourceId, Coefficient = d.Coefficient }));
                if (field is not null) saved.DepositIds.AddRange(compositionByField[field.Id].Select(d => new ScanSnapshotDepositEntity { ResourceId = d.ResourceId, Coefficient = d.Coefficient }));
                snapshot.ObjectIds.Add(saved);
            }

            _db.ScanSnapshots.Add(snapshot);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _scheduler.ScheduleAsync(new ScheduledEventRequest(sectorId, ExpireEventType, expiresAt, JsonSerializer.Serialize(snapshot.Id), ExpireEventType + ":" + snapshot.Id.ToString(CultureInfo.InvariantCulture)), cancellationToken).ConfigureAwait(false);
            Dictionary<int, string> classes = await _db.ObjectTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.ObjectClass, cancellationToken).ConfigureAwait(false);
            return new ScanSnapshotResult(snapshot.Id, sectorId, centre.Name, snapshot.X, snapshot.Y, snapshot.Z, radius, now,
                [.. snapshot.SystemIds.Select(s => new ScanSystemResult(s.SystemId, s.SectorId, s.Name))],
                [.. snapshot.ObjectIds.Select(o => new ScanObjectResult(o.CelestialObjectId, o.SystemId, o.SectorId, o.Name, classes[o.ObjectTypeId], o.PlanetId, o.DepositIds.Count > 0))]);
        }, cancellationToken);

    /// <summary>Removes a snapshot whose scheduled expiration has arrived.</summary>
    /// <param name="id">The snapshot identifier.</param>
    /// <param name="now">The event time.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the work.</returns>
    public Task<int> ExpireAsync(long id, DateTimeOffset now, CancellationToken cancellationToken) =>
        _unit.RunAsync(async () => await _db.ScanSnapshots.Where(s => s.Id == id && s.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false), cancellationToken);
}

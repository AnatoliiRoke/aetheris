using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.Core.Scheduling;
using Aetheris.Core.Time;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Extraction;
using Aetheris.World.Generation;
using Aetheris.World.Geometry;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The life of asteroid fields: processing, depletion and the appearance of new fields
/// (task 00004, section 2.4, rule 2; decisions 5, 10, 11, 14, 18).
/// </summary>
public sealed class FieldService
{
    /// <summary>
    /// The event type that replaces depleted fields of a sector.
    /// </summary>
    public const string RespawnEventType = "world.field_respawn";

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly WorldReferenceRepository _reference;
    private readonly IEventScheduler _scheduler;
    private readonly IGameClock _clock;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="reference">The generator input.</param>
    /// <param name="scheduler">The event scheduler.</param>
    /// <param name="clock">The game clock.</param>
    /// <param name="timeProvider">The real clock.</param>
    public FieldService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        WorldReferenceRepository reference,
        IEventScheduler scheduler,
        IGameClock clock,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _reference = reference;
        _scheduler = scheduler;
        _clock = clock;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Processes one cycle on a field. Several players work on one field at once; the row lock
    /// orders them, and whoever comes first takes what is left.
    /// </summary>
    /// <param name="fieldId">The field.</param>
    /// <param name="processingMass">The mass the module processes per cycle.</param>
    /// <param name="freeHold">The free room of the hold.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The yield of the cycle.</returns>
    public Task<ExtractionResult> ExtractAsync(long fieldId, long processingMass, long freeHold, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                AsteroidFieldEntity field = (await _dbContext.Fields
                    .FromSql($"SELECT * FROM asteroid_field WHERE id = {fieldId} AND depleted_at IS NULL FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new GameException(GameErrorCodes.NotFound, $"The field {fieldId} does not exist.");

                List<ResourceShare> composition = await _dbContext.FieldCompositions.AsNoTracking()
                    .Where(share => share.AsteroidFieldId == fieldId)
                    .OrderBy(share => share.Id)
                    .Select(share => new ResourceShare(share.ResourceId, share.Coefficient))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                ExtractionResult result = FieldExtraction.Extract(field.Mass, composition, processingMass, freeHold);
                field.Mass = result.RemainingMass;

                if (result.Depleted)
                {
                    await DepleteAsync(field, cancellationToken).ConfigureAwait(false);
                }

                return result;
            },
            cancellationToken);

    /// <summary>
    /// Adds new fields to a sector until it holds the minimum number again; at least one
    /// field appears, because the event follows a depleted one.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="seedSalt">A value that makes the random choice of this call its own, for example the event identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The new fields.</returns>
    public Task<IReadOnlyList<long>> SpawnFieldsAsync(int sectorId, long seedSalt, CancellationToken cancellationToken) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                WorldGenerationConfig config = await _reference.LoadAsync(cancellationToken).ConfigureAwait(false);
                WorldSettings settings = config.Settings;

                SectorEntity sector = await _dbContext.Sectors.AsNoTracking()
                    .SingleOrDefaultAsync(entry => entry.Id == sectorId, cancellationToken).ConfigureAwait(false)
                    ?? throw new GameException(GameErrorCodes.NotFound, $"The sector {sectorId} does not exist.");

                int active = await _dbContext.Fields
                    .CountAsync(field => field.SectorId == sectorId && field.DepletedAt == null, cancellationToken).ConfigureAwait(false);
                int everCreated = await _dbContext.Fields
                    .CountAsync(field => field.SectorId == sectorId, cancellationToken).ConfigureAwait(false);

                // The outermost orbits that are still free can take a field.
                List<StarSystemEntity> freeOrbits = await _dbContext.Systems.AsNoTracking()
                    .Where(system => system.SectorId == sectorId)
                    .Where(system => !_dbContext.Fields.Any(field =>
                        field.SystemId == system.Id && field.DepletedAt == null))
                    .OrderBy(system => system.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                int toCreate = Math.Max(1, settings.MinFieldsPerSector - active);
                WorldRandom random = new(WorldRandom.DeriveSeed(sector.Seed, seedSalt));
                DateTimeOffset now = _timeProvider.GetUtcNow();
                List<AsteroidFieldEntity> created = [];

                for (int index = 0; index < toCreate; index++)
                {
                    string name = SectorGenerator.FieldName(settings, sector.Name, everCreated + index + 1);
                    StarSystemEntity? system = null;

                    if (freeOrbits.Count > 0 && random.Chance(settings.FieldRespawnInSystemProbability))
                    {
                        system = freeOrbits[random.NextInclusive(0, freeOrbits.Count - 1)];
                        freeOrbits.Remove(system);
                    }

                    Point3 position = system is null
                        ? SectorGenerator.RandomPoint(random, settings)
                        : new Point3(system.X, system.Y, system.Z);

                    FieldPlan plan = SectorGenerator.CreateField(random, config, name, system is null ? null : settings.OrbitCount, position);
                    CelestialObjectEntity entry = WorldGenerationService.NewObject(sectorId, plan.ObjectTypeId, plan.Orbit, plan.Name, position, now);
                    entry.SystemId = system?.Id;

                    _dbContext.Objects.Add(entry);
                    await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                    AsteroidFieldEntity field = WorldGenerationService.NewField(sectorId, entry, plan);
                    _dbContext.Fields.Add(field);
                    created.Add(field);
                }

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                _dbContext.GenerationLog.Add(new WorldGenerationLogEntity
                {
                    GalaxyId = sector.GalaxyId,
                    SectorId = sectorId,
                    Action = WorldGenerationActions.FieldsSpawned,
                    ActorId = WorldGenerationService.SystemActor,
                    Detail = JsonSerializer.Serialize(new { field_ids = created.Select(field => field.Id) }),
                    At = now,
                });

                return (IReadOnlyList<long>)[.. created.Select(field => field.Id)];
            },
            cancellationToken);

    private async Task DepleteAsync(AsteroidFieldEntity field, CancellationToken cancellationToken)
    {
        WorldSettings settings = _reference.Settings;
        DateTimeOffset gameNow = _clock.GameNow;

        // The field disappears: it stays as a record, but no query of the map shows it.
        field.DepletedAt = gameNow;

        _dbContext.GenerationLog.Add(new WorldGenerationLogEntity
        {
            SectorId = field.SectorId,
            Action = WorldGenerationActions.FieldDepleted,
            ActorId = WorldGenerationService.SystemActor,
            Detail = JsonSerializer.Serialize(new { field_id = field.Id }),
            At = _timeProvider.GetUtcNow(),
        });

        await _scheduler.ScheduleAsync(
            new ScheduledEventRequest(
                field.SectorId,
                RespawnEventType,
                gameNow + settings.FieldRespawnDelay,
                JsonSerializer.Serialize(new { sector_id = field.SectorId, field_id = field.Id }),
                RespawnEventType + ":" + field.Id.ToString(CultureInfo.InvariantCulture)),
            cancellationToken).ConfigureAwait(false);
    }
}

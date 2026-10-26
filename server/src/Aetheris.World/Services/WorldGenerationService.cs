using System.Diagnostics;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;
using Aetheris.World.Geometry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aetheris.World.Services;

/// <summary>
/// Generates the galaxy and its sectors and stores them (task 00004, sections 2.2, 2.5, 2.6).
/// </summary>
/// <remarks>
/// The description of a sector comes from the pure <see cref="SectorGenerator"/>; this
/// service only reads its input and writes its output, one sector per transaction, with a
/// record in <c>world_generation_log</c>.
/// </remarks>
public sealed class WorldGenerationService
{
    /// <summary>
    /// The actor of the actions the server starts itself.
    /// </summary>
    public const string SystemActor = "system";

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly WorldReferenceRepository _reference;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorldGenerationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldGenerationService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="reference">The generator input.</param>
    /// <param name="timeProvider">The clock.</param>
    /// <param name="logger">The logger.</param>
    public WorldGenerationService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        WorldReferenceRepository reference,
        TimeProvider timeProvider,
        ILogger<WorldGenerationService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _reference = reference;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates the galaxy and the initial sectors when they are missing. A repeated call
    /// changes nothing.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What was created.</returns>
    public async Task<WorldBootstrapResult> EnsureWorldAsync(CancellationToken cancellationToken)
    {
        bool referenceSeeded = await _unitOfWork.RunAsync(
            async () =>
            {
                bool seeded = await WorldReferenceSeed.SeedAsync(_dbContext, cancellationToken).ConfigureAwait(false);
                seeded |= await WorldReferenceSeed.SeedRacesAsync(_dbContext, cancellationToken).ConfigureAwait(false);

                if (seeded)
                {
                    AddLog(null, null, WorldGenerationActions.ReferenceSeeded, null, SystemActor, new { });
                }

                return seeded;
            },
            cancellationToken).ConfigureAwait(false);

        (GalaxyEntity galaxy, bool galaxyCreated) = await EnsureGalaxyAsync(cancellationToken).ConfigureAwait(false);
        WorldSettings settings = _reference.Settings;

        List<int> existing = await _dbContext.Sectors.AsNoTracking()
            .Where(sector => sector.GalaxyId == galaxy.Id)
            .Select(sector => sector.LayoutIndex)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<int> created = [];

        for (int layoutIndex = 0; layoutIndex < settings.InitialSectorCount; layoutIndex++)
        {
            if (existing.Contains(layoutIndex))
            {
                continue;
            }

            SectorSummary sector = await CreateSectorAsync(
                galaxy,
                layoutIndex,
                WorldRandom.DeriveSeed(galaxy.Seed, layoutIndex),
                layoutIndex < settings.NewbieSectorCount,
                SystemActor,
                cancellationToken).ConfigureAwait(false);

            created.Add(sector.Id);
        }

        return new WorldBootstrapResult(referenceSeeded, galaxyCreated, created);
    }

    /// <summary>
    /// Adds a sector at the edge of the galaxy (task 00004, section 2.4, rule 4).
    /// </summary>
    /// <param name="seed">The seed, or <see langword="null"/> to derive it from the galaxy seed.</param>
    /// <param name="isNewbie">Whether newcomers start in the new sector.</param>
    /// <param name="actorId">Who adds the sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The new sector.</returns>
    public async Task<SectorSummary> AddSectorAsync(long? seed, bool isNewbie, string actorId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(actorId);

        (GalaxyEntity galaxy, _) = await EnsureGalaxyAsync(cancellationToken).ConfigureAwait(false);

        int? lastIndex = await _dbContext.Sectors.AsNoTracking()
            .Where(sector => sector.GalaxyId == galaxy.Id)
            .MaxAsync(sector => (int?)sector.LayoutIndex, cancellationToken).ConfigureAwait(false);

        int layoutIndex = lastIndex is null ? 0 : lastIndex.Value + 1;

        return await CreateSectorAsync(
            galaxy,
            layoutIndex,
            seed ?? WorldRandom.DeriveSeed(galaxy.Seed, layoutIndex),
            isNewbie,
            actorId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates an empty sector again: a sector where no body has an owner.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="seed">The new seed, or <see langword="null"/> to keep the old one.</param>
    /// <param name="actorId">Who starts the action.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The sector.</returns>
    public async Task<SectorSummary> RegenerateSectorAsync(int sectorId, long? seed, string actorId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(actorId);

        WorldGenerationConfig config = await LoadValidConfigAsync(cancellationToken).ConfigureAwait(false);

        return await _unitOfWork.RunAsync(
            async () =>
            {
                SectorEntity sector = (await _dbContext.Sectors
                    .FromSql($"SELECT * FROM sector WHERE id = {sectorId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new GameException(GameErrorCodes.NotFound, $"The sector {sectorId} does not exist.");

                bool hasOwners = await _dbContext.Planets
                    .AnyAsync(planet => planet.SectorId == sectorId && planet.OwnerPlayerId != null, cancellationToken)
                    .ConfigureAwait(false);

                if (hasOwners)
                {
                    throw new GameException(WorldErrorCodes.SectorNotEmpty, $"The sector {sectorId} has owners.");
                }

                await DeleteContentAsync(sectorId, cancellationToken).ConfigureAwait(false);

                long oldSeed = sector.Seed;
                sector.Seed = seed ?? sector.Seed;

                Stopwatch stopwatch = Stopwatch.StartNew();
                SectorPlan plan = SectorGenerator.Generate(sector.Seed, sector.Name, config);
                await PersistContentAsync(sector, plan, cancellationToken).ConfigureAwait(false);

                AddLog(sector.GalaxyId, sector.Id, WorldGenerationActions.SectorRegenerated, sector.Seed, actorId, new
                {
                    old_seed = oldSeed,
                    systems = plan.SystemIds.Count,
                    elapsed_ms = stopwatch.ElapsedMilliseconds,
                });

                return ToSummary(sector, plan);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Counts the content of a sector for the admin panel.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The statistics, or <see langword="null"/> when the sector does not exist.</returns>
    public async Task<SectorStatistics?> GetStatisticsAsync(int sectorId, CancellationToken cancellationToken)
    {
        SectorEntity? sector = await _dbContext.Sectors.AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == sectorId, cancellationToken).ConfigureAwait(false);

        if (sector is null)
        {
            return null;
        }

        int systems = await _dbContext.Systems.CountAsync(system => system.SectorId == sectorId, cancellationToken).ConfigureAwait(false);

        Dictionary<string, int> byType = await _dbContext.Objects.AsNoTracking()
            .Where(entry => entry.SectorId == sectorId)
            .Join(_dbContext.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => type.Code)
            .GroupBy(code => code)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.Code, entry => entry.Count, cancellationToken).ConfigureAwait(false);

        var bodies = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.SectorId == sectorId)
            .Join(_dbContext.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { planet.OwnerPlayerId, entry.ObjectTypeId })
            .Join(_dbContext.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => new { entry.OwnerPlayerId, type.IsColonizable })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var fields = await _dbContext.Fields.AsNoTracking()
            .Where(field => field.SectorId == sectorId && field.DepletedAt == null)
            .Select(field => field.Mass)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        int nebulae = await _dbContext.Nebulae.CountAsync(nebula => nebula.SectorId == sectorId, cancellationToken).ConfigureAwait(false);

        return new SectorStatistics(
            sector.Id,
            sector.Name,
            sector.Seed,
            sector.IsNewbie,
            systems,
            byType,
            bodies.Count,
            bodies.Count(body => body.IsColonizable && body.OwnerPlayerId == null),
            bodies.Count(body => body.OwnerPlayerId != null),
            fields.Count,
            fields.Sum(),
            nebulae);
    }

    /// <summary>
    /// Reads the generator input and refuses an inconsistent one.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The input.</returns>
    public async Task<WorldGenerationConfig> LoadValidConfigAsync(CancellationToken cancellationToken)
    {
        WorldGenerationConfig config = await _reference.LoadAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> errors = WorldConfigValidator.Validate(config);

        if (errors.Count > 0)
        {
            _logger.LogError("The world configuration is not consistent: {Errors}", string.Join(" ", errors));

            throw new GameException(
                WorldErrorCodes.ConfigInvalid,
                "The world configuration is not consistent.",
                new Dictionary<string, object?> { ["errors"] = errors });
        }

        return config;
    }

    private async Task<(GalaxyEntity Galaxy, bool Created)> EnsureGalaxyAsync(CancellationToken cancellationToken)
    {
        GalaxyEntity? galaxy = await _dbContext.Galaxies.AsNoTracking()
            .OrderBy(entry => entry.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (galaxy is not null)
        {
            return (galaxy, false);
        }

        GalaxyEntity created = await _unitOfWork.RunAsync(
            async () =>
            {
                GalaxyEntity entity = new()
                {
                    Name = "galaxy",
                    Seed = _reference.Settings.GalaxySeed,
                    CreatedAt = _timeProvider.GetUtcNow(),
                };

                _dbContext.Galaxies.Add(entity);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                AddLog(entity.Id, null, WorldGenerationActions.GalaxyCreated, entity.Seed, SystemActor, new { });

                return entity;
            },
            cancellationToken).ConfigureAwait(false);

        _dbContext.Entry(created).State = EntityState.Detached;

        return (created, true);
    }

    private async Task<SectorSummary> CreateSectorAsync(
        GalaxyEntity galaxy,
        int layoutIndex,
        long seed,
        bool isNewbie,
        string actorId,
        CancellationToken cancellationToken)
    {
        WorldGenerationConfig config = await LoadValidConfigAsync(cancellationToken).ConfigureAwait(false);
        WorldSettings settings = config.Settings;

        string name = NameTemplate.Format(settings.Names.Sector, (NameTemplate.Index, layoutIndex + 1));
        Stopwatch stopwatch = Stopwatch.StartNew();
        SectorPlan plan = SectorGenerator.Generate(seed, name, config);
        long generatedMs = stopwatch.ElapsedMilliseconds;

        SectorSummary summary = await _unitOfWork.RunAsync(
            async () =>
            {
                Point3 origin = SectorLayout.Origin(layoutIndex, settings.SectorSpacing);
                SectorEntity sector = new()
                {
                    GalaxyId = galaxy.Id,
                    Name = name,
                    Seed = seed,
                    LayoutIndex = layoutIndex,
                    X = origin.X,
                    Y = origin.Y,
                    Z = origin.Z,
                    IsNewbie = isNewbie,
                    CreatedAt = _timeProvider.GetUtcNow(),
                };

                _dbContext.Sectors.Add(sector);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await PersistContentAsync(sector, plan, cancellationToken).ConfigureAwait(false);

                AddLog(galaxy.Id, sector.Id, WorldGenerationActions.SectorGenerated, seed, actorId, new
                {
                    layout_index = layoutIndex,
                    systems = plan.SystemIds.Count,
                    generated_ms = generatedMs,
                    stored_ms = stopwatch.ElapsedMilliseconds - generatedMs,
                });

                return ToSummary(sector, plan);
            },
            cancellationToken).ConfigureAwait(false);

        _dbContext.ChangeTracker.Clear();

        _logger.LogInformation(
            "Sector {SectorId} generated with {Systems} systems in {Elapsed} ms.",
            summary.Id,
            summary.SystemCount,
            stopwatch.ElapsedMilliseconds);

        return summary;
    }

    /// <summary>
    /// Writes the content of a generated sector.
    /// </summary>
    private async Task PersistContentAsync(SectorEntity sector, SectorPlan plan, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        int sectorId = sector.Id;

        List<(StarSystemEntity System, SystemPlan Plan)> systems = [];
        List<(CelestialObjectEntity Object, BodyPlan Body)> bodies = [];
        List<(CelestialObjectEntity Object, FieldPlan Field)> fields = [];
        List<(CelestialObjectEntity Object, NebulaPlan Nebula)> nebulae = [];

        foreach (SystemPlan systemPlan in plan.SystemIds)
        {
            StarSystemEntity system = new()
            {
                SectorId = sectorId,
                Number = systemPlan.Number,
                Name = systemPlan.Name,
                X = systemPlan.Position.X,
                Y = systemPlan.Position.Y,
                Z = systemPlan.Position.Z,
                InNebula = systemPlan.InNebula,
            };

            system.ObjectIds.Add(NewObject(sectorId, systemPlan.StarTypeId, null, systemPlan.Name, systemPlan.Position, now));

            foreach (BodyPlan body in systemPlan.BodyIds)
            {
                CelestialObjectEntity entry = NewObject(sectorId, body.ObjectTypeId, body.Orbit, body.Name, systemPlan.Position, now);
                system.ObjectIds.Add(entry);
                bodies.Add((entry, body));
            }

            if (systemPlan.Field is { } field)
            {
                CelestialObjectEntity entry = NewObject(sectorId, field.ObjectTypeId, field.Orbit, field.Name, field.Position, now);
                system.ObjectIds.Add(entry);
                fields.Add((entry, field));
            }

            systems.Add((system, systemPlan));
        }

        foreach (FieldPlan field in plan.FreeFieldIds)
        {
            fields.Add((NewObject(sectorId, field.ObjectTypeId, null, field.Name, field.Position, now), field));
        }

        foreach (NebulaPlan nebula in plan.NebulaIds)
        {
            nebulae.Add((NewObject(sectorId, nebula.ObjectTypeId, null, nebula.Name, nebula.Position, now), nebula));
        }

        _dbContext.Systems.AddRange(systems.Select(entry => entry.System));
        _dbContext.Objects.AddRange(fields.Where(entry => entry.Field.Orbit is null).Select(entry => entry.Object));
        _dbContext.Objects.AddRange(nebulae.Select(entry => entry.Object));
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach ((CelestialObjectEntity entry, BodyPlan body) in bodies)
        {
            _dbContext.Planets.Add(new PlanetEntity
            {
                SectorId = sectorId,
                SystemId = entry.SystemId!.Value,
                CelestialObjectId = entry.Id,
                SizeClassId = body.SizeClassId,
                Size = body.Size,
                DepositIds =
                [
                    .. body.DepositIds.Select(share => new DepositEntity
                    {
                        SectorId = sectorId,
                        ResourceId = share.ResourceId,
                        Coefficient = share.Coefficient,
                    }),
                ],
            });
        }

        foreach ((CelestialObjectEntity entry, FieldPlan field) in fields)
        {
            _dbContext.Fields.Add(NewField(sectorId, entry, field));
        }

        foreach ((CelestialObjectEntity entry, NebulaPlan nebula) in nebulae)
        {
            _dbContext.Nebulae.Add(new NebulaEntity
            {
                SectorId = sectorId,
                CelestialObjectId = entry.Id,
                Radius = nebula.Radius,
                BlocksJumpDrive = nebula.BlocksJumpDrive,
                CompositionIds =
                [
                    .. nebula.CompositionIds.Select(share => new NebulaCompositionEntity
                    {
                        SectorId = sectorId,
                        ResourceId = share.ResourceId,
                        Coefficient = share.Coefficient,
                    }),
                ],
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the field row of a stored celestial object.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="entry">The stored celestial object of the field.</param>
    /// <param name="field">The field description.</param>
    /// <returns>The field row.</returns>
    public static AsteroidFieldEntity NewField(int sectorId, CelestialObjectEntity entry, FieldPlan field)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(field);

        return new AsteroidFieldEntity
        {
            SectorId = sectorId,
            SystemId = entry.SystemId,
            CelestialObjectId = entry.Id,
            InitialMass = field.Mass,
            Mass = field.Mass,
            CompositionIds =
            [
                .. field.CompositionIds.Select(share => new FieldCompositionEntity
                {
                    SectorId = sectorId,
                    ResourceId = share.ResourceId,
                    Coefficient = share.Coefficient,
                }),
            ],
        };
    }

    /// <summary>
    /// Creates a celestial object row.
    /// </summary>
    /// <param name="sectorId">The sector.</param>
    /// <param name="objectTypeId">The type.</param>
    /// <param name="orbit">The orbit.</param>
    /// <param name="name">The name.</param>
    /// <param name="position">The position within the sector.</param>
    /// <param name="now">The moment of creation.</param>
    /// <returns>The row.</returns>
    public static CelestialObjectEntity NewObject(int sectorId, int objectTypeId, int? orbit, string name, Point3 position, DateTimeOffset now) =>
        new()
        {
            SectorId = sectorId,
            ObjectTypeId = objectTypeId,
            Orbit = orbit,
            Name = name,
            X = position.X,
            Y = position.Y,
            Z = position.Z,
            CreatedAt = now,
        };

    private async Task DeleteContentAsync(int sectorId, CancellationToken cancellationToken)
    {
        await _dbContext.Deposits.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.FieldCompositions.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.NebulaCompositions.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.StartAllocations.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Planets.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Fields.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Nebulae.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Objects.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await _dbContext.Systems.Where(entry => entry.SectorId == sectorId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    private void AddLog(int? galaxyId, int? sectorId, string action, long? seed, string actorId, object detail)
    {
        _dbContext.GenerationLog.Add(new WorldGenerationLogEntity
        {
            GalaxyId = galaxyId,
            SectorId = sectorId,
            Action = action,
            Seed = seed,
            ActorId = actorId,
            Detail = JsonSerializer.Serialize(detail),
            At = _timeProvider.GetUtcNow(),
        });
    }

    private static SectorSummary ToSummary(SectorEntity sector, SectorPlan plan) =>
        new(
            sector.Id,
            sector.Name,
            sector.Seed,
            sector.LayoutIndex,
            sector.IsNewbie,
            new Point3(sector.X, sector.Y, sector.Z),
            plan.SystemIds.Count);
}

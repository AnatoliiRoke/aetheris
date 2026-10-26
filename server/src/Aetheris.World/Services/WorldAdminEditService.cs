using System.Globalization;
using System.Text.Json;
using Aetheris.Core.Errors;
using Aetheris.World.Api;
using Aetheris.World.Configuration;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Aetheris.World.Generation;
using Aetheris.World.Geometry;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The codes of the fields of a refused admin change, sent to the client as localisation keys.
/// </summary>
public static class AdminEditErrors
{
    /// <summary>The value is missing.</summary>
    public const string Required = "required";

    /// <summary>The text is longer than its column.</summary>
    public const string TooLong = "too_long";

    /// <summary>The value lies outside its bounds.</summary>
    public const string OutOfBounds = "out_of_bounds";

    /// <summary>The value must not be negative.</summary>
    public const string Negative = "negative";

    /// <summary>The smallest value of a range is larger than the largest.</summary>
    public const string RangeInverted = "range_inverted";

    /// <summary>The referenced row does not exist.</summary>
    public const string Unknown = "unknown";

    /// <summary>The type belongs to another class.</summary>
    public const string ClassMismatch = "class_mismatch";

    /// <summary>The resource appears twice.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>The shares of a composition exceed the largest total share.</summary>
    public const string ShareTooLarge = "share_too_large";

    /// <summary>The resource may not lie on a body.</summary>
    public const string NotAllowedOnBody = "not_allowed_on_body";

    /// <summary>Another object holds the orbit.</summary>
    public const string OrbitTaken = "orbit_taken";

    /// <summary>The value is fixed by the star system of the object.</summary>
    public const string FixedBySystem = "fixed_by_system";

    /// <summary>A field that is not depleted needs a mass.</summary>
    public const string MassZero = "mass_zero";

    /// <summary>The owned body would get a type a player may not own.</summary>
    public const string NotColonizable = "not_colonizable";

    /// <summary>The value does not apply to the class of the type.</summary>
    public const string NotApplicable = "not_applicable";
}

/// <summary>
/// A change of the admin panel that was refused; nothing was written.
/// </summary>
public sealed class WorldAdminEditException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorldAdminEditException"/> class.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="fields">The codes of the refused fields, by the field name of the request.</param>
    /// <param name="problems">The problems of the generation rules the change would cause.</param>
    public WorldAdminEditException(string code, IReadOnlyDictionary<string, string[]>? fields = null, IReadOnlyList<string>? problems = null)
        : base("The admin change was refused: " + code)
    {
        Code = code;
        Fields = fields ?? new Dictionary<string, string[]>();
        Problems = problems ?? [];
    }

    /// <summary>Gets the error code.</summary>
    public string Code { get; }

    /// <summary>Gets the codes of the refused fields.</summary>
    public IReadOnlyDictionary<string, string[]> Fields { get; }

    /// <summary>Gets the problems of the generation rules the change would cause.</summary>
    public IReadOnlyList<string> Problems { get; }
}

/// <summary>
/// What a stored admin change did, for the journal.
/// </summary>
/// <param name="Target">The changed record, for example <c>field:12</c>.</param>
/// <param name="OldValue">The changed values before, as JSON.</param>
/// <param name="NewValue">The changed values after, as JSON.</param>
/// <param name="Id">The identifier of the record.</param>
/// <param name="Version">The new version of the record.</param>
public sealed record AdminChange(string Target, string OldValue, string NewValue, long Id, int Version);

/// <summary>
/// The manual corrections of the generated world and of the generation rules in the admin
/// panel (task 00009).
/// </summary>
/// <remarks>
/// <para>
/// Every change locks its row, compares the version the administrator saw and refuses an
/// older one (decision 6): a cycle of mining or another administrator in between is never
/// overwritten. All checks run on the server with the bounds from the configuration and the
/// reference tables (decision 4); a refused change writes nothing.
/// </para>
/// <para>
/// The owner of a body and the name the owner gave are data of the player and are never
/// changed here (decision 5). A change of the generation rules affects only the objects
/// generated later; the reference tables are read on every generation, so no cache needs a
/// restart. A rule change that would make the generator refuse to run is refused.
/// </para>
/// </remarks>
public sealed class WorldAdminEditService
{
    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly WorldReferenceRepository _reference;
    private readonly WorldAdminQueryService _queries;
    private readonly FieldService _fields;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldAdminEditService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the request.</param>
    /// <param name="reference">The parameters and the reference tables.</param>
    /// <param name="queries">The table queries of the admin panel.</param>
    /// <param name="fields">The life of the fields, for a depletion.</param>
    public WorldAdminEditService(
        WorldDbContext dbContext,
        WorldUnitOfWork unitOfWork,
        WorldReferenceRepository reference,
        WorldAdminQueryService queries,
        FieldService fields)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(fields);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _reference = reference;
        _queries = queries;
        _fields = fields;
    }

    /// <summary>
    /// Reads the card of an asteroid field.
    /// </summary>
    /// <param name="fieldId">The field.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The card, or <see langword="null"/> when the field does not exist.</returns>
    public async Task<AdminFieldCard?> GetFieldCardAsync(long fieldId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.Fields.AsNoTracking()
            .Where(field => field.Id == fieldId)
            .Join(_dbContext.Objects, field => field.CelestialObjectId, entry => entry.Id, (field, entry) => new { Field = field, Object = entry })
            .Join(_dbContext.ObjectTypes, item => item.Object.ObjectTypeId, type => type.Id, (item, type) => new { item.Field, item.Object, Type = type })
            .Select(item => new
            {
                item.Field,
                item.Object,
                item.Type,
                SystemName = _dbContext.Systems.Where(system => system.Id == item.Field.SystemId).Select(system => system.Name).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        WorldSettings settings = _reference.Settings;
        ILookup<long, AdminShareDto> compositions = await _queries.CompositionsAsync([fieldId], cancellationToken).ConfigureAwait(false);

        return new AdminFieldCard(
            row.Field.Id,
            row.Field.Version,
            row.Object.Id,
            row.Object.Name,
            row.Type.Id,
            row.Type.Code,
            row.Type.NameKey,
            row.Field.SectorId,
            row.Field.SystemId,
            row.SystemName,
            row.Object.X,
            row.Object.Y,
            row.Object.Z,
            row.Field.InitialMass,
            row.Field.Mass,
            row.Field.DepletedAt,
            [.. compositions[fieldId].OrderByDescending(share => share.Coefficient)],
            new AdminFieldBounds(settings.FieldMaxCompositionShare, settings.SectorSize, settings.SectorDepth));
    }

    /// <summary>
    /// Reads the card of a body.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The card, or <see langword="null"/> when the body does not exist.</returns>
    public async Task<AdminBodyCard?> GetBodyCardAsync(long planetId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.Planets.AsNoTracking()
            .Where(planet => planet.Id == planetId)
            .Join(_dbContext.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { Planet = planet, Object = entry })
            .Join(_dbContext.ObjectTypes, body => body.Object.ObjectTypeId, type => type.Id, (body, type) => new { body.Planet, body.Object, Type = type })
            .Join(_dbContext.Systems, body => body.Planet.SystemId, system => system.Id, (body, system) => new { body.Planet, body.Object, body.Type, System = system })
            .Join(_dbContext.SizeClasses, body => body.Planet.SizeClassId, size => size.Id, (body, size) => new { body.Planet, body.Object, body.Type, body.System, Size = size })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        WorldSettings settings = _reference.Settings;
        ILookup<long, AdminShareDto> deposits = await _queries.DepositsAsync([planetId], cancellationToken).ConfigureAwait(false);

        List<AdminTakenOrbitDto> takenOrbits = await TakenOrbitsAsync(row.Planet.SystemId, row.Object.Id, cancellationToken).ConfigureAwait(false);
        List<int> allowedResourceIds = await AllowedOnBodyAsync(cancellationToken).ConfigureAwait(false);
        List<AdminSizeClassDto> sizeClasses = await _dbContext.SizeClasses.AsNoTracking()
            .OrderBy(size => size.Id)
            .Select(size => new AdminSizeClassDto(size.Id, size.Code, size.NameKey, size.SizeMin, size.SizeMax))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AdminBodyCard(
            row.Planet.Id,
            row.Planet.Version,
            row.Object.Id,
            row.Object.Name,
            row.Planet.Name,
            row.Planet.OwnerPlayerId,
            row.Type.Id,
            row.Type.Code,
            row.Type.NameKey,
            row.Type.ObjectClass,
            row.Planet.SectorId,
            row.Planet.SystemId,
            row.System.Name,
            row.Object.Orbit,
            row.Object.X,
            row.Object.Y,
            row.Object.Z,
            row.Size.Id,
            row.Size.Code,
            row.Size.NameKey,
            row.Planet.Size,
            [.. deposits[planetId].OrderByDescending(share => share.Coefficient)],
            new AdminBodyBounds(
                1,
                BodyOrbitMax(settings),
                takenOrbits,
                settings.DepositCoefficientMin,
                settings.DepositCoefficientMax,
                allowedResourceIds,
                sizeClasses));
    }

    /// <summary>
    /// Reads the card of a star system.
    /// </summary>
    /// <param name="systemId">The system.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The card, or <see langword="null"/> when the system does not exist.</returns>
    public async Task<AdminSystemCard?> GetSystemCardAsync(long systemId, CancellationToken cancellationToken)
    {
        var row = await _dbContext.Systems.AsNoTracking()
            .Where(system => system.Id == systemId)
            .Join(_dbContext.Sectors, system => system.SectorId, sector => sector.Id, (system, sector) => new { System = system, SectorName = sector.Name })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        CelestialObjectTypeEntity? star = await StarTypeQuery(systemId).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        WorldSettings settings = _reference.Settings;

        return new AdminSystemCard(
            row.System.Id,
            row.System.Version,
            row.System.SectorId,
            row.SectorName,
            row.System.Number,
            row.System.Name,
            row.System.X,
            row.System.Y,
            row.System.Z,
            row.System.InNebula,
            star?.Id,
            star?.Code,
            star?.NameKey,
            new AdminSystemBounds(settings.SectorSize, settings.SectorDepth));
    }

    /// <summary>
    /// Changes an asteroid field: its name, type, place, masses, depletion and composition.
    /// </summary>
    /// <param name="fieldId">The field.</param>
    /// <param name="request">The new state.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The field does not exist, changed in between or the change breaks a rule.</exception>
    public Task<AdminChange> UpdateFieldAsync(long fieldId, AdminFieldUpdate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                AsteroidFieldEntity field = (await _dbContext.Fields
                    .FromSql($"SELECT * FROM asteroid_field WHERE id = {fieldId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(GameErrorCodes.NotFound);

                CheckVersion(field.Version, request.Version);

                CelestialObjectEntity entry = await _dbContext.Objects
                    .SingleAsync(item => item.Id == field.CelestialObjectId, cancellationToken).ConfigureAwait(false);
                List<FieldCompositionEntity> composition = await _dbContext.FieldCompositions
                    .Where(share => share.AsteroidFieldId == fieldId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                WorldSettings settings = _reference.Settings;
                Dictionary<string, List<string>> errors = [];
                string name = CheckName(errors, request.Name);
                bool restores = field.DepletedAt is not null && !request.Depleted;

                CelestialObjectTypeEntity? type = await FindTypeAsync(request.ObjectTypeId, cancellationToken).ConfigureAwait(false);
                CheckClass(errors, "object_type_id", type, WorldObjectClasses.AsteroidField);

                if (field.SystemId is null)
                {
                    CheckPosition(errors, settings, request.X, request.Y, request.Z);
                }
                else if (!SamePosition(entry, request.X, request.Y, request.Z))
                {
                    Add(errors, "x", AdminEditErrors.FixedBySystem);
                }

                if (request.Mass < 0)
                {
                    Add(errors, "mass", AdminEditErrors.Negative);
                }
                else if (request.Mass == 0 && !request.Depleted)
                {
                    Add(errors, "mass", AdminEditErrors.MassZero);
                }

                if (request.InitialMass < 0)
                {
                    Add(errors, "initial_mass", AdminEditErrors.Negative);
                }

                IReadOnlyList<AdminShareInput> shares = request.CompositionIds ?? [];
                HashSet<int> resourceIds = await ResourceIdsAsync(cancellationToken).ConfigureAwait(false);
                CheckShares(errors, "composition_ids", shares, resourceIds, null, coefficient => coefficient > 0 && coefficient <= 1);

                if (shares.Sum(share => share.Coefficient) > settings.FieldMaxCompositionShare)
                {
                    Add(errors, "composition_ids", AdminEditErrors.ShareTooLarge);
                }

                // The outermost orbit holds one field: a field comes back only when it is free.
                if (restores && field.SystemId is not null && await _dbContext.Fields.AnyAsync(
                    other => other.SystemId == field.SystemId && other.Id != fieldId && other.DepletedAt == null,
                    cancellationToken).ConfigureAwait(false))
                {
                    Add(errors, "depleted", AdminEditErrors.OrbitTaken);
                }

                ThrowIfInvalid(errors);

                ChangeSet changes = new();
                changes.Set("name", entry.Name, name);
                changes.Set("object_type_id", entry.ObjectTypeId, type!.Id);
                changes.Set("x", entry.X, request.X);
                changes.Set("y", entry.Y, request.Y);
                changes.Set("z", entry.Z, request.Z);
                changes.Set("mass", field.Mass, request.Mass);
                changes.Set("initial_mass", field.InitialMass, request.InitialMass);
                changes.Set("depleted", field.DepletedAt is not null, request.Depleted);
                Dictionary<int, string> codes = await ResourceCodesAsync(cancellationToken).ConfigureAwait(false);
                changes.Set("composition_ids", Describe(codes, composition.Select(share => (share.ResourceId, share.Coefficient))), Describe(codes, shares.Select(share => (share.ResourceId, share.Coefficient))));

                entry.Name = name;
                entry.ObjectTypeId = type.Id;
                entry.X = request.X;
                entry.Y = request.Y;
                entry.Z = request.Z;
                field.Mass = request.Mass;
                field.InitialMass = request.InitialMass;
                ReplaceShares(
                    composition,
                    shares,
                    share => share.ResourceId,
                    (share, coefficient) => share.Coefficient = coefficient,
                    input => _dbContext.FieldCompositions.Add(new FieldCompositionEntity
                    {
                        SectorId = field.SectorId,
                        AsteroidFieldId = field.Id,
                        ResourceId = input.ResourceId,
                        Coefficient = input.Coefficient,
                    }),
                    share => _dbContext.FieldCompositions.Remove(share));

                if (request.Depleted && field.DepletedAt is null)
                {
                    await _fields.DepleteAsync(field, cancellationToken).ConfigureAwait(false);
                }
                else if (restores)
                {
                    field.DepletedAt = null;
                }

                field.Version++;

                return changes.ToChange("field:" + Text(fieldId), fieldId, field.Version);
            },
            cancellationToken);
    }

    /// <summary>
    /// Changes a body: its name, type, size, orbit and deposits. The owner is never changed.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="request">The new state.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The body does not exist, changed in between or the change breaks a rule.</exception>
    public Task<AdminChange> UpdateBodyAsync(long planetId, AdminBodyUpdate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                PlanetEntity planet = (await _dbContext.Planets
                    .FromSql($"SELECT * FROM planet WHERE id = {planetId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(GameErrorCodes.NotFound);

                CheckVersion(planet.Version, request.Version);

                CelestialObjectEntity entry = await _dbContext.Objects
                    .SingleAsync(item => item.Id == planet.CelestialObjectId, cancellationToken).ConfigureAwait(false);
                CelestialObjectTypeEntity currentType = await _dbContext.ObjectTypes.AsNoTracking()
                    .SingleAsync(type => type.Id == entry.ObjectTypeId, cancellationToken).ConfigureAwait(false);
                List<DepositEntity> deposits = await _dbContext.Deposits
                    .Where(deposit => deposit.PlanetId == planetId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                WorldSettings settings = _reference.Settings;
                Dictionary<string, List<string>> errors = [];
                string name = CheckName(errors, request.Name);

                CelestialObjectTypeEntity? type = await FindTypeAsync(request.ObjectTypeId, cancellationToken).ConfigureAwait(false);
                CheckClass(errors, "object_type_id", type, currentType.ObjectClass);

                if (type is not null && planet.OwnerPlayerId is not null && !type.IsColonizable)
                {
                    Add(errors, "object_type_id", AdminEditErrors.NotColonizable);
                }

                PlanetSizeClassEntity? sizeClass = await _dbContext.SizeClasses.AsNoTracking()
                    .SingleOrDefaultAsync(size => size.Id == request.SizeClassId, cancellationToken).ConfigureAwait(false);

                if (sizeClass is null)
                {
                    Add(errors, "size_class_id", AdminEditErrors.Unknown);
                }
                else if (request.Size < sizeClass.SizeMin || request.Size > sizeClass.SizeMax)
                {
                    Add(errors, "size", AdminEditErrors.OutOfBounds);
                }

                if (request.Orbit < 1 || request.Orbit > BodyOrbitMax(settings))
                {
                    Add(errors, "orbit", AdminEditErrors.OutOfBounds);
                }
                else if ((await TakenOrbitsAsync(planet.SystemId, entry.Id, cancellationToken).ConfigureAwait(false)).Exists(taken => taken.Orbit == request.Orbit))
                {
                    Add(errors, "orbit", AdminEditErrors.OrbitTaken);
                }

                IReadOnlyList<AdminShareInput> shares = request.DepositIds ?? [];
                HashSet<int> resourceIds = await ResourceIdsAsync(cancellationToken).ConfigureAwait(false);
                HashSet<int> allowed = [.. await AllowedOnBodyAsync(cancellationToken).ConfigureAwait(false)];
                CheckShares(
                    errors,
                    "deposit_ids",
                    shares,
                    resourceIds,
                    allowed,
                    coefficient => coefficient >= settings.DepositCoefficientMin && coefficient <= settings.DepositCoefficientMax);

                ThrowIfInvalid(errors);

                ChangeSet changes = new();
                changes.Set("name", entry.Name, name);
                changes.Set("object_type_id", entry.ObjectTypeId, type!.Id);
                changes.Set("size_class_id", planet.SizeClassId, sizeClass!.Id);
                changes.Set("size", planet.Size, request.Size);
                changes.Set("orbit", entry.Orbit, request.Orbit);
                Dictionary<int, string> codes = await ResourceCodesAsync(cancellationToken).ConfigureAwait(false);
                changes.Set("deposit_ids", Describe(codes, deposits.Select(deposit => (deposit.ResourceId, deposit.Coefficient))), Describe(codes, shares.Select(share => (share.ResourceId, share.Coefficient))));

                entry.Name = name;
                entry.ObjectTypeId = type.Id;
                entry.Orbit = request.Orbit;
                planet.SizeClassId = sizeClass.Id;
                planet.Size = request.Size;
                ReplaceShares(
                    deposits,
                    shares,
                    deposit => deposit.ResourceId,
                    (deposit, coefficient) => deposit.Coefficient = coefficient,
                    input => _dbContext.Deposits.Add(new DepositEntity
                    {
                        SectorId = planet.SectorId,
                        PlanetId = planet.Id,
                        ResourceId = input.ResourceId,
                        Coefficient = input.Coefficient,
                    }),
                    deposit => _dbContext.Deposits.Remove(deposit));

                planet.Version++;

                return changes.ToChange("planet:" + Text(planetId), planetId, planet.Version);
            },
            cancellationToken);
    }

    /// <summary>
    /// Changes a star system: its name, place and star type. The objects of the system move
    /// with it, and whether it lies in a nebula is computed again.
    /// </summary>
    /// <param name="systemId">The system.</param>
    /// <param name="request">The new state.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The system does not exist, changed in between or the change breaks a rule.</exception>
    public Task<AdminChange> UpdateSystemAsync(long systemId, AdminSystemUpdate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                StarSystemEntity system = (await _dbContext.Systems
                    .FromSql($"SELECT * FROM star_system WHERE id = {systemId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(GameErrorCodes.NotFound);

                CheckVersion(system.Version, request.Version);

                WorldSettings settings = _reference.Settings;
                Dictionary<string, List<string>> errors = [];
                string name = CheckName(errors, request.Name);
                CheckPosition(errors, settings, request.X, request.Y, request.Z);

                CelestialObjectTypeEntity? starType = await FindTypeAsync(request.StarTypeId, cancellationToken).ConfigureAwait(false);
                CheckClass(errors, "star_type_id", starType, WorldObjectClasses.Star);

                ThrowIfInvalid(errors);

                List<CelestialObjectEntity> objects = await _dbContext.Objects
                    .Where(entry => entry.SystemId == systemId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                HashSet<int> starTypeIds = [.. await _dbContext.ObjectTypes.AsNoTracking()
                    .Where(type => type.ObjectClass == WorldObjectClasses.Star)
                    .Select(type => type.Id)
                    .ToListAsync(cancellationToken).ConfigureAwait(false)];
                CelestialObjectEntity? star = objects.Find(entry => starTypeIds.Contains(entry.ObjectTypeId));

                Point3 position = new(request.X, request.Y, request.Z);
                bool inNebula = await InNebulaAsync(system.SectorId, position, cancellationToken).ConfigureAwait(false);

                ChangeSet changes = new();
                changes.Set("name", system.Name, name);
                changes.Set("x", system.X, request.X);
                changes.Set("y", system.Y, request.Y);
                changes.Set("z", system.Z, request.Z);
                changes.Set("in_nebula", system.InNebula, inNebula);
                changes.Set("star_type_id", star?.ObjectTypeId, star is null ? null : starType!.Id);

                system.Name = name;
                system.X = request.X;
                system.Y = request.Y;
                system.Z = request.Z;
                system.InNebula = inNebula;

                // Every object of a system lies at the place of the system.
                foreach (CelestialObjectEntity entry in objects)
                {
                    entry.X = request.X;
                    entry.Y = request.Y;
                    entry.Z = request.Z;
                }

                if (star is not null)
                {
                    star.ObjectTypeId = starType!.Id;
                }

                system.Version++;

                return changes.ToChange("system:" + Text(systemId), systemId, system.Version);
            },
            cancellationToken);
    }

    /// <summary>
    /// Changes an object type: its weight, the mass range of a field type and whether a player
    /// may own it. Weight zero switches the type off for new objects; a type is never deleted.
    /// </summary>
    /// <param name="objectTypeId">The type.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The type does not exist, changed in between or the change breaks a rule.</exception>
    public Task<AdminChange> UpdateObjectTypeAsync(int objectTypeId, AdminObjectTypeUpdate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                IReadOnlyList<string> problemsBefore = await ProblemsAsync(cancellationToken).ConfigureAwait(false);

                CelestialObjectTypeEntity type = (await _dbContext.ObjectTypes
                    .FromSql($"SELECT * FROM celestial_object_type WHERE id = {objectTypeId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(GameErrorCodes.NotFound);

                CheckVersion(type.Version, request.Version);

                Dictionary<string, List<string>> errors = [];

                if (request.GenerationWeight < 0)
                {
                    Add(errors, "generation_weight", AdminEditErrors.Negative);
                }

                if (type.ObjectClass == WorldObjectClasses.AsteroidField)
                {
                    if (request.MassMin is null || request.MassMax is null)
                    {
                        Add(errors, request.MassMin is null ? "mass_min" : "mass_max", AdminEditErrors.Required);
                    }
                    else if (request.MassMin <= 0)
                    {
                        Add(errors, "mass_min", AdminEditErrors.OutOfBounds);
                    }
                    else if (request.MassMin > request.MassMax)
                    {
                        Add(errors, "mass_max", AdminEditErrors.RangeInverted);
                    }
                }
                else if (request.MassMin is not null || request.MassMax is not null)
                {
                    Add(errors, "mass_min", AdminEditErrors.NotApplicable);
                }

                // Only a planet can belong to a player; a gas giant holds stations and is never owned.
                if (request.IsColonizable && type.ObjectClass != WorldObjectClasses.Planet)
                {
                    Add(errors, "is_colonizable", AdminEditErrors.NotApplicable);
                }

                ThrowIfInvalid(errors);

                ChangeSet changes = new();
                changes.Set("generation_weight", type.GenerationWeight, request.GenerationWeight);
                changes.Set("mass_min", type.MassMin, request.MassMin);
                changes.Set("mass_max", type.MassMax, request.MassMax);
                changes.Set("is_colonizable", type.IsColonizable, request.IsColonizable);

                type.GenerationWeight = request.GenerationWeight;
                type.MassMin = request.MassMin;
                type.MassMax = request.MassMax;
                type.IsColonizable = request.IsColonizable;
                type.Version++;

                await CheckGeneratorAsync(problemsBefore, cancellationToken).ConfigureAwait(false);

                return changes.ToChange("object_type:" + Text(objectTypeId), objectTypeId, type.Version);
            },
            cancellationToken);
    }

    /// <summary>
    /// Changes how many resources a generation rule group picks.
    /// </summary>
    /// <param name="ruleGroupId">The group.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The group does not exist, changed in between or the change breaks a rule.</exception>
    public Task<AdminChange> UpdateRuleGroupAsync(int ruleGroupId, AdminRuleGroupUpdate request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                IReadOnlyList<string> problemsBefore = await ProblemsAsync(cancellationToken).ConfigureAwait(false);

                GenerationRuleGroupEntity group = (await _dbContext.RuleGroups
                    .FromSql($"SELECT * FROM generation_rule_group WHERE id = {ruleGroupId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(GameErrorCodes.NotFound);

                CheckVersion(group.Version, request.Version);

                Dictionary<string, List<string>> errors = [];

                if (request.MinPicks < 0)
                {
                    Add(errors, "min_picks", AdminEditErrors.Negative);
                }
                else if (request.MinPicks > request.MaxPicks)
                {
                    Add(errors, "max_picks", AdminEditErrors.RangeInverted);
                }

                if (request.ExtraPickProbability is < 0d or > 1d || double.IsNaN(request.ExtraPickProbability))
                {
                    Add(errors, "extra_pick_probability", AdminEditErrors.OutOfBounds);
                }

                ThrowIfInvalid(errors);

                ChangeSet changes = new();
                changes.Set("min_picks", group.MinPicks, request.MinPicks);
                changes.Set("max_picks", group.MaxPicks, request.MaxPicks);
                changes.Set("extra_pick_probability", group.ExtraPickProbability, request.ExtraPickProbability);

                group.MinPicks = request.MinPicks;
                group.MaxPicks = request.MaxPicks;
                group.ExtraPickProbability = request.ExtraPickProbability;
                group.Version++;

                await CheckGeneratorAsync(problemsBefore, cancellationToken).ConfigureAwait(false);

                return changes.ToChange("generation_rule_group:" + Text(ruleGroupId), ruleGroupId, group.Version);
            },
            cancellationToken);
    }

    /// <summary>
    /// Changes a generation rule; switching it off keeps the row.
    /// </summary>
    /// <param name="ruleId">The rule.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The rule does not exist, changed in between or the change breaks a rule.</exception>
    public Task<AdminChange> UpdateRuleAsync(int ruleId, AdminRuleInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                IReadOnlyList<string> problemsBefore = await ProblemsAsync(cancellationToken).ConfigureAwait(false);

                GenerationRuleEntity rule = (await _dbContext.Rules
                    .FromSql($"SELECT * FROM generation_rule WHERE id = {ruleId} FOR UPDATE")
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(GameErrorCodes.NotFound);

                CheckVersion(rule.Version, request.Version);

                GenerationRuleGroupEntity group = await _dbContext.RuleGroups.AsNoTracking()
                    .SingleAsync(entry => entry.Id == rule.RuleGroupId, cancellationToken).ConfigureAwait(false);

                await CheckRuleAsync(group, ruleId, request, cancellationToken).ConfigureAwait(false);

                ChangeSet changes = new();
                changes.Set("resource_id", rule.ResourceId, request.ResourceId);
                changes.Set("weight", rule.Weight, request.Weight);
                changes.Set("coefficient_min", rule.CoefficientMin, request.CoefficientMin);
                changes.Set("coefficient_max", rule.CoefficientMax, request.CoefficientMax);
                changes.Set("rich_coefficient_min", rule.RichCoefficientMin, request.RichCoefficientMin);
                changes.Set("rich_coefficient_max", rule.RichCoefficientMax, request.RichCoefficientMax);
                changes.Set("is_active", rule.IsActive, request.IsActive);

                rule.ResourceId = request.ResourceId;
                rule.Weight = request.Weight;
                rule.CoefficientMin = request.CoefficientMin;
                rule.CoefficientMax = request.CoefficientMax;
                rule.RichCoefficientMin = request.RichCoefficientMin;
                rule.RichCoefficientMax = request.RichCoefficientMax;
                rule.IsActive = request.IsActive;
                rule.Version++;

                await CheckGeneratorAsync(problemsBefore, cancellationToken).ConfigureAwait(false);

                return changes.ToChange("generation_rule:" + Text(ruleId), ruleId, rule.Version);
            },
            cancellationToken);
    }

    /// <summary>
    /// Adds a generation rule to a group.
    /// </summary>
    /// <param name="request">The values of the rule and its group.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>What changed.</returns>
    /// <exception cref="WorldAdminEditException">The group does not exist or the rule breaks a rule.</exception>
    public Task<AdminChange> AddRuleAsync(AdminRuleInput request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _unitOfWork.RunAsync(
            async () =>
            {
                IReadOnlyList<string> problemsBefore = await ProblemsAsync(cancellationToken).ConfigureAwait(false);

                // The lock of the group orders two additions of the same resource.
                GenerationRuleGroupEntity group = (await _dbContext.RuleGroups
                    .FromSql($"SELECT * FROM generation_rule_group WHERE id = {request.RuleGroupId} FOR UPDATE")
                    .AsNoTracking()
                    .ToListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault()
                    ?? throw new WorldAdminEditException(
                        GameErrorCodes.ValidationFailed,
                        new Dictionary<string, string[]> { ["rule_group_id"] = [AdminEditErrors.Unknown] });

                await CheckRuleAsync(group, null, request, cancellationToken).ConfigureAwait(false);

                GenerationRuleEntity rule = new()
                {
                    RuleGroupId = group.Id,
                    ResourceId = request.ResourceId,
                    Weight = request.Weight,
                    CoefficientMin = request.CoefficientMin,
                    CoefficientMax = request.CoefficientMax,
                    RichCoefficientMin = request.RichCoefficientMin,
                    RichCoefficientMax = request.RichCoefficientMax,
                    IsActive = request.IsActive,
                };

                _dbContext.Rules.Add(rule);
                await CheckGeneratorAsync(problemsBefore, cancellationToken).ConfigureAwait(false);

                ChangeSet changes = new();
                changes.Set("rule_group_id", null, group.Id);
                changes.Set("resource_id", null, request.ResourceId);
                changes.Set("weight", null, request.Weight);
                changes.Set("coefficient_min", null, request.CoefficientMin);
                changes.Set("coefficient_max", null, request.CoefficientMax);
                changes.Set("rich_coefficient_min", null, request.RichCoefficientMin);
                changes.Set("rich_coefficient_max", null, request.RichCoefficientMax);
                changes.Set("is_active", null, request.IsActive);

                return changes.ToChange("generation_rule:" + Text(rule.Id), rule.Id, rule.Version);
            },
            cancellationToken);
    }

    private async Task CheckRuleAsync(GenerationRuleGroupEntity group, int? ruleId, AdminRuleInput request, CancellationToken cancellationToken)
    {
        WorldSettings settings = _reference.Settings;
        Dictionary<string, List<string>> errors = [];
        string objectClass = await _dbContext.ObjectTypes.AsNoTracking()
            .Where(type => type.Id == group.ObjectTypeId)
            .Select(type => type.ObjectClass)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        bool isBody = objectClass == WorldObjectClasses.Planet || objectClass == WorldObjectClasses.GasGiant;

        if (!(await ResourceIdsAsync(cancellationToken).ConfigureAwait(false)).Contains(request.ResourceId))
        {
            Add(errors, "resource_id", AdminEditErrors.Unknown);
        }
        else if (await _dbContext.Rules.AnyAsync(
            rule => rule.RuleGroupId == group.Id && rule.ResourceId == request.ResourceId && rule.Id != ruleId,
            cancellationToken).ConfigureAwait(false))
        {
            Add(errors, "resource_id", AdminEditErrors.Duplicate);
        }
        else if (isBody && !(await AllowedOnBodyAsync(cancellationToken).ConfigureAwait(false)).Contains(request.ResourceId))
        {
            Add(errors, "resource_id", AdminEditErrors.NotAllowedOnBody);
        }

        if (request.Weight < 0)
        {
            Add(errors, "weight", AdminEditErrors.Negative);
        }

        CheckCoefficients(errors, "coefficient_min", "coefficient_max", request.CoefficientMin, request.CoefficientMax);

        if (request.RichCoefficientMin is null != request.RichCoefficientMax is null)
        {
            Add(errors, request.RichCoefficientMin is null ? "rich_coefficient_min" : "rich_coefficient_max", AdminEditErrors.Required);
        }
        else if (request.RichCoefficientMin is { } richMinimum && request.RichCoefficientMax is { } richMaximum)
        {
            if (objectClass != WorldObjectClasses.Planet)
            {
                Add(errors, "rich_coefficient_min", AdminEditErrors.NotApplicable);
            }

            CheckCoefficients(errors, "rich_coefficient_min", "rich_coefficient_max", richMinimum, richMaximum);
        }

        // A deposit of a body is a richness multiplier within the range of the configuration;
        // a share of a field or a nebula never exceeds the whole (task 00009, decisions 10 and 12).
        decimal minimum = isBody ? settings.DepositCoefficientMin : 0;
        decimal maximum = isBody ? settings.DepositCoefficientMax : 1;

        foreach ((string name, decimal? value) in new[]
        {
            ("coefficient_min", (decimal?)request.CoefficientMin),
            ("coefficient_max", request.CoefficientMax),
            ("rich_coefficient_min", request.RichCoefficientMin),
            ("rich_coefficient_max", request.RichCoefficientMax),
        })
        {
            if (value is { } coefficient && (coefficient < minimum || coefficient > maximum))
            {
                Add(errors, name, AdminEditErrors.OutOfBounds);
            }
        }

        ThrowIfInvalid(errors);
    }

    private static void CheckCoefficients(Dictionary<string, List<string>> errors, string minimumName, string maximumName, decimal minimum, decimal maximum)
    {
        if (minimum < 0)
        {
            Add(errors, minimumName, AdminEditErrors.Negative);
        }
        else if (minimum > maximum)
        {
            Add(errors, maximumName, AdminEditErrors.RangeInverted);
        }
    }

    /// <summary>
    /// Saves the staged change inside the transaction and reads the generator input again: a
    /// change that adds a problem the generator refuses to run with is refused, and the
    /// transaction is rolled back. Problems that existed before are not blamed on the change.
    /// </summary>
    private async Task CheckGeneratorAsync(IReadOnlyList<string> problemsBefore, CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        List<string> added = [.. (await ProblemsAsync(cancellationToken).ConfigureAwait(false)).Except(problemsBefore, StringComparer.Ordinal)];

        if (added.Count > 0)
        {
            throw new WorldAdminEditException(WorldErrorCodes.ConfigInvalid, null, added);
        }
    }

    private async Task<IReadOnlyList<string>> ProblemsAsync(CancellationToken cancellationToken) =>
        WorldConfigValidator.Validate(await _reference.LoadAsync(cancellationToken).ConfigureAwait(false));

    private IQueryable<CelestialObjectTypeEntity> StarTypeQuery(long systemId) =>
        _dbContext.Objects
            .Where(entry => entry.SystemId == systemId)
            .Join(_dbContext.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => type)
            .Where(type => type.ObjectClass == WorldObjectClasses.Star);

    private Task<CelestialObjectTypeEntity?> FindTypeAsync(int objectTypeId, CancellationToken cancellationToken) =>
        _dbContext.ObjectTypes.AsNoTracking().SingleOrDefaultAsync(type => type.Id == objectTypeId, cancellationToken);

    private async Task<HashSet<int>> ResourceIdsAsync(CancellationToken cancellationToken) =>
        [.. await _dbContext.ResourceTypes.AsNoTracking().Select(resource => resource.Id).ToListAsync(cancellationToken).ConfigureAwait(false)];

    private Task<List<int>> AllowedOnBodyAsync(CancellationToken cancellationToken) =>
        _dbContext.ResourceTypes.AsNoTracking()
            .Join(_dbContext.ResourceGroups, resource => resource.ResourceGroupId, group => group.Id, (resource, group) => new { resource.Id, group.AllowedOnPlanet })
            .Where(item => item.AllowedOnPlanet)
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The orbits of a system held by other objects; a depleted field holds none.
    /// </summary>
    private Task<List<AdminTakenOrbitDto>> TakenOrbitsAsync(long systemId, long exceptObjectId, CancellationToken cancellationToken) =>
        _dbContext.Objects.AsNoTracking()
            .Where(entry => entry.SystemId == systemId && entry.Id != exceptObjectId && entry.Orbit != null)
            .Where(entry => !_dbContext.Fields.Any(field => field.CelestialObjectId == entry.Id && field.DepletedAt != null))
            .OrderBy(entry => entry.Orbit)
            .Select(entry => new AdminTakenOrbitDto(entry.Orbit!.Value, entry.Name))
            .ToListAsync(cancellationToken);

    private async Task<bool> InNebulaAsync(int sectorId, Point3 position, CancellationToken cancellationToken)
    {
        var nebulae = await _dbContext.Nebulae.AsNoTracking()
            .Where(nebula => nebula.SectorId == sectorId)
            .Join(_dbContext.Objects, nebula => nebula.CelestialObjectId, entry => entry.Id, (nebula, entry) => new { nebula.Radius, entry.X, entry.Y, entry.Z })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return nebulae.Exists(nebula => WorldGeometry.Distance(new Point3(nebula.X, nebula.Y, nebula.Z), position) <= nebula.Radius);
    }

    // The outermost orbit is kept for a field (task 00004, decision 4).
    private static int BodyOrbitMax(WorldSettings settings) => settings.OrbitCount - 1;

    private static void CheckVersion(int current, int seen)
    {
        if (current != seen)
        {
            throw new WorldAdminEditException(WorldErrorCodes.VersionConflict);
        }
    }

    private static string CheckName(Dictionary<string, List<string>> errors, string? name)
    {
        string trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            Add(errors, "name", AdminEditErrors.Required);
        }
        else if (trimmed.Length > WorldDbContext.NameLength)
        {
            Add(errors, "name", AdminEditErrors.TooLong);
        }

        return trimmed;
    }

    private static void CheckClass(Dictionary<string, List<string>> errors, string field, CelestialObjectTypeEntity? type, string objectClass)
    {
        if (type is null)
        {
            Add(errors, field, AdminEditErrors.Unknown);
        }
        else if (type.ObjectClass != objectClass)
        {
            Add(errors, field, AdminEditErrors.ClassMismatch);
        }
    }

    private static void CheckPosition(Dictionary<string, List<string>> errors, WorldSettings settings, double x, double y, double z)
    {
        CheckAxis(errors, "x", x, settings.SectorSize);
        CheckAxis(errors, "y", y, settings.SectorSize);
        CheckAxis(errors, "z", z, settings.SectorDepth);
    }

    private static void CheckAxis(Dictionary<string, List<string>> errors, string field, double value, double maximum)
    {
        if (!double.IsFinite(value) || value < 0 || value > maximum)
        {
            Add(errors, field, AdminEditErrors.OutOfBounds);
        }
    }

    private static bool SamePosition(CelestialObjectEntity entry, double x, double y, double z) =>
        entry.X.Equals(x) && entry.Y.Equals(y) && entry.Z.Equals(z);

    private static void CheckShares(
        Dictionary<string, List<string>> errors,
        string field,
        IReadOnlyList<AdminShareInput> shares,
        HashSet<int> resourceIds,
        HashSet<int>? allowedResourceIds,
        Func<decimal, bool> accepts)
    {
        if (shares.Select(share => share.ResourceId).Distinct().Count() != shares.Count)
        {
            Add(errors, field, AdminEditErrors.Duplicate);
        }

        if (shares.Any(share => !resourceIds.Contains(share.ResourceId)))
        {
            Add(errors, field, AdminEditErrors.Unknown);
        }
        else if (allowedResourceIds is not null && shares.Any(share => !allowedResourceIds.Contains(share.ResourceId)))
        {
            Add(errors, field, AdminEditErrors.NotAllowedOnBody);
        }

        if (shares.Any(share => !accepts(share.Coefficient)))
        {
            Add(errors, field, AdminEditErrors.OutOfBounds);
        }
    }

    /// <summary>
    /// Brings a stored set of shares to the sent one: a kept resource is updated in place, so
    /// the unique index of the resource is never broken in between.
    /// </summary>
    private static void ReplaceShares<T>(
        List<T> stored,
        IReadOnlyList<AdminShareInput> sent,
        Func<T, int> resourceOf,
        Action<T, decimal> setCoefficient,
        Action<AdminShareInput> add,
        Action<T> remove)
    {
        foreach (T share in stored)
        {
            AdminShareInput? kept = sent.FirstOrDefault(input => input.ResourceId == resourceOf(share));

            if (kept is null)
            {
                remove(share);
            }
            else
            {
                setCoefficient(share, kept.Coefficient);
            }
        }

        foreach (AdminShareInput input in sent.Where(input => !stored.Exists(share => resourceOf(share) == input.ResourceId)))
        {
            add(input);
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string code)
    {
        if (!errors.TryGetValue(field, out List<string>? codes))
        {
            codes = [];
            errors[field] = codes;
        }

        if (!codes.Contains(code, StringComparer.Ordinal))
        {
            codes.Add(code);
        }
    }

    private static void ThrowIfInvalid(Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
        {
            throw new WorldAdminEditException(
                GameErrorCodes.ValidationFailed,
                errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray(), StringComparer.Ordinal));
        }
    }

    private static string Describe(Dictionary<int, string> codes, IEnumerable<(int ResourceId, decimal Coefficient)> shares) =>
        string.Join(
            ", ",
            shares.OrderBy(share => share.ResourceId).Select(share =>
                codes.GetValueOrDefault(share.ResourceId, Text(share.ResourceId)) + ":" + share.Coefficient.ToString(CultureInfo.InvariantCulture)));

    private Task<Dictionary<int, string>> ResourceCodesAsync(CancellationToken cancellationToken) =>
        _dbContext.ResourceTypes.AsNoTracking().ToDictionaryAsync(resource => resource.Id, resource => resource.Code, cancellationToken);

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The values a change touched, before and after, for the journal.
    /// </summary>
    private sealed class ChangeSet
    {
        private readonly Dictionary<string, object?> _old = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _new = new(StringComparer.Ordinal);

        public void Set(string name, object? oldValue, object? newValue)
        {
            if (!Equals(oldValue, newValue))
            {
                _old[name] = oldValue;
                _new[name] = newValue;
            }
        }

        public AdminChange ToChange(string target, long id, int version) =>
            new(target, JsonSerializer.Serialize(_old), JsonSerializer.Serialize(_new), id, version);
    }
}

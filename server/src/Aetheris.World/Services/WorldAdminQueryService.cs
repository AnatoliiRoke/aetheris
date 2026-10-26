using Aetheris.Core.Admin;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// The tables of the generated world for the admin panel: sectors, star systems, bodies and
/// asteroid fields with their composition (task 00007, decision 2).
/// </summary>
/// <remarks>
/// Every table is filtered, searched, sorted and paged in the database. The sort column is
/// taken from a fixed list per table; an unknown one falls back to the identifier. The shares
/// of the bodies and fields of a page are read in one query, never per row.
/// </remarks>
public sealed class WorldAdminQueryService
{
    /// <summary>The sort column of the name.</summary>
    public const string SortByName = "name";

    /// <summary>The sort column of the number of star systems.</summary>
    public const string SortBySystems = "system_count";

    /// <summary>The sort column of the creation time.</summary>
    public const string SortByCreatedAt = "created_at";

    /// <summary>The sort column of the number within the sector.</summary>
    public const string SortByNumber = "number";

    /// <summary>The sort column of the number of bodies.</summary>
    public const string SortByBodies = "body_count";

    /// <summary>The sort column of the size of a body.</summary>
    public const string SortBySize = "size";

    /// <summary>The sort column of the orbit of a body.</summary>
    public const string SortByOrbit = "orbit";

    /// <summary>The sort column of the mass of a field.</summary>
    public const string SortByMass = "mass";

    private readonly WorldDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldAdminQueryService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    public WorldAdminQueryService(WorldDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists the sectors.
    /// </summary>
    /// <param name="page">The page; the search text finds a name or an identifier.</param>
    /// <param name="isNewbie">Only the sectors for newcomers or only the others.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminSectorRow>> GetSectorsAsync(PageRequest page, bool? isNewbie, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<SectorEntity> sectors = _dbContext.Sectors.AsNoTracking();

        if (isNewbie is not null)
        {
            sectors = sectors.Where(sector => sector.IsNewbie == isNewbie.Value);
        }

        if (page.Search is { } search)
        {
            long? number = AdminPaging.AsNumber(search);
            string pattern = AdminPaging.ContainsPattern(search);

            sectors = number is null
                ? sectors.Where(sector => EF.Functions.ILike(sector.Name, pattern, AdminPaging.LikeEscape))
                : sectors.Where(sector => sector.Id == number);
        }

        var rows = sectors.Select(sector => new
        {
            sector.Id,
            sector.Name,
            sector.Seed,
            sector.LayoutIndex,
            sector.IsNewbie,
            sector.X,
            sector.Y,
            sector.Z,
            sector.CreatedAt,
            SystemCount = _dbContext.Systems.Count(system => system.SectorId == sector.Id),
            BodyCount = _dbContext.Planets.Count(planet => planet.SectorId == sector.Id),
            OwnedCount = _dbContext.Planets.Count(planet => planet.SectorId == sector.Id && planet.OwnerPlayerId != null),
            FieldCount = _dbContext.Fields.Count(field => field.SectorId == sector.Id && field.DepletedAt == null),
        });

        rows = page.Sort switch
        {
            SortByName => Order(rows, row => row.Name, page.Descending),
            SortBySystems => Order(rows, row => row.SystemCount, page.Descending),
            SortByCreatedAt => Order(rows, row => row.CreatedAt, page.Descending),
            _ => Order(rows, row => row.Id, page.Descending),
        };

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PageResult<AdminSectorRow>(
            [.. items.Select(item => new AdminSectorRow(
                item.Id,
                item.Name,
                item.Seed,
                item.LayoutIndex,
                item.IsNewbie,
                item.X,
                item.Y,
                item.Z,
                item.CreatedAt,
                item.SystemCount,
                item.BodyCount,
                item.OwnedCount,
                item.FieldCount))],
            total,
            page.Page,
            page.PageSize);
    }

    /// <summary>
    /// Lists the star systems.
    /// </summary>
    /// <param name="page">The page; the search text finds a name or an identifier.</param>
    /// <param name="sectorId">Only this sector.</param>
    /// <param name="inNebula">Only the systems inside or only the systems outside a nebula.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminSystemRow>> GetSystemsAsync(PageRequest page, int? sectorId, bool? inNebula, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        IQueryable<StarSystemEntity> systems = _dbContext.Systems.AsNoTracking();

        if (sectorId is not null)
        {
            systems = systems.Where(system => system.SectorId == sectorId.Value);
        }

        if (inNebula is not null)
        {
            systems = systems.Where(system => system.InNebula == inNebula.Value);
        }

        if (page.Search is { } search)
        {
            long? number = AdminPaging.AsNumber(search);
            string pattern = AdminPaging.ContainsPattern(search);

            systems = number is null
                ? systems.Where(system => EF.Functions.ILike(system.Name, pattern, AdminPaging.LikeEscape))
                : systems.Where(system => system.Id == number);
        }

        var rows = systems
            .Join(_dbContext.Sectors, system => system.SectorId, sector => sector.Id, (system, sector) => new
            {
                system.Id,
                system.SectorId,
                SectorName = sector.Name,
                system.Number,
                system.Name,
                system.X,
                system.Y,
                system.Z,
                system.InNebula,
                system.NewbieCount,
                BodyCount = _dbContext.Planets.Count(planet => planet.SystemId == system.Id),
                FieldCount = _dbContext.Fields.Count(field => field.SystemId == system.Id && field.DepletedAt == null),
            });

        rows = page.Sort switch
        {
            SortByName => Order(rows, row => row.Name, page.Descending),
            SortByNumber => Order(rows, row => row.Number, page.Descending),
            SortByBodies => Order(rows, row => row.BodyCount, page.Descending),
            _ => Order(rows, row => row.Id, page.Descending),
        };

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PageResult<AdminSystemRow>(
            [.. items.Select(item => new AdminSystemRow(
                item.Id,
                item.SectorId,
                item.SectorName,
                item.Number,
                item.Name,
                item.X,
                item.Y,
                item.Z,
                item.InNebula,
                item.NewbieCount,
                item.BodyCount,
                item.FieldCount))],
            total,
            page.Page,
            page.PageSize);
    }

    /// <summary>
    /// Lists the bodies: the planets and the gas giants.
    /// </summary>
    /// <param name="page">The page; the search text finds a name or an identifier.</param>
    /// <param name="filter">The filters.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminBodyRow>> GetBodiesAsync(PageRequest page, AdminBodyFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        var bodies = _dbContext.Planets.AsNoTracking()
            .Join(_dbContext.Objects, planet => planet.CelestialObjectId, entry => entry.Id, (planet, entry) => new { Planet = planet, Object = entry })
            .Join(_dbContext.ObjectTypes, body => body.Object.ObjectTypeId, type => type.Id, (body, type) => new { body.Planet, body.Object, Type = type })
            .Join(_dbContext.Systems, body => body.Planet.SystemId, system => system.Id, (body, system) => new { body.Planet, body.Object, body.Type, System = system })
            .Join(_dbContext.SizeClasses, body => body.Planet.SizeClassId, size => size.Id, (body, size) => new { body.Planet, body.Object, body.Type, body.System, Size = size });

        if (filter.SectorId is not null)
        {
            bodies = bodies.Where(body => body.Planet.SectorId == filter.SectorId.Value);
        }

        if (filter.SystemId is not null)
        {
            bodies = bodies.Where(body => body.Planet.SystemId == filter.SystemId.Value);
        }

        if (filter.ObjectTypeId is not null)
        {
            bodies = bodies.Where(body => body.Type.Id == filter.ObjectTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.ObjectClass))
        {
            bodies = bodies.Where(body => body.Type.ObjectClass == filter.ObjectClass);
        }

        if (filter.Owned is not null)
        {
            bodies = filter.Owned.Value
                ? bodies.Where(body => body.Planet.OwnerPlayerId != null)
                : bodies.Where(body => body.Planet.OwnerPlayerId == null);
        }

        if (page.Search is { } search)
        {
            long? number = AdminPaging.AsNumber(search);
            string pattern = AdminPaging.ContainsPattern(search);

            bodies = number is null
                ? bodies.Where(body => EF.Functions.ILike(body.Object.Name, pattern, AdminPaging.LikeEscape)
                    || (body.Planet.Name != null && EF.Functions.ILike(body.Planet.Name, pattern, AdminPaging.LikeEscape)))
                : bodies.Where(body => body.Planet.Id == number);
        }

        var rows = bodies.Select(body => new
        {
            body.Planet.Id,
            CelestialObjectId = body.Object.Id,
            Name = body.Planet.Name ?? body.Object.Name,
            ObjectTypeId = body.Type.Id,
            ObjectTypeCode = body.Type.Code,
            body.Type.ObjectClass,
            body.Planet.SectorId,
            body.Planet.SystemId,
            SystemName = body.System.Name,
            body.Object.Orbit,
            SizeClassCode = body.Size.Code,
            body.Planet.Size,
            body.Planet.OwnerPlayerId,
        });

        rows = page.Sort switch
        {
            SortByName => Order(rows, row => row.Name, page.Descending),
            SortBySize => Order(rows, row => row.Size, page.Descending),
            SortByOrbit => Order(rows, row => row.Orbit, page.Descending),
            _ => Order(rows, row => row.Id, page.Descending),
        };

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        List<long> planetIds = [.. items.Select(item => item.Id)];
        ILookup<long, AdminShareDto> deposits = (await _dbContext.Deposits.AsNoTracking()
            .Where(deposit => planetIds.Contains(deposit.PlanetId))
            .Join(_dbContext.ResourceTypes, deposit => deposit.ResourceId, resource => resource.Id, (deposit, resource) => new
            {
                deposit.PlanetId,
                Share = new AdminShareDto(resource.Id, resource.Code, deposit.Coefficient),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.PlanetId, entry => entry.Share);

        return new PageResult<AdminBodyRow>(
            [.. items.Select(item => new AdminBodyRow(
                item.Id,
                item.CelestialObjectId,
                item.Name,
                item.ObjectTypeId,
                item.ObjectTypeCode,
                item.ObjectClass,
                item.SectorId,
                item.SystemId,
                item.SystemName,
                item.Orbit,
                item.SizeClassCode,
                item.Size,
                item.OwnerPlayerId,
                [.. deposits[item.Id].OrderByDescending(share => share.Coefficient)]))],
            total,
            page.Page,
            page.PageSize);
    }

    /// <summary>
    /// Lists the asteroid fields with their composition.
    /// </summary>
    /// <param name="page">The page; the search text finds a name or an identifier.</param>
    /// <param name="filter">The filters.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The page.</returns>
    public async Task<PageResult<AdminFieldRow>> GetFieldsAsync(PageRequest page, AdminFieldFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);

        var fields = _dbContext.Fields.AsNoTracking()
            .Join(_dbContext.Objects, field => field.CelestialObjectId, entry => entry.Id, (field, entry) => new { Field = field, Object = entry })
            .Join(_dbContext.ObjectTypes, item => item.Object.ObjectTypeId, type => type.Id, (item, type) => new { item.Field, item.Object, Type = type });

        if (filter.SectorId is not null)
        {
            fields = fields.Where(item => item.Field.SectorId == filter.SectorId.Value);
        }

        if (filter.SystemId is not null)
        {
            fields = fields.Where(item => item.Field.SystemId == filter.SystemId.Value);
        }

        if (filter.Free is not null)
        {
            fields = filter.Free.Value
                ? fields.Where(item => item.Field.SystemId == null)
                : fields.Where(item => item.Field.SystemId != null);
        }

        if (filter.Depleted is not null)
        {
            fields = filter.Depleted.Value
                ? fields.Where(item => item.Field.DepletedAt != null)
                : fields.Where(item => item.Field.DepletedAt == null);
        }

        if (page.Search is { } search)
        {
            long? number = AdminPaging.AsNumber(search);
            string pattern = AdminPaging.ContainsPattern(search);

            fields = number is null
                ? fields.Where(item => EF.Functions.ILike(item.Object.Name, pattern, AdminPaging.LikeEscape))
                : fields.Where(item => item.Field.Id == number);
        }

        var rows = fields.Select(item => new
        {
            item.Field.Id,
            CelestialObjectId = item.Object.Id,
            item.Object.Name,
            ObjectTypeCode = item.Type.Code,
            item.Field.SectorId,
            item.Field.SystemId,
            SystemName = _dbContext.Systems.Where(system => system.Id == item.Field.SystemId).Select(system => system.Name).FirstOrDefault(),
            item.Field.InitialMass,
            item.Field.Mass,
            item.Field.DepletedAt,
        });

        rows = page.Sort switch
        {
            SortByName => Order(rows, row => row.Name, page.Descending),
            SortByMass => Order(rows, row => row.Mass, page.Descending),
            _ => Order(rows, row => row.Id, page.Descending),
        };

        int total = await rows.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await rows.Skip(page.Skip).Take(page.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        List<long> fieldIds = [.. items.Select(item => item.Id)];
        ILookup<long, AdminShareDto> compositions = (await _dbContext.FieldCompositions.AsNoTracking()
            .Where(share => fieldIds.Contains(share.AsteroidFieldId))
            .Join(_dbContext.ResourceTypes, share => share.ResourceId, resource => resource.Id, (share, resource) => new
            {
                share.AsteroidFieldId,
                Share = new AdminShareDto(resource.Id, resource.Code, share.Coefficient),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToLookup(entry => entry.AsteroidFieldId, entry => entry.Share);

        return new PageResult<AdminFieldRow>(
            [.. items.Select(item => new AdminFieldRow(
                item.Id,
                item.CelestialObjectId,
                item.Name,
                item.ObjectTypeCode,
                item.SectorId,
                item.SystemId,
                item.SystemName,
                item.InitialMass,
                item.Mass,
                item.DepletedAt,
                [.. compositions[item.Id].OrderByDescending(share => share.Coefficient)]))],
            total,
            page.Page,
            page.PageSize);
    }

    private static IQueryable<T> Order<T, TKey>(IQueryable<T> rows, System.Linq.Expressions.Expression<Func<T, TKey>> key, bool descending) =>
        descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
}

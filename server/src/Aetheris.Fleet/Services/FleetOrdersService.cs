using Aetheris.Fleet.Data;
using Aetheris.Production;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The orders of a flight plan that a fleet may be given (task 00073).
/// </summary>
/// <remarks>
/// <para>
/// A fleet opens an order in two ways: a module fitted to one of its ships opens the order it
/// carries (task 00058), and a module in the hold of one of its ships opens the order it carries
/// (task 00073). The orders of the fitted modules are the orders of the catalog of the ships; the
/// orders of the cargo are the orders of the catalog too, so the fleet names no module and no
/// order of its own.
/// </para>
/// <para>
/// The number of the queries does not depend on the number of the ships of the fleet: the ships,
/// their modules and their cargo are read in three queries and the orders are collected in the
/// memory.
/// </para>
/// </remarks>
public sealed class FleetOrdersService
{
    private readonly FleetDbContext _dbContext;
    private readonly IProductionModule _production;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetOrdersService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="production">The catalog of the ships and their modules.</param>
    public FleetOrdersService(FleetDbContext dbContext, IProductionModule production)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(production);

        _dbContext = dbContext;
        _production = production;
    }

    /// <summary>
    /// Reads the orders the fleet may be given.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>
    /// The codes of the orders, in the order of the catalog; an order that no module opens is
    /// absent.
    /// </returns>
    public async Task<IReadOnlyList<string>> AvailableAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return [];
        }

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);

        // The order of a module of the catalog is the same for every ship, so the catalog is read
        // once and the modules are looked up by their kind.
        Dictionary<int, ShipModuleDto> byType = catalog.Modules.ToDictionary(module => module.Id);

        List<int> fittedTypes = await _dbContext.ShipModules.AsNoTracking()
            .Where(module => shipIds.Contains(module.ShipId))
            .Select(module => module.PartTypeId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<int> carriedTypes = await _dbContext.ShipPartCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId) && cargo.Quantity > 0)
            .Select(cargo => cargo.PartTypeId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        SortedSet<string> orders = new(StringComparer.Ordinal);

        foreach (int typeId in fittedTypes)
        {
            if (byType.TryGetValue(typeId, out ShipModuleDto? module) && module.FlightOrder is { } order)
            {
                orders.Add(order);
            }
        }

        foreach (int typeId in carriedTypes)
        {
            if (byType.TryGetValue(typeId, out ShipModuleDto? module) && module.CargoOrder is { } order)
            {
                orders.Add(order);
            }
        }

        return [.. orders];
    }
}

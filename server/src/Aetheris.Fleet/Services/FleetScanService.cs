using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>Finds the working system scanners carried by a fleet.</summary>
public sealed class FleetScanService
{
    private readonly FleetDbContext _dbContext;
    private readonly IProductionModule _production;

    /// <summary>Initializes a new instance of the <see cref="FleetScanService"/> class.</summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="production">The production catalog.</param>
    public FleetScanService(FleetDbContext dbContext, IProductionModule production)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(production);
        _dbContext = dbContext;
        _production = production;
    }

    /// <summary>Reads the best scan radius and duration from the fleet's working modules.</summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The system scanner parameters and whether a fleet scanner is fitted.</returns>
    public async Task<FleetScanModules> ReadAsync(long fleetId, CancellationToken cancellationToken)
    {
        int[] partIds = [.. await (from module in _dbContext.ShipModules.AsNoTracking()
            join ship in _dbContext.FleetShips.AsNoTracking() on module.ShipId equals ship.ShipId
            where ship.FleetId == fleetId && !module.IsHull && module.Quantity > 0
                && module.ConditionCode != ShipModuleConditions.Destroyed
            select module.PartTypeId).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false)];
        if (partIds.Length == 0)
        {
            return new FleetScanModules(null, false);
        }

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        HashSet<int> fittedIds = [.. partIds];
        ShipModuleDto[] fitted = [.. catalog.Modules.Where(module => fittedIds.Contains(module.Id))];
        var scanners = fitted.Where(module => module.ScanTarget == "systems")
            .Select(module => new
            {
                Radius = module.Facts?.FirstOrDefault(fact => fact.Kind == CharacteristicKinds.ScanRadius)?.Amount ?? 0m,
                Duration = module.Facts?.FirstOrDefault(fact => fact.Kind == CharacteristicKinds.ScanDuration)?.Amount ?? 0m,
            })
            .Where(module => module.Radius > 0m && module.Duration > 0m)
            .ToArray();
        return new FleetScanModules(scanners.Length == 0 ? null : new ScanCapabilities(scanners.Max(module => module.Radius), scanners.Min(module => module.Duration)), fitted.Any(module => module.ScanTarget == "fleets"));
    }
}

/// <summary>The radius and duration available to a fleet.</summary>
/// <param name="Radius">The maximum radius in light years.</param>
/// <param name="DurationSeconds">The minimum duration in game seconds.</param>
public sealed record ScanCapabilities(decimal Radius, decimal DurationSeconds);

/// <summary>The scanners carried by a fleet.</summary>
/// <param name="Systems">The usable system scanner parameters, if any.</param>
/// <param name="HasFleetScanner">Whether the fleet carries a scanner of fleets.</param>
public sealed record FleetScanModules(ScanCapabilities? Systems, bool HasFleetScanner);

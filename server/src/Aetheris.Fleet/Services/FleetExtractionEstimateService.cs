using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The estimate of the extraction of a fleet at a place (task 00050).
/// </summary>
/// <remarks>
/// <para>
/// The player writes a flight plan and wants to know what the order of extraction will bring him:
/// how long the fleet will work, how long the holds of the fleet will hold the yield, and how long
/// the place itself will last. The first time is his own, the other two are counted from the
/// modules of the ships of the fleet and from the mass of the place.
/// </para>
/// <para>
/// The estimate is a reading: it changes nothing and writes nothing.
/// </para>
/// </remarks>
public sealed class FleetExtractionEstimateService
{
    private readonly FleetDbContext _dbContext;
    private readonly FleetExtractionService _extraction;
    private readonly IProductionModule _production;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="FleetExtractionEstimateService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="extraction">The rules of the extraction.</param>
    /// <param name="production">The catalog of the modules.</param>
    /// <param name="world">The world module.</param>
    public FleetExtractionEstimateService(
        FleetDbContext dbContext,
        FleetExtractionService extraction,
        IProductionModule production,
        IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _extraction = extraction;
        _production = production;
        _world = world;
    }

    /// <summary>
    /// Estimates what the extraction of a fleet at a place will take (task 00050).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="objectId">The object of the map the fleet works at.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The estimate, or <see langword="null"/> when the place is unknown.</returns>
    public async Task<FleetExtractionService.ExtractionEstimate?> EstimateAsync(
        long fleetId,
        long objectId,
        CancellationToken cancellationToken = default)
    {
        PlaceContentDto? place = await _world.GetPlaceContentAsync(objectId, cancellationToken).ConfigureAwait(false);

        if (place is null)
        {
            return null;
        }

        FleetExtractionService.ExtractionSource? source = SourceOf(place);

        if (source is null)
        {
            return null;
        }

        IReadOnlyList<FleetExtractionService.ExtractionModules> modules =
            await _extraction.ModulesAsync(fleetId, cancellationToken).ConfigureAwait(false);

        long free = await FreeHoldAsync(fleetId, cancellationToken).ConfigureAwait(false);
        long resourceTons = FleetExtractionService.ResourceCycleTons(modules);
        long processedTons = FleetExtractionService.CycleMass(modules, source.Value);
        long mass = place.Mass;

        long holdFull = FleetExtractionService.SecondsOfCycles(free, resourceTons, FleetExtractionCycleService.CycleSeconds);
        long placeEmpty = FleetExtractionService.SecondsOfCycles(mass, processedTons, FleetExtractionCycleService.CycleSeconds);

        return new FleetExtractionService.ExtractionEstimate(
            FleetExtractionCycleService.CycleSeconds,
            resourceTons,
            processedTons,
            free,
            mass,
            holdFull,
            placeEmpty);
    }

    /// <summary>
    /// The room that is left in the holds of the ships of a fleet, in tonnes (task 00050).
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The room that is left, in tonnes.</returns>
    private async Task<long> FreeHoldAsync(long fleetId, CancellationToken cancellationToken)
    {
        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        if (shipIds.Length == 0)
        {
            return 0;
        }

        List<ShipEntity> ships = await _dbContext.Ships.AsNoTracking()
            .Where(ship => shipIds.Contains(ship.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<ShipModuleEntity> modules = await _dbContext.ShipModules.AsNoTracking()
            .Where(module => shipIds.Contains(module.ShipId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<long, long> carried = await _dbContext.ShipCargos.AsNoTracking()
            .Where(cargo => shipIds.Contains(cargo.ShipId))
            .GroupBy(cargo => cargo.ShipId)
            .Select(group => new { ShipId = group.Key, Amount = group.Sum(cargo => cargo.Amount) })
            .ToDictionaryAsync(row => row.ShipId, row => row.Amount, cancellationToken).ConfigureAwait(false);

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ShipHullDto> hulls = catalog.Hulls.ToDictionary(hull => hull.Id);
        Dictionary<int, ShipModuleDto> byType = catalog.Modules.ToDictionary(module => module.Id);

        long[] designIds = [.. ships.Select(ship => ship.ShipDesignId).Distinct()];
        Dictionary<long, int> hullOfDesign = await _dbContext.ShipDesigns.AsNoTracking()
            .Where(design => designIds.Contains(design.Id))
            .Select(design => new { design.Id, design.HullPartTypeId })
            .ToDictionaryAsync(row => row.Id, row => row.HullPartTypeId, cancellationToken).ConfigureAwait(false);

        long free = 0;

        foreach (ShipEntity ship in ships)
        {
            int hullTypeId = hullOfDesign.GetValueOrDefault(ship.ShipDesignId);
            decimal capacity = hulls.TryGetValue(hullTypeId, out ShipHullDto? hull) ? hull.HoldCapacity : 0m;

            foreach (ShipModuleEntity module in modules.Where(entry => entry.ShipId == ship.Id))
            {
                if (byType.TryGetValue(module.PartTypeId, out ShipModuleDto? known))
                {
                    capacity += known.CargoCapacity;
                }
            }

            free += Math.Max(0, (long)Math.Floor(capacity) - carried.GetValueOrDefault(ship.Id));
        }

        return free;
    }

    /// <summary>
    /// The kind of the place a fleet may take from, or nothing when the place holds nothing to
    /// take (task 00050).
    /// </summary>
    /// <param name="place">The place.</param>
    /// <returns>The kind of the place, or <see langword="null"/>.</returns>
    private static FleetExtractionService.ExtractionSource? SourceOf(PlaceContentDto place)
    {
        if (place.Object.AsteroidFieldId is not null)
        {
            return FleetExtractionService.ExtractionSource.Field;
        }

        if (place.Object.NebulaId is not null)
        {
            return FleetExtractionService.ExtractionSource.Nebula;
        }

        if (place.Object.ObjectClass == WorldObjectClasses.GasGiant)
        {
            return FleetExtractionService.ExtractionSource.GasGiant;
        }

        return null;
    }
}

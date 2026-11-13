using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Aetheris.Production;
using Aetheris.Production.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The drives of the ships of a fleet, by their class (task 00072).
/// </summary>
/// <remarks>
/// <para>
/// A ship may carry drives of several classes, so the drives of a fleet are read once and grouped
/// by their class: the ordinary drives fly inside a system, the hyperdrives fly between the
/// systems and the jump drives jump between them. The class of a drive is the attribute
/// <c>engine_kind</c> of the part, so the data of the production decides it.
/// </para>
/// <para>
/// The catalog of the production is read once for the whole fleet, so the number of the queries
/// does not grow with the number of the ships.
/// </para>
/// </remarks>
public sealed class EngineKindsReader
{
    private readonly FleetDbContext _dbContext;
    private readonly IProductionModule _production;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineKindsReader"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    /// <param name="production">The catalog of the ships, for the class and the fuel of the drives.</param>
    public EngineKindsReader(FleetDbContext dbContext, IProductionModule production)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(production);

        _dbContext = dbContext;
        _production = production;
    }

    /// <summary>
    /// Reads the drives of the ships of a fleet, by their class.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The drives of the fleet, grouped by their class.</returns>
    public async Task<FleetEngines> ReadAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        List<long> shipIds = await _dbContext.FleetShips.AsNoTracking()
            .Where(entry => entry.FleetId == fleetId)
            .OrderBy(entry => entry.ShipId)
            .Select(entry => entry.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (shipIds.Count == 0)
        {
            return new FleetEngines([], []);
        }

        var modules = await _dbContext.ShipModules.AsNoTracking()
            .Where(module => shipIds.Contains(module.ShipId))
            .Select(module => new { module.ShipId, module.PartTypeId, module.Quantity })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (modules.Count == 0)
        {
            return new FleetEngines([], shipIds);
        }

        ShipCatalogDto catalog = await _production.GetShipCatalogAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<int, ShipModuleDto> byType = catalog.Modules.ToDictionary(module => module.Id);

        List<FleetDrive> drives = [];

        foreach (var module in modules)
        {
            if (!byType.TryGetValue(module.PartTypeId, out ShipModuleDto? known))
            {
                continue;
            }

            // A drive is a module of the component «drive» that carries the class of the drives.
            // A module without the class is a drive of the old data, so it flies the ordinary way.
            if (!string.Equals(known.Component, ShipDesignService.DriveComponent, StringComparison.Ordinal))
            {
                continue;
            }

            string kind = known.EngineKind ?? FlightEngineKinds.Normal;
            decimal fuelUse = FactOf(known, CharacteristicKinds.FuelUse);
            decimal jumpPrep = FactOf(known, CharacteristicKinds.JumpPrep);

            List<FleetFuelShare> fuel =
            [
                .. (known.Fuel ?? []).Select(row => new FleetFuelShare(row.ResourceTypeId, (decimal)row.Share)),
            ];

            drives.Add(new FleetDrive(
                module.ShipId,
                kind,
                known.DrivePush,
                fuelUse,
                jumpPrep,
                module.Quantity,
                fuel));
        }

        return new FleetEngines(drives, shipIds);
    }

    /// <summary>
    /// Reads the value of a characteristic of a module.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <param name="kind">The kind of the characteristic.</param>
    /// <returns>The value, or zero when the module does not carry the characteristic.</returns>
    private static decimal FactOf(ShipModuleDto module, string kind)
    {
        foreach (ShipModuleFactDto fact in module.Facts ?? [])
        {
            if (string.Equals(fact.Kind, kind, StringComparison.Ordinal))
            {
                return fact.Amount;
            }
        }

        return 0m;
    }
}

/// <summary>
/// The drives of the ships of a fleet (task 00072).
/// </summary>
/// <remarks>
/// A class of drives is one of <see cref="FlightEngineKinds.Normal"/>,
/// <see cref="FlightEngineKinds.Hyper"/> and <see cref="FlightEngineKinds.Jump"/>; the class of the
/// ordinary drives holds the gravitational drives as well, which work only inside a star system.
/// </remarks>
/// <param name="Drives">The drives of the fleet.</param>
/// <param name="ShipIds">Every ship of the fleet, with a drive or without one.</param>
public sealed record FleetEngines(IReadOnlyList<FleetDrive> Drives, IReadOnlyList<long> ShipIds)
{
    /// <summary>
    /// Gets the drives of one class.
    /// </summary>
    /// <param name="engineClass">The class of the drives.</param>
    /// <param name="inSystem">Whether the drives work inside a star system: in open space the gravitational drives do not work.</param>
    /// <returns>The drives of the class.</returns>
    public IReadOnlyList<FleetDrive> Of(string engineClass, bool inSystem = true) =>
        [.. Drives.Where(drive => FlightEngineKinds.BelongsTo(drive.Kind, engineClass)
            && (inSystem || !string.Equals(drive.Kind, FlightEngineKinds.Gravity, StringComparison.Ordinal)))];

    /// <summary>
    /// Gets the time of the preparation of a jump of the fleet.
    /// </summary>
    /// <remarks>
    /// The preparation lasts as long as the slowest ship of the fleet prepares, so the time is the
    /// greatest of the ships that have a jump drive.
    /// </remarks>
    /// <returns>The time in seconds, or zero when the fleet has no jump drive.</returns>
    public decimal JumpPrepSeconds()
    {
        IReadOnlyList<FleetDrive> drives = Of(FlightEngineKinds.Jump);

        return drives.Count == 0 ? 0m : drives.Max(drive => drive.JumpPrepSeconds);
    }

    /// <summary>
    /// Gets the push of the drives of a class of one ship.
    /// </summary>
    /// <param name="shipId">The ship.</param>
    /// <param name="engineClass">The class of the drives.</param>
    /// <param name="inSystem">Whether the ship flies inside a star system.</param>
    /// <returns>The push in kilonewtons, or zero when the ship has no drive of the class.</returns>
    public decimal PushOfShip(long shipId, string engineClass, bool inSystem = true) =>
        Of(engineClass, inSystem)
            .Where(drive => drive.ShipId == shipId)
            .Sum(drive => drive.Push * drive.Quantity);

    /// <summary>
    /// Gets the ships of the fleet that have no drive of a class.
    /// </summary>
    /// <remarks>
    /// A ship without the drive of the class grounds the fleet for a flight of that class, so the
    /// command of such a flight is refused with the list of the ships.
    /// </remarks>
    /// <param name="engineClass">The class of the drives.</param>
    /// <returns>The identifiers of the ships, in ascending order.</returns>
    public IReadOnlyList<long> ShipsWithout(string engineClass)
    {
        HashSet<long> equipped = [.. Of(engineClass).Select(drive => drive.ShipId)];

        return [.. ShipIds.Where(shipId => !equipped.Contains(shipId)).Order()];
    }
}

/// <summary>
/// One drive of a ship of a fleet (task 00072).
/// </summary>
/// <param name="ShipId">The ship the drive is fitted to.</param>
/// <param name="Kind">The class of the drive, one of <see cref="FlightEngineKinds"/>.</param>
/// <param name="Push">The push the drive gives, in kilonewtons.</param>
/// <param name="FuelUse">How much fuel the drive burns in an hour of its work.</param>
/// <param name="JumpPrepSeconds">How long the drive prepares for a jump, in seconds.</param>
/// <param name="Quantity">How many such drives the ship carries.</param>
/// <param name="Fuel">The fuel of the drive and the share of every resource in it.</param>
public sealed record FleetDrive(
    long ShipId,
    string Kind,
    decimal Push,
    decimal FuelUse,
    decimal JumpPrepSeconds,
    long Quantity,
    IReadOnlyList<FleetFuelShare> Fuel);

/// <summary>
/// One resource of the fuel of a drive and its share in it (task 00072).
/// </summary>
/// <param name="ResourceTypeId">The resource the drive burns.</param>
/// <param name="Share">The share of the resource in the fuel of the drive.</param>
public sealed record FleetFuelShare(int ResourceTypeId, decimal Share);

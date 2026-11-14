using Aetheris.Core.Errors;
using Aetheris.Fleet.Data;
using Aetheris.Fleet.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Fleet.Services;

/// <summary>
/// The fuel of the fleet: what it holds and what it burns (task 00072).
/// </summary>
/// <remarks>
/// <para>
/// The fuel is a cargo of the ships of the fleet. The row of the fleet is only the sum of those
/// holds, so a burn takes the fuel from the ships, in the order they joined the fleet, and lowers
/// the sum by the same amount. The burn happens at the start of a leg, in the transaction of the
/// start, so a flight never begins without the fuel for it.
/// </para>
/// <para>
/// When the hold holds less than the leg needs, the flight is refused with the code
/// <see cref="FleetErrorCodes.FuelShort"/> and the plan stops: the fleet waits where it is and
/// the player may fill the hold and carry the plan out again.
/// </para>
/// </remarks>
public sealed class FuelService
{
    private readonly FleetDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="FuelService"/> class.
    /// </summary>
    /// <param name="dbContext">The fleet context.</param>
    public FuelService(FleetDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        _dbContext = dbContext;
    }

    /// <summary>
    /// Reads what the hold of a fleet holds, by the identifier of the resource.
    /// </summary>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The amounts by the identifier of the resource.</returns>
    public async Task<IReadOnlyDictionary<int, decimal>> ReadAsync(long fleetId, CancellationToken cancellationToken = default)
    {
        Dictionary<int, decimal> amounts = await _dbContext.FleetCargos.AsNoTracking()
            .Where(cargo => cargo.FleetId == fleetId && cargo.Amount > 0)
            .ToDictionaryAsync(cargo => cargo.ResourceId, cargo => (decimal)cargo.Amount, cancellationToken)
            .ConfigureAwait(false);

        return amounts;
    }

    /// <summary>
    /// Burns the fuel of a leg of a flight from the hold of the fleet.
    /// </summary>
    /// <remarks>
    /// The holds of the ships and the sum of the fleet are read once and written once, so the whole
    /// fuel of a leg is taken with one update and not one for every resource.
    /// </remarks>
    /// <param name="fleetId">The fleet.</param>
    /// <param name="propellant">The fuel of the leg, by the identifier of the resource.</param>
    /// <param name="now">The moment of game time.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>A task that completes when the fuel is burned.</returns>
    /// <exception cref="GameException">The hold holds less than the leg needs.</exception>
    public async Task ConsumeAsync(
        long fleetId,
        IReadOnlyDictionary<int, decimal> propellant,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(propellant);

        if (propellant.Count == 0)
        {
            return;
        }

        long[] shipIds = [.. await _dbContext.FleetShips.AsNoTracking()
            .Where(member => member.FleetId == fleetId)
            .OrderBy(member => member.Position)
            .Select(member => member.ShipId)
            .ToListAsync(cancellationToken).ConfigureAwait(false)];

        List<ShipCargoEntity> shipRows = shipIds.Length == 0
            ? []
            : await _dbContext.ShipCargos
                .Where(cargo => shipIds.Contains(cargo.ShipId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<FleetCargoEntity> fleetRows = await _dbContext.FleetCargos
            .Where(cargo => cargo.FleetId == fleetId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<int, FleetCargoEntity> fleetByResource = fleetRows.ToDictionary(row => row.ResourceId);

        foreach ((int resourceTypeId, decimal amount) in propellant)
        {
            if (amount <= 0m)
            {
                continue;
            }

            long need = (long)Math.Ceiling(amount);
            long have = Held(shipRows, fleetByResource, resourceTypeId);

            if (have < need)
            {
                throw new GameException(
                    FleetErrorCodes.FuelShort,
                    $"The fleet has {have} units of the fuel it needs {need} of.");
            }
        }

        foreach ((int resourceTypeId, decimal amount) in propellant)
        {
            if (amount <= 0m)
            {
                continue;
            }

            long need = (long)Math.Ceiling(amount);

            // The ships carry the fuel. The sum of the fleet loses the same amount, so a later
            // recount of the sum cannot put the burned fuel back. A hold that exists only as the
            // sum (no rows on the ships) loses the fuel from that sum.
            TakeFromShips(shipRows, shipIds, resourceTypeId, need, now);
            TakeFromFleet(fleetByResource, resourceTypeId, need, now);
        }

        _dbContext.ShipCargos.RemoveRange(shipRows.Where(row => row.Amount <= 0));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// How much of a resource the fleet can burn: the holds of its ships, or the sum of the fleet
    /// when the fuel was written only into that sum.
    /// </summary>
    /// <param name="shipRows">The cargo of the ships of the fleet.</param>
    /// <param name="fleetByResource">The sum of the fleet, by the resource.</param>
    /// <param name="resourceTypeId">The resource.</param>
    /// <returns>The amount the fleet can burn.</returns>
    private static long Held(
        IReadOnlyList<ShipCargoEntity> shipRows,
        IReadOnlyDictionary<int, FleetCargoEntity> fleetByResource,
        int resourceTypeId)
    {
        long aboard = 0;
        bool aboardShips = false;

        foreach (ShipCargoEntity row in shipRows)
        {
            if (row.ResourceId != resourceTypeId || row.Amount <= 0)
            {
                continue;
            }

            aboard += row.Amount;
            aboardShips = true;
        }

        if (aboardShips)
        {
            return aboard;
        }

        return fleetByResource.TryGetValue(resourceTypeId, out FleetCargoEntity? fleetRow) ? fleetRow.Amount : 0L;
    }

    /// <summary>
    /// Takes a resource out of the holds of the ships, in the order the ships joined the fleet.
    /// </summary>
    /// <param name="shipRows">The cargo of the ships of the fleet.</param>
    /// <param name="shipIds">The ships, in the order they joined the fleet.</param>
    /// <param name="resourceTypeId">The resource.</param>
    /// <param name="need">How much to take.</param>
    /// <param name="now">The moment of game time.</param>
    private static void TakeFromShips(
        List<ShipCargoEntity> shipRows,
        long[] shipIds,
        int resourceTypeId,
        long need,
        DateTimeOffset now)
    {
        long left = need;

        foreach (ShipCargoEntity row in shipRows
            .Where(row => row.ResourceId == resourceTypeId && row.Amount > 0)
            .OrderBy(row => Array.IndexOf(shipIds, row.ShipId)))
        {
            long taken = Math.Min(left, row.Amount);

            if (taken <= 0)
            {
                continue;
            }

            row.Amount -= taken;
            row.UpdatedAt = now;
            left -= taken;
        }
    }

    /// <summary>
    /// Lowers the sum of the fleet by the fuel a leg burned.
    /// </summary>
    /// <param name="fleetByResource">The sum of the fleet, by the resource.</param>
    /// <param name="resourceTypeId">The resource.</param>
    /// <param name="amount">How much the leg burned.</param>
    /// <param name="now">The moment of game time.</param>
    private static void TakeFromFleet(
        IReadOnlyDictionary<int, FleetCargoEntity> fleetByResource,
        int resourceTypeId,
        long amount,
        DateTimeOffset now)
    {
        if (amount <= 0 || !fleetByResource.TryGetValue(resourceTypeId, out FleetCargoEntity? row))
        {
            return;
        }

        row.Amount = Math.Max(0L, row.Amount - amount);
        row.UpdatedAt = now;
    }
}

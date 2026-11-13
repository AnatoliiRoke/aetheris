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
/// The fuel is a cargo of the fleet: it lies in the common hold of the fleet, so the ships of the
/// fleet share it. The fuel is burned at the start of a leg of a flight, in the transaction of the
/// start of the flight, so a flight never begins without the fuel for it.
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
    /// The rows of the hold are read once and written once, so the whole fuel of a leg is taken
    /// with one update of the table and not one for every resource.
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

        List<FleetCargoEntity> rows = await _dbContext.FleetCargos
            .Where(cargo => cargo.FleetId == fleetId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<int, FleetCargoEntity> byResource = rows.ToDictionary(row => row.ResourceId);

        foreach ((int resourceTypeId, decimal amount) in propellant)
        {
            if (amount <= 0m)
            {
                continue;
            }

            FleetCargoEntity? row = byResource.GetValueOrDefault(resourceTypeId);

            if (row is null || row.Amount < (long)Math.Ceiling(amount))
            {
                long have = row?.Amount ?? 0L;

                throw new GameException(
                    FleetErrorCodes.FuelShort,
                    $"The fleet has {have} units of the fuel it needs {Math.Ceiling(amount)} of.");
            }
        }

        foreach ((int resourceTypeId, decimal amount) in propellant)
        {
            if (amount <= 0m)
            {
                continue;
            }

            FleetCargoEntity? row = byResource.GetValueOrDefault(resourceTypeId);

            if (row is null)
            {
                continue;
            }

            row.Amount -= (long)Math.Ceiling(amount);
            row.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

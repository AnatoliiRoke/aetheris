using Aetheris.Economy.Data;
using Aetheris.Economy.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Economy.Services;

/// <summary>
/// Writes the upkeep of the ships of a civilization (task 00039).
/// </summary>
public sealed class ShipUpkeepLedger : IShipUpkeepLedger
{
    private readonly EconomyDbContext _dbContext;
    private readonly EconomyUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShipUpkeepLedger"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    public ShipUpkeepLedger(EconomyDbContext dbContext, EconomyUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public Task SetPerHourAsync(string playerId, decimal perHour, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        decimal rate = perHour > 0m ? perHour : 0m;

        return _unitOfWork.RunAsync(
            async () =>
            {
                ShipUpkeepEntity? row = await _dbContext.ShipUpkeeps
                    .SingleOrDefaultAsync(entry => entry.PlayerId == playerId, cancellationToken)
                    .ConfigureAwait(false);

                if (row is null)
                {
                    _dbContext.ShipUpkeeps.Add(new ShipUpkeepEntity { PlayerId = playerId, PerHour = rate });
                }
                else
                {
                    row.PerHour = rate;
                }

                return true;
            },
            cancellationToken);
    }
}

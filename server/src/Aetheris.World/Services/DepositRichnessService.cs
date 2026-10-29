using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Changes the richness of the deposits of a body and writes the change in the journal of the
/// world (task 00014, decision 2).
/// </summary>
/// <remarks>
/// The deposits belong to the world module, so another module that makes a body rich asks
/// this service and never reaches into the tables of the world (documentation/tech/01,
/// section 8).
/// </remarks>
public sealed class DepositRichnessService
{
    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositRichnessService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the world module.</param>
    public DepositRichnessService(WorldDbContext dbContext, WorldUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Sets the coefficient of every deposit of a body and writes the change in the journal.
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="coefficient">The new coefficient of the deposits.</param>
    /// <param name="actorId">Who changed the deposits.</param>
    /// <param name="reason">Why the deposits changed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The number of the deposits that changed.</returns>
    public Task<int> SetCoefficientAsync(
        long planetId,
        decimal coefficient,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                List<DepositEntity> deposits = await _dbContext.Deposits
                    .Where(deposit => deposit.PlanetId == planetId)
                    .OrderBy(deposit => deposit.ResourceId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<DepositChangeEntity> changes = [];

                foreach (DepositEntity deposit in deposits)
                {
                    decimal before = deposit.Coefficient;

                    if (before == coefficient)
                    {
                        continue;
                    }

                    deposit.Coefficient = coefficient;

                    changes.Add(new DepositChangeEntity
                    {
                        PlanetId = planetId,
                        SectorId = deposit.SectorId,
                        ResourceId = deposit.ResourceId,
                        OldCoefficient = before,
                        NewCoefficient = coefficient,
                        Reason = reason,
                        ActorId = actorId,
                        At = DateTimeOffset.UtcNow,
                    });
                }

                if (changes.Count == 0)
                {
                    return 0;
                }

                _dbContext.DepositChanges.AddRange(changes);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return changes.Count;
            },
            cancellationToken);
}

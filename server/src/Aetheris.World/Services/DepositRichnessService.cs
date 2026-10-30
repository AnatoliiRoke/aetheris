using Aetheris.Core.Time;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Terraforms a free body: makes its deposits rich, marks it and writes the change in the
/// journal of the world (task 00014, section 9).
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
    private readonly IGameClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositRichnessService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the world module.</param>
    public DepositRichnessService(WorldDbContext dbContext, WorldUnitOfWork unitOfWork, IGameClock clock)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>
    /// Terraforms a free body: raises the coefficient of every poorer deposit, marks the body as
    /// terraformed and writes the change in the journal, all in one transaction (task 00014,
    /// section 9).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="coefficient">The new coefficient of the deposits.</param>
    /// <param name="actorId">Who terraformed the body.</param>
    /// <param name="reason">Why the deposits changed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome: a colony or a body terraformed before is refused and keeps its deposits.</returns>
    public Task<TerraformResult> TerraformAsync(
        long planetId,
        decimal coefficient,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default) =>
        _unitOfWork.RunAsync(
            async () =>
            {
                // The row of the body is locked first, so two devices on one body wait for each
                // other and only the first one finds the body not terraformed.
                PlanetEntity? planet = await _dbContext.Planets
                    .FromSql($"SELECT * FROM planet WHERE id = {planetId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (planet is null)
                {
                    return new TerraformResult(TerraformOutcomes.Unknown, 0);
                }

                // Only a free body is terraformed: it stays free, and its owner is the one who
                // colonizes it later.
                if (planet.OwnerPlayerId is not null)
                {
                    return new TerraformResult(TerraformOutcomes.Colonized, 0);
                }

                if (planet.IsTerraformed)
                {
                    return new TerraformResult(TerraformOutcomes.AlreadyTerraformed, 0);
                }

                List<DepositEntity> deposits = await _dbContext.Deposits
                    .FromSql($"SELECT * FROM deposit WHERE planet_id = {planetId} ORDER BY resource_id FOR UPDATE")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<DepositChangeEntity> changes = [];

                foreach (DepositEntity deposit in deposits)
                {
                    decimal before = deposit.Coefficient;

                    // A deposit that is already as rich or richer keeps its richness: the
                    // change only raises the poor deposits.
                    if (before >= coefficient)
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
                        At = _clock.GameNow,
                    });
                }

                planet.IsTerraformed = true;
                planet.Version++;
                _dbContext.DepositChanges.AddRange(changes);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return new TerraformResult(TerraformOutcomes.Done, changes.Count);
            },
            cancellationToken);
}

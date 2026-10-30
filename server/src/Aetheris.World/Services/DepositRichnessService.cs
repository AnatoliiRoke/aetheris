using Aetheris.Core.Randomness;
using Aetheris.Core.Time;
using Aetheris.World.Api;
using Aetheris.World.Data;
using Aetheris.World.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.World.Services;

/// <summary>
/// Terraforms a free body: makes its deposits rich, each by a roll of its own, marks it and
/// writes the change in the journal of the world (task 00014, sections 9 and 11).
/// </summary>
/// <remarks>
/// The deposits belong to the world module, so another module that makes a body rich asks
/// this service and never reaches into the tables of the world (documentation/tech/01,
/// section 8).
/// </remarks>
public sealed class DepositRichnessService
{
    /// <summary>
    /// The decimal places of a coefficient of a deposit, the scale of its column.
    /// </summary>
    private const int CoefficientScale = 6;

    private readonly WorldDbContext _dbContext;
    private readonly WorldUnitOfWork _unitOfWork;
    private readonly IGameClock _clock;
    private readonly IRandomSource _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositRichnessService"/> class.
    /// </summary>
    /// <param name="dbContext">The world context.</param>
    /// <param name="unitOfWork">The transaction of the world module.</param>
    /// <param name="clock">The clock of the game, for the time of the journal.</param>
    /// <param name="random">The source of the random numbers, so a test makes the roll repeatable.</param>
    public DepositRichnessService(WorldDbContext dbContext, WorldUnitOfWork unitOfWork, IGameClock clock, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(random);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _random = random;
    }

    /// <summary>
    /// Terraforms a free body that may be settled: rolls a multiplier in the range for every
    /// deposit, raises the deposits that are poorer than their roll, marks the body as
    /// terraformed and writes the change in the journal, all in one transaction (task 00014,
    /// sections 9 and 11).
    /// </summary>
    /// <param name="planetId">The body.</param>
    /// <param name="multiplierMin">The lowest multiplier of the roll.</param>
    /// <param name="multiplierMax">The highest multiplier of the roll.</param>
    /// <param name="actorId">Who terraformed the body.</param>
    /// <param name="reason">Why the deposits changed.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The outcome: a colony, a body that may not be colonized and a body terraformed before are refused and keep their deposits.</returns>
    public Task<TerraformResult> TerraformAsync(
        long planetId,
        decimal multiplierMin,
        decimal multiplierMax,
        string actorId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(multiplierMax, multiplierMin);

        return _unitOfWork.RunAsync(
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
                    return new TerraformResult(TerraformOutcomes.Unknown, []);
                }

                // Only a free body is terraformed: it stays free, and its owner is the one who
                // colonizes it later.
                if (planet.OwnerPlayerId is not null)
                {
                    return new TerraformResult(TerraformOutcomes.Colonized, []);
                }

                // Only a body that may be settled is terraformed; the type of the body says so,
                // a gas giant may not (task 00014, section 11).
                bool isColonizable = await _dbContext.Objects
                    .Where(entry => entry.Id == planet.CelestialObjectId)
                    .Join(_dbContext.ObjectTypes, entry => entry.ObjectTypeId, type => type.Id, (entry, type) => type.IsColonizable)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!isColonizable)
                {
                    return new TerraformResult(TerraformOutcomes.NotColonizable, []);
                }

                if (planet.IsTerraformed)
                {
                    return new TerraformResult(TerraformOutcomes.AlreadyTerraformed, []);
                }

                List<DepositEntity> deposits = await _dbContext.Deposits
                    .FromSql($"SELECT * FROM deposit WHERE planet_id = {planetId} ORDER BY resource_id FOR UPDATE")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<DepositChangeEntity> changes = [];
                List<TerraformedDeposit> raised = [];

                foreach (DepositEntity deposit in deposits)
                {
                    decimal before = deposit.Coefficient;

                    // Every deposit rolls its own multiplier, in the order of the resources.
                    decimal rolled = Roll(multiplierMin, multiplierMax);

                    // A deposit that is already as rich or richer keeps its richness: the
                    // change only raises the poor deposits.
                    if (before >= rolled)
                    {
                        continue;
                    }

                    deposit.Coefficient = rolled;
                    raised.Add(new TerraformedDeposit(deposit.ResourceId, before, rolled));

                    changes.Add(new DepositChangeEntity
                    {
                        PlanetId = planetId,
                        SectorId = deposit.SectorId,
                        ResourceId = deposit.ResourceId,
                        OldCoefficient = before,
                        NewCoefficient = rolled,
                        Reason = reason,
                        ActorId = actorId,
                        At = _clock.GameNow,
                    });
                }

                planet.IsTerraformed = true;
                planet.Version++;
                _dbContext.DepositChanges.AddRange(changes);
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return new TerraformResult(TerraformOutcomes.Done, raised);
            },
            cancellationToken);
    }

    /// <summary>
    /// Rolls a multiplier between the two bounds, to the scale of the column.
    /// </summary>
    /// <param name="multiplierMin">The lowest multiplier.</param>
    /// <param name="multiplierMax">The highest multiplier.</param>
    /// <returns>The multiplier.</returns>
    private decimal Roll(decimal multiplierMin, decimal multiplierMax) =>
        Math.Round(multiplierMin + ((decimal)_random.NextDouble() * (multiplierMax - multiplierMin)), CoefficientScale);
}

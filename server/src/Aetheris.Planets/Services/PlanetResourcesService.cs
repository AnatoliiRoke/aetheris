using Aetheris.Core.Errors;
using Aetheris.Planets.Data;
using Aetheris.Planets.Data.Entities;
using Aetheris.World;
using Aetheris.World.Api;
using Microsoft.EntityFrameworkCore;

namespace Aetheris.Planets.Services;

/// <summary>
/// The balance of a body for the modules that pay for something on a body (task 00012).
/// </summary>
/// <remarks>
/// The change joins the transaction of the request, so the payment of a research and the
/// record of its queue are committed together.
/// </remarks>
public sealed class PlanetResourcesService : IPlanetResources
{
    private readonly PlanetsDbContext _dbContext;
    private readonly PlanetsUnitOfWork _unitOfWork;
    private readonly IWorldModule _world;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlanetResourcesService"/> class.
    /// </summary>
    /// <param name="dbContext">The context of the module.</param>
    /// <param name="unitOfWork">The transaction of the module.</param>
    /// <param name="world">The world, for the owner of a body.</param>
    public PlanetResourcesService(PlanetsDbContext dbContext, PlanetsUnitOfWork unitOfWork, IWorldModule world)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(world);

        _dbContext = dbContext;
        _unitOfWork = unitOfWork;
        _world = world;
    }

    /// <inheritdoc />
    public Task TakeAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(planetId, playerId, amounts, give: false, cancellationToken);

    /// <inheritdoc />
    public Task GiveAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        CancellationToken cancellationToken = default) =>
        ChangeAsync(planetId, playerId, amounts, give: true, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<int, long>> GetBalanceAsync(
        long planetId,
        string playerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);

        await EnsureOwnedAsync(planetId, playerId, cancellationToken).ConfigureAwait(false);

        return await _dbContext.PlanetResources.AsNoTracking()
            .Where(row => row.PlanetId == planetId)
            .ToDictionaryAsync(row => row.ResourceTypeId, row => row.Amount, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task ChangeAsync(
        long planetId,
        string playerId,
        IReadOnlyDictionary<int, long> amounts,
        bool give,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(playerId);
        ArgumentNullException.ThrowIfNull(amounts);

        if (amounts.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _unitOfWork.RunAsync(
            async () =>
            {
                await EnsureOwnedAsync(planetId, playerId, cancellationToken).ConfigureAwait(false);

                // The row of the body is locked first, so two changes of the same body never
                // spend the same resources twice.
                await _dbContext.PlanetStates
                    .FromSql($"SELECT * FROM planet_state WHERE planet_id = {planetId} FOR UPDATE")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<PlanetResourceEntity> rows = await _dbContext.PlanetResources
                    .Where(row => row.PlanetId == planetId)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach ((int resourceTypeId, long amount) in amounts)
                {
                    if (amount < 0)
                    {
                        throw new GameException(PlanetsErrorCodes.ResourcesShort, "The amount of a resource cannot be negative.");
                    }

                    PlanetResourceEntity? row = rows.FirstOrDefault(entry => entry.ResourceTypeId == resourceTypeId);

                    if (row is null)
                    {
                        if (give)
                        {
                            _dbContext.PlanetResources.Add(new PlanetResourceEntity { PlanetId = planetId, ResourceTypeId = resourceTypeId, Amount = amount });
                        }
                        else if (amount > 0)
                        {
                            throw new GameException(PlanetsErrorCodes.ResourcesShort, "The body lacks resources for the cost.");
                        }

                        continue;
                    }

                    if (give)
                    {
                        row.Amount += amount;
                    }
                    else
                    {
                        if (row.Amount < amount)
                        {
                            throw new GameException(PlanetsErrorCodes.ResourcesShort, "The body lacks resources for the cost.");
                        }

                        row.Amount -= amount;
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                return true;
            },
            cancellationToken);
    }

    private async Task EnsureOwnedAsync(long planetId, string playerId, CancellationToken cancellationToken)
    {
        PlanetFacts? facts = await _world.GetPlanetFactsAsync(planetId, cancellationToken).ConfigureAwait(false);

        if (facts is null || !string.Equals(facts.OwnerPlayerId, playerId, StringComparison.Ordinal))
        {
            throw new GameException(PlanetsErrorCodes.PlanetUnknown, "The body is not a colony of the player.");
        }
    }
}
